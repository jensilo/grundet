namespace Grundet.Api.Features.Billing;

public interface ITeamUsageRecalculationService
{
    Task RecalculateAllTeamsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    Task RecalculateAllTeamsIfExistingSnapshotsAreStaleAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
