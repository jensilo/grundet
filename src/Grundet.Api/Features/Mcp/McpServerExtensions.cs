using Grundet.Api.Common.Auth;
using Grundet.Api.Middleware;

namespace Grundet.Api.Features.Mcp;

public static class McpServerExtensions
{
    /// <summary>Authorization policy for the MCP endpoint: an authenticated principal via the ApiKey scheme only.</summary>
    public const string AuthorizationPolicy = "Mcp";

    public const string RateLimitPolicy = "mcp";

    public static IServiceCollection AddGrundetMcpServer(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<McpTeamScope>();

        // Stateless: each request carries its own ApiKey auth + RLS context; no server→client messages
        // are needed for read tools, and statelessness is what keeps consecutive requests isolated.
        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<IntentTools>()
            .WithTools<ProjectTools>();

        return services;
    }

    public static IEndpointConventionBuilder MapGrundetMcp(this IEndpointRouteBuilder endpoints)
        => endpoints.MapMcp("/mcp")
            .RequireAuthorization(AuthorizationPolicy)
            .RequireRateLimiting(RateLimitPolicy)
            .SkipOnboardingGuard();
}
