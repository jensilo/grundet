using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class JiraIntegrationTests(ApiIntegrationFixture fixture)
{
    private static AuthSession? SharedLead;
    private static AuthSession? SharedContributor;

    [Fact]
    public async Task Contributor_CanGetProjectMapping_ButCannotPutOrDelete()
    {
        fixture.EmailSink.Clear();
        var lead = await GetSharedLeadAsync();
        var contributor = await GetSharedContributorAsync();
        string contributorEmail = contributor.Email;
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.InviteContributorAsync(lead, team.Id, contributorEmail);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);
        await SeedJiraConnectionAsync(team.Id);

        var leadPutResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/jira/project",
            new { projectKey = "GRD" });
        Assert.Equal(HttpStatusCode.NoContent, leadPutResponse.StatusCode);

        var contributorGetResponse = await contributor.Client.GetAsync($"/api/v1/projects/{project.Id}/jira/project");
        Assert.Equal(HttpStatusCode.OK, contributorGetResponse.StatusCode);
        var mapping = await contributorGetResponse.Content.ReadFromJsonAsync<JiraProjectMappingResponse>();
        Assert.NotNull(mapping);
        Assert.Equal(project.Id, mapping!.ProjectId);
        Assert.Equal("GRD", mapping.ProjectKey);
        Assert.Equal("Explicit", mapping.Source);

        var contributorPutResponse = await contributor.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/jira/project",
            new { projectKey = "NEW" });
        Assert.Equal(HttpStatusCode.Forbidden, contributorPutResponse.StatusCode);

        var contributorDeleteResponse = await contributor.Client.DeleteAsync($"/api/v1/projects/{project.Id}/jira/project");
        Assert.Equal(HttpStatusCode.Forbidden, contributorDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Lead_CanPutGetAndDeleteProjectMapping()
    {
        fixture.EmailSink.Clear();
        var lead = await GetSharedLeadAsync();
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        await SeedJiraConnectionAsync(team.Id);

        var putResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/jira/project",
            new { projectKey = "abc" });
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await lead.Client.GetAsync($"/api/v1/projects/{project.Id}/jira/project");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var mapping = await getResponse.Content.ReadFromJsonAsync<JiraProjectMappingResponse>();
        Assert.NotNull(mapping);
        Assert.Equal(project.Id, mapping!.ProjectId);
        Assert.Equal("ABC", mapping.ProjectKey);
        Assert.Equal("Explicit", mapping.Source);

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/projects/{project.Id}/jira/project");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDeleteResponse = await lead.Client.GetAsync($"/api/v1/projects/{project.Id}/jira/project");
        Assert.Equal(HttpStatusCode.OK, getAfterDeleteResponse.StatusCode);
        var mappingAfterDelete = await getAfterDeleteResponse.Content.ReadFromJsonAsync<JiraProjectMappingResponse>();
        Assert.NotNull(mappingAfterDelete);
        Assert.Equal(project.Id, mappingAfterDelete!.ProjectId);
        Assert.Null(mappingAfterDelete.ProjectKey);
        Assert.Equal("None", mappingAfterDelete.Source);
    }

    [Fact]
    public async Task TeamProjectMappings_ReturnsOnlyActiveTeamProjects()
    {
        fixture.EmailSink.Clear();
        var lead = await GetSharedLeadAsync();
        var team = await TestApi.CreateTeamAsync(lead);
        var activeProject = await TestApi.CreateProjectAsync(lead, team.Id, "Active");
        var deletedProject = await TestApi.CreateProjectAsync(lead, team.Id, "Deleted");
        await SeedJiraConnectionAsync(team.Id);

        var activePut = await lead.Client.PutAsJsonAsync(
            $"/api/v1/projects/{activeProject.Id}/jira/project",
            new { projectKey = "act" });
        Assert.Equal(HttpStatusCode.NoContent, activePut.StatusCode);

        var deletedPut = await lead.Client.PutAsJsonAsync(
            $"/api/v1/projects/{deletedProject.Id}/jira/project",
            new { projectKey = "del" });
        Assert.Equal(HttpStatusCode.NoContent, deletedPut.StatusCode);

        var deleteProjectResponse = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/projects/{deletedProject.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteProjectResponse.StatusCode);

        var listResponse = await lead.Client.GetAsync($"/api/v1/teams/{team.Id}/jira/project-mappings");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var mappings = await listResponse.Content.ReadFromJsonAsync<List<JiraProjectMappingListItemResponse>>();
        Assert.NotNull(mappings);
        Assert.Single(mappings!);
        Assert.Equal(activeProject.Id, mappings[0].ProjectId);
        Assert.Equal("Active", mappings[0].ProjectName);
        Assert.Equal("ACT", mappings[0].ProjectKey);
    }

    [Fact]
    public async Task HandleCallback_WithTamperedState_ReturnsBadRequest()
    {
        fixture.EmailSink.Clear();
        var lead = await GetSharedLeadAsync();

        var response = await lead.Client.GetAsync(
            "/api/v1/jira/callback?code=anything&state=tampered_garbage");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TeamDefaultProjectEndpoint_RemainsBackwardCompatible()
    {
        fixture.EmailSink.Clear();
        var lead = await GetSharedLeadAsync();
        var team = await TestApi.CreateTeamAsync(lead);
        await SeedJiraConnectionAsync(team.Id);

        var response = await lead.Client.PutAsJsonAsync(
            $"/api/v1/teams/{team.Id}/jira/project",
            new { projectKey = "fallback" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var connection = await db.JiraConnections
                .AsNoTracking()
                .SingleAsync(x => x.TeamId == team.Id);
            Assert.Equal("FALLBACK", connection.JiraProjectKey);
        });
    }

    private async Task SeedJiraConnectionAsync(Guid teamId) => await fixture.WithDbContextAsync(async db =>
                                                                    {
                                                                        var existing = await db.JiraConnections
                                                                            .SingleOrDefaultAsync(x => x.TeamId == teamId);

                                                                        if (existing is not null)
                                                                        {
                                                                            return;
                                                                        }

                                                                        db.JiraConnections.Add(new JiraConnection
                                                                        {
                                                                            Id = Guid.NewGuid(),
                                                                            TeamId = teamId,
                                                                            CloudId = "test-cloud",
                                                                            AccessToken = "test-access",
                                                                            RefreshToken = "test-refresh",
                                                                            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
                                                                            JiraProjectKey = null,
                                                                            CreatedAt = DateTime.UtcNow
                                                                        });

                                                                        await db.SaveChangesAsync();
                                                                    });

    private async Task<AuthSession> GetSharedLeadAsync()
    {
        SharedLead ??= await TestApi.SignInAsync(fixture);
        return SharedLead;
    }

    private async Task<AuthSession> GetSharedContributorAsync()
    {
        SharedContributor ??= await TestApi.SignInAsync(fixture);
        return SharedContributor;
    }

    private sealed record JiraProjectMappingResponse(Guid ProjectId, string? ProjectKey, string Source);

    private sealed record JiraProjectMappingListItemResponse(Guid ProjectId, string ProjectName, string ProjectKey);
}
