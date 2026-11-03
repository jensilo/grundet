namespace Grundet.Api.Features.Jira;

public interface IJiraStore
{
    Task<JiraConnectionRecord?> GetConnectionAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<string?> GetDefaultProjectKeyAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<bool> HasConnectionAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task UpsertConnectionAsync(JiraConnectionRecord connection, CancellationToken cancellationToken = default);

    Task DeleteConnectionAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<JiraProjectContext?> GetProjectContextAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<JiraProjectMappingRecord?> GetProjectMappingAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task UpsertProjectMappingAsync(Guid teamId, Guid projectId, string projectKey, CancellationToken cancellationToken = default);

    Task DeleteProjectMappingAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraProjectMappingListItem>> GetProjectMappingsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task DeleteProjectMappingsForTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<JiraExportWorkItem?> GetExportWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task SetProjectKeyAsync(Guid teamId, string projectKey, CancellationToken cancellationToken = default);
}

public sealed record JiraConnectionRecord(
    Guid Id,
    Guid TeamId,
    string CloudId,
    string AccessToken,
    string RefreshToken,
    DateTime TokenExpiresAt,
    string? JiraProjectKey,
    DateTime CreatedAt);

public sealed record JiraProjectMappingRecord(
    Guid TeamId,
    Guid ProjectId,
    string JiraProjectKey,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record JiraProjectMappingListItem(
    Guid ProjectId,
    string ProjectName,
    string JiraProjectKey);

public sealed record JiraProjectContext(
    Guid ProjectId,
    Guid TeamId,
    string ProjectName);

public sealed record JiraExportWorkItem(
    Guid WorkItemId,
    Guid TeamId,
    Guid ProjectId,
    string Title,
    string? Description,
    IReadOnlyList<JiraExportRequirement> Requirements);

public sealed record JiraExportRequirement(
    Guid RequirementId,
    string Text,
    string Status);
