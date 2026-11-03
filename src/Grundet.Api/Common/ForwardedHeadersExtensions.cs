using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;

namespace Grundet.Api.Common;

/// <summary>
/// Configures ASP.NET Core's forwarded-headers middleware so rate limiting (and anything else that
/// reads <c>HttpContext.Connection.RemoteIpAddress</c>) sees the real client IP instead of the
/// reverse proxy's IP. See docs/infra/deployment-and-scaling-considerations.md,
/// "Forwarded Headers & Rate Limiting".
/// </summary>
public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddApiForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            (string[] knownProxies, string[] knownNetworks) = GetTrustedProxyConfig(configuration);

            if (knownProxies.Length == 0 && knownNetworks.Length == 0)
            {
                // No operator-supplied trust config: keep ASP.NET Core's safe defaults (loopback-only
                // KnownProxies/KnownNetworks). Do NOT blindly trust all proxies here — an unvalidated
                // X-Forwarded-For header from an untrusted hop is a client-IP spoofing surface.
                return;
            }

            // ForwardedHeadersOptions is pre-populated with loopback defaults (KnownProxies:
            // 127.0.0.1/::1, KnownNetworks: loopback range). Clear them so only the operator-configured
            // Traefik hop is trusted once explicit config is supplied.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (string proxy in knownProxies)
            {
                if (IPAddress.TryParse(proxy, out IPAddress? address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (string network in knownNetworks)
            {
                if (System.Net.IPNetwork.TryParse(network, out System.Net.IPNetwork parsed))
                {
                    options.KnownIPNetworks.Add(parsed);
                }
            }
        });

        return services;
    }

    public static WebApplication UseApiForwardedHeaders(this WebApplication app)
    {
        app.UseForwardedHeaders();

        (string[] knownProxies, string[] knownNetworks) = GetTrustedProxyConfig(app.Configuration);
        bool isProxiedEnvironment = app.Environment.IsProduction()
            || app.Configuration.GetValue<bool?>("ForwardedHeaders:Proxied") == true;

        if (isProxiedEnvironment && knownProxies.Length == 0 && knownNetworks.Length == 0)
        {
            ForwardedHeadersLog.NotConfigured(app.Logger);
        }

        return app;
    }

    private static (string[] KnownProxies, string[] KnownNetworks) GetTrustedProxyConfig(IConfiguration configuration)
    {
        string[] knownProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        string[] knownNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
        return (knownProxies, knownNetworks);
    }
}

internal static partial class ForwardedHeadersLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message =
        "ForwardedHeaders:KnownProxies / ForwardedHeaders:KnownNetworks are not configured while running in a " +
        "proxied environment. Every request's RemoteIpAddress will be the reverse proxy's own IP, so rate-limit " +
        "policies (which partition on RemoteIpAddress) will collapse ALL clients behind the proxy into a single " +
        "shared bucket. This must be fixed and confirmed against the real network topology at first live deploy " +
        "— see docs/infra/deployment-and-scaling-considerations.md, 'Forwarded Headers & Rate Limiting'.")]
    public static partial void NotConfigured(ILogger logger);
}
