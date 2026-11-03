namespace Grundet.Api.Features.Search;

public interface ISearchStore
{
    Task<List<SearchResultRow>> SearchRequirementsAsync(
        RequirementSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<List<SearchResultRow>> SearchWorkItemsAsync(
        WorkItemSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<List<SearchResultRow>> SearchFeaturesAsync(
        FeatureSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<List<WorkspaceSearchResultRow>> SearchWorkspaceRequirementsAsync(
        WorkspaceSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<List<WorkspaceSearchResultRow>> SearchWorkspaceWorkItemsAsync(
        WorkspaceSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<List<WorkspaceSearchResultRow>> SearchWorkspaceFeaturesAsync(
        WorkspaceSearchQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class SearchResultRow
{
    // Mutable setters are required for EF Core SqlQueryRaw materialization.
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? SubTitle { get; set; }
    public float Rank { get; set; }
}

public sealed class WorkspaceSearchResultRow
{
    // Mutable setters are required for EF Core SqlQueryRaw materialization.
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? SubTitle { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public float Rank { get; set; }
}
