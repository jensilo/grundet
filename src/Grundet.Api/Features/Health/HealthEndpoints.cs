using Grundet.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.Health;

public static class HealthEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapGet("/health", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
        {
            // Lightweight readiness probe for Docker HEALTHCHECK / Coolify rolling restarts: ping the
            // DB directly rather than just returning 200 unconditionally. Trade-off: a transient DB
            // blip (e.g. a brief network hiccup) marks the container unhealthy even though the API
            // process itself is fine, which can cause Coolify to cycle it. Accepted for now — a
            // container that can't reach the DB isn't meaningfully "ready" either.
            bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? Results.Ok(new HealthResponse("ok"))
                : Results.Problem(detail: "Database unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        })
            .WithName("GetHealth")
            .WithSummary("Health check")
            .Produces<HealthResponse>()
            .Produces(StatusCodes.Status503ServiceUnavailable);
}

public record HealthResponse(string Status);
