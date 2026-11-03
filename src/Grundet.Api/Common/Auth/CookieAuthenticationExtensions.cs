using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Grundet.Api.Common.Auth;

public static class CookieAuthenticationExtensions
{
    public static IServiceCollection AddCookieAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        int cookieExpireDays = configuration.GetValue<int?>("Auth:CookieExpireDays") ?? 30;
        int absoluteSessionDays = Math.Max(1, configuration.GetValue<int?>("Auth:AbsoluteSessionDays") ?? 90);
        string? cookieDomain = configuration.GetValue<string>("Auth:CookieDomain");

        // Intentionally omit the __Host- prefix when CookieDomain is configured because cross-subdomain sharing requires Domain.
        string cookieName = string.IsNullOrWhiteSpace(cookieDomain)
            ? "__Host-grundet.session"
            : "grundet.session";

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = cookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                // Lax (not Strict) is required for magic-link and OAuth flows: the browser navigates
                // from an external origin (email client) to the app, which SameSite=Strict treats as
                // cross-site and drops the cookie. Lax sends the cookie on top-level GET navigations
                // from any origin while still blocking cross-site POST/PUT/DELETE (CSRF protection).
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(cookieExpireDays);
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnValidatePrincipal = async context =>
                    {
                        var issuedUtc = context.Properties.IssuedUtc;
                        bool isWithinAbsoluteLifetime = issuedUtc.HasValue
                            && DateTimeOffset.UtcNow - issuedUtc.Value <= TimeSpan.FromDays(absoluteSessionDays);

                        if (isWithinAbsoluteLifetime)
                        {
                            return;
                        }

                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };

                if (!string.IsNullOrWhiteSpace(cookieDomain))
                {
                    options.Cookie.Domain = cookieDomain;
                }
            });

        return services;
    }
}
