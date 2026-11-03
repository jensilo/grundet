using System.Security.Claims;
using Grundet.Api.Common.Errors;

namespace Grundet.Api.Common.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string? value = principal.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(value)
            ? throw HttpProblemException.Unauthorized(
                type: ProblemTypes.Unauthorized,
                detail: "Authenticated user is missing required 'sub' claim.")
            : !Guid.TryParse(value, out var userId)
            ? throw HttpProblemException.Unauthorized(
                type: ProblemTypes.Unauthorized,
                detail: "Authenticated user has invalid 'sub' claim. Expected GUID.")
            : userId;
    }
}
