using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class RequirementLinkedWorkIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task GetRequirementWorkItems_ReturnsLinkedWorkWithRefsAndNotStaleByDefault()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent with linked work");
        var workItemA = await TestApi.CreateWorkItemAsync(lead, project.Id, "First work item");
        var workItemB = await TestApi.CreateWorkItemAsync(lead, project.Id, "Second work item");

        await TestApi.LinkRequirementAsync(lead, workItemA.WorkItemId, requirement.Requirement.RequirementId);
        await TestApi.LinkRequirementAsync(lead, workItemB.WorkItemId, requirement.Requirement.RequirementId);

        var linkedWork = await TestApi.GetRequirementWorkItemsAsync(lead, requirement.Requirement.RequirementId);

        Assert.Equal(2, linkedWork.Count);
        Assert.Contains(linkedWork, work => work.WorkItemId == workItemA.WorkItemId);
        Assert.Contains(linkedWork, work => work.WorkItemId == workItemB.WorkItemId);
        Assert.All(linkedWork, work => Assert.StartsWith("WO-", work.Ref));
    }

    [Fact]
    public async Task GetRequirementWorkItems_StillReturnsLinkAfterIntentUpdate()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent v1");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work that follows the latest intent version");

        await TestApi.LinkRequirementAsync(lead, workItem.WorkItemId, requirement.Requirement.RequirementId);
        // Updating the intent does not touch its links — linked work keeps showing up and always
        // reflects the latest intent version (there is no per-link snapshot to drift).
        await TestApi.UpdateRequirementAsync(lead, requirement.Requirement.RequirementId, "Intent v2");

        var linkedWork = await TestApi.GetRequirementWorkItemsAsync(lead, requirement.Requirement.RequirementId);

        var entry = Assert.Single(linkedWork);
        Assert.Equal(workItem.WorkItemId, entry.WorkItemId);
    }

    [Fact]
    public async Task GetRequirementWorkItems_OmitsUnlinkedWork()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent that loses its work");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work to be unlinked");

        var link = await TestApi.LinkRequirementAsync(lead, workItem.WorkItemId, requirement.Requirement.RequirementId);
        await TestApi.UnlinkRequirementAsync(lead, workItem.WorkItemId, link.WorkItemLinkId);

        var linkedWork = await TestApi.GetRequirementWorkItemsAsync(lead, requirement.Requirement.RequirementId);

        Assert.Empty(linkedWork);
    }
}
