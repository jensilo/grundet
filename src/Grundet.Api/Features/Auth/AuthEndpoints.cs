using System.Net.Mail;
using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Features.Teams;
using Grundet.Api.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace Grundet.Api.Features.Auth;

public static class AuthEndpoints
{
    public const string MagicLinkRateLimitPolicy = "auth-magic-link";
    public const string VerifyMagicLinkRateLimitPolicy = "auth-verify-magic-link";

    public static void Map(RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth");

        auth.MapPost("/magic-link", async (
                SendMagicLinkRequest request,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    throw HttpProblemException.BadRequest(
                        type: ProblemTypes.Validation,
                        detail: "Email is required.");
                }

                if (!MailAddress.TryCreate(request.Email.Trim(), out _))
                {
                    throw HttpProblemException.BadRequest(
                        type: ProblemTypes.Validation,
                        detail: "Email format is invalid.");
                }

                await authService.SendMagicLinkAsync(request.Email, cancellationToken);

                return Results.Accepted();
            })
            .RequireRateLimiting(MagicLinkRateLimitPolicy)
            .AllowAnonymous()
            .SkipOnboardingGuard()
            .WithName("SendMagicLink")
            .WithSummary("Send a magic link email")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapGet("/verify", async (
                string token,
                HttpContext httpContext,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                MagicLinkVerificationResult? verification;

                try
                {
                    verification = await authService.VerifyMagicLinkAsync(token, cancellationToken);
                }
                catch (HttpProblemException exception) when (exception.Type == ProblemTypes.PrivateAlphaAccessRequired)
                {
                    return Results.Redirect(authService.BuildAuthErrorRedirectUrl("private_alpha_access_required"));
                }

                if (verification is null)
                {
                    return httpContext.User.Identity?.IsAuthenticated == true
                        ? Results.Redirect(authService.BuildDashboardRedirectUrl())
                        : Results.Redirect(authService.BuildAuthErrorRedirectUrl("expired_link"));
                }

                await SignInAsync(httpContext, verification.User, cancellationToken);

                return Results.Redirect(verification.RedirectUrl);
            })
            .RequireRateLimiting(VerifyMagicLinkRateLimitPolicy)
            .SkipOnboardingGuard()
            .AllowAnonymous()
            .WithName("VerifyMagicLink")
            .WithSummary("Verify a magic link token and redirect")
            .Produces(StatusCodes.Status302Found);

        // Logout is identity-scoped and has no tenant resource id to authorize.
        auth.MapPost("/logout", async (HttpContext httpContext) =>
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .SkipOnboardingGuard()
            .WithName("Logout")
            .WithSummary("Log out the current user")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/invite/accept", async (
                AcceptInviteRequest request,
                ClaimsPrincipal principal,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                if (!AuthService.IsValidTokenFormat(request.Token))
                {
                    throw HttpProblemException.BadRequest(
                        type: ProblemTypes.InvalidToken,
                        detail: "Invite token is invalid.");
                }

                var acceptedInvite = await teamService.AcceptInviteAsync(
                    request.Token,
                    principal.GetUserId(),
                    cancellationToken);

                return Results.Ok(acceptedInvite);
            })
            .RequireAuthorization()
            .SkipOnboardingGuard()
            .WithName("AcceptInvite")
            .WithSummary("Accept a team invite")
            .Produces<AcceptInviteResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    public sealed record AcceptInviteRequest(string Token);

    internal static async Task SignInAsync(HttpContext httpContext, AuthUser user, CancellationToken cancellationToken)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new(ClaimTypes.Email, user.Email)
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim("displayName", user.DisplayName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            IssuedUtc = DateTimeOffset.UtcNow
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);
    }

    public sealed record SendMagicLinkRequest(string Email);
}
