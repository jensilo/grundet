using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Microsoft.AspNetCore.DataProtection;

namespace Grundet.Api.Features.Jira;

public sealed partial class JiraService(
    IJiraStore jiraStore,
    HttpClient httpClient,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<JiraService> logger,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex JiraProjectKeyRegex = new(
        "^[A-Z][A-Z0-9_]{0,31}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IDataProtector _stateProtector = dataProtectionProvider.CreateProtector("Grundet.JiraOAuthState");

    public string GetAuthorizationUrl(AuthorizedTeamId teamId)
    {
        teamId.ThrowIfDefault();

        string clientId = GetRequiredConfig("Jira:ClientId");
        string redirectUri = GetRequiredConfig("Jira:RedirectUri");
        string state = _stateProtector.Protect($"{teamId.Value}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        var query = new Dictionary<string, string?>
        {
            ["audience"] = "api.atlassian.com",
            ["client_id"] = clientId,
            ["scope"] = "read:jira-work write:jira-work offline_access",
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["response_type"] = "code",
            ["prompt"] = "consent"
        };

        return $"https://auth.atlassian.com/authorize?{BuildQueryString(query)}";
    }

    public Guid ExtractTeamIdFromState(string state) => ValidateState(state);

    public string BuildTeamIntegrationsRedirectUrl(Guid teamId)
        => BuildAppRedirectUrl($"/teams/{teamId}#jira-integration");

    public async Task<JiraConnectionStatusResponse> HandleCallbackAsync(
        AuthorizedTeamId teamId,
        string code,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, "OAuth callback code is required.");
        }

        var tokenResponse = await ExchangeCodeAsync(code, cancellationToken);
        string cloudId = await GetCloudIdAsync(tokenResponse.AccessToken, cancellationToken);
        var now = DateTime.UtcNow;
        string refreshToken = tokenResponse.RefreshToken
            ?? throw new InvalidOperationException("Jira OAuth token response did not include a refresh token.");
        var connection = new JiraConnectionRecord(
            Guid.NewGuid(),
            teamId.Value,
            cloudId,
            tokenResponse.AccessToken,
            refreshToken,
            now.AddSeconds(tokenResponse.ExpiresIn),
            null,
            now);

        await jiraStore.UpsertConnectionAsync(connection, cancellationToken);
        return new JiraConnectionStatusResponse(true, null);
    }

    public async Task<JiraConnectionStatusResponse> GetConnectionStatusAsync(
        AuthorizedTeamId teamId,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();

        var connection = await jiraStore.GetConnectionAsync(teamId.Value, cancellationToken);
        return connection is null
            ? new JiraConnectionStatusResponse(false, null)
            : new JiraConnectionStatusResponse(true, connection.JiraProjectKey);
    }

    public Task DeleteConnectionAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        return jiraStore.DeleteConnectionAsync(teamId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<JiraProjectSummary>> GetProjectsAsync(
        AuthorizedTeamId teamId,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();

        var connection = await EnsureTokenFreshAsync(teamId.Value, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.atlassian.com/ex/jira/{connection.CloudId}/rest/api/3/project/search?maxResults=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            int statusCode = (int)response.StatusCode;
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogJiraFailure("project list", statusCode, responseBody);
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"Jira project list failed with status {statusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<JiraProjectSearchResponse>(JsonOptions, cancellationToken)
            ?? new JiraProjectSearchResponse([]);

        return result.Values
            .Select(p => new JiraProjectSummary(p.Key, p.Name))
            .ToList();
    }

    public async Task SetProjectKeyAsync(
        AuthorizedTeamId teamId,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();

        string normalizedProjectKey = NormalizeAndValidateProjectKey(projectKey);

        bool hasConnection = await jiraStore.HasConnectionAsync(teamId.Value, cancellationToken);
        if (!hasConnection)
        {
            throw HttpProblemException.Conflict(
                ProblemTypes.JiraConnectionRequired,
                "Jira connection is required before setting a project key.");
        }

        await jiraStore.SetProjectKeyAsync(teamId.Value, normalizedProjectKey, cancellationToken);
    }

    public async Task<JiraProjectMappingResponse> GetProjectMappingAsync(
        AuthorizedProjectId projectId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        var project = await GetProjectContextOrThrowAsync(projectId.Value, cancellationToken);
        var explicitMapping = await jiraStore.GetProjectMappingAsync(project.ProjectId, cancellationToken);
        if (explicitMapping is not null)
        {
            return new JiraProjectMappingResponse(
                project.ProjectId,
                explicitMapping.JiraProjectKey,
                JiraProjectMappingSource.Explicit);
        }

        string? defaultProjectKey = await jiraStore.GetDefaultProjectKeyAsync(project.TeamId, cancellationToken);
        return !string.IsNullOrWhiteSpace(defaultProjectKey)
            ? new JiraProjectMappingResponse(
                project.ProjectId,
                defaultProjectKey,
                JiraProjectMappingSource.TeamDefault)
            : new JiraProjectMappingResponse(project.ProjectId, null, JiraProjectMappingSource.None);
    }

    public async Task SetProjectMappingAsync(
        AuthorizedProjectId projectId,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        string normalizedProjectKey = NormalizeAndValidateProjectKey(projectKey);
        var project = await GetProjectContextOrThrowAsync(projectId.Value, cancellationToken);
        bool hasConnection = await jiraStore.HasConnectionAsync(project.TeamId, cancellationToken);
        if (!hasConnection)
        {
            throw HttpProblemException.Conflict(
                ProblemTypes.JiraConnectionRequired,
                "Jira connection is required before setting a project key.");
        }

        await jiraStore.UpsertProjectMappingAsync(
            project.TeamId,
            project.ProjectId,
            normalizedProjectKey,
            cancellationToken);
    }

    public Task DeleteProjectMappingAsync(
        AuthorizedProjectId projectId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        return jiraStore.DeleteProjectMappingAsync(projectId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<JiraProjectMappingListItemResponse>> GetProjectMappingsAsync(
        AuthorizedTeamId teamId,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();

        var mappings = await jiraStore.GetProjectMappingsAsync(teamId.Value, cancellationToken);
        return mappings
            .Select(mapping => new JiraProjectMappingListItemResponse(
                mapping.ProjectId,
                mapping.ProjectName,
                mapping.JiraProjectKey))
            .ToList();
    }

    public async Task<JiraExportResponse> ExportWorkItemAsync(
        AuthorizedWorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();

        var workItem = await jiraStore.GetExportWorkItemAsync(workItemId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item '{workItemId.Value}' was not found.");
        // TeamId is resolved from the already-authorized work item lookup above.
        var connection = await EnsureTokenFreshAsync(workItem.TeamId, cancellationToken);
        var explicitMapping = await jiraStore.GetProjectMappingAsync(workItem.ProjectId, cancellationToken);
        string? projectKey = explicitMapping?.JiraProjectKey;

        if (string.IsNullOrWhiteSpace(projectKey))
        {
            projectKey = connection.JiraProjectKey;
        }

        if (string.IsNullOrWhiteSpace(projectKey))
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "A Jira project mapping is required before exporting this work item. Use PUT /projects/{projectId}/jira/project, or set a team default with PUT /teams/{teamId}/jira/project.");
        }

        object payload = BuildIssuePayload(workItem, projectKey);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.atlassian.com/ex/jira/{connection.CloudId}/rest/api/3/issue")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            int statusCode = (int)response.StatusCode;
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogJiraFailure("issue export", statusCode, responseBody);
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"Jira issue export failed with status {statusCode}.");
        }

        var createdIssue = await response.Content.ReadFromJsonAsync<JiraIssueCreateResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Jira issue create response was empty.");

        return new JiraExportResponse(createdIssue.Id, createdIssue.Key, createdIssue.Self);
    }

    private async Task<JiraConnectionRecord> EnsureTokenFreshAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var connection = await jiraStore.GetConnectionAsync(teamId, cancellationToken)
            ?? throw HttpProblemException.Conflict(
                ProblemTypes.JiraConnectionRequired,
                "Jira connection is required before exporting work items.");

        if (connection.TokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            return connection;
        }

        var tokenResponse = await RefreshTokenAsync(connection.RefreshToken, cancellationToken);
        var refreshedConnection = connection with
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
                ? connection.RefreshToken
                : tokenResponse.RefreshToken,
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
        };

        try
        {
            await jiraStore.UpsertConnectionAsync(refreshedConnection, cancellationToken);
        }
        catch (Exception ex)
        {
            // Tokens were obtained from Atlassian but could not be persisted. If Atlassian rotated
            // the refresh token, the stored token is now dead and the next refresh attempt will fail.
            // The current request can proceed with the freshly-obtained tokens, but the team will
            // likely need to reconnect Jira on the next expiry.
            LogTokenPersistenceFailure(logger, teamId, ex);
        }

        return refreshedConnection;
    }

    private async Task<JiraTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var payload = new
        {
            grant_type = "authorization_code",
            client_id = GetRequiredConfig("Jira:ClientId"),
            client_secret = GetRequiredConfig("Jira:ClientSecret"),
            code,
            redirect_uri = GetRequiredConfig("Jira:RedirectUri")
        };

        return await SendTokenRequestAsync(payload, cancellationToken);
    }

    private async Task<JiraTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var payload = new
        {
            grant_type = "refresh_token",
            client_id = GetRequiredConfig("Jira:ClientId"),
            client_secret = GetRequiredConfig("Jira:ClientSecret"),
            refresh_token = refreshToken
        };

        return await SendTokenRequestAsync(payload, cancellationToken);
    }

    private async Task<JiraTokenResponse> SendTokenRequestAsync(object payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "https://auth.atlassian.com/oauth/token",
            payload,
            JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            int statusCode = (int)response.StatusCode;
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogJiraFailure("OAuth token request", statusCode, responseBody);
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"Jira OAuth token request failed with status {statusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<JiraTokenResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Jira OAuth token response was empty.");
    }

    private async Task<string> GetCloudIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.atlassian.com/oauth/token/accessible-resources");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            int statusCode = (int)response.StatusCode;
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogJiraFailure("accessible-resources request", statusCode, responseBody);
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"Jira accessible-resources request failed with status {statusCode}.");
        }

        var resources = await response.Content.ReadFromJsonAsync<List<JiraAccessibleResource>>(JsonOptions, cancellationToken)
            ?? [];
        string? cloudId = resources.FirstOrDefault()?.Id;

        return string.IsNullOrWhiteSpace(cloudId)
            ? throw HttpProblemException.BadRequest(ProblemTypes.Validation, "No accessible Jira site was returned.")
            : cloudId;
    }

    private object BuildIssuePayload(JiraExportWorkItem workItem, string projectKey)
    {
        string? issueType = configuration.GetValue<string>("Jira:IssueType");
        var descriptionLines = new List<string>();

        if (!string.IsNullOrWhiteSpace(workItem.Description))
        {
            descriptionLines.Add(workItem.Description);
        }

        var inlineRequirements = workItem.Requirements.Take(10).ToList();
        if (inlineRequirements.Count > 0)
        {
            descriptionLines.Add("Linked requirements:");
            descriptionLines.AddRange(inlineRequirements.Select(requirement =>
                $"- {requirement.Text} ({requirement.Status})"));
        }

        if (workItem.Requirements.Count > 10)
        {
            descriptionLines.Add($"Full requirement list: {BuildWorkItemUrl(workItem.WorkItemId)}");
        }

        return new
        {
            fields = new
            {
                project = new { key = projectKey },
                summary = workItem.Title,
                description = BuildAdfDocument(descriptionLines),
                issuetype = new { name = string.IsNullOrWhiteSpace(issueType) ? "Task" : issueType }
            }
        };
    }

    private static object BuildAdfDocument(List<string> lines)
    {
        var contentLines = lines.Count == 0 ? ["Exported from Grundet."] : lines;

        return new
        {
            type = "doc",
            version = 1,
            content = contentLines
                .Select(line => new
                {
                    type = "paragraph",
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = line
                        }
                    }
                })
                .ToArray()
        };
    }

    private string BuildWorkItemUrl(Guid workItemId)
    {
        string baseUrl = GetRequiredConfig("MagicLink:PublicAppBaseUrl").TrimEnd('/');
        return $"{baseUrl}/work-items/{workItemId}";
    }

    private string BuildAppRedirectUrl(string path)
    {
        string baseUrl = GetRequiredConfig("MagicLink:PublicAppBaseUrl").TrimEnd('/');

        return !path.StartsWith('/')
            ? throw HttpProblemException.InternalServerError(
                ProblemTypes.ServerMisconfiguration,
                "App redirect path must be an absolute path.")
            : $"{baseUrl}{path}";
    }

    private void LogJiraFailure(string operation, int statusCode, string responseBody)
    {
        if (statusCode >= 500)
        {
            LogJiraError(logger, operation, statusCode, responseBody);
            return;
        }

        LogJiraWarning(logger, operation, statusCode, responseBody);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Jira {Operation} failed with status {StatusCode}. Atlassian response body: {ResponseBody}")]
    private static partial void LogJiraError(ILogger logger, string operation, int statusCode, string responseBody);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jira {Operation} failed with status {StatusCode}. Atlassian response body: {ResponseBody}")]
    private static partial void LogJiraWarning(ILogger logger, string operation, int statusCode, string responseBody);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Jira token refresh succeeded but tokens could not be persisted for team {TeamId}. " +
                  "If Atlassian rotated the refresh token, the stored token is now invalid and the team will need to reconnect Jira.")]
    private static partial void LogTokenPersistenceFailure(ILogger logger, Guid teamId, Exception exception);

    private Guid ValidateState(string state)
    {
        try
        {
            string[] parts = _stateProtector.Unprotect(state).Split(':');

            if (parts.Length != 2
                || !Guid.TryParse(parts[0], out var teamId)
                || !long.TryParse(parts[1], out long issuedAtUnixSeconds))
            {
                throw HttpProblemException.BadRequest(ProblemTypes.Validation, "OAuth state is invalid.");
            }

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnixSeconds);
            return DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromMinutes(10)
                ? throw HttpProblemException.BadRequest(ProblemTypes.Validation, "OAuth state is invalid or expired.")
                : teamId;
        }
        catch (Exception exception) when (exception is not HttpProblemException)
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, "OAuth state is invalid.");
        }
    }

    private static string NormalizeAndValidateProjectKey(string projectKey)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, "Project key is required.");
        }

        string normalizedProjectKey = projectKey.Trim().ToUpperInvariant();
        return !JiraProjectKeyRegex.IsMatch(normalizedProjectKey)
            ? throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "Project key must match ^[A-Z][A-Z0-9_]{0,31}$.")
            : normalizedProjectKey;
    }

    private async Task<JiraProjectContext> GetProjectContextOrThrowAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await jiraStore.GetProjectContextAsync(projectId, cancellationToken) ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Project '{projectId}' was not found.");

        return project;
    }

    private string GetRequiredConfig(string key)
    {
        string? value = configuration.GetValue<string>(key);

        return string.IsNullOrWhiteSpace(value)
            ? throw HttpProblemException.InternalServerError(
                ProblemTypes.ServerMisconfiguration,
                $"{key} must be configured.")
            : value;
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string?> values)
        => string.Join(
            "&",
            values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));

    private sealed record JiraTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record JiraAccessibleResource([property: JsonPropertyName("id")] string Id);

    private sealed record JiraIssueCreateResponse(string Id, string Key, string Self);
}

public sealed record JiraAuthorizationUrlResponse(string AuthorizationUrl);

public sealed record JiraConnectionStatusResponse(bool Connected, string? ProjectKey);

public sealed record JiraProjectSummary(string Key, string Name);

public sealed record JiraProjectMappingResponse(Guid ProjectId, string? ProjectKey, JiraProjectMappingSource Source);

public sealed record JiraProjectMappingListItemResponse(Guid ProjectId, string ProjectName, string ProjectKey);

public sealed record JiraExportResponse(string Id, string Key, string Self);

public enum JiraProjectMappingSource
{
    Explicit,
    TeamDefault,
    None
}

file sealed record JiraProjectSearchResponse(
    [property: JsonPropertyName("values")] IReadOnlyList<JiraProjectItem> Values);

file sealed record JiraProjectItem(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name);
