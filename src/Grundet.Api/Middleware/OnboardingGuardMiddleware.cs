using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Features.Auth;

namespace Grundet.Api.Middleware;

public sealed class OnboardingGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuthService authService)
    {
        if (!ShouldCheckOnboarding(context))
        {
            await next(context);
            return;
        }

        var user = await authService.GetUserByIdAsync(context.User.GetUserId(), context.RequestAborted) ?? throw HttpProblemException.Unauthorized(
                type: ProblemTypes.Unauthorized,
                detail: "Authenticated user could not be resolved.");

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            await next(context);
            return;
        }

        throw HttpProblemException.Forbidden(
            type: ProblemTypes.OnboardingRequired,
            detail: "Complete onboarding before accessing this resource.");
    }

    private static bool ShouldCheckOnboarding(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var endpoint = context.GetEndpoint();
        return endpoint is not null && endpoint.Metadata.GetMetadata<SkipOnboardingGuardMetadata>() is null;
    }
}

public static class OnboardingGuardMiddlewareExtensions
{
    public static IApplicationBuilder UseOnboardingGuard(this IApplicationBuilder app)
        => app.UseMiddleware<OnboardingGuardMiddleware>();
}
