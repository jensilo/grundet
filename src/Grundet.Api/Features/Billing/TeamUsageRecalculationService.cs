using Grundet.Api.Data.Entities;
using Microsoft.Extensions.Options;

namespace Grundet.Api.Features.Billing;

public sealed class TeamUsageRecalculationService(
    ITeamQuotaStore quotaStore,
    IOptions<BillingUsageRecalculationOptions> options,
    ILogger<TeamUsageRecalculationService> logger) : ITeamUsageRecalculationService
{
    public async Task RecalculateAllTeamsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (!await quotaStore.TryAcquireUsageRecalculationLockAsync(cancellationToken))
        {
            TeamUsageRecalculationLog.LockHeld(logger);
            return;
        }

        try
        {
            await RecalculateAllTeamsWithoutLockAsync(nowUtc, cancellationToken);
        }
        finally
        {
            await quotaStore.ReleaseUsageRecalculationLockAsync(cancellationToken);
        }
    }

    public async Task RecalculateAllTeamsIfExistingSnapshotsAreStaleAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (!await quotaStore.TryAcquireUsageRecalculationLockAsync(cancellationToken))
        {
            TeamUsageRecalculationLog.StartupLockHeld(logger);
            return;
        }

        try
        {
            DateTime staleBeforeUtc = nowUtc.AddHours(-options.Value.StaleAfterHours);
            if (!await quotaStore.HasAnyStaleExistingUsageSnapshotAsync(staleBeforeUtc, cancellationToken))
            {
                TeamUsageRecalculationLog.StartupSnapshotsFresh(logger);
                return;
            }

            await RecalculateAllTeamsWithoutLockAsync(nowUtc, cancellationToken);
        }
        finally
        {
            await quotaStore.ReleaseUsageRecalculationLockAsync(cancellationToken);
        }
    }

    private async Task RecalculateAllTeamsWithoutLockAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var teamIds = await quotaStore.ListTeamIdsAsync(cancellationToken);
        int failedCount = 0;

        foreach (Guid teamId in teamIds)
        {
            try
            {
                await RecalculateTeamAsync(teamId, nowUtc, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedCount++;
                TeamUsageRecalculationLog.TeamFailed(logger, exception, teamId);
            }
        }

        TeamUsageRecalculationLog.Completed(logger, teamIds.Count, failedCount);
    }

    private async Task RecalculateTeamAsync(Guid teamId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var plan = await quotaStore.GetTeamPlanAsync(teamId, cancellationToken);
        if (plan is null)
        {
            TeamUsageRecalculationLog.MissingTeam(logger, teamId);
            return;
        }

        var previous = await quotaStore.GetUsageSnapshotAsync(teamId, cancellationToken);
        var usage = await quotaStore.CountUsageForRecalculationAsync(teamId, cancellationToken);
        var status = TeamQuotaService.CalculateRequirementWorkItemStatus(
            plan.Value,
            usage.TotalRequirementsAndWorkItems,
            previous,
            nowUtc,
            out DateTime? overLimitSince,
            out DateTime? enforcementStartsAt);
        var snapshot = new TeamUsageSnapshot
        {
            TeamId = teamId,
            Plan = plan.Value,
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
    }
}
