using Grundet.Api.Common.Auth;
using Grundet.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Middleware;

public sealed class RlsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext, RlsContext rlsContext)
    {
        if (context.Request.Path.StartsWithSegments("/openapi")
            || context.Request.Path.StartsWithSegments("/scalar")
            || context.Request.Path.StartsWithSegments("/health"))
        {
            // /health is an unauthenticated readiness probe (Docker HEALTHCHECK / Coolify). It has no
            // user context to apply, and opening a DB connection + RLS context here would duplicate
            // (and complicate the failure mode of) the explicit CanConnectAsync check the endpoint
            // itself performs. Keep it outside the RLS/user-context pipeline entirely.
            await next(context);
            return;
        }

        rlsContext.UserId = context.User.Identity?.IsAuthenticated == true
            ? context.User.GetUserId().ToString()
            : string.Empty;

        await dbContext.Database.OpenConnectionAsync(context.RequestAborted);
        try
        {
            await rlsContext.ApplyAsync(dbContext.Database, context.RequestAborted);
            await next(context);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}

public static class RlsMiddlewareExtensions
{
    public static IApplicationBuilder UseRlsContext(this IApplicationBuilder app)
        => app.UseMiddleware<RlsMiddleware>();
}
