namespace Grundet.Api.Features.Auth;

public sealed record AuthUser(Guid Id, string Email, string? DisplayName, DateTime CreatedAt, DateTime? OnboardingCompletedAt);

public sealed record MagicLinkTokenRecord(
    Guid Id,
    Guid? UserId,
    string Email,
    string TokenHash,
    DateTime ExpiresAt,
    DateTime? UsedAt);

public sealed record MagicLinkVerificationResult(AuthUser User, bool IsNewUser, string RedirectUrl);
