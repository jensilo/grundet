using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Grundet.Api.Common.Email;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.AlphaWaitlist;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class MagicLinkIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task MagicLinkToken_IsSingleUse()
    {
        fixture.EmailSink.Clear();
        string email = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false);

        string token = await IssueMagicLinkAndGetTokenAsync(client, email);

        var firstVerify = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, firstVerify.StatusCode);

        // Second use: token is already redeemed but the client is now authenticated,
        // so the endpoint redirects to the dashboard instead of showing an error.
        var secondVerify = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, secondVerify.StatusCode);
        Assert.StartsWith("https://app.localhost/dashboard", secondVerify.Headers.Location?.ToString());
    }

    [Fact]
    public async Task MagicLink_VerifyUrl_UsesPublicApiBaseUrl()
    {
        fixture.EmailSink.Clear();
        string email = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false);

        await IssueMagicLinkAndGetTokenAsync(client, email);

        string verifyUrl = fixture.EmailSink.GetLatestMagicLinkUrl(email);
        Assert.StartsWith("https://api.localhost/api/v1/auth/verify?token=", verifyUrl);
    }

    [Fact]
    public async Task MagicLink_PostVerify_RedirectsToPublicAppBaseUrl()
    {
        fixture.EmailSink.Clear();
        string email = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false);

        string token = await IssueMagicLinkAndGetTokenAsync(client, email);

        var verifyResponse = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);

        string? location = verifyResponse.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("https://app.localhost", location);
    }

    [Fact]
    public async Task AlphaAccess_ApprovedWaitlistEmail_CanCreateAccount()
    {
        fixture.EmailSink.Clear();
        string email = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false, alphaAccessEnabled: true);
        await AddWaitlistEntryAsync(email, AlphaWaitlistStatus.Approved);

        string token = await IssueMagicLinkAndGetTokenAsync(client, email);
        var verifyResponse = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        Assert.StartsWith("https://app.localhost/onboarding", verifyResponse.Headers.Location?.ToString());
        Assert.True(await UserExistsAsync(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(AlphaWaitlistStatus.Pending)]
    [InlineData(AlphaWaitlistStatus.Declined)]
    public async Task AlphaAccess_UnapprovedNewUser_CannotCreateAccount(AlphaWaitlistStatus? waitlistStatus)
    {
        fixture.EmailSink.Clear();
        string email = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false, alphaAccessEnabled: true);

        if (waitlistStatus is not null)
        {
            await AddWaitlistEntryAsync(email, waitlistStatus.Value);
        }

        string token = await IssueMagicLinkAndGetTokenAsync(client, email);
        var verifyResponse = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        Assert.StartsWith(
            "https://app.localhost/auth/error?reason=private_alpha_access_required",
            verifyResponse.Headers.Location?.ToString());
        Assert.False(await UserExistsAsync(email));
    }

    [Fact]
    public async Task AlphaAccess_ExistingUser_CanStillSignInWithoutWaitlistApproval()
    {
        fixture.EmailSink.Clear();
        string email = fixture.CreateUniqueEmail();
        await fixture.WithDbContextAsync(async db =>
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = "Existing User",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient(allowAutoRedirect: false, alphaAccessEnabled: true);

        string token = await IssueMagicLinkAndGetTokenAsync(client, email);
        var verifyResponse = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        Assert.StartsWith("https://app.localhost/", verifyResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ExpiredMagicLinkToken_RedirectsToAuthError()
    {
        fixture.EmailSink.Clear();
        string email = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false);

        string token = await IssueMagicLinkAndGetTokenAsync(client, email);
        string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        await fixture.WithDbContextAsync(async db =>
        {
            var row = await db.MagicLinkTokens
                .SingleAsync(item => item.TokenHash == tokenHash);
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var verifyResponse = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        Assert.StartsWith(
            "https://app.localhost/auth/error?reason=expired_link",
            verifyResponse.Headers.Location?.ToString());
    }

    private async Task<string> IssueMagicLinkAndGetTokenAsync(HttpClient client, string email)
    {
        var issueResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/magic-link",
            new { email });
        Assert.Equal(HttpStatusCode.Accepted, issueResponse.StatusCode);

        return fixture.EmailSink.GetLatestMagicLinkToken(email);
    }

    private Task<bool> UserExistsAsync(string email)
        => fixture.WithDbContextAsync(async db =>
            await db.Users.AnyAsync(user => user.Email == EmailNormalizer.Normalize(email)));

    private async Task AddWaitlistEntryAsync(string email, AlphaWaitlistStatus status)
        => await fixture.WithDbContextAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.AlphaWaitlistEntries.Add(new AlphaWaitlistEntry
            {
                Id = Guid.NewGuid(),
                EmailOriginal = email,
                EmailNormalized = EmailNormalizer.Normalize(email),
                Name = "Alpha Tester",
                AccountType = AlphaWaitlistAccountType.Personal,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now,
                ApprovedAt = status == AlphaWaitlistStatus.Approved ? now : null,
                ApprovedBy = status == AlphaWaitlistStatus.Approved ? "test" : null,
                DeclinedAt = status == AlphaWaitlistStatus.Declined ? now : null,
                DeclinedBy = status == AlphaWaitlistStatus.Declined ? "test" : null
            });
            await db.SaveChangesAsync();
        });
}
