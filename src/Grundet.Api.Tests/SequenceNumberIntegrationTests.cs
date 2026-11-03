using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class SequenceNumberIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task Requirements_GetDistinctSequenceNumbers_AndResolveByRef()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var first = await TestApi.CreateRequirementAsync(lead, feature.Id, "First sequence requirement");
        var second = await TestApi.CreateRequirementAsync(lead, feature.Id, "Second sequence requirement");

        Assert.True(first.Requirement.SequenceNumber > 0);
        Assert.True(second.Requirement.SequenceNumber > first.Requirement.SequenceNumber);
        Assert.NotEqual(first.Requirement.SequenceNumber, second.Requirement.SequenceNumber);

        var resolvedFirst = await TestApi.ResolveProjectReferenceAsync(
            lead,
            project.Id,
            $"IN-{first.Requirement.SequenceNumber}");
        Assert.Equal("requirement", resolvedFirst.Type);
        Assert.Equal(first.Requirement.RequirementId, resolvedFirst.Id);
        Assert.Equal(first.Requirement.SequenceNumber, resolvedFirst.SequenceNumber);

        var resolvedSecond = await TestApi.ResolveProjectReferenceAsync(
            lead,
            project.Id,
            $"IN-{second.Requirement.SequenceNumber}");
        Assert.Equal("requirement", resolvedSecond.Type);
        Assert.Equal(second.Requirement.RequirementId, resolvedSecond.Id);
        Assert.Equal(second.Requirement.SequenceNumber, resolvedSecond.SequenceNumber);
    }

    [Fact]
    public async Task WorkItems_GetDistinctSequenceNumbers_AndResolveByRef()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var first = await TestApi.CreateWorkItemAsync(lead, project.Id, "First sequence work item");
        var second = await TestApi.CreateWorkItemAsync(lead, project.Id, "Second sequence work item");

        Assert.True(first.SequenceNumber > 0);
        Assert.True(second.SequenceNumber > first.SequenceNumber);
        Assert.NotEqual(first.SequenceNumber, second.SequenceNumber);

        var resolvedFirst = await TestApi.ResolveProjectReferenceAsync(
            lead,
            project.Id,
            $"WO-{first.SequenceNumber}");
        Assert.Equal("workItem", resolvedFirst.Type);
        Assert.Equal(first.WorkItemId, resolvedFirst.Id);
        Assert.Equal(first.SequenceNumber, resolvedFirst.SequenceNumber);

        var resolvedSecond = await TestApi.ResolveProjectReferenceAsync(
            lead,
            project.Id,
            $"WO-{second.SequenceNumber}");
        Assert.Equal("workItem", resolvedSecond.Type);
        Assert.Equal(second.WorkItemId, resolvedSecond.Id);
        Assert.Equal(second.SequenceNumber, resolvedSecond.SequenceNumber);
    }
}
