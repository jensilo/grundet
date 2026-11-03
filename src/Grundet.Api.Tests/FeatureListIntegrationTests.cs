using System.Net;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class FeatureListIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task ListFeatures_WithQuery_FiltersByNameSubstringCaseInsensitive()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateFeatureAsync(lead, project.Id, "Authentication");
        await TestApi.CreateFeatureAsync(lead, project.Id, "Billing");
        await TestApi.CreateFeatureAsync(lead, project.Id, "Payment authorization");

        var result = await TestApi.ListFeaturesAsync(lead, project.Id, q: "auth");

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, feature => Assert.Contains("auth", feature.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListFeatures_WithQueryMatchingNothing_ReturnsEmptyWithZeroTotal()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateFeatureAsync(lead, project.Id, "Authentication");
        await TestApi.CreateFeatureAsync(lead, project.Id, "Billing");

        var result = await TestApi.ListFeaturesAsync(lead, project.Id, q: "nonexistent-feature");

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListFeatures_WithoutQuery_ReturnsAllFeatures()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateFeatureAsync(lead, project.Id, "Alpha");
        await TestApi.CreateFeatureAsync(lead, project.Id, "Beta");
        await TestApi.CreateFeatureAsync(lead, project.Id, "Gamma");

        var result = await TestApi.ListFeaturesAsync(lead, project.Id);

        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task ListFeatures_QueryWithLikeWildcards_TreatsThemLiterally()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateFeatureAsync(lead, project.Id, "100% coverage");
        await TestApi.CreateFeatureAsync(lead, project.Id, "Billing");

        // A literal '%' must not behave as a wildcard — it should only match the feature whose name
        // actually contains '%'.
        var result = await TestApi.ListFeaturesAsync(lead, project.Id, q: "100%");

        Assert.Equal(1, result.Total);
        Assert.Equal("100% coverage", Assert.Single(result.Items).Name);
    }

    [Fact]
    public async Task GetFeature_ExistingFeature_ReturnsIt()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id, "Authentication");

        var result = await TestApi.GetFeatureAsync(lead, project.Id, feature.Id);

        Assert.Equal(feature.Id, result.Id);
        Assert.Equal("Authentication", result.Name);
    }

    [Fact]
    public async Task GetFeature_UnknownFeature_Returns404()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var response = await TestApi.GetFeatureRawAsync(lead, project.Id, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetFeature_FeatureInAnotherTenant_IsNotAccessible()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id, "Authentication");

        var outsider = await TestApi.SignInAsync(fixture);

        var response = await TestApi.GetFeatureRawAsync(outsider, project.Id, feature.Id);

        // RequireProjectAccessAsync 404s for non-members to avoid leaking project existence.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
