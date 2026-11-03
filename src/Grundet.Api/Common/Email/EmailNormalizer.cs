using Grundet.Api.Common.Errors;

namespace Grundet.Api.Common.Email;

public static class EmailNormalizer
{
    public static string Normalize(string email) => string.IsNullOrWhiteSpace(email)
        ? throw HttpProblemException.BadRequest(
            type: ProblemTypes.Validation,
            detail: "Email is required.")
        : email.Trim().ToLowerInvariant();

    public static bool TryNormalize(string? email, out string normalizedEmail)
    {
        normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(normalizedEmail);
    }
}
