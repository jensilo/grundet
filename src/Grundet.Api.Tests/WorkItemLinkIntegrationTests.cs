using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class WorkItemLinkIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task DeletingLinkedRequirement_LeavesLinkUntouched()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement to be deleted");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item linked to deleted requirement");

        var linked = await TestApi.LinkRequirementAsync(
            lead,
            workItem.WorkItemId,
            requirement.Requirement.RequirementId);

        var deleteResponse = await lead.Client.DeleteAsync(
            $"/api/v1/requirements/{requirement.Requirement.RequirementId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var requirementHistory = await db.Requirements
                .Where(row => row.RequirementId == requirement.Requirement.RequirementId)
                .ToListAsync();

            Assert.Equal(2, requirementHistory.Count);

            // Deleting the intent must not append any link row: the link stays a single
            // Linked version and surfaces as deleted via the latest-version lookup at read time.
            var linkHistory = await db.WorkItemLinks
                .Where(row => row.WorkItemLinkId == linked.WorkItemLinkId)
                .ToListAsync();

            var linkRow = Assert.Single(linkHistory);
            Assert.Equal(WorkItemLinkChange.Linked, linkRow.ChangeType);
            Assert.Equal(linked.Id, linkRow.Id);
        });
    }

    [Fact]
    public async Task WorkItemLinkLifecycle_AppendsVersionHistoryAndLeavesPreviousRowsUnchanged()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Initial requirement");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item with linked requirement");

        var linked = await TestApi.LinkRequirementAsync(
            lead,
            workItem.WorkItemId,
            requirement.Requirement.RequirementId);
        var unlinked = await TestApi.UnlinkRequirementAsync(
            lead,
            workItem.WorkItemId,
            linked.WorkItemLinkId);

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.WorkItemLinks
                .Where(row => row.WorkItemLinkId == linked.WorkItemLinkId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);
            Assert.Equal(
                [
                    WorkItemLinkChange.Linked,
                    WorkItemLinkChange.Unlinked
                ],
                history.Select(row => row.ChangeType).ToArray());

            var linkedRow = history.Single(row => row.Id == linked.Id);
            var unlinkedRow = history.Single(row => row.Id == unlinked.Id);

            Assert.All(history, row => Assert.Equal(linked.WorkItemLinkId, row.WorkItemLinkId));
            Assert.Equal(workItem.WorkItemId, linkedRow.WorkItemId);
            Assert.Equal(requirement.Requirement.RequirementId, linkedRow.RequirementId);
            Assert.Equal(linkedRow.RequirementId, unlinkedRow.RequirementId);
            Assert.NotEqual(linkedRow.Id, unlinkedRow.Id);
        });
    }

    [Fact]
    public async Task LinkRequirement_FromDifferentProject_ReturnsBadRequest()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var projectA = await TestApi.CreateProjectAsync(lead, team.Id, "Project A");
        var projectB = await TestApi.CreateProjectAsync(lead, team.Id, "Project B");
        var featureB = await TestApi.CreateFeatureAsync(lead, projectB.Id);
        var requirementB = await TestApi.CreateRequirementAsync(lead, featureB.Id, "Requirement in project B");
        var workItemA = await TestApi.CreateWorkItemAsync(lead, projectA.Id, "Work item in project A");

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemA.WorkItemId}/links",
            new { requirementId = requirementB.Requirement.RequirementId });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LinkRequirement_WhenAlreadyActivelyLinked_ReturnsConflict()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement for duplicate link test");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item for duplicate link test");

        await TestApi.LinkRequirementAsync(lead, workItem.WorkItemId, requirement.Requirement.RequirementId);

        var secondResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItem.WorkItemId}/links",
            new { requirementId = requirement.Requirement.RequirementId });

        Assert.Equal(System.Net.HttpStatusCode.Conflict, secondResponse.StatusCode);
    }
}
