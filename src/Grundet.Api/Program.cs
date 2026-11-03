using System.Text.Json.Serialization;
using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Email;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Images;
using Grundet.Api.Common.OpenApi;
using Grundet.Api.Data;
using Grundet.Api.Features.AlphaWaitlist;
using Grundet.Api.Features.ApiKeys;
using Grundet.Api.Features.Attachments;
using Grundet.Api.Features.Auth;
using Grundet.Api.Features.Billing;
using Grundet.Api.Features.Changelog;
using Grundet.Api.Features.DomainFeatures;
using Grundet.Api.Features.Drafts;
using Grundet.Api.Features.Feedback;
using Grundet.Api.Features.Health;
using Grundet.Api.Features.Jira;
using Grundet.Api.Features.Labels;
using Grundet.Api.Features.Mcp;
using Grundet.Api.Features.Permissions;
using Grundet.Api.Features.Projects;
using Grundet.Api.Features.Requirements;
using Grundet.Api.Features.Search;
using Grundet.Api.Features.Teams;
using Grundet.Api.Features.Users;
using Grundet.Api.Features.WorkItems;
using Grundet.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // Optional per-developer local override — gitignored, lowest precedence.
    // Env vars take highest precedence (e.g. Auth__CookieDomain=.grundet.test for subdomain dev).
    builder.Configuration
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

builder.Services.AddOpenApi(options =>
    options.AddOperationTransformer<BinaryResponseOperationTransformer>());
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddConfiguredDataProtection(builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<HttpProblemExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.Configure<BillingUsageRecalculationOptions>(
    builder.Configuration.GetSection("BillingUsageRecalculation"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(buildDbConnectionString(builder.Configuration)));

builder.Services.AddCookieAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddApiKeyAuthentication();
builder.Services.AddAuthorization(options =>
    options.AddPolicy(McpServerExtensions.AuthorizationPolicy, policy => policy
        .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()));
builder.Services.AddApiRateLimiting();
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddApiForwardedHeaders(builder.Configuration);

builder.Services.AddScoped<RlsContext>();
builder.Services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddScoped<IAuthStore, AuthStore>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IAlphaWaitlistStore, AlphaWaitlistStore>();
builder.Services.AddScoped<AlphaWaitlistService>();
builder.Services.AddHostedService<MagicLinkTokenCleanupService>();

builder.Services.AddScoped<ITeamStore, TeamStore>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<ITeamApiKeyStore, TeamApiKeyStore>();
builder.Services.AddScoped<TeamApiKeyService>();
builder.Services.AddScoped<ITeamQuotaStore, TeamQuotaStore>();
builder.Services.AddScoped<ITeamQuotaService, TeamQuotaService>();
builder.Services.AddScoped<ITeamUsageRecalculationService, TeamUsageRecalculationService>();
builder.Services.AddHostedService<TeamUsageRecalculationHostedService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddScoped<IProjectStore, ProjectStore>();
builder.Services.AddScoped<ProjectService>();

builder.Services.AddScoped<IFeatureStore, FeatureStore>();
builder.Services.AddScoped<FeatureService>();

builder.Services.AddScoped<IRequirementStore, RequirementStore>();
builder.Services.AddScoped<RequirementService>();
builder.Services.Configure<AttachmentOptions>(builder.Configuration.GetSection("Attachments"));
builder.Services.AddScoped<IAttachmentStore, AttachmentStore>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<IDraftStore, DraftStore>();
builder.Services.AddScoped<DraftService>();
builder.Services.AddHostedService<DraftCleanupService>();

builder.Services.AddScoped<IWorkItemStore, WorkItemStore>();
builder.Services.AddScoped<IWorkItemLinkStore, WorkItemLinkStore>();
builder.Services.AddScoped<WorkItemService>();

builder.Services.AddScoped<ILabelStore, LabelStore>();
builder.Services.AddScoped<LabelService>();
builder.Services.AddScoped<IChangelogStore, ChangelogStore>();
builder.Services.AddScoped<ChangelogService>();

builder.Services.AddScoped<ISearchStore, SearchStore>();
builder.Services.AddScoped<SearchService>();

builder.Services.Configure<FeedbackOptions>(builder.Configuration.GetSection("Feedback"));
builder.Services.AddScoped<IImageAttachmentStorage, SkiaImageAttachmentStorage>();
builder.Services.AddScoped<IFeedbackStore, FeedbackStore>();
builder.Services.AddScoped<FeedbackService>();

builder.Services.AddScoped<IJiraStore, JiraStore>();
builder.Services.AddHttpClient<JiraService>();

builder.Services.AddGrundetMcpServer();

var app = builder.Build();

// Must run before anything that reads HttpContext.Connection.RemoteIpAddress (rate limiting below,
// most notably) so the real client IP — not the reverse proxy's — is used for partitioning.
app.UseApiForwardedHeaders();
app.UseExceptionHandler();
app.UseApiCors();
app.UseMagicLinkRequestEnricher();
app.UseRateLimiter();
app.UseAuthentication();
// Promote a valid ApiKey principal onto /mcp requests before RLS context is applied, so the tool's
// DB work runs as the key's issuing user. Cookie auth is unaffected.
app.UseMcpApiKeyAuthentication();
app.UseRlsContext();
app.UseOnboardingGuard();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
HealthEndpoints.Map(app);

var apiV1 = app.MapGroup("/api/v1");
AuthEndpoints.Map(apiV1);
AlphaWaitlistEndpoints.Map(apiV1);
UsersEndpoints.Map(apiV1);
TeamEndpoints.Map(apiV1);
TeamApiKeyEndpoints.Map(apiV1);
ProjectEndpoints.Map(apiV1);
FeatureEndpoints.Map(apiV1);
RequirementEndpoints.Map(apiV1);
AttachmentEndpoints.Map(apiV1);
WorkItemEndpoints.Map(apiV1);
DraftEndpoints.Map(apiV1);
LabelEndpoints.Map(apiV1);
ChangelogEndpoints.Map(apiV1);
SearchEndpoints.Map(apiV1);
FeedbackEndpoints.Map(apiV1);
JiraEndpoints.Map(apiV1);

app.MapGrundetMcp();

app.Run();

static string buildDbConnectionString(IConfiguration configuration)
{
    var csBuilder = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Default"));

    int? configuredMaxPoolSize = configuration.GetValue<int?>("Npgsql:MaxPoolSize");
    if (configuredMaxPoolSize is int explicitMaxPoolSize)
    {
        csBuilder.MaxPoolSize = explicitMaxPoolSize;
    }
    else if (csBuilder.MaxPoolSize == 100)
    {
        // csBuilder.MaxPoolSize == 100 means the connection string didn't set one — that's Npgsql's
        // own built-in default. Postgres' own max_connections defaults to 100 and must be shared
        // across every API instance + the migrator + admin roles (see
        // docs/infra/deployment-and-scaling-considerations.md, "PgBouncer Mode Must Match RLS
        // Strategy"). Leaving Npgsql's 100-per-instance default unconstrained means 2-3 API
        // instances alone could exhaust the whole DB connection budget. 20 is a conservative
        // per-instance cap for the current 2-3 instance deployment shape; override via
        // Npgsql:MaxPoolSize if that shape changes (e.g. PgBouncer fronting the DB).
        csBuilder.MaxPoolSize = 20;
    }

    return csBuilder.ConnectionString;
}

public partial class Program;
