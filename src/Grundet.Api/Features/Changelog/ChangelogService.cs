using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Features.DomainFeatures;

namespace Grundet.Api.Features.Changelog;

public sealed class ChangelogService(
    IChangelogStore changelogStore,
    IFeatureStore featureStore)
{
    public async Task<PagedResponse<ChangelogEntryResponse>> GetProjectChangelogAsync(
        AuthorizedProjectId projectId,
        ChangelogQuery query,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        // A feature filter must belong to this project. Feature access is already authorized at the
        // endpoint; here we enforce the route-level cross-project rule (mismatch -> 404), consistent
        // with other project-scoped reads. authorId needs no extra check: the query is project-scoped,
        // so a non-member author simply yields no rows (no cross-tenant leak).
        if (query.FeatureId is Guid featureId)
        {
            _ = await featureStore.GetByProjectAsync(projectId.Value, featureId, cancellationToken)
                ?? throw HttpProblemException.NotFound(
                    ProblemTypes.NotFound,
                    $"Feature '{featureId}' was not found in this project.");
        }

        var (items, total) = await changelogStore.GetProjectChangelogAsync(
            projectId.Value, query, cancellationToken);

        if (items.Count > 0)
        {
            var authors = await changelogStore.GetAuthorsAsync(
                projectId.Value,
                items.Select(item => item.ChangedByUserId).Distinct().ToArray(),
                cancellationToken);

            items = items
                .Select(item =>
                {
                    var author = authors.GetValueOrDefault(item.ChangedByUserId);
                    return item with
                    {
                        ChangedByName = author?.DisplayName,
                        ChangedByIsActiveMember = author?.IsActiveMember ?? false,
                    };
                })
                .ToList();
        }

        return PagedResponse.Create(items, query.Page, query.PageSize, total);
    }

    /// <summary>
    /// The distinct set of authors that appear anywhere in the project changelog, including members who
    /// have since left the team. Powers the changelog author filter so past contributors stay selectable.
    /// </summary>
    public async Task<IReadOnlyList<ChangelogAuthorResponse>> GetAuthorsAsync(
        AuthorizedProjectId projectId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        var ids = await changelogStore.GetChangelogAuthorIdsAsync(projectId.Value, cancellationToken);
        if (ids.Count == 0)
        {
            return [];
        }

        var authors = await changelogStore.GetAuthorsAsync(projectId.Value, ids, cancellationToken);

        // Drop ids that no longer resolve to a user (deleted accounts): they are useless as a filter,
        // and the timeline already renders them as an unknown member. Active members surface first.
        return authors
            .Select(pair => new ChangelogAuthorResponse(pair.Key, pair.Value.DisplayName, pair.Value.IsActiveMember))
            .OrderByDescending(author => author.IsActiveMember)
            .ThenBy(author => author.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

// Deliberate exception: unlike the rest of the backend (Requirement/WorkItem/WorkItemLink), the
// changelog intentionally speaks the product's UI vocabulary (Intent/Work/Link) on the wire. Do
// not rename to match backend domain naming — this enum is part of the OpenAPI contract and
// renaming it would ripple into the generated TypeScript client and frontend.
public enum ChangelogEntityKind
{
    Intent,
    Work,
    Link
}

/// <summary>
/// Project changelog query. <paramref name="From"/> is inclusive, <paramref name="To"/> is exclusive,
/// both UTC. A non-null <paramref name="FeatureId"/> restricts results to Intent rows for that feature
/// (Work and Link rows are not feature-scoped and are excluded). An empty <paramref name="EntityKinds"/>
/// or <paramref name="ChangeTypes"/> means "no filter on that dimension".
/// </summary>
public sealed record ChangelogQuery(
    IReadOnlyList<ChangelogEntityKind> EntityKinds,
    IReadOnlyList<string> ChangeTypes,
    Guid? FeatureId,
    Guid? AuthorId,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize);

/// <summary>
/// A single changelog row. For <c>Link</c> rows, <paramref name="Reference"/>/<paramref name="LinkedWorkItemId"/>
/// identify the work item and <paramref name="Title"/>/<paramref name="LinkedRequirementId"/> identify the
/// requirement, so the frontend can link both ends to their detail pages. Both linked ids are null for
/// Intent and Work rows (whose own <paramref name="LogicalId"/> already addresses the item).
/// </summary>
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

/// <summary>
/// A distinct author of changelog activity in a project. <paramref name="IsActiveMember"/> is false when
/// the user has left the project's team; the frontend uses it to mark former members.
/// </summary>
public sealed record ChangelogAuthorResponse(
    Guid UserId,
    string DisplayName,
    bool IsActiveMember);
