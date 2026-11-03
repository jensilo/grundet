using System.Security.Cryptography;
using System.Text;
using Grundet.Api.Common.Email;
using Grundet.Api.Common.Errors;
using Grundet.Api.Features.AlphaWaitlist;

namespace Grundet.Api.Features.Auth;

public sealed class AuthService(
    IAuthStore authStore,
    IEmailService emailService,
    AlphaWaitlistService alphaWaitlistService,
    IConfiguration configuration)
{
    internal const int MinTokenLength = 16;
    internal const int MaxTokenLength = 512;
    internal const int MagicLinkTokenLength = 64;
    private readonly int _magicLinkExpiryMinutes = Math.Max(1, configuration.GetValue<int?>("MagicLink:ExpiryMinutes") ?? 30);
    private readonly string? _apiBaseUrl = configuration.GetValue<string>("MagicLink:PublicApiBaseUrl")?.TrimEnd('/');
    private readonly string? _appBaseUrl = configuration.GetValue<string>("MagicLink:PublicAppBaseUrl")?.TrimEnd('/');
    private readonly bool _alphaAccessEnabled = configuration.GetValue<bool?>("AlphaAccess:Enabled") ?? true;

    public async Task SendMagicLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = EmailNormalizer.Normalize(email);
        string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string tokenHash = ComputeTokenHash(rawToken);
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_magicLinkExpiryMinutes);

        await authStore.InsertMagicLinkTokenAsync(
            new MagicLinkTokenRecord(Guid.NewGuid(), null, normalizedEmail, tokenHash, expiresAt, null),
            cancellationToken);

        string verifyUrl = BuildVerifyUrl(rawToken);
        string htmlBody = EmailTemplates.BuildMagicLinkEmail(verifyUrl, _magicLinkExpiryMinutes);

        await emailService.SendAsync(normalizedEmail, "Your Grundet sign-in link", htmlBody, cancellationToken);
    }

    public async Task<MagicLinkVerificationResult?> VerifyMagicLinkAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        string? normalizedToken = rawToken?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedToken) || normalizedToken.Length != MagicLinkTokenLength)
        {
            return null;
        }

        if (!IsValidTokenFormat(normalizedToken))
        {
            return null;
        }

        string tokenHash = ComputeTokenHash(normalizedToken);
        var now = DateTime.UtcNow;
        var token = await authStore.RedeemMagicLinkTokenAsync(tokenHash, now, cancellationToken);

        if (token is null)
        {
            return null;
        }

        var (user, isNewUser) = await GetOrCreateUserAsync(token.Email, cancellationToken);
        // Onboarding is unfinished until both the display name (Step 1) and the completion
        // flag (Step 2) are set; /onboarding resumes at the right step.
        bool onboardingUnfinished =
            string.IsNullOrWhiteSpace(user.DisplayName) || user.OnboardingCompletedAt is null;
        string redirectPath = onboardingUnfinished ? "/onboarding" : "/";
        string redirectUrl = BuildAppRedirectUrl(redirectPath);

        return new MagicLinkVerificationResult(user, isNewUser, redirectUrl);
    }

    public async Task<(AuthUser User, bool IsNewUser)> GetOrCreateUserAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = EmailNormalizer.Normalize(email);
        var existingUser = await authStore.FindUserByEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            return (existingUser, false);
        }

        if (_alphaAccessEnabled && !await alphaWaitlistService.IsApprovedEmailAsync(normalizedEmail, cancellationToken))
        {
            throw HttpProblemException.Forbidden(
                type: ProblemTypes.PrivateAlphaAccessRequired,
                detail: AlphaWaitlistService.PrivateAlphaMessage);
        }

        var insertedUser = await authStore.InsertUserWithSoloWorkspaceAsync(normalizedEmail, null, DateTime.UtcNow, cancellationToken);
        return (insertedUser, true);
    }

    public Task<AuthUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => authStore.FindUserByIdAsync(userId, cancellationToken);

    public async Task<AuthUser?> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        string normalizedDisplayName = NormalizeDisplayName(displayName);
        return await authStore.UpdateDisplayNameAsync(userId, normalizedDisplayName, cancellationToken);
    }

    public Task<AuthUser?> CompleteOnboardingAsync(Guid userId, CancellationToken cancellationToken = default)
        => authStore.CompleteOnboardingAsync(userId, DateTime.UtcNow, cancellationToken);

    public string BuildInviteAcceptRedirectUrl(string token)
    {
        string normalizedToken = token?.Trim() ?? string.Empty;

        return !IsValidTokenFormat(normalizedToken)
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.InvalidToken,
                detail: "Invite token is invalid.")
            : BuildAppRedirectUrl($"/invite/accept?token={Uri.EscapeDataString(normalizedToken)}");
    }

    public string BuildTeamRedirectUrl(Guid teamId)
        => BuildAppRedirectUrl($"/teams/{teamId}");

    public string BuildDashboardRedirectUrl()
        => BuildAppRedirectUrl("/dashboard");

    public string BuildAuthErrorRedirectUrl(string reason)
        => BuildAppRedirectUrl($"/auth/error?reason={Uri.EscapeDataString(reason)}");

    internal static bool IsValidTokenFormat(string? token)
    {
        string? trimmed = token?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (trimmed.Length is < MinTokenLength or > MaxTokenLength)
        {
            return false;
        }

        foreach (char character in trimmed)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string ComputeTokenHash(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    private static string NormalizeDisplayName(string displayName)
    {
        string trimmed = displayName?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(trimmed)
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Display name is required.")
            : trimmed.Length > 120
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Display name cannot exceed 120 characters.")
            : trimmed;
    }

    private string BuildVerifyUrl(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(_apiBaseUrl))
        {
            throw HttpProblemException.InternalServerError(
                type: ProblemTypes.ServerMisconfiguration,
                detail: "MagicLink:PublicApiBaseUrl must be configured to build verify URLs.");
        }

        string encodedToken = Uri.EscapeDataString(rawToken);
        return $"{_apiBaseUrl}/api/v1/auth/verify?token={encodedToken}";
    }

    private string BuildAppRedirectUrl(string path) => string.IsNullOrWhiteSpace(_appBaseUrl)
            ? throw HttpProblemException.InternalServerError(
                type: ProblemTypes.ServerMisconfiguration,
                detail: "MagicLink:PublicAppBaseUrl must be configured to build app redirects.")
            : !path.StartsWith('/')
            ? throw HttpProblemException.InternalServerError(
                type: ProblemTypes.ServerMisconfiguration,
                detail: "App redirect path must be an absolute path.")
            : $"{_appBaseUrl}{path}";
}
