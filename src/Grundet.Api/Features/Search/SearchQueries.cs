using Grundet.Api.Common.Auth;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Search;

public sealed record RequirementSearchCommand(
    string? Query,
    IReadOnlyList<RequirementStatus> Statuses,
    IReadOnlyList<RequirementPriority> Priorities,
    IReadOnlyList<string> Labels,
    AuthorizedFeatureId? FeatureId);

public sealed record WorkItemSearchCommand(
    string? Query,
    IReadOnlyList<WorkItemStatus> Statuses,
    IReadOnlyList<WorkItemPriority> Priorities,
    IReadOnlyList<WorkItemType> Types,
    Guid? AssignedToUserId,
    IReadOnlyList<string> Labels);

public sealed record FeatureSearchCommand(string? Query);

public sealed record RequirementSearchQuery(
    Guid ProjectId,
    string Query,
    IReadOnlyList<RequirementStatus> Statuses,
    IReadOnlyList<RequirementPriority> Priorities,
    IReadOnlyList<string> Labels,
    AuthorizedFeatureId? FeatureId,
    int Limit)
{
    public static RequirementSearchQuery From(Guid projectId, RequirementSearchCommand command)
        => new(
            projectId,
            SearchQuerySanitizer.NormalizeQuery(command.Query),
            SearchQuerySanitizer.NormalizeEnums(command.Statuses),
            SearchQuerySanitizer.NormalizeEnums(command.Priorities),
            SearchQuerySanitizer.NormalizeLabels(command.Labels),
            command.FeatureId,
            SearchLimits.ProjectResultsPerType);
}

public sealed record WorkItemSearchQuery(
    Guid ProjectId,
    string Query,
    IReadOnlyList<WorkItemStatus> Statuses,
    IReadOnlyList<WorkItemPriority> Priorities,
    IReadOnlyList<WorkItemType> Types,
    Guid? AssignedToUserId,
    IReadOnlyList<string> Labels,
    int Limit)
{
    public static WorkItemSearchQuery From(Guid projectId, WorkItemSearchCommand command)
        => new(
            projectId,
            SearchQuerySanitizer.NormalizeQuery(command.Query),
            SearchQuerySanitizer.NormalizeEnums(command.Statuses),
            SearchQuerySanitizer.NormalizeEnums(command.Priorities),
            SearchQuerySanitizer.NormalizeEnums(command.Types),
            command.AssignedToUserId,
            SearchQuerySanitizer.NormalizeLabels(command.Labels),
            SearchLimits.ProjectResultsPerType);
}

public sealed record FeatureSearchQuery(Guid ProjectId, string Query, int Limit)
{
    public static FeatureSearchQuery From(Guid projectId, FeatureSearchCommand command)
        => new(projectId, SearchQuerySanitizer.NormalizeQuery(command.Query), SearchLimits.ProjectResultsPerType);
}

// Workspace-wide search. ProjectId is an optional scope: when null the query spans every
// project the caller can access (RLS scopes the rows to the caller's team membership); when
// set, results are restricted to that already-authorized project. Limit caps results per
// entity type so the topbar command palette stays fast.
public sealed record WorkspaceSearchQuery(Guid? ProjectId, string Query, int Limit);

internal static class SearchLimits
{
    // Project-scoped search (dedicated search page) can afford a larger cap per entity type than
    // the workspace command palette (SearchService.WorkspaceResultsPerType), since it renders a
    // full page of results rather than a topbar dropdown.
    public const int ProjectResultsPerType = 20;
}

internal static class SearchQuerySanitizer
{
    public static string NormalizeQuery(string? query)
        => query?.Trim() ?? string.Empty;

    public static IReadOnlyList<TEnum> NormalizeEnums<TEnum>(IReadOnlyList<TEnum> values)
        where TEnum : struct, Enum
        => values
            .Distinct()
            .ToArray();

    public static IReadOnlyList<string> NormalizeLabels(IReadOnlyList<string> labels)
        => labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .Where(label => label.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
