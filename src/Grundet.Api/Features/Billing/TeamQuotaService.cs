using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Billing;

public sealed class TeamQuotaService(ITeamQuotaStore quotaStore) : ITeamQuotaService
{
    private const string CalculationCadence = "Usage is calculated once per day.";
    private static readonly TimeSpan RequirementWorkItemGracePeriod = TimeSpan.FromDays(14);

    public async Task<TeamUsageSummary> GetUsageAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        var plan = await quotaStore.GetTeamPlanAsync(teamId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{teamId.Value}' was not found.");
        var snapshot = await quotaStore.GetUsageSnapshotAsync(teamId.Value, cancellationToken);

        return ToSummary(plan, snapshot);
    }

    public async Task<TeamUsageSummary> RecalculateUsageAsync(AuthorizedTeamId teamId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        var plan = await quotaStore.GetTeamPlanAsync(teamId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{teamId.Value}' was not found.");
        var previous = await quotaStore.GetUsageSnapshotAsync(teamId.Value, cancellationToken);
        var usage = await quotaStore.CountUsageAsync(teamId.Value, cancellationToken);
        var status = CalculateRequirementWorkItemStatus(plan, usage.TotalRequirementsAndWorkItems, previous, nowUtc, out DateTime? overLimitSince, out DateTime? enforcementStartsAt);
        var snapshot = new TeamUsageSnapshot
        {
            TeamId = teamId.Value,
            Plan = plan,
            ProjectCount = usage.Projects,
            MemberCount = usage.Members,
            RequirementCount = usage.Requirements,
            WorkItemCount = usage.WorkItems,
            RequirementWorkItemStatus = status,
            OverLimitSince = overLimitSince,
            EnforcementStartsAt = enforcementStartsAt,
            CalculatedAt = nowUtc
        };

        await quotaStore.UpsertUsageSnapshotAsync(snapshot, cancellationToken);
        return ToSummary(plan, snapshot);
    }

    public async Task EnsureCanCreateProjectAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        var plan = await quotaStore.GetTeamPlanAsync(teamId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{teamId.Value}' was not found.");
        var limits = GetLimits(plan);

        if (limits.MaxProjects is int maxProjects)
        {
            int current = await quotaStore.CountProjectsAsync(teamId.Value, cancellationToken);
            if (current >= maxProjects)
            {
                throw QuotaExceeded("Project limit reached for this workspace plan.");
            }
        }
    }

    public async Task EnsureCanCreateRequirementAsync(AuthorizedFeatureId featureId, CancellationToken cancellationToken = default)
    {
        featureId.ThrowIfDefault();
        var status = await quotaStore.GetFeatureRequirementWorkItemStatusAsync(featureId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Feature '{featureId.Value}' was not found.");
        EnsureRequirementWorkItemEnforcementAllowsCreate(status);
    }

    public async Task EnsureCanCreateWorkItemAsync(AuthorizedProjectId projectId, CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        var status = await quotaStore.GetProjectRequirementWorkItemStatusAsync(projectId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Project '{projectId.Value}' was not found.");
        EnsureRequirementWorkItemEnforcementAllowsCreate(status);
    }

    public async Task EnsureCanInviteMemberAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        var usage = await quotaStore.GetTeamMemberUsageAsync(teamId.Value, DateTime.UtcNow, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{teamId.Value}' was not found.");
        var limits = GetLimits(usage.Plan);

        if (!usage.Plan.AllowsCollaboration())
        {
            throw QuotaExceeded("Solo workspaces do not allow member invites. Create a team workspace to collaborate.");
        }

        if (limits.MaxMembers is int maxMembers && usage.ActiveMembersAndPendingInvites >= maxMembers)
        {
            throw QuotaExceeded("Member limit reached for this workspace plan.");
        }
    }

    public async Task<TeamPlan> GetTeamPlanAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        // Store method is nullable; team existence is already guaranteed by the caller's prior
        // RequireTeamAccessAsync, so a null here is a genuine NotFound rather than an auth concern.
        return await quotaStore.GetTeamPlanAsync(teamId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{teamId.Value}' was not found.");
    }

    public static TeamLimits GetLimits(TeamPlan plan)
        => plan switch
        {
            TeamPlan.Solo => new TeamLimits(3, 1_000, 1, "Fair-use included"),
            TeamPlan.Power => new TeamLimits(8, 5_000, 1, "Fair-use included"),
            TeamPlan.Expert => new TeamLimits(20, 30_000, 1, "Fair-use included"),
            TeamPlan.TeamS => new TeamLimits(6, 10_000, 5, "Fair-use included"),
            TeamPlan.TeamM => new TeamLimits(12, 50_000, 10, "Fair-use included"),
            TeamPlan.TeamL => new TeamLimits(24, 100_000, 15, "Fair-use included"),
            TeamPlan.TeamAlpha => new TeamLimits(10, null, null, "Within included usage"),
            TeamPlan.TeamUnlimited => new TeamLimits(null, null, null, "Within included usage"),
            _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unknown team plan.")
        };

    internal static RequirementWorkItemLimitStatus CalculateRequirementWorkItemStatus(
        TeamPlan plan,
        int totalItems,
        TeamUsageSnapshot? previous,
        DateTime nowUtc,
        out DateTime? overLimitSince,
        out DateTime? enforcementStartsAt)
    {
        var limits = GetLimits(plan);
        overLimitSince = null;
        enforcementStartsAt = null;

        if (limits.MaxTotalRequirementsAndWorkItems is not int maxItems)
        {
            return RequirementWorkItemLimitStatus.WithinLimit;
        }

        if (totalItems > maxItems)
        {
            overLimitSince = previous?.OverLimitSince ?? nowUtc;
            enforcementStartsAt = overLimitSince.Value.Add(RequirementWorkItemGracePeriod);
            return nowUtc >= enforcementStartsAt.Value
                ? RequirementWorkItemLimitStatus.EnforcementActive
                : RequirementWorkItemLimitStatus.OverLimitGrace;
        }

        decimal approachingThreshold = maxItems * 0.8m;
        return totalItems >= approachingThreshold
            ? RequirementWorkItemLimitStatus.ApproachingLimit
            : RequirementWorkItemLimitStatus.WithinLimit;
    }

    private static void EnsureRequirementWorkItemEnforcementAllowsCreate(RequirementWorkItemLimitStatus status)
    {
        if (status == RequirementWorkItemLimitStatus.EnforcementActive)
        {
            throw QuotaExceeded("Intent and work item limit enforcement is active for this workspace plan.");
        }
    }

    private static TeamUsageSummary ToSummary(TeamPlan plan, TeamUsageSnapshot? snapshot)
    {
        var usage = snapshot is null
            ? null
            : new TeamUsageCounts(snapshot.ProjectCount, snapshot.MemberCount, snapshot.RequirementCount, snapshot.WorkItemCount);
        var status = snapshot?.RequirementWorkItemStatus ?? RequirementWorkItemLimitStatus.WithinLimit;
        return new TeamUsageSummary(
            plan,
            GetLimits(plan),
            usage,
            snapshot?.CalculatedAt,
            status,
            snapshot?.OverLimitSince,
            snapshot?.EnforcementStartsAt,
            CalculationCadence,
            RecommendAction(usage));
    }

    private static string RecommendAction(TeamUsageCounts? usage)
    {
        if (usage is null)
        {
            return "Usage has not been calculated yet.";
        }

        foreach (TeamPlan plan in new[] { TeamPlan.Solo, TeamPlan.Power, TeamPlan.Expert, TeamPlan.TeamS, TeamPlan.TeamM, TeamPlan.TeamL })
        {
            var limits = GetLimits(plan);
            if (Fits(usage, limits))
            {
                return $"Current usage fits {plan}.";
            }
        }

        return "Contact support for enterprise limits.";
    }

    private static bool Fits(TeamUsageCounts usage, TeamLimits limits)
        => (!limits.MaxProjects.HasValue || usage.Projects <= limits.MaxProjects.Value)
            && (!limits.MaxMembers.HasValue || usage.Members <= limits.MaxMembers.Value)
            && (!limits.MaxTotalRequirementsAndWorkItems.HasValue || usage.TotalRequirementsAndWorkItems <= limits.MaxTotalRequirementsAndWorkItems.Value);

    private static HttpProblemException QuotaExceeded(string detail)
        => HttpProblemException.Conflict(ProblemTypes.QuotaExceeded, detail);
}
