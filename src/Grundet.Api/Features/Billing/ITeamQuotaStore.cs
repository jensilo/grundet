using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Billing;

public interface ITeamQuotaStore
{
    Task<TeamPlan?> GetTeamPlanAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<int> CountProjectsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<ProjectTeamUsage?> GetProjectTeamUsageAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectTeamUsage?> GetFeatureTeamUsageAsync(Guid featureId, CancellationToken cancellationToken = default);

    Task<RequirementWorkItemLimitStatus?> GetProjectRequirementWorkItemStatusAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<RequirementWorkItemLimitStatus?> GetFeatureRequirementWorkItemStatusAsync(Guid featureId, CancellationToken cancellationToken = default);

    Task<TeamUsageSnapshot?> GetUsageSnapshotAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamUsageCounts> CountUsageAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamUsageCounts> CountUsageForRecalculationAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task UpsertUsageSnapshotAsync(TeamUsageSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<TeamMemberUsage?> GetTeamMemberUsageAsync(Guid teamId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<bool> TryAcquireUsageRecalculationLockAsync(CancellationToken cancellationToken = default);

    Task ReleaseUsageRecalculationLockAsync(CancellationToken cancellationToken = default);

    Task<bool> HasAnyStaleExistingUsageSnapshotAsync(DateTime staleBeforeUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListTeamIdsAsync(CancellationToken cancellationToken = default);
}

public sealed record ProjectTeamUsage(Guid TeamId, TeamPlan Plan, int TotalRequirementsAndWorkItems);

public sealed record TeamMemberUsage(TeamPlan Plan, int ActiveMembersAndPendingInvites);
