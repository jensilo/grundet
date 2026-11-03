using System.Net;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class ReferenceResolutionIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task Resolve_RequirementReference_ReturnsTitleAndStatus()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(
            lead, feature.Id, "System shall preview linked references");

        var resolved = await TestApi.ResolveProjectReferenceAsync(
            lead, project.Id, requirement.Requirement.Ref);

        Assert.Equal("requirement", resolved.Type);
        Assert.Equal(requirement.Requirement.RequirementId, resolved.Id);
        Assert.Equal("System shall preview linked references", resolved.Title);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Status));
    }

    [Fact]
    public async Task Resolve_BareReference_ResolvesAcrossKinds()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // Requirements and work items share one per-project sequence counter.
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent owns a sequence");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work owns the next sequence");

        var resolvedRequirement = await TestApi.ResolveProjectReferenceAsync(
            lead, project.Id, $"#{requirement.Requirement.SequenceNumber}");
        var resolvedWorkItem = await TestApi.ResolveProjectReferenceAsync(
            lead, project.Id, workItem.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal("requirement", resolvedRequirement.Type);
        Assert.Equal(requirement.Requirement.RequirementId, resolvedRequirement.Id);
        Assert.Equal($"IN-{requirement.Requirement.SequenceNumber}", resolvedRequirement.Ref);

        Assert.Equal("workItem", resolvedWorkItem.Type);
        Assert.Equal(workItem.WorkItemId, resolvedWorkItem.Id);
        Assert.Equal($"WO-{workItem.SequenceNumber}", resolvedWorkItem.Ref);
    }

    [Fact]
    public async Task Resolve_WrongKindForExistingSequence_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Only an intent here");

        // The sequence belongs to a requirement, so asking for a work item must miss.
        var response = await TestApi.ResolveProjectReferenceRawAsync(
            lead, project.Id, $"WO-{requirement.Requirement.SequenceNumber}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_UnknownReference_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var response = await TestApi.ResolveProjectReferenceRawAsync(lead, project.Id, "IN-99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("REQ-1")]
    [InlineData("WI-1")]
    public async Task Resolve_DroppedLegacyPrefix_ReturnsBadRequest(string legacyRef)
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // Sequence 1 exists, so a 400 proves the prefix itself is rejected — REQ-/WI- are
        // no longer recognised reference forms (no back-compat).
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent owns sequence 1");

        var response = await TestApi.ResolveProjectReferenceRawAsync(lead, project.Id, legacyRef);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_MalformedReference_ReturnsBadRequest()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var response = await TestApi.ResolveProjectReferenceRawAsync(lead, project.Id, "not-a-ref");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
