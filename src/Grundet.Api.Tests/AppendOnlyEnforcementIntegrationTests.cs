using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class AppendOnlyEnforcementIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task DirectUpdate_OnRequirements_IsBlockedByTrigger()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var req = await TestApi.CreateRequirementAsync(lead, feature.Id, "test requirement");

        await fixture.WithDbContextAsync(async db =>
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.ExecuteSqlRawAsync(
                    $"""UPDATE "Requirements" SET "Text" = 'modified' WHERE "Id" = '{req.Requirement.Id}'"""));
            Assert.Contains("append-only", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DirectDelete_OnRequirements_IsBlockedByTrigger()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var req = await TestApi.CreateRequirementAsync(lead, feature.Id, "test requirement");

        await fixture.WithDbContextAsync(async db =>
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.ExecuteSqlRawAsync(
                    $"""DELETE FROM "Requirements" WHERE "Id" = '{req.Requirement.Id}'"""));
            Assert.Contains("append-only", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DirectUpdate_OnWorkItems_IsBlockedByTrigger()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "test work item");

        await fixture.WithDbContextAsync(async db =>
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.ExecuteSqlRawAsync(
                    $"""UPDATE "WorkItems" SET "Title" = 'modified' WHERE "Id" = '{workItem.Id}'"""));
            Assert.Contains("append-only", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DirectDelete_OnWorkItems_IsBlockedByTrigger()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "test work item");

        await fixture.WithDbContextAsync(async db =>
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.ExecuteSqlRawAsync(
                    $"""DELETE FROM "WorkItems" WHERE "Id" = '{workItem.Id}'"""));
            Assert.Contains("append-only", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DirectUpdate_OnWorkItemLinks_IsBlockedByTrigger()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var req = await TestApi.CreateRequirementAsync(lead, feature.Id, "req for link test");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "wi for link test");
        var link = await TestApi.LinkRequirementAsync(lead, workItem.WorkItemId, req.Requirement.RequirementId);

        await fixture.WithDbContextAsync(async db =>
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.ExecuteSqlRawAsync(
                    $"""UPDATE "WorkItemLinks" SET "ChangeType" = 'Unlinked' WHERE "Id" = '{link.Id}'"""));
            Assert.Contains("append-only", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DirectDelete_OnWorkItemLinks_IsBlockedByTrigger()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var req = await TestApi.CreateRequirementAsync(lead, feature.Id, "req for link delete test");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "wi for link delete test");
        var link = await TestApi.LinkRequirementAsync(lead, workItem.WorkItemId, req.Requirement.RequirementId);

        await fixture.WithDbContextAsync(async db =>
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.ExecuteSqlRawAsync(
                    $"""DELETE FROM "WorkItemLinks" WHERE "Id" = '{link.Id}'"""));
            Assert.Contains("append-only", ex.MessageText, StringComparison.OrdinalIgnoreCase);
        });
    }
}
