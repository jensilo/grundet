using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class UserOnboardingIntegrationTests(ApiIntegrationFixture fixture)
{
    private const string CompleteOnboardingPath = "/api/v1/users/me/onboarding/complete";

    [Fact]
    public async Task GetMe_ReturnsNullOnboardingCompletedAtForNewUser()
    {
        var user = await TestApi.SignInAsync(fixture);

        var response = await user.Client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(me);
        Assert.Null(me.OnboardingCompletedAt);
    }

    [Fact]
    public async Task CompleteOnboarding_SetsTimestampAndPreservesItOnRepeatCalls()
    {
        var user = await TestApi.SignInAsync(fixture);

        var firstResponse = await user.Client.PostAsync(CompleteOnboardingPath, content: null);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstMe = await firstResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(firstMe);
        Assert.NotNull(firstMe.OnboardingCompletedAt);

        var secondResponse = await user.Client.PostAsync(CompleteOnboardingPath, content: null);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondMe = await secondResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(secondMe);
        Assert.Equal(firstMe.OnboardingCompletedAt, secondMe.OnboardingCompletedAt);

        var meResponse = await user.Client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(me);
        Assert.Equal(firstMe.OnboardingCompletedAt, me.OnboardingCompletedAt);
    }

    [Fact]
    public async Task CompleteOnboarding_IsCallableBeforeDisplayNameIsSet()
    {
        // Sign in via magic link without setting a display name — the onboarding guard
        // must not block the completion endpoint (.SkipOnboardingGuard()).
        string email = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false);

        var magicLinkResponse = await client.PostAsJsonAsync("/api/v1/auth/magic-link", new { email });
        Assert.Equal(HttpStatusCode.Accepted, magicLinkResponse.StatusCode);
        string token = fixture.EmailSink.GetLatestMagicLinkToken(email);
        var verifyResponse = await client.GetAsync($"/api/v1/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);

        var completeResponse = await client.PostAsync(CompleteOnboardingPath, content: null);

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var me = await completeResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(me);
        Assert.Null(me.DisplayName);
        Assert.NotNull(me.OnboardingCompletedAt);
    }

    [Fact]
    public async Task CompleteOnboarding_RequiresAuthentication()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsync(CompleteOnboardingPath, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_IsSelfScopedAndIgnoresAnyRequestBody()
    {
        var caller = await TestApi.SignInAsync(fixture);
        var otherUser = await TestApi.SignInAsync(fixture);

        // The endpoint accepts no body; a smuggled user id must not complete anyone else.
        var response = await caller.Client.PostAsJsonAsync(
            CompleteOnboardingPath,
            new { userId = otherUser.UserId, id = otherUser.UserId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var callerMe = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(callerMe);
        Assert.Equal(caller.UserId, callerMe.Id);
        Assert.NotNull(callerMe.OnboardingCompletedAt);

        var otherMeResponse = await otherUser.Client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, otherMeResponse.StatusCode);
        var otherMe = await otherMeResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(otherMe);
        Assert.Null(otherMe.OnboardingCompletedAt);
    }
}
