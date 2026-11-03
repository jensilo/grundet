using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Projects;

public interface IProjectStore
{
    Task AddProjectAsync(Project project, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Project> Items, int Total)> ListPageByTeamAsync(
        Guid teamId,
        int page,
        int pageSize,
        Guid? lastSeenId,
        CancellationToken cancellationToken = default);

    Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Project?> GetByTeamAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default);

    Task<Project?> GetByTeamForUpdateAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task SoftDeleteProjectAsync(Guid teamId, Guid projectId, Guid changedByUserId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<ResolvedRequirementReference?> ResolveRequirementReferenceAsync(
        Guid projectId,
        int sequenceNumber,
        CancellationToken cancellationToken = default);

    Task<ResolvedWorkItemReference?> ResolveWorkItemReferenceAsync(
        Guid projectId,
        int sequenceNumber,
        CancellationToken cancellationToken = default);
}

public sealed record ResolvedRequirementReference(
    Guid RequirementId,
    int SequenceNumber,
    string Title,
    RequirementStatus Status);

public sealed record ResolvedWorkItemReference(
    Guid WorkItemId,
    int SequenceNumber,
    string Title,
    WorkItemStatus Status);
