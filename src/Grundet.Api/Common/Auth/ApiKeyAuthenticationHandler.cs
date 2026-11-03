using System.Security.Claims;
using System.Text.Encodings.Web;
using Grundet.Api.Common.Errors;
using Grundet.Api.Features.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Grundet.Api.Common.Auth;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
}

public static class ApiKeyClaimTypes
{
    /// <summary>
    /// The Team the key is scoped to. This is an <em>additional</em> constraint enforced by the
    /// team-scope guard — never a replacement for <see cref="IResourceAuthorizationService"/>.
    /// </summary>
    public const string TeamId = "team_id";

    /// <summary>The TeamApiKey row id. Its presence marks a principal as API-key authenticated.</summary>
    public const string ApiKeyId = "api_key_id";
}

/// <summary>
/// Authenticates <c>Authorization: Bearer grundet_…</c> requests as the key's issuing user.
/// Registered only for the MCP endpoint in alpha (see ADR-008). Every failure mode returns one
/// generic 401 via <see cref="HandleChallengeAsync"/> — no signal that distinguishes a revoked or
/// expired key from a non-existent one.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authorization = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            // Not an API-key credential — let the challenge produce a 401 without asserting failure.
            return AuthenticateResult.NoResult();
        }

        string rawKey = authorization[BearerPrefix.Length..].Trim();
        if (!IsWellFormed(rawKey))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        string keyHash = TeamApiKeyService.ComputeKeyHash(rawKey);
        var store = Context.RequestServices.GetRequiredService<ITeamApiKeyStore>();
        var now = DateTime.UtcNow;

        var apiKey = await store.FindActiveByHashAsync(keyHash, now, Context.RequestAborted);
        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        await store.TouchLastUsedAsync(keyHash, now, Context.RequestAborted);

        var claims = new[]
        {
            // "sub" mirrors the cookie principal so RlsContext + IResourceAuthorizationService work unchanged.
            new Claim("sub", apiKey.CreatedByUserId.ToString()),
            new Claim(ApiKeyClaimTypes.TeamId, apiKey.TeamId.ToString()),
            new Claim(ApiKeyClaimTypes.ApiKeyId, apiKey.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(
            ProblemDetailsExtensions.CreateProblem(
                status: StatusCodes.Status401Unauthorized,
                type: ProblemTypes.Unauthorized,
                title: "Unauthorized",
                detail: "Invalid or missing API key."),
            Context.RequestAborted);
    }

    private static bool IsWellFormed(string rawKey)
    {
        if (!rawKey.StartsWith(TeamApiKeyService.KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string randomPart = rawKey[TeamApiKeyService.KeyPrefix.Length..];
        if (randomPart.Length is < 16 or > 128)
        {
            return false;
        }

        foreach (char character in randomPart)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}

public static class ApiKeyAuthenticationExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services)
    {
        // Adds the ApiKey scheme without changing the default scheme (cookie). Applied to the MCP
        // endpoint only in Phase 2; registering here keeps the credential and its handler together.
        services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme, configureOptions: null);
        return services;
    }
}
