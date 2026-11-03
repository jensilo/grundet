using Grundet.Api.Common.Auth;
using Microsoft.AspNetCore.Authentication;

namespace Grundet.Api.Features.Mcp;

/// <summary>
/// Authenticates the ApiKey scheme for <c>/mcp</c> requests <em>before</em> <see cref="Middleware.RlsMiddleware"/>
/// runs, so <c>app.current_user_id</c> is set to the key's issuing user for the tool's DB work. Endpoint
/// authorization (<see cref="McpServerExtensions.AuthorizationPolicy"/>) still produces the generic 401 on failure;
/// this middleware only promotes a successful API-key principal onto the request ahead of the RLS context.
/// </summary>
public sealed class McpApiKeyAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/mcp"))
        {
            var result = await context.AuthenticateAsync(ApiKeyAuthenticationDefaults.AuthenticationScheme);
            if (result.Succeeded && result.Principal is not null)
            {
                context.User = result.Principal;
            }
        }

        await next(context);
    }
}

public static class McpApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseMcpApiKeyAuthentication(this IApplicationBuilder app)
        => app.UseMiddleware<McpApiKeyAuthenticationMiddleware>();
}
