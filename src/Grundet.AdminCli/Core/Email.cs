namespace Grundet.AdminCli.Core;

internal static class Email
{
    /// <summary>
    /// Mirrors the API's EmailNormalizer (trim + lower-invariant). Do not introduce a
    /// CLI-only normalization rule; auth/waitlist matching depends on this being identical.
    /// </summary>
    public static string Normalize(string email) => string.IsNullOrWhiteSpace(email)
        ? throw new ArgumentException("Email is required.")
        : email.Trim().ToLowerInvariant();
}
