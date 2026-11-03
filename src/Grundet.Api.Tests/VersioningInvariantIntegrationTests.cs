using System.Net;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class VersioningInvariantIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task UpdatingRequirement_AppendsNewVersionAndLeavesPreviousRowUnchanged()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var created = await TestApi.CreateRequirementAsync(lead, feature.Id, "Initial requirement text");

        var updated = await TestApi.UpdateRequirementAsync(
            lead,
            created.Requirement.RequirementId,
            "Updated requirement text");

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.Requirements
                .Where(row => row.RequirementId == created.Requirement.RequirementId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);

            var originalRow = history.Single(row => row.Id == created.Requirement.Id);
            var updatedRow = history.Single(row => row.Id == updated.Requirement.Id);

            Assert.Equal(RequirementChange.Created, originalRow.ChangeType);
            Assert.Equal("Initial requirement text", originalRow.Text);
            Assert.Equal(RequirementChange.Updated, updatedRow.ChangeType);
            Assert.Equal("Updated requirement text", updatedRow.Text);
            Assert.Equal(originalRow.SequenceNumber, updatedRow.SequenceNumber);
            Assert.NotEqual(originalRow.Id, updatedRow.Id);
        });
    }

    [Fact]
    public async Task UpdatingWorkItem_AppendsNewVersionAndLeavesPreviousRowUnchanged()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var created = await TestApi.CreateWorkItemAsync(lead, project.Id, "Initial work item title");

        var updated = await TestApi.UpdateWorkItemAsync(
            lead,
            created.WorkItemId,
            "Updated work item title");

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.WorkItems
                .Where(row => row.WorkItemId == created.WorkItemId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);

            var originalRow = history.Single(row => row.Id == created.Id);
            var updatedRow = history.Single(row => row.Id == updated.Id);

            Assert.Equal(WorkItemChange.Created, originalRow.ChangeType);
            Assert.Equal("Initial work item title", originalRow.Title);
            Assert.Equal(WorkItemChange.Updated, updatedRow.ChangeType);
            Assert.Equal("Updated work item title", updatedRow.Title);
            Assert.Equal(originalRow.SequenceNumber, updatedRow.SequenceNumber);
            Assert.NotEqual(originalRow.Id, updatedRow.Id);
        });
    }

    [Fact]
    public async Task DeletingRequirement_AppendsDeletedVersionAndLeavesPreviousRowUnchanged()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var created = await TestApi.CreateRequirementAsync(lead, feature.Id, "Initial requirement text");

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/requirements/{created.Requirement.RequirementId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.Requirements
                .Where(row => row.RequirementId == created.Requirement.RequirementId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);

            var originalRow = history.Single(row => row.Id == created.Requirement.Id);
            var deletedRow = history.Single(row => row.ChangeType == RequirementChange.Deleted);

            Assert.Equal(RequirementChange.Created, originalRow.ChangeType);
            Assert.Equal("Initial requirement text", originalRow.Text);
            Assert.Equal(created.Requirement.RequirementId, originalRow.RequirementId);
            Assert.Equal(created.Requirement.SequenceNumber, originalRow.SequenceNumber);

            Assert.Equal(RequirementChange.Deleted, deletedRow.ChangeType);
            Assert.Equal(created.Requirement.RequirementId, deletedRow.RequirementId);
            Assert.Equal(created.Requirement.SequenceNumber, deletedRow.SequenceNumber);
            Assert.NotEqual(originalRow.Id, deletedRow.Id);
        });
    }

    [Fact]
    public async Task DeletingWorkItem_AppendsDeletedVersionAndLeavesPreviousRowUnchanged()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var created = await TestApi.CreateWorkItemAsync(lead, project.Id, "Initial work item title");

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/work-items/{created.WorkItemId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.WorkItems
                .Where(row => row.WorkItemId == created.WorkItemId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);

            var originalRow = history.Single(row => row.Id == created.Id);
            var deletedRow = history.Single(row => row.ChangeType == WorkItemChange.Deleted);

            Assert.Equal(WorkItemChange.Created, originalRow.ChangeType);
            Assert.Equal("Initial work item title", originalRow.Title);
            Assert.Equal(created.WorkItemId, originalRow.WorkItemId);
            Assert.Equal(created.SequenceNumber, originalRow.SequenceNumber);

            Assert.Equal(WorkItemChange.Deleted, deletedRow.ChangeType);
            Assert.Equal(created.WorkItemId, deletedRow.WorkItemId);
            Assert.Equal(created.SequenceNumber, deletedRow.SequenceNumber);
            Assert.NotEqual(originalRow.Id, deletedRow.Id);
        });
    }

    [Fact]
    public async Task DeleteWorkItem_WithActiveLinks_AppendsDeletedRowAndUnlinkedRowForEachActiveLink()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement linked to deleted work item");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item with active link");

        var linked = await TestApi.LinkRequirementAsync(
            lead,
            workItem.WorkItemId,
            requirement.Requirement.RequirementId);

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/work-items/{workItem.WorkItemId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var workItemHistory = await db.WorkItems
                .Where(row => row.WorkItemId == workItem.WorkItemId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, workItemHistory.Count);
            Assert.Contains(workItemHistory, row => row.ChangeType == WorkItemChange.Deleted);

            var linkHistory = await db.WorkItemLinks
                .Where(row => row.WorkItemLinkId == linked.WorkItemLinkId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, linkHistory.Count);

            var linkedRow = linkHistory.Single(row => row.ChangeType == WorkItemLinkChange.Linked);
            var unlinkedRow = linkHistory.Single(row => row.ChangeType == WorkItemLinkChange.Unlinked);

            Assert.Equal(linked.WorkItemLinkId, unlinkedRow.WorkItemLinkId);
            Assert.Equal(linkedRow.RequirementId, unlinkedRow.RequirementId);
            Assert.NotEqual(linkedRow.Id, unlinkedRow.Id);
        });
    }
}
