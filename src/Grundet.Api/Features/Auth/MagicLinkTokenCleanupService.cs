namespace Grundet.Api.Features.Auth;

public sealed partial class MagicLinkTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<MagicLinkTokenCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    private static readonly TimeSpan RetentionBuffer = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        await DeleteExpiredTokensAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DeleteExpiredTokensAsync(stoppingToken);
        }
    }

    private async Task DeleteExpiredTokensAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var authStore = scope.ServiceProvider.GetRequiredService<IAuthStore>();
            var cutoff = DateTime.UtcNow.Subtract(RetentionBuffer);
            int deletedCount = await authStore.DeleteExpiredMagicLinkTokensAsync(cutoff, cancellationToken);

            if (deletedCount > 0)
            {
                LogTokensDeleted(logger, deletedCount);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {DeletedCount} expired magic-link tokens past retention.")]
    private static partial void LogTokensDeleted(ILogger logger, int deletedCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete expired magic-link tokens.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
