using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Data.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Grundet.Api.Tests.Infrastructure;

public static class TestApi
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string[] IntegrationLabels = ["integration"];
    private static readonly string[] DefaultAcceptanceCriteria = ["AC"];

    public static async Task<AuthSession> SignInAsync(
        ApiIntegrationFixture fixture,
        string? email = null,
        string? displayName = null,
        bool? createTeamAlphaWorkspaces = null)
    {
        string resolvedEmail = email ?? fixture.CreateUniqueEmail();
        string resolvedDisplayName = displayName ?? $"User-{Guid.NewGuid():N}"[..13];
        var client = fixture.CreateClient(allowAutoRedirect: false, createTeamAlphaWorkspaces: createTeamAlphaWorkspaces);

        var magicLinkResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/magic-link",
            new { email = resolvedEmail });
        await AssertStatusCodeAsync(magicLinkResponse, HttpStatusCode.Accepted);

        string token = fixture.EmailSink.GetLatestMagicLinkToken(resolvedEmail);
        var verifyResponse = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        await AssertStatusCodeAsync(verifyResponse, HttpStatusCode.Redirect);

        var updateMeResponse = await client.PutAsJsonAsync(
            "/api/v1/users/me",
            new { displayName = resolvedDisplayName });
        await AssertStatusCodeAsync(updateMeResponse, HttpStatusCode.OK);

        var me = await ReadAsAsync<MeResponse>(updateMeResponse);
        return new AuthSession(client, me.Id, resolvedEmail);
    }

    public static async Task<TeamResponse> CreateTeamAsync(AuthSession user, string? name = null)
    {
        var response = await user.Client.PostAsJsonAsync(
            "/api/v1/teams/",
            new { name = name ?? $"Team {Guid.NewGuid():N}" });
        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<TeamResponse>(response);
    }

    public static async Task<List<TeamListItemResponse>> ListTeamsAsync(AuthSession user)
    {
        var response = await user.Client.GetAsync("/api/v1/teams");
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<List<TeamListItemResponse>>(response);
    }

    public static async Task<List<TeamMemberSummaryResponse>> ListTeamMembersAsync(AuthSession user, Guid teamId)
    {
        var response = await user.Client.GetAsync($"/api/v1/teams/{teamId}/members");
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<List<TeamMemberSummaryResponse>>(response);
    }

    public static async Task<ProjectResponse> CreateProjectAsync(AuthSession user, Guid teamId, string? name = null)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/teams/{teamId}/projects",
            new { name = name ?? $"Project {Guid.NewGuid():N}", description = "Integration test project" });
        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<ProjectResponse>(response);
    }

    public static async Task<FeatureResponse> CreateFeatureAsync(AuthSession user, Guid projectId, string? name = null)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/features",
            new { name = name ?? $"Feature {Guid.NewGuid():N}", description = "Integration test feature" });
        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<FeatureResponse>(response);
    }

    public static async Task<SaveRequirementResponse> CreateRequirementAsync(AuthSession user, Guid featureId, string text)
        => await CreateRequirementAsync(user, featureId, text, IntegrationLabels);

    public static async Task<SaveRequirementResponse> CreateRequirementAsync(
        AuthSession user,
        Guid featureId,
        string text,
        string[] labels)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/features/{featureId}/requirements",
            new
            {
                text,
                status = "Draft",
                priority = "Must",
                labels,
                acceptanceCriteria = DefaultAcceptanceCriteria
            });

        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<SaveRequirementResponse>(response);
    }

    public static async Task<SaveRequirementResponse> CreateRequirementWithPriorityAsync(
        AuthSession user,
        Guid featureId,
        string text,
        string priority)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/features/{featureId}/requirements",
            new
            {
                text,
                status = "Draft",
                priority,
                labels = IntegrationLabels,
                acceptanceCriteria = DefaultAcceptanceCriteria
            });

        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<SaveRequirementResponse>(response);
    }

    public static async Task<SaveRequirementResponse> UpdateRequirementAsync(AuthSession user, Guid requirementId, string text)
    {
        var response = await user.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new
            {
                text,
                priority = "Should",
                reason = "Updated in integration test"
            });

        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<SaveRequirementResponse>(response);
    }

    public static async Task DeleteRequirementAsync(AuthSession user, Guid requirementId)
    {
        var response = await user.Client.DeleteAsync($"/api/v1/requirements/{requirementId}");
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
    }

    public static async Task<WorkItemResponse> CreateWorkItemAsync(AuthSession user, Guid projectId, string title)
        => await CreateWorkItemAsync(user, projectId, title, IntegrationLabels);

    public static async Task<WorkItemResponse> CreateWorkItemAsync(
        AuthSession user,
        Guid projectId,
        string title,
        string[] labels)
        => await CreateWorkItemAsync(user, projectId, title, labels, type: "Task", assignedToUserId: null);

    public static async Task<WorkItemResponse> CreateWorkItemAsync(
        AuthSession user,
        Guid projectId,
        string title,
        string[] labels,
        string type,
        Guid? assignedToUserId)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            new
            {
                title,
                description = "Integration test work item",
                status = "Todo",
                priority = "Medium",
                labels,
                assignedToUserId,
                blockedReason = (string?)null,
                reason = (string?)null,
                type
            });

        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<WorkItemResponse>(response);
    }

    public static async Task<WorkItemResponse> CreateWorkItemWithPriorityAsync(
        AuthSession user,
        Guid projectId,
        string title,
        string priority)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            new
            {
                title,
                description = "Integration test work item",
                status = "Todo",
                priority,
                labels = IntegrationLabels,
                assignedToUserId = (Guid?)null,
                blockedReason = (string?)null,
                reason = (string?)null,
                type = "Task"
            });

        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<WorkItemResponse>(response);
    }

    public static async Task<WorkItemResponse> UpdateWorkItemAsync(AuthSession user, Guid workItemId, string title)
    {
        var response = await user.Client.PutAsJsonAsync(
            $"/api/v1/work-items/{workItemId}",
            new
            {
                title,
                status = "InProgress",
                reason = "Updated in integration test"
            });

        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<WorkItemResponse>(response);
    }

    public static async Task DeleteWorkItemAsync(AuthSession user, Guid workItemId)
    {
        var response = await user.Client.DeleteAsync($"/api/v1/work-items/{workItemId}");
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
    }

    public static async Task<WorkItemLinkResponse> LinkRequirementAsync(
        AuthSession user,
        Guid workItemId,
        Guid requirementId)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/links",
            new { requirementId });

        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return await ReadAsAsync<WorkItemLinkResponse>(response);
    }

    public static async Task<WorkItemLinkResponse> UnlinkRequirementAsync(
        AuthSession user,
        Guid workItemId,
        Guid linkId)
    {
        var response = await user.Client.DeleteAsync(
            $"/api/v1/work-items/{workItemId}/links/{linkId}");

        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<WorkItemLinkResponse>(response);
    }

    public static async Task<List<LinkedWorkItemResponse>> GetRequirementWorkItemsAsync(
        AuthSession user,
        Guid requirementId)
    {
        var response = await user.Client.GetAsync(
            $"/api/v1/requirements/{requirementId}/work-items");

        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<List<LinkedWorkItemResponse>>(response);
    }

    public static async Task<string> InviteContributorAsync(AuthSession lead, Guid teamId, string contributorEmail)
    {
        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/teams/{teamId}/invites",
            new
            {
                email = contributorEmail,
                role = "Contributor"
            });
        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        return contributorEmail;
    }

    public static async Task AcceptInviteAsync(
        ApiIntegrationFixture fixture,
        AuthSession contributor,
        string contributorEmail)
    {
        string inviteToken = fixture.EmailSink.GetLatestInviteToken(contributorEmail);
        var response = await contributor.Client.PostAsJsonAsync(
            "/api/v1/invite/accept",
            new { token = inviteToken });
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
    }

    public static async Task<ResolveProjectReferenceResponse> ResolveProjectReferenceAsync(
        AuthSession user,
        Guid projectId,
        string reference)
    {
        var response = await ResolveProjectReferenceRawAsync(user, projectId, reference);
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<ResolveProjectReferenceResponse>(response);
    }

    public static Task<HttpResponseMessage> ResolveProjectReferenceRawAsync(
        AuthSession user,
        Guid projectId,
        string reference)
        => user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/resolve", new Dictionary<string, string?>
            {
                ["ref"] = reference
            }));

    public static async Task<SearchGroupedResponse> SearchProjectAsync(
        AuthSession user,
        Guid projectId,
        string query)
    {
        var response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/search", new Dictionary<string, string?>
            {
                ["query"] = query
            }));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<SearchGroupedResponse>(response);
    }

    public static async Task<PagedRequirementsResponse> ListRequirementsAsync(
        AuthSession user,
        Guid featureId,
        int? page = null,
        int? pageSize = null,
        IEnumerable<string>? labels = null)
    {
        var queryParams = new List<KeyValuePair<string, string?>>();
        if (page.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("page", page.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (pageSize.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("pageSize", pageSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (labels is not null)
        {
            queryParams.AddRange(
                labels.Select(label => new KeyValuePair<string, string?>("labels", label)));
        }

        HttpResponseMessage response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString(
                $"/api/v1/features/{featureId}/requirements", queryParams));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<PagedRequirementsResponse>(response);
    }

    public static async Task<PagedRequirementsResponse> ListProjectRequirementsAsync(
        AuthSession user,
        Guid projectId,
        int? page = null,
        int? pageSize = null,
        Guid? featureId = null,
        string? query = null,
        string? sort = null,
        IEnumerable<string>? labels = null)
    {
        var queryParams = new List<KeyValuePair<string, string?>>();
        if (page.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("page", page.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (pageSize.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("pageSize", pageSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (featureId.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("featureId", featureId.Value.ToString()));
        }
        if (query is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("q", query));
        }
        if (sort is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("sort", sort));
        }
        if (labels is not null)
        {
            queryParams.AddRange(
                labels.Select(label => new KeyValuePair<string, string?>("labels", label)));
        }

        HttpResponseMessage response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString(
                $"/api/v1/projects/{projectId}/requirements", queryParams));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<PagedRequirementsResponse>(response);
    }

    public static async Task<PagedWorkItemsResponse> ListProjectWorkItemsAsync(
        AuthSession user,
        Guid projectId,
        int? page = null,
        int? pageSize = null,
        string? query = null,
        string? sort = null,
        IEnumerable<string>? labels = null,
        IEnumerable<string>? types = null,
        string? assignedTo = null)
    {
        var queryParams = new List<KeyValuePair<string, string?>>();
        if (page.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("page", page.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (pageSize.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("pageSize", pageSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (query is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("q", query));
        }
        if (sort is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("sort", sort));
        }
        if (labels is not null)
        {
            queryParams.AddRange(
                labels.Select(label => new KeyValuePair<string, string?>("labels", label)));
        }
        if (types is not null)
        {
            queryParams.AddRange(
                types.Select(type => new KeyValuePair<string, string?>("type", type)));
        }
        if (assignedTo is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("assignedTo", assignedTo));
        }

        HttpResponseMessage response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString(
                $"/api/v1/projects/{projectId}/work-items", queryParams));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<PagedWorkItemsResponse>(response);
    }

    public static Task<HttpResponseMessage> ListRequirementsRawAsync(
        AuthSession user,
        Guid featureId,
        string queryString = "")
        => user.Client.GetAsync($"/api/v1/features/{featureId}/requirements{queryString}");

    public static async Task<PagedRequirementsResponse> ListRequirementsWithQueryStringAsync(
        AuthSession user,
        Guid featureId,
        string queryString)
    {
        var response = await ListRequirementsRawAsync(user, featureId, queryString);
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<PagedRequirementsResponse>(response);
    }

    public static async Task<List<SearchResultResponse>> SearchRequirementsAsync(
        AuthSession user,
        Guid projectId,
        string query)
    {
        var response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/requirements/search", new Dictionary<string, string?>
            {
                ["query"] = query
            }));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<List<SearchResultResponse>>(response);
    }

    public static async Task<List<SearchResultResponse>> SearchWorkItemsAsync(
        AuthSession user,
        Guid projectId,
        string query)
    {
        var response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/work-items/search", new Dictionary<string, string?>
            {
                ["query"] = query
            }));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<List<SearchResultResponse>>(response);
    }

    public static async Task<List<SearchResultResponse>> SearchFeaturesAsync(
        AuthSession user,
        Guid projectId,
        string query)
    {
        var response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/features/search", new Dictionary<string, string?>
            {
                ["query"] = query
            }));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<List<SearchResultResponse>>(response);
    }

    public static async Task<PagedFeaturesResponse> ListFeaturesAsync(
        AuthSession user,
        Guid projectId,
        string? q = null,
        int? page = null,
        int? pageSize = null)
    {
        var queryParams = new List<KeyValuePair<string, string?>>();
        if (q is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("q", q));
        }
        if (page.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("page", page.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (pageSize.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("pageSize", pageSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        HttpResponseMessage response = await user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/features", queryParams));
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<PagedFeaturesResponse>(response);
    }

    public static Task<HttpResponseMessage> GetFeatureRawAsync(AuthSession user, Guid projectId, Guid featureId)
        => user.Client.GetAsync($"/api/v1/projects/{projectId}/features/{featureId}");

    public static async Task<FeatureResponse> GetFeatureAsync(AuthSession user, Guid projectId, Guid featureId)
    {
        HttpResponseMessage response = await GetFeatureRawAsync(user, projectId, featureId);
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<FeatureResponse>(response);
    }

    public static Task<HttpResponseMessage> SearchProjectRawAsync(
        AuthSession user,
        Guid projectId,
        string query)
        => user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/search", new Dictionary<string, string?>
            {
                ["query"] = query
            }));

    public static async Task<WorkspaceSearchResponse> SearchWorkspaceAsync(
        AuthSession user,
        string query,
        Guid? projectId = null)
    {
        var response = await SearchWorkspaceRawAsync(user, query, projectId);
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<WorkspaceSearchResponse>(response);
    }

    public static Task<HttpResponseMessage> SearchWorkspaceRawAsync(
        AuthSession user,
        string query,
        Guid? projectId = null)
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["query"] = query
        };
        if (projectId.HasValue)
        {
            queryParams["projectId"] = projectId.Value.ToString();
        }

        return user.Client.GetAsync(
            QueryHelpers.AddQueryString("/api/v1/search", queryParams));
    }

    public static async Task<PagedChangelogResponse> GetProjectChangelogAsync(
        AuthSession user,
        Guid projectId,
        IEnumerable<string>? entityTypes = null,
        IEnumerable<string>? changeTypes = null,
        Guid? featureId = null,
        Guid? authorId = null,
        string? from = null,
        string? to = null,
        int? page = null,
        int? pageSize = null)
    {
        var response = await GetProjectChangelogRawAsync(
            user, projectId, entityTypes, changeTypes, featureId, authorId, from, to, page, pageSize);
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<PagedChangelogResponse>(response);
    }

    public static async Task<IReadOnlyList<ChangelogAuthorResponse>> GetProjectChangelogAuthorsAsync(
        AuthSession user,
        Guid projectId)
    {
        var response = await user.Client.GetAsync($"/api/v1/projects/{projectId}/changelog/authors");
        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
        return await ReadAsAsync<List<ChangelogAuthorResponse>>(response);
    }

    public static async Task RemoveTeamMemberAsync(AuthSession lead, Guid teamId, Guid userId)
    {
        var response = await lead.Client.DeleteAsync($"/api/v1/teams/{teamId}/members/{userId}");
        await AssertStatusCodeAsync(response, HttpStatusCode.NoContent);
    }

    public static Task<HttpResponseMessage> GetProjectChangelogRawAsync(
        AuthSession user,
        Guid projectId,
        IEnumerable<string>? entityTypes = null,
        IEnumerable<string>? changeTypes = null,
        Guid? featureId = null,
        Guid? authorId = null,
        string? from = null,
        string? to = null,
        int? page = null,
        int? pageSize = null)
    {
        var queryParams = new List<KeyValuePair<string, string?>>();
        if (entityTypes is not null)
        {
            queryParams.AddRange(entityTypes.Select(value => new KeyValuePair<string, string?>("entityTypes", value)));
        }
        if (changeTypes is not null)
        {
            queryParams.AddRange(changeTypes.Select(value => new KeyValuePair<string, string?>("changeTypes", value)));
        }
        if (featureId.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("featureId", featureId.Value.ToString()));
        }
        if (authorId.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("authorId", authorId.Value.ToString()));
        }
        if (from is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("from", from));
        }
        if (to is not null)
        {
            queryParams.Add(new KeyValuePair<string, string?>("to", to));
        }
        if (page.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("page", page.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (pageSize.HasValue)
        {
            queryParams.Add(new KeyValuePair<string, string?>("pageSize", pageSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return user.Client.GetAsync(
            QueryHelpers.AddQueryString($"/api/v1/projects/{projectId}/changelog", queryParams));
    }

    private static async Task AssertStatusCodeAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        Assert.Fail(
            $"Expected HTTP {(int)expected} ({expected}) but got {(int)response.StatusCode} ({response.StatusCode}). Response body: {body}");
    }

    private static async Task<T> ReadAsAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed record AuthSession(HttpClient Client, Guid UserId, string Email);

public sealed record MeResponse(Guid Id, string Email, string? DisplayName, DateTime? OnboardingCompletedAt);

public sealed record TeamResponse(Guid Id, string Name, DateTime CreatedAt);

public sealed record TeamListItemResponse(Guid Id, string Name, DateTime CreatedAt, TeamRole Role);

public sealed record TeamMemberSummaryResponse(Guid UserId, string Email, string? DisplayName, TeamRole Role);

public sealed record ProjectResponse(Guid Id, Guid TeamId, string Name, string? Description, DateTime CreatedAt);

public sealed record FeatureResponse(Guid Id, Guid ProjectId, string Name, string? Description, DateTime CreatedAt);

public sealed record PagedFeaturesResponse(
    IReadOnlyList<FeatureResponse> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    int TotalPages,
    Guid? LastSeenId);

public sealed record RequirementResponse(
    Guid Id,
    Guid RequirementId,
    Guid FeatureId,
    int SequenceNumber,
    string Ref);

public sealed record SaveRequirementResponse(RequirementResponse Requirement);

public sealed record WorkItemResponse(
    Guid Id,
    Guid WorkItemId,
    Guid ProjectId,
    Guid? AssignedToUserId,
    int SequenceNumber,
    string Ref,
    string Title,
    string? Type,
    string? AssignedToDisplayName);

public sealed record WorkItemLinkResponse(
    Guid Id,
    Guid WorkItemLinkId,
    Guid WorkItemId,
    Guid RequirementId,
    WorkItemLinkChange ChangeType,
    bool IsDeleted);

public sealed record LinkedWorkItemResponse(
    Guid WorkItemLinkId,
    Guid WorkItemId,
    string Ref,
    string Title,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    WorkItemType Type,
    Guid? AssignedToUserId,
    string? AssignedToDisplayName,
    string? BlockedReason);

public sealed record ResolveProjectReferenceResponse(
    string Ref,
    string Type,
    Guid Id,
    int SequenceNumber,
    string Title,
    string Status);

public sealed record SearchResultResponse(string Type, string Id, string Title, string? Status, string? SubTitle);

public sealed record SearchGroupedResponse(
    IReadOnlyList<SearchResultResponse> Requirements,
    IReadOnlyList<SearchResultResponse> WorkItems,
    IReadOnlyList<SearchResultResponse> Features);

public sealed record WorkspaceSearchResultResponse(
    string Type,
    string Id,
    string Title,
    string? Status,
    string? SubTitle,
    string ProjectId,
    string ProjectName,
    string TeamName);

public sealed record WorkspaceSearchResponse(
    IReadOnlyList<WorkspaceSearchResultResponse> Requirements,
    IReadOnlyList<WorkspaceSearchResultResponse> WorkItems,
    IReadOnlyList<WorkspaceSearchResultResponse> Features);

public sealed record PagedRequirementsResponse(
    IReadOnlyList<RequirementResponse> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    int TotalPages,
    Guid? LastSeenId);

public sealed record ChangelogEntryResponse(
    Guid EntryId,
    string EntityKind,
    Guid LogicalId,
    string? Reference,
    string Title,
    string ChangeType,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    string? ChangedByName,
    bool ChangedByIsActiveMember,
    Guid? FeatureId,
    Guid? LinkedWorkItemId,
    Guid? LinkedRequirementId);

public sealed record ChangelogAuthorResponse(
    Guid UserId,
    string DisplayName,
    bool IsActiveMember);

public sealed record PagedChangelogResponse(
    IReadOnlyList<ChangelogEntryResponse> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    int TotalPages,
    Guid? LastSeenId);

public sealed record PagedWorkItemsResponse(
    IReadOnlyList<WorkItemResponse> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    int TotalPages,
    Guid? LastSeenId);
