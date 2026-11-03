using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Features.Auth;
using Grundet.Api.Middleware;

namespace Grundet.Api.Features.Users;

public static class UsersEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var users = group.MapGroup("/users").RequireAuthorization();
        // /users/me is identity-scoped and does not accept tenant resource ids, so it
        // intentionally does not call IResourceAuthorizationService.

        users.MapGet("/me", async (
                ClaimsPrincipal principal,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var user = await authService.GetUserByIdAsync(principal.GetUserId(), cancellationToken) ?? throw HttpProblemException.Unauthorized(
                        type: ProblemTypes.Unauthorized,
                        detail: "Authenticated user could not be resolved.");

                return Results.Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.OnboardingCompletedAt));
            })
            .WithName("GetMe")
            .WithSummary("Get the current user")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .SkipOnboardingGuard();

        users.MapPut("/me", async (
                UpdateMeRequest request,
                ClaimsPrincipal principal,
                HttpContext httpContext,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var updatedUser = await authService.UpdateDisplayNameAsync(
                    principal.GetUserId(),
                    request.DisplayName,
                    cancellationToken) ?? throw HttpProblemException.Unauthorized(
                        type: ProblemTypes.Unauthorized,
                        detail: "Authenticated user could not be resolved.");

                await AuthEndpoints.SignInAsync(httpContext, updatedUser, cancellationToken);
                return Results.Ok(new MeResponse(updatedUser.Id, updatedUser.Email, updatedUser.DisplayName, updatedUser.OnboardingCompletedAt));
            })
            .WithName("UpdateMe")
            .WithSummary("Update the current user's display name")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .SkipOnboardingGuard();

        users.MapPost("/me/onboarding/complete", async (
                ClaimsPrincipal principal,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                // Self-scoped: the target user is always the authenticated principal; the
                // request carries no body. Idempotent — repeat calls keep the first timestamp.
                var user = await authService.CompleteOnboardingAsync(principal.GetUserId(), cancellationToken) ?? throw HttpProblemException.Unauthorized(
                        type: ProblemTypes.Unauthorized,
                        detail: "Authenticated user could not be resolved.");

                return Results.Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.OnboardingCompletedAt));
            })
            .WithName("CompleteOnboarding")
            .WithSummary("Mark the current user's first-run onboarding as completed")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .SkipOnboardingGuard();
    }

    public sealed record MeResponse(Guid Id, string Email, string? DisplayName, DateTime? OnboardingCompletedAt);

    public sealed record UpdateMeRequest(string DisplayName);
}
