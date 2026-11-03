using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class CascadeDeleteIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task DeletingFeature_AppendsRequirementDeletedVersionWithProjectId()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Feature cascade requirement");

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/projects/{project.Id}/features/{feature.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var versions = await db.Requirements
                .Where(row => row.RequirementId == requirement.Requirement.RequirementId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, versions.Count);
            Assert.Equal(RequirementChange.Created, versions[0].ChangeType);
            Assert.Equal(RequirementChange.Deleted, versions[1].ChangeType);
            Assert.Equal(project.Id, versions[1].ProjectId);
            Assert.Equal(feature.Id, versions[1].FeatureId);
            Assert.Equal(versions[0].ProjectId, versions[1].ProjectId);
        });
    }

    [Fact]
    public async Task DeletingProject_CascadesToFeaturesAndRequirements()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var featureA = await TestApi.CreateFeatureAsync(lead, project.Id, "Feature A");
        var featureB = await TestApi.CreateFeatureAsync(lead, project.Id, "Feature B");
        var requirementA = await TestApi.CreateRequirementAsync(lead, featureA.Id, "Project cascade req A");
        var requirementB = await TestApi.CreateRequirementAsync(lead, featureB.Id, "Project cascade req B");

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var projectRow = await db.Projects
                .IgnoreQueryFilters()
                .SingleAsync(row => row.Id == project.Id);
            Assert.NotNull(projectRow.DeletedAt);

            var features = await db.Features
                .IgnoreQueryFilters()
                .Where(row => row.ProjectId == project.Id)
                .ToListAsync();
            Assert.Equal(2, features.Count);
            Assert.All(features, row => Assert.NotNull(row.DeletedAt));

            var logicalRequirementIds = new[]
            {
                requirementA.Requirement.RequirementId,
                requirementB.Requirement.RequirementId
            };

            foreach (var logicalRequirementId in logicalRequirementIds)
            {
                var latest = await db.Requirements
                    .Where(row => row.RequirementId == logicalRequirementId)
                    .OrderByDescending(row => row.ChangedAt)
                    .ThenByDescending(row => row.Id)
                    .FirstAsync();

                Assert.Equal(RequirementChange.Deleted, latest.ChangeType);
                Assert.Equal(project.Id, latest.ProjectId);
            }
        });
    }

    [Fact]
    public async Task DeletingTeam_CascadesToProjectsFeaturesAndRequirements()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Team cascade requirement");

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var teamRow = await db.Teams
                .IgnoreQueryFilters()
                .SingleAsync(row => row.Id == team.Id);
            Assert.NotNull(teamRow.DeletedAt);

            var projectRow = await db.Projects
                .IgnoreQueryFilters()
                .SingleAsync(row => row.Id == project.Id);
            Assert.NotNull(projectRow.DeletedAt);

            var featureRow = await db.Features
                .IgnoreQueryFilters()
                .SingleAsync(row => row.Id == feature.Id);
            Assert.NotNull(featureRow.DeletedAt);

            var latestRequirement = await db.Requirements
                .Where(row => row.RequirementId == requirement.Requirement.RequirementId)
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.Id)
                .FirstAsync();

            Assert.Equal(RequirementChange.Deleted, latestRequirement.ChangeType);
            Assert.Equal(project.Id, latestRequirement.ProjectId);
        });
    }

    [Fact]
    public async Task DeletingFeature_WithLinkedWorkItems_AppendsUnlinkedRowsForActiveLinks()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement in feature with linked work item");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item linked to feature requirement");

        var linked = await TestApi.LinkRequirementAsync(
            lead,
            workItem.WorkItemId,
            requirement.Requirement.RequirementId);

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/projects/{project.Id}/features/{feature.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var linkHistory = await db.WorkItemLinks
                .Where(row => row.WorkItemLinkId == linked.WorkItemLinkId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, linkHistory.Count);
            var unlinkedRow = linkHistory.Single(row => row.ChangeType == WorkItemLinkChange.Unlinked);
            Assert.Equal(linked.WorkItemLinkId, unlinkedRow.WorkItemLinkId);
        });
    }

    [Fact]
    public async Task DeletingTeam_WithWorkItemsAndActiveLinks_AppendsDeletedWorkItemAndUnlinkedLinkRows()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Team cascade requirement with link");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item for team cascade");

        var linked = await TestApi.LinkRequirementAsync(
            lead,
            workItem.WorkItemId,
            requirement.Requirement.RequirementId);

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var latestWorkItem = await db.WorkItems
                .Where(row => row.WorkItemId == workItem.WorkItemId)
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.Id)
                .FirstAsync();

            Assert.Equal(WorkItemChange.Deleted, latestWorkItem.ChangeType);

            var linkHistory = await db.WorkItemLinks
                .Where(row => row.WorkItemLinkId == linked.WorkItemLinkId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, linkHistory.Count);
            var unlinkedRow = linkHistory.Single(row => row.ChangeType == WorkItemLinkChange.Unlinked);
            Assert.Equal(linked.WorkItemLinkId, unlinkedRow.WorkItemLinkId);
        });
    }

    [Fact]
    public async Task DeletingProject_WithWorkItemsAndActiveLinks_AppendsDeletedWorkItemAndUnlinkedLinkRows()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Project cascade requirement with link");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item for project cascade");

        var linked = await TestApi.LinkRequirementAsync(
            lead,
            workItem.WorkItemId,
            requirement.Requirement.RequirementId);

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var latestWorkItem = await db.WorkItems
                .Where(row => row.WorkItemId == workItem.WorkItemId)
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.Id)
                .FirstAsync();

            Assert.Equal(WorkItemChange.Deleted, latestWorkItem.ChangeType);

            var linkHistory = await db.WorkItemLinks
                .Where(row => row.WorkItemLinkId == linked.WorkItemLinkId)
                .OrderBy(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            Assert.Equal(2, linkHistory.Count);
            var unlinkedRow = linkHistory.Single(row => row.ChangeType == WorkItemLinkChange.Unlinked);
            Assert.Equal(linked.WorkItemLinkId, unlinkedRow.WorkItemLinkId);
        });
    }
}
