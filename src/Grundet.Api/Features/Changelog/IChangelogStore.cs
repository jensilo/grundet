namespace Grundet.Api.Features.Changelog;

public interface IChangelogStore
{
    Task<(IReadOnlyList<ChangelogEntryResponse> Items, int Total)> GetProjectChangelogAsync(
        Guid projectId,
        ChangelogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the given author ids to a display name and whether they are still an active member of
    /// the project's team. Ids that no longer resolve to a user (e.g. a deleted account) are absent
    /// from the result; callers fall back to an "unknown member" label for those.
    /// </summary>
    Task<Dictionary<Guid, ChangelogAuthor>> GetAuthorsAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct author ids across every Intent, Work, and Link change in the project, regardless of
    /// whether the author is still a team member. Used to build the changelog author filter.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetChangelogAuthorIdsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ChangelogAuthor(string DisplayName, bool IsActiveMember);
