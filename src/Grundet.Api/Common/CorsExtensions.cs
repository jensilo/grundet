namespace Grundet.Api.Common;

public static class CorsExtensions
{
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        string[] allowedOrigins = GetAllowedOrigins(configuration);

        if (allowedOrigins.Length > 0)
        {
            services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins(allowedOrigins)
                          .AllowCredentials()
                          .AllowAnyHeader()
                          .AllowAnyMethod()));
        }

        return services;
    }

    public static WebApplication UseApiCors(this WebApplication app)
    {
        if (GetAllowedOrigins(app.Configuration).Length > 0)
        {
            app.UseCors();
        }

        return app;
    }

    private static string[] GetAllowedOrigins(IConfiguration configuration)
    {
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            return allowedOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)).ToArray();
        }

        string? allowedOrigin = configuration.GetValue<string>("Cors:AllowedOrigin");
        return string.IsNullOrWhiteSpace(allowedOrigin) ? [] : [allowedOrigin];
    }
}
