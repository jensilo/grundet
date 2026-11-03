using Grundet.Api.Common.Auth;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Billing;

public interface ITeamQuotaService
{
    Task<TeamUsageSummary> GetUsageAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default);

    Task<TeamUsageSummary> RecalculateUsageAsync(AuthorizedTeamId teamId, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task EnsureCanCreateProjectAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default);

    Task EnsureCanCreateRequirementAsync(AuthorizedFeatureId featureId, CancellationToken cancellationToken = default);

    Task EnsureCanCreateWorkItemAsync(AuthorizedProjectId projectId, CancellationToken cancellationToken = default);

    Task EnsureCanInviteMemberAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default);

    Task<TeamPlan> GetTeamPlanAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default);
}

public sealed record TeamLimits(
    int? MaxProjects,
    int? MaxTotalRequirementsAndWorkItems,
    int? MaxMembers,
    string AiUsageLabel)
{
    public bool IsUnlimited => MaxProjects is null && MaxTotalRequirementsAndWorkItems is null && MaxMembers is null;
}

public sealed record TeamUsageSummary(
    TeamPlan Plan,
    TeamLimits Limits,
    TeamUsageCounts? Usage,
    DateTime? CalculatedAt,
    RequirementWorkItemLimitStatus RequirementWorkItemStatus,
    DateTime? OverLimitSince,
    DateTime? EnforcementStartsAt,
    string CalculationCadence,
    string RecommendedAction);

public sealed record TeamUsageCounts(
    int Projects,
    int Members,
    int Requirements,
    int WorkItems)
{
    public int TotalRequirementsAndWorkItems => Requirements + WorkItems;
}
