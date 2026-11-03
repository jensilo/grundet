using System.Security.Claims;
using System.Threading.RateLimiting;
using Grundet.Api.Common.Errors;
using Grundet.Api.Features.AlphaWaitlist;
using Grundet.Api.Middleware;
using Microsoft.AspNetCore.RateLimiting;

namespace Grundet.Api.Features.Auth;

public static class AuthRateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ProblemDetailsExtensions.CreateProblem(
                        status: StatusCodes.Status429TooManyRequests,
                        type: ProblemTypes.TooManyRequests,
                        title: "Too Many Requests",
                        detail: "Too many requests. Please try again later."),
                    cancellationToken);
            };

            options.AddPolicy<string>(AuthEndpoints.MagicLinkRateLimitPolicy, context =>
            {
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                int permitLimit = Math.Max(1, configuration.GetValue<int?>("RateLimit:MagicLink:PermitLimit") ?? 5);
                int windowMinutes = Math.Max(1, configuration.GetValue<int?>("RateLimit:MagicLink:WindowMinutes") ?? 10);
                string ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                string normalizedEmail = context.Items.TryGetValue(MagicLinkRequestEnricherMiddleware.EmailItemKey, out object? value)
                    && value is string email
                    && !string.IsNullOrWhiteSpace(email)
                        ? email
                        : "__missing-email";
                string key = $"{ipAddress}:{normalizedEmail}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(windowMinutes),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy<string>(AuthEndpoints.VerifyMagicLinkRateLimitPolicy, context =>
            {
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                int permitLimit = Math.Max(1, configuration.GetValue<int?>("RateLimit:VerifyMagicLink:PermitLimit") ?? 20);
                int windowSeconds = Math.Max(1, configuration.GetValue<int?>("RateLimit:VerifyMagicLink:WindowSeconds") ?? 60);
                string ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    ipAddress,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy<string>("team-invite-mutation", context =>
            {
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                int permitLimit = Math.Max(1, configuration.GetValue<int?>("RateLimit:TeamInvites:PermitLimit") ?? 5);
                int windowDays = Math.Max(1, configuration.GetValue<int?>("RateLimit:TeamInvites:WindowDays") ?? 7);
                string userId = context.User.FindFirstValue("sub") ?? "anonymous";
                string teamId = context.Request.RouteValues.TryGetValue("teamId", out object? value) && value is not null
                    ? value.ToString() ?? "missing-team"
                    : "missing-team";
                string key = $"{userId}:{teamId}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromDays(windowDays),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy<string>("team-api-key-mutation", context =>
            {
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                int permitLimit = Math.Max(1, configuration.GetValue<int?>("RateLimit:TeamApiKeys:PermitLimit") ?? 10);
                int windowMinutes = Math.Max(1, configuration.GetValue<int?>("RateLimit:TeamApiKeys:WindowMinutes") ?? 60);
                string userId = context.User.FindFirstValue("sub") ?? "anonymous";
                string teamId = context.Request.RouteValues.TryGetValue("teamId", out object? value) && value is not null
                    ? value.ToString() ?? "missing-team"
                    : "missing-team";
                string key = $"{userId}:{teamId}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(windowMinutes),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy<string>("mcp", context =>
            {
                // Rate limiting runs before authentication, so the API-key principal is not yet known;
                // partition by client IP.
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                int permitLimit = Math.Max(1, configuration.GetValue<int?>("RateLimit:Mcp:PermitLimit") ?? 120);
                int windowSeconds = Math.Max(1, configuration.GetValue<int?>("RateLimit:Mcp:WindowSeconds") ?? 60);
                string ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    ipAddress,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy<string>(AlphaWaitlistEndpoints.RateLimitPolicy, context =>
            {
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                int permitLimit = Math.Max(1, configuration.GetValue<int?>("RateLimit:AlphaWaitlist:PermitLimit") ?? 5);
                int windowMinutes = Math.Max(1, configuration.GetValue<int?>("RateLimit:AlphaWaitlist:WindowMinutes") ?? 10);
                string ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    ipAddress,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(windowMinutes),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }
}
