namespace Grundet.Api.Features.Auth;

public interface IAuthStore
{
    Task InsertMagicLinkTokenAsync(MagicLinkTokenRecord token, CancellationToken cancellationToken = default);

    Task<MagicLinkTokenRecord?> RedeemMagicLinkTokenAsync(string tokenHash, DateTime redeemedAtUtc, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredMagicLinkTokensAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    Task<AuthUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<AuthUser?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AuthUser> InsertUserAsync(string email, string? displayName, DateTime createdAtUtc, CancellationToken cancellationToken = default);

    Task<AuthUser> InsertUserWithSoloWorkspaceAsync(string email, string? displayName, DateTime createdAtUtc, CancellationToken cancellationToken = default);

    Task<AuthUser?> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default);

    Task<AuthUser?> CompleteOnboardingAsync(Guid userId, DateTime completedAtUtc, CancellationToken cancellationToken = default);
}
