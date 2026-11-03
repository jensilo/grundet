using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class LabelIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string[] FreshLabels = ["fresh"];

    [Fact]
    public async Task ProjectLabels_AggregatesRequirementsAndWorkItems_OrderedByCountThenAlpha()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "R1", ["payments", "urgent"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "R2", ["payments"]);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "W1", ["payments", "backend"]);

        var labels = await GetProjectLabelsAsync(lead, project.Id);

        // payments: 3 entities; backend + urgent: 1 each, alpha order.
        Assert.Equal(["payments", "backend", "urgent"], labels.Select(label => label.Value).ToArray());
        Assert.Equal([3, 1, 1], labels.Select(label => label.Count).ToArray());
    }

    [Fact]
    public async Task ProjectLabels_CaseFoldsForGrouping_AndCountsEntityOnce()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // "Bug" appears on two entities (two casings); a single entity carrying both casings
        // must still count once for that entity.
        await TestApi.CreateRequirementAsync(lead, feature.Id, "R1", ["Bug", "bug"]);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "W1", ["BUG"]);

        var labels = await GetProjectLabelsAsync(lead, project.Id);

        var bug = Assert.Single(labels);
        Assert.Equal(2, bug.Count);
        Assert.Equal("bug", bug.Value.ToLowerInvariant());
    }

    [Fact]
    public async Task ProjectLabels_AreProjectIsolated()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var projectA = await TestApi.CreateProjectAsync(lead, team.Id);
        var projectB = await TestApi.CreateProjectAsync(lead, team.Id);
        var featureA = await TestApi.CreateFeatureAsync(lead, projectA.Id);

        await TestApi.CreateRequirementAsync(lead, featureA.Id, "R1", ["only-in-a"]);

        var labelsB = await GetProjectLabelsAsync(lead, projectB.Id);

        Assert.Empty(labelsB);
    }

    [Fact]
    public async Task ProjectLabels_ReflectOnlyCurrentVersion()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "W1", ["stale"]);

        // Replace the labels; the superseded version's label must not surface.
        var updateResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/work-items/{workItem.WorkItemId}",
            new { title = "W1", labels = FreshLabels, reason = "relabel" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var labels = await GetProjectLabelsAsync(lead, project.Id);

        Assert.Equal(["fresh"], labels.Select(label => label.Value).ToArray());
    }

    [Fact]
    public async Task ProjectLabels_CrossTenant_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        _ = await TestApi.CreateTeamAsync(outsider);

        var response = await outsider.Client.GetAsync($"/api/v1/projects/{ownerProject.Id}/labels");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<IReadOnlyList<LabelSuggestion>> GetProjectLabelsAsync(AuthSession user, Guid projectId)
    {
        var response = await user.Client.GetAsync($"/api/v1/projects/{projectId}/labels");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectLabelsResponse>(JsonOptions);
        Assert.NotNull(body);
        return body!.Labels;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record LabelSuggestion(string Value, int Count);

    private sealed record ProjectLabelsResponse(IReadOnlyList<LabelSuggestion> Labels);
}
