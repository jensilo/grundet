using System.Net;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class SearchIntegrationTests(ApiIntegrationFixture fixture)
{
    // ── Requirements ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchRequirements_WithMatchingText_ReturnsSameTenantResult()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id);
        await TestApi.CreateRequirementAsync(owner, feature.Id, "System shall authenticate users via magic link");

        var results = await TestApi.SearchRequirementsAsync(owner, project.Id, "magic link");

        Assert.Single(results);
        Assert.Equal("requirement", results[0].Type);
        Assert.Contains("magic link", results[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchRequirements_WithEmptyQuery_ReturnsEmpty()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id);
        await TestApi.CreateRequirementAsync(owner, feature.Id, "First requirement");
        await TestApi.CreateRequirementAsync(owner, feature.Id, "Second requirement");

        var results = await TestApi.SearchRequirementsAsync(owner, project.Id, "");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchRequirements_QueryMatchesNoText_ReturnsEmpty()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id);
        await TestApi.CreateRequirementAsync(owner, feature.Id, "System shall support authentication");

        var results = await TestApi.SearchRequirementsAsync(owner, project.Id, "xyzzy_no_match_zzz");

        Assert.Empty(results);
    }

    // ── Work items ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchWorkItems_WithMatchingText_ReturnsSameTenantResult()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateWorkItemAsync(owner, project.Id, "Implement OAuth provider integration");

        var results = await TestApi.SearchWorkItemsAsync(owner, project.Id, "OAuth");

        Assert.Single(results);
        Assert.Equal("workitem", results[0].Type);
        Assert.Contains("OAuth", results[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchWorkItems_WithEmptyQuery_ReturnsEmpty()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateWorkItemAsync(owner, project.Id, "First task");
        await TestApi.CreateWorkItemAsync(owner, project.Id, "Second task");

        var results = await TestApi.SearchWorkItemsAsync(owner, project.Id, "");

        Assert.Empty(results);
    }

    // ── Features ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchFeatures_WithMatchingText_ReturnsSameTenantResult()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateFeatureAsync(owner, project.Id, "Notification Centre");

        var results = await TestApi.SearchFeaturesAsync(owner, project.Id, "notification");

        Assert.Single(results);
        Assert.Equal("feature", results[0].Type);
        Assert.Contains("Notification", results[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    // ── Global (combined) search ──────────────────────────────────────────────

    [Fact]
    public async Task SearchProject_WithMatchingQuery_ReturnsResultsAcrossAllTypes()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id, "Reporting dashboard");
        await TestApi.CreateRequirementAsync(owner, feature.Id, "System shall export reporting data as CSV");
        await TestApi.CreateWorkItemAsync(owner, project.Id, "Build reporting pipeline");

        var grouped = await TestApi.SearchProjectAsync(owner, project.Id, "reporting");

        Assert.NotEmpty(grouped.Requirements);
        Assert.NotEmpty(grouped.WorkItems);
        Assert.NotEmpty(grouped.Features);
    }

    // ── Workspace (cross-project) search ──────────────────────────────────────

    [Fact]
    public async Task SearchWorkspace_WithMatchingQuery_ReturnsResultsAcrossProjects()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);

        var projectA = await TestApi.CreateProjectAsync(owner, team.Id);
        var featureA = await TestApi.CreateFeatureAsync(owner, projectA.Id);
        await TestApi.CreateRequirementAsync(owner, featureA.Id, "System shall export telemetry data");

        var projectB = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateWorkItemAsync(owner, projectB.Id, "Build telemetry ingestion pipeline");

        var grouped = await TestApi.SearchWorkspaceAsync(owner, "telemetry");

        Assert.Single(grouped.Requirements);
        Assert.Single(grouped.WorkItems);
        Assert.Equal(projectA.Id.ToString(), grouped.Requirements[0].ProjectId);
        Assert.Equal(projectB.Id.ToString(), grouped.WorkItems[0].ProjectId);
    }

    [Fact]
    public async Task SearchWorkspace_CarriesProjectAndTeamContext()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateWorkItemAsync(owner, project.Id, "Investigate telemetry latency");

        var grouped = await TestApi.SearchWorkspaceAsync(owner, "telemetry");

        var result = Assert.Single(grouped.WorkItems);
        Assert.Equal(project.Id.ToString(), result.ProjectId);
        Assert.Equal(project.Name, result.ProjectName);
        Assert.Equal(team.Name, result.TeamName);
    }

    [Fact]
    public async Task SearchWorkspace_ScopedToProject_ExcludesOtherProjects()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);

        var projectA = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateWorkItemAsync(owner, projectA.Id, "Telemetry exporter for project A");

        var projectB = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateWorkItemAsync(owner, projectB.Id, "Telemetry exporter for project B");

        var grouped = await TestApi.SearchWorkspaceAsync(owner, "telemetry", projectA.Id);

        var result = Assert.Single(grouped.WorkItems);
        Assert.Equal(projectA.Id.ToString(), result.ProjectId);
    }

    [Fact]
    public async Task SearchWorkspace_WithEmptyQuery_ReturnsEmpty()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);
        await TestApi.CreateWorkItemAsync(owner, project.Id, "Some work item");

        var grouped = await TestApi.SearchWorkspaceAsync(owner, "");

        Assert.Empty(grouped.Requirements);
        Assert.Empty(grouped.WorkItems);
        Assert.Empty(grouped.Features);
    }

    [Fact]
    public async Task SearchWorkspace_CrossTenant_DoesNotLeakOtherTenants()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        await TestApi.CreateWorkItemAsync(owner, ownerProject.Id, "Confidential telemetry rework");
        _ = await TestApi.CreateTeamAsync(outsider);

        var grouped = await TestApi.SearchWorkspaceAsync(outsider, "telemetry");

        Assert.Empty(grouped.Requirements);
        Assert.Empty(grouped.WorkItems);
        Assert.Empty(grouped.Features);
    }

    [Fact]
    public async Task SearchWorkspace_ScopedToCrossTenantProject_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        _ = await TestApi.CreateTeamAsync(outsider);

        var response = await TestApi.SearchWorkspaceRawAsync(outsider, "telemetry", ownerProject.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchWorkspace_Unauthenticated_ReturnsUnauthorized()
    {
        fixture.EmailSink.Clear();

        var anonymousClient = fixture.CreateClient();
        var response = await anonymousClient.GetAsync("/api/v1/search?query=test");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── RLS / cross-tenant isolation ─────────────────────────────────────────

    [Fact]
    public async Task SearchRequirements_CrossTenantProject_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        var ownerFeature = await TestApi.CreateFeatureAsync(owner, ownerProject.Id);
        await TestApi.CreateRequirementAsync(owner, ownerFeature.Id, "Confidential requirement");
        _ = await TestApi.CreateTeamAsync(outsider);

        // RLS hides the project from the outsider's session → authorization resolves to 404.
        var response = await TestApi.SearchProjectRawAsync(outsider, ownerProject.Id, "confidential");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchProject_Unauthenticated_ReturnsUnauthorized()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner);
        var project = await TestApi.CreateProjectAsync(owner, team.Id);

        var anonymousClient = fixture.CreateClient();
        var response = await anonymousClient.GetAsync($"/api/v1/projects/{project.Id}/search?query=test");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
