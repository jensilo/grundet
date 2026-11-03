using System.Text.Json;
using Grundet.Api.Common.Email;

namespace Grundet.Api.Middleware;

// Reads the email field from the magic-link request body before rate limiting runs,
// so the per-IP+email composite key can be used in the rate limit policy.
public sealed class MagicLinkRequestEnricherMiddleware(RequestDelegate next)
{
    internal const string EmailItemKey = "__MagicLinkRateLimitEmail";

    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? string.Empty;
        bool isMagicLinkRequest = HttpMethods.IsPost(context.Request.Method)
            && path.Equals("/api/v1/auth/magic-link", StringComparison.OrdinalIgnoreCase);

        if (!isMagicLinkRequest)
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();

        try
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            if (body.RootElement.ValueKind == JsonValueKind.Object
                && body.RootElement.TryGetProperty("email", out var emailElement)
                && emailElement.ValueKind == JsonValueKind.String)
            {
                if (EmailNormalizer.TryNormalize(emailElement.GetString(), out string email) && email.Length <= 254)
                {
                    context.Items[EmailItemKey] = email;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed payloads and let endpoint-level validation handle them.
        }
        finally
        {
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }

        await next(context);
    }
}

public static class MagicLinkRequestEnricherMiddlewareExtensions
{
    public static IApplicationBuilder UseMagicLinkRequestEnricher(this IApplicationBuilder app)
        => app.UseMiddleware<MagicLinkRequestEnricherMiddleware>();
}
