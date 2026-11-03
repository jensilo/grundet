using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class AlphaWaitlistIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task SubmitAlphaWaitlist_CreatesPendingEntryWithNormalizedEmailAndSendsConfirmationEmail()
    {
        fixture.EmailSink.Clear();
        var client = fixture.CreateClient();
        string email = $"  {fixture.CreateUniqueEmail().ToUpperInvariant()}  ";

        var response = await client.PostAsJsonAsync(
            "/api/v1/alpha-waitlist",
            new
            {
                accountType = "Personal",
                name = "Alex Morgan",
                email,
                reason = "Testing alpha access."
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var entry = await fixture.WithDbContextAsync(async db =>
            await db.AlphaWaitlistEntries.SingleAsync(item => item.EmailNormalized == normalizedEmail));
        Assert.Equal(email.Trim(), entry.EmailOriginal);
        Assert.Equal(normalizedEmail, entry.EmailNormalized);
        Assert.Equal(AlphaWaitlistStatus.Pending, entry.Status);

        var messages = fixture.EmailSink.GetMessages(normalizedEmail);
        var message = Assert.Single(messages);
        Assert.Equal("We received your Grundet alpha request", message.Subject);
        Assert.Contains("We received your Grundet alpha request.", message.HtmlBody);
    }

    [Fact]
    public async Task SubmitAlphaWaitlist_NormalizedPendingDuplicateUpdatesProfileWithoutStatusLeakAndDoesNotResendEmail()
    {
        fixture.EmailSink.Clear();
        var client = fixture.CreateClient();
        string email = fixture.CreateUniqueEmail();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/alpha-waitlist",
            new
            {
                accountType = "Personal",
                name = "First name",
                email
            });
        var secondResponse = await client.PostAsJsonAsync(
            "/api/v1/alpha-waitlist",
            new
            {
                accountType = "Personal",
                name = "Second name",
                email = email.ToUpperInvariant()
            });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var entry = await fixture.WithDbContextAsync(async db =>
            await db.AlphaWaitlistEntries.SingleAsync(item => item.EmailNormalized == email));
        Assert.Equal("Second name", entry.Name);
        Assert.Equal(AlphaWaitlistStatus.Pending, entry.Status);

        var messages = fixture.EmailSink.GetMessages(email);
        Assert.Single(messages);
    }

    [Fact]
    public async Task SubmitAlphaWaitlist_ApprovedDuplicateReturnsGenericSuccessWithoutChangingStatusOrSendingEmail()
    {
        fixture.EmailSink.Clear();
        var client = fixture.CreateClient();
        string email = fixture.CreateUniqueEmail();
        await fixture.WithDbContextAsync(async db =>
        {
            db.AlphaWaitlistEntries.Add(new AlphaWaitlistEntry
            {
                Id = Guid.NewGuid(),
                EmailOriginal = email,
                EmailNormalized = email,
                Name = "Approved user",
                AccountType = AlphaWaitlistAccountType.Personal,
                Status = AlphaWaitlistStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                ApprovedBy = "test"
            });
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync(
            "/api/v1/alpha-waitlist",
            new
            {
                accountType = "Personal",
                name = "Changed name",
                email = email.ToUpperInvariant()
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entry = await fixture.WithDbContextAsync(async db =>
            await db.AlphaWaitlistEntries.SingleAsync(item => item.EmailNormalized == email));
        Assert.Equal("Approved user", entry.Name);
        Assert.Equal(AlphaWaitlistStatus.Approved, entry.Status);
        Assert.Empty(fixture.EmailSink.GetMessages(email));
    }

    [Fact]
    public async Task SubmitAlphaWaitlist_BusinessRequiresBusinessFields()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/alpha-waitlist",
            new
            {
                accountType = "Business",
                name = "Alex Morgan",
                email = fixture.CreateUniqueEmail()
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    public async Task SubmitAlphaWaitlist_NumericAccountTypeReturnsBadRequest(string accountType)
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/alpha-waitlist",
            new
            {
                accountType,
                name = "Alex Morgan",
                email = fixture.CreateUniqueEmail()
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAlphaWaitlist_NumericOptionalEnumValueReturnsBadRequest()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/alpha-waitlist",
            new
            {
                accountType = "Personal",
                name = "Alex Morgan",
                email = fixture.CreateUniqueEmail(),
                lineOfWork = "0"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
