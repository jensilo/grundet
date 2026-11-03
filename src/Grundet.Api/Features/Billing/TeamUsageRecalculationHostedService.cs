using Microsoft.Extensions.Options;

namespace Grundet.Api.Features.Billing;

public sealed class TeamUsageRecalculationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<BillingUsageRecalculationOptions> options,
    ILogger<TeamUsageRecalculationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            TeamUsageRecalculationLog.SchedulerDisabled(logger);
            return;
        }

        if (settings.StartupDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.StartupDelaySeconds), stoppingToken);
        }

        await RunStartupCatchUpAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay = GetDelayUntilNextRun(DateTime.UtcNow, settings.DailyRunTimeUtc);
            await Task.Delay(delay, stoppingToken);
            await RunScheduledRecalculationAsync(stoppingToken);
        }
    }

    internal static TimeSpan GetDelayUntilNextRun(DateTime nowUtc, TimeOnly dailyRunTimeUtc)
    {
        var todayRunUtc = new DateTime(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            dailyRunTimeUtc.Hour,
            dailyRunTimeUtc.Minute,
            dailyRunTimeUtc.Second,
            DateTimeKind.Utc);
        DateTime nextRunUtc = todayRunUtc > nowUtc ? todayRunUtc : todayRunUtc.AddDays(1);
        return nextRunUtc - nowUtc;
    }

    private async Task RunStartupCatchUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITeamUsageRecalculationService>();
            await service.RecalculateAllTeamsIfExistingSnapshotsAreStaleAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            TeamUsageRecalculationLog.StartupFailed(logger, exception);
        }
    }

    private async Task RunScheduledRecalculationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITeamUsageRecalculationService>();
            await service.RecalculateAllTeamsAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            TeamUsageRecalculationLog.ScheduledFailed(logger, exception);
        }
    }
}
