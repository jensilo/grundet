namespace Grundet.Api.Features.Billing;

internal static partial class TeamUsageRecalculationLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Skipping team usage recalculation because another instance holds the lock.")]
    public static partial void LockHeld(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Skipping startup team usage recalculation because another instance holds the lock.")]
    public static partial void StartupLockHeld(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Skipping startup team usage recalculation because existing snapshots are fresh.")]
    public static partial void StartupSnapshotsFresh(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to recalculate usage for team {TeamId}.")]
    public static partial void TeamFailed(ILogger logger, Exception exception, Guid teamId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Team usage recalculation completed for {TeamCount} teams with {FailedCount} failures.")]
    public static partial void Completed(ILogger logger, int teamCount, int failedCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Skipping usage recalculation for missing team {TeamId}.")]
    public static partial void MissingTeam(ILogger logger, Guid teamId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Team usage recalculation scheduler is disabled.")]
    public static partial void SchedulerDisabled(ILogger logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Startup team usage recalculation failed.")]
    public static partial void StartupFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Scheduled team usage recalculation failed.")]
    public static partial void ScheduledFailed(ILogger logger, Exception exception);
}
