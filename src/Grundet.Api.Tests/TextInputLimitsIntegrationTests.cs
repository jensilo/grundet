using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Common.Validation;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class TextInputLimitsIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task CreateRequirement_TextAtLimit_Succeeds_OverLimit_Rejected()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var atLimit = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new { text = new string('a', TextInput.RequirementTextMaxLength) });
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);

        var overLimit = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new { text = new string('a', TextInput.RequirementTextMaxLength + 1) });
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
    }

    [Fact]
    public async Task CreateRequirement_RationaleOverLimit_Rejected()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall validate inputs.",
                rationale = new string('a', TextInput.DescriptionMaxLength + 1)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRequirement_AcceptanceCriterionOverLimit_Rejected()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall validate inputs.",
                acceptanceCriteria = new[] { new string('a', TextInput.AcceptanceCriterionMaxLength + 1) }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
