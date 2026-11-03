using System.Net;
using System.Net.Http.Json;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class AuthorizationIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly string[] AuthTestLabels = ["auth-test"];
    private static readonly string[] DefaultAcceptanceCriteria = ["AC"];
    private static AuthSession? SharedOwner;
    private static AuthSession? SharedOutsider;

    [Fact]
    public async Task Contributor_CanCreateProject()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        string contributorEmail = fixture.CreateUniqueEmail();
        var contributor = await TestApi.SignInAsync(fixture, contributorEmail);

        var team = await TestApi.CreateTeamAsync(lead);
        await TestApi.InviteContributorAsync(lead, team.Id, contributorEmail);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        var project = await TestApi.CreateProjectAsync(contributor, team.Id, "Contributor project");

        Assert.Equal("Contributor project", project.Name);
    }

    [Fact]
    public async Task Contributor_IsDenied_OnLeadOnlyProjectDeleteEndpoint()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        string contributorEmail = fixture.CreateUniqueEmail();
        var contributor = await TestApi.SignInAsync(fixture, contributorEmail);

        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        await TestApi.InviteContributorAsync(lead, team.Id, contributorEmail);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        var deleteResponse = await contributor.Client.DeleteAsync($"/api/v1/teams/{team.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task CrossTenantProjectAccess_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await GetSharedOwnerAsync();
        var outsider = await GetSharedOutsiderAsync();

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);

        // Create a separate tenant context so the outsider is authenticated but not a member of the owner team.
        _ = await TestApi.CreateTeamAsync(outsider);

        var response = await outsider.Client.GetAsync($"/api/v1/projects/{ownerProject.Id}/features");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CrossTenantRequirementMutations_ReturnNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await GetSharedOwnerAsync();
        var outsider = await GetSharedOutsiderAsync();

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        var ownerFeature = await TestApi.CreateFeatureAsync(owner, ownerProject.Id);
        var ownerRequirement = await TestApi.CreateRequirementAsync(owner, ownerFeature.Id, "Owner requirement");

        _ = await TestApi.CreateTeamAsync(outsider);

        var createResponse = await outsider.Client.PostAsJsonAsync(
            $"/api/v1/features/{ownerFeature.Id}/requirements",
            new
            {
                text = "Outsider attempt",
                status = "Draft",
                priority = "Must",
                labels = AuthTestLabels,
                acceptanceCriteria = DefaultAcceptanceCriteria,
                reason = (string?)null
            });
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);

        var updateResponse = await outsider.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{ownerRequirement.Requirement.RequirementId}",
            new
            {
                text = "Outsider update attempt",
                priority = "Should",
                reason = "Should not succeed"
            });
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task CrossTenantWorkItemMutations_ReturnNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await GetSharedOwnerAsync();
        var outsider = await GetSharedOutsiderAsync();

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        var ownerWorkItem = await TestApi.CreateWorkItemAsync(owner, ownerProject.Id, "Owner work item");

        _ = await TestApi.CreateTeamAsync(outsider);

        var createResponse = await outsider.Client.PostAsJsonAsync(
            $"/api/v1/projects/{ownerProject.Id}/work-items",
            new
            {
                title = "Outsider attempt",
                description = "Should be denied",
                status = "Todo",
                priority = "Medium",
                labels = AuthTestLabels,
                assignedToUserId = (Guid?)null,
                blockedReason = (string?)null,
                reason = (string?)null,
                type = "Task"
            });
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);

        var updateResponse = await outsider.Client.PutAsJsonAsync(
            $"/api/v1/work-items/{ownerWorkItem.WorkItemId}",
            new
            {
                title = "Outsider update attempt",
                status = "InProgress",
                reason = "Should not succeed"
            });
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task SearchEndpoints_DenyCrossTenantProjectAndFeatureFilterAccess_WithNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await GetSharedOwnerAsync();
        var outsider = await GetSharedOutsiderAsync();

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        var ownerFeature = await TestApi.CreateFeatureAsync(owner, ownerProject.Id);

        var outsiderTeam = await TestApi.CreateTeamAsync(outsider);
        var outsiderProject = await TestApi.CreateProjectAsync(outsider, outsiderTeam.Id);

        var projectSearchResponse = await outsider.Client.GetAsync(
            $"/api/v1/projects/{ownerProject.Id}/search?q=requirement");
        Assert.Equal(HttpStatusCode.NotFound, projectSearchResponse.StatusCode);

        var filterSearchResponse = await outsider.Client.GetAsync(
            $"/api/v1/projects/{outsiderProject.Id}/requirements/search?q=requirement&featureId={ownerFeature.Id}");
        Assert.Equal(HttpStatusCode.NotFound, filterSearchResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectScopedRequirementRoutes_WithMismatchedProjectId_ReturnNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await GetSharedOwnerAsync();

        var team = await TestApi.CreateTeamAsync(owner);
        var projectA = await TestApi.CreateProjectAsync(owner, team.Id);
        var projectB = await TestApi.CreateProjectAsync(owner, team.Id);
        var featureB = await TestApi.CreateFeatureAsync(owner, projectB.Id);
        var requirement = await TestApi.CreateRequirementAsync(owner, featureB.Id, "Project B requirement");

        var getResponse = await owner.Client.GetAsync(
            $"/api/v1/projects/{projectA.Id}/requirements/{requirement.Requirement.RequirementId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var putResponse = await owner.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectA.Id}/requirements/{requirement.Requirement.RequirementId}",
            new { reason = "Should not pass" });
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);

        var deleteResponse = await owner.Client.DeleteAsync(
            $"/api/v1/projects/{projectA.Id}/requirements/{requirement.Requirement.RequirementId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectScopedWorkItemRoutes_WithMismatchedProjectId_ReturnNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await GetSharedOwnerAsync();

        var team = await TestApi.CreateTeamAsync(owner);
        var projectA = await TestApi.CreateProjectAsync(owner, team.Id);
        var projectB = await TestApi.CreateProjectAsync(owner, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(owner, projectB.Id, "Project B work item");

        var getResponse = await owner.Client.GetAsync(
            $"/api/v1/projects/{projectA.Id}/work-items/{workItem.WorkItemId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var putResponse = await owner.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectA.Id}/work-items/{workItem.WorkItemId}",
            new { reason = "Should not pass" });
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);

        var deleteResponse = await owner.Client.DeleteAsync(
            $"/api/v1/projects/{projectA.Id}/work-items/{workItem.WorkItemId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task RemoveTeamMember_WithNonExistentMemberId_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        var response = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/members/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeTeamMemberRole_WithNonExistentMemberId_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        var response = await lead.Client.PutAsJsonAsync(
            $"/api/v1/teams/{team.Id}/members/{Guid.NewGuid()}/role",
            new { role = "Contributor" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<AuthSession> GetSharedOwnerAsync()
    {
        SharedOwner ??= await TestApi.SignInAsync(fixture);
        return SharedOwner;
    }

    private async Task<AuthSession> GetSharedOutsiderAsync()
    {
        SharedOutsider ??= await TestApi.SignInAsync(fixture);
        return SharedOutsider;
    }
}
