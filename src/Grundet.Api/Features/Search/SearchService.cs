using Grundet.Api.Common.Auth;

namespace Grundet.Api.Features.Search;

public sealed class SearchService(ISearchStore searchStore)
{
    public async Task<SearchGroupedResponse> SearchAsync(
        AuthorizedProjectId projectId,
        RequirementSearchCommand requirementCommand,
        WorkItemSearchCommand workItemCommand,
        FeatureSearchCommand featureCommand,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        var requirementTask = SearchRequirementsAsync(projectId, requirementCommand, cancellationToken);
        var workItemTask = SearchWorkItemsAsync(projectId, workItemCommand, cancellationToken);
        var featureTask = SearchFeaturesAsync(projectId, featureCommand, cancellationToken);

        await Task.WhenAll(requirementTask, workItemTask, featureTask);

        return new SearchGroupedResponse(
            await requirementTask,
            await workItemTask,
            await featureTask);
    }

    public async Task<List<SearchResultResponse>> SearchRequirementsAsync(
        AuthorizedProjectId projectId,
        RequirementSearchCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        var query = RequirementSearchQuery.From(projectId.Value, command);
        if (query.Query.Length == 0)
        {
            return [];
        }

        var rows = await searchStore.SearchRequirementsAsync(query, cancellationToken);
        return MapResults(rows);
    }

    public async Task<List<SearchResultResponse>> SearchWorkItemsAsync(
        AuthorizedProjectId projectId,
        WorkItemSearchCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        var query = WorkItemSearchQuery.From(projectId.Value, command);
        if (query.Query.Length == 0)
        {
            return [];
        }

        var rows = await searchStore.SearchWorkItemsAsync(query, cancellationToken);
        return MapResults(rows);
    }

    public async Task<List<SearchResultResponse>> SearchFeaturesAsync(
        AuthorizedProjectId projectId,
        FeatureSearchCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        var query = FeatureSearchQuery.From(projectId.Value, command);
        if (query.Query.Length == 0)
        {
            return [];
        }

        var rows = await searchStore.SearchFeaturesAsync(query, cancellationToken);
        return MapResults(rows);
    }

    public async Task<WorkspaceSearchResponse> SearchWorkspaceAsync(
        AuthorizedProjectId? scopedProjectId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuery = SearchQuerySanitizer.NormalizeQuery(query);
        if (normalizedQuery.Length == 0)
        {
            return WorkspaceSearchResponse.Empty;
        }

        Guid? projectId = null;
        if (scopedProjectId.HasValue)
        {
            scopedProjectId.Value.ThrowIfDefault();
            projectId = scopedProjectId.Value.Value;
        }

        var workspaceQuery = new WorkspaceSearchQuery(projectId, normalizedQuery, WorkspaceResultsPerType);

        var requirementTask = searchStore.SearchWorkspaceRequirementsAsync(workspaceQuery, cancellationToken);
        var workItemTask = searchStore.SearchWorkspaceWorkItemsAsync(workspaceQuery, cancellationToken);
        var featureTask = searchStore.SearchWorkspaceFeaturesAsync(workspaceQuery, cancellationToken);

        await Task.WhenAll(requirementTask, workItemTask, featureTask);

        return new WorkspaceSearchResponse(
            MapWorkspaceResults(await requirementTask),
            MapWorkspaceResults(await workItemTask),
            MapWorkspaceResults(await featureTask));
    }

    private const int WorkspaceResultsPerType = 8;

    private static List<SearchResultResponse> MapResults(IEnumerable<SearchResultRow> results)
        => results
            .Select(result => new SearchResultResponse(result.Type, result.Id, result.Title, result.Status, result.SubTitle))
            .ToList();

    private static List<WorkspaceSearchResultResponse> MapWorkspaceResults(IEnumerable<WorkspaceSearchResultRow> results)
        => results
            .Select(result => new WorkspaceSearchResultResponse(
                result.Type,
                result.Id,
                result.Title,
                result.Status,
                result.SubTitle,
                result.ProjectId,
                result.ProjectName,
                result.TeamName))
            .ToList();
}

public sealed record SearchGroupedResponse(
    IReadOnlyList<SearchResultResponse> Requirements,
    IReadOnlyList<SearchResultResponse> WorkItems,
    IReadOnlyList<SearchResultResponse> Features);

public sealed record SearchResultResponse(string Type, string Id, string Title, string? Status, string? SubTitle);

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
    IReadOnlyList<WorkspaceSearchResultResponse> Features)
{
    public static WorkspaceSearchResponse Empty { get; } = new([], [], []);
}
