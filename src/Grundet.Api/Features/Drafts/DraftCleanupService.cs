namespace Grundet.Api.Features.Drafts;

public sealed partial class DraftCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<DraftCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        await DeleteExpiredDraftsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DeleteExpiredDraftsAsync(stoppingToken);
        }
    }

    private async Task DeleteExpiredDraftsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var draftStore = scope.ServiceProvider.GetRequiredService<IDraftStore>();
            var cutoff = DateTime.UtcNow.AddYears(-1);
            int deletedCount = await draftStore.DeleteExpiredDraftsAsync(cutoff, cancellationToken);

            if (deletedCount > 0)
            {
                LogDraftsDeleted(logger, deletedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogCleanupFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {DeletedCount} expired drafts.")]
    private static partial void LogDraftsDeleted(ILogger logger, int deletedCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete expired drafts.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
