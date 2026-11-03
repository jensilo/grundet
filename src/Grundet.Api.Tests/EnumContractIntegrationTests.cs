using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class EnumContractIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task CreateRequirement_WithNumericEnumBodyValue_Returns400()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id);

        var response = await owner.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "Numeric enum contract regression",
                status = 0,
                priority = "Must",
                labels = Array.Empty<string>(),
                acceptanceCriteria = Array.Empty<string>(),
                reason = (string?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRequirement_WithNumericStringEnumBodyValue_Returns400()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id);

        var response = await owner.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "Numeric string enum contract regression",
                status = "0",
                priority = "Must",
                labels = Array.Empty<string>(),
                acceptanceCriteria = Array.Empty<string>(),
                reason = (string?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRequirements_WithNumericEnumQueryValue_Returns400()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id);

        var response = await TestApi.ListRequirementsRawAsync(owner, feature.Id, "?status=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListWorkItems_WithNumericEnumQueryValue_Returns400()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);

        var response = await owner.Client.GetAsync($"/api/v1/projects/{project.Id}/work-items?status=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Smart")]
    [InlineData("smart")]
    [InlineData("0")]
    public async Task ListProjectRequirements_WithInvalidSortQueryValue_Returns400(string sort)
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);

        var response = await owner.Client.GetAsync($"/api/v1/projects/{project.Id}/requirements?sort={sort}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Smart")]
    [InlineData("smart")]
    [InlineData("0")]
    public async Task ListFeatureRequirements_WithInvalidSortQueryValue_Returns400(string sort)
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id);

        var response = await owner.Client.GetAsync($"/api/v1/features/{feature.Id}/requirements?sort={sort}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Smart")]
    [InlineData("smart")]
    [InlineData("0")]
    public async Task ListWorkItems_WithInvalidSortQueryValue_Returns400(string sort)
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);

        var response = await owner.Client.GetAsync($"/api/v1/projects/{project.Id}/work-items?sort={sort}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchRequirements_WithNumericEnumQueryValue_Returns400()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);

        var response = await owner.Client.GetAsync($"/api/v1/projects/{project.Id}/requirements/search?status=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchWorkItems_WithNumericEnumQueryValue_Returns400()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);

        var response = await owner.Client.GetAsync($"/api/v1/projects/{project.Id}/work-items/search?type=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
