using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.AlphaWaitlist;

public interface IAlphaWaitlistStore
{
    Task<bool> UpsertPendingAsync(AlphaWaitlistEntry entry, CancellationToken cancellationToken = default);

    Task<bool> IsApprovedAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task ApproveViaTeamInviteAsync(string normalizedEmail, string originalEmail, string reason, DateTime utcNow, CancellationToken cancellationToken = default);
}
