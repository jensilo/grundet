using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class WorkItemCreateLinkIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task CreateWorkItem_WithRequirementIds_LinksAllInOneTransaction()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var first = await TestApi.CreateRequirementAsync(lead, feature.Id, "First intent to link");
        var second = await TestApi.CreateRequirementAsync(lead, feature.Id, "Second intent to link");

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/work-items",
            new
            {
                title = "Work item linked to two intents on create",
                priority = "Medium",
                requirementIds = new[]
                {
                    first.Requirement.RequirementId,
                    second.Requirement.RequirementId
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.NotNull(created);

        await fixture.WithDbContextAsync(async db =>
        {
            var links = await db.WorkItemLinks
                .Where(row => row.WorkItemId == created!.WorkItemId)
                .ToListAsync();

            Assert.Equal(2, links.Count);
            Assert.All(links, row => Assert.Equal(WorkItemLinkChange.Linked, row.ChangeType));

            Assert.Contains(links, row => row.RequirementId == first.Requirement.RequirementId);
            Assert.Contains(links, row => row.RequirementId == second.Requirement.RequirementId);
        });
    }

    [Fact]
    public async Task CreateWorkItem_WithDuplicateRequirementIds_LinksOnce()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Intent linked once despite duplicates");

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/work-items",
            new
            {
                title = "Work item with duplicate requirement ids",
                priority = "Medium",
                requirementIds = new[]
                {
                    requirement.Requirement.RequirementId,
                    requirement.Requirement.RequirementId
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.NotNull(created);

        await fixture.WithDbContextAsync(async db =>
        {
            int linkCount = await db.WorkItemLinks
                .CountAsync(row => row.WorkItemId == created!.WorkItemId);
            Assert.Equal(1, linkCount);
        });
    }

    [Fact]
    public async Task CreateWorkItem_WithRequirementFromDifferentProject_ReturnsBadRequestAndPersistsNothing()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var projectA = await TestApi.CreateProjectAsync(lead, team.Id, "Project A");
        var projectB = await TestApi.CreateProjectAsync(lead, team.Id, "Project B");
        var featureB = await TestApi.CreateFeatureAsync(lead, projectB.Id);
        var requirementB = await TestApi.CreateRequirementAsync(lead, featureB.Id, "Requirement in project B");

        const string title = "Work item that must not be created with a cross-project link";
        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectA.Id}/work-items",
            new
            {
                title,
                priority = "Medium",
                requirementIds = new[] { requirementB.Requirement.RequirementId }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            // The link validation throws before SaveChanges, so neither the work item nor any link
            // is committed — creation is atomic.
            bool workItemExists = await db.WorkItems.AnyAsync(row => row.Title == title);
            Assert.False(workItemExists);
        });
    }
}
