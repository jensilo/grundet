using Grundet.Api.Common.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grundet.Api.Tests.Infrastructure;

public sealed class GrundetApiFactory(
    string connectionString,
    IEmailService emailService,
    string feedbackAttachmentsRoot,
    string requirementAttachmentsRoot) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Feedback:AttachmentsRoot"] = feedbackAttachmentsRoot,
                ["Attachments:AttachmentsRoot"] = requirementAttachmentsRoot,
                ["BillingUsageRecalculation:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton(emailService);
        });
    }
}
