namespace Grundet.Api.Features.Billing;

public sealed class BillingUsageRecalculationOptions
{
    public bool Enabled { get; init; } = true;

    public int StartupDelaySeconds { get; init; } = 300;

    public TimeOnly DailyRunTimeUtc { get; init; } = new(3, 30);

    public int StaleAfterHours { get; init; } = 20;
}
