using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class DraftIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    // --- New-requirement (feature-scoped) drafts ---

    [Fact]
    public async Task NewRequirementDraft_UpsertThenGet_RoundTripsContent()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var upserted = await UpsertDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft", new { text = "Work in progress", schemaVersion = 1 });

        Assert.Equal(DraftKind.Requirement, upserted.Kind);
        Assert.Equal(feature.Id, upserted.ScopeId);
        Assert.Null(upserted.EntityId);
        Assert.Equal("Work in progress", upserted.DraftContent.GetProperty("text").GetString());

        var fetched = await GetDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft");

        Assert.NotNull(fetched.Draft);
        Assert.Equal(upserted.Id, fetched.Draft!.Id);
        Assert.Equal("Work in progress", fetched.Draft.DraftContent.GetProperty("text").GetString());
    }

    [Fact]
    public async Task NewRequirementDraft_GetWithNoDraft_ReturnsOkWithNullDraft()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await lead.Client.GetAsync($"/api/v1/features/{feature.Id}/draft");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsAsync<GetDraftResponse>(response);
        Assert.Null(body.Draft);
    }

    [Fact]
    public async Task NewRequirementDraft_UpsertTwice_UpdatesSameRow()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var first = await UpsertDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft", new { text = "first" });
        var second = await UpsertDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft", new { text = "second" });

        // Null EntityId must not produce a duplicate row: same draft id, updated content.
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("second", second.DraftContent.GetProperty("text").GetString());
    }

    [Fact]
    public async Task NewRequirementDraft_Delete_RemovesDraft()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await UpsertDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft", new { text = "discard me" });

        var deleteResponse = await lead.Client.DeleteAsync($"/api/v1/features/{feature.Id}/draft");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var fetched = await GetDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft");
        Assert.Null(fetched.Draft);
    }

    [Fact]
    public async Task CreatingRequirement_DeletesNewRequirementDraft()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await UpsertDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft", new { text = "draft text" });
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Final requirement text");

        var fetched = await GetDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft");
        Assert.Null(fetched.Draft);
    }

    // --- Existing-requirement drafts ---

    [Fact]
    public async Task ExistingRequirementDraft_RoundTrips_AndDeletedOnUpdate()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Original text");
        Guid requirementId = requirement.Requirement.RequirementId;

        var upserted = await UpsertDraftAsync(lead, $"/api/v1/requirements/{requirementId}/draft", new { text = "edited text" });
        Assert.Equal(requirementId, upserted.EntityId);

        await TestApi.UpdateRequirementAsync(lead, requirementId, "Saved text");

        var fetched = await GetDraftAsync(lead, $"/api/v1/requirements/{requirementId}/draft");
        Assert.Null(fetched.Draft);
    }

    // --- New-work-item (project-scoped) drafts ---

    [Fact]
    public async Task NewWorkItemDraft_RoundTrips_AndDeletedOnCreate()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var upserted = await UpsertDraftAsync(lead, $"/api/v1/projects/{project.Id}/work-draft", new { title = "draft title" });
        Assert.Equal(DraftKind.WorkItem, upserted.Kind);
        Assert.Equal(project.Id, upserted.ScopeId);
        Assert.Null(upserted.EntityId);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Final work item");

        var fetched = await GetDraftAsync(lead, $"/api/v1/projects/{project.Id}/work-draft");
        Assert.Null(fetched.Draft);
    }

    // --- Existing-work-item drafts ---

    [Fact]
    public async Task ExistingWorkItemDraft_RoundTrips_AndDeletedOnUpdate()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Original title");

        var upserted = await UpsertDraftAsync(lead, $"/api/v1/work-items/{workItem.WorkItemId}/draft", new { title = "edited title" });
        Assert.Equal(workItem.WorkItemId, upserted.EntityId);

        await TestApi.UpdateWorkItemAsync(lead, workItem.WorkItemId, "Saved title");

        var fetched = await GetDraftAsync(lead, $"/api/v1/work-items/{workItem.WorkItemId}/draft");
        Assert.Null(fetched.Draft);
    }

    // --- Per-user isolation ---

    [Fact]
    public async Task NewRequirementDraft_IsPrivatePerUser()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        string contributorEmail = fixture.CreateUniqueEmail();
        var contributor = await TestApi.SignInAsync(fixture, contributorEmail);

        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        await TestApi.InviteContributorAsync(lead, team.Id, contributorEmail);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        await UpsertDraftAsync(lead, $"/api/v1/features/{feature.Id}/draft", new { text = "lead's private draft" });

        // Same feature, different user: the contributor must not see the lead's draft.
        var contributorView = await GetDraftAsync(contributor, $"/api/v1/features/{feature.Id}/draft");
        Assert.Null(contributorView.Draft);
    }

    // --- Cross-tenant authorization ---

    [Fact]
    public async Task CrossTenantDraftEndpoints_ReturnNotFound()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        var ownerFeature = await TestApi.CreateFeatureAsync(owner, ownerProject.Id);
        var ownerRequirement = await TestApi.CreateRequirementAsync(owner, ownerFeature.Id, "Owner requirement");
        var ownerWorkItem = await TestApi.CreateWorkItemAsync(owner, ownerProject.Id, "Owner work item");

        // Authenticated but unrelated tenant.
        _ = await TestApi.CreateTeamAsync(outsider);

        var draftBody = new { draftContent = new { text = "intruder" } };

        string[] targets =
        [
            $"/api/v1/features/{ownerFeature.Id}/draft",
            $"/api/v1/requirements/{ownerRequirement.Requirement.RequirementId}/draft",
            $"/api/v1/projects/{ownerProject.Id}/work-draft",
            $"/api/v1/work-items/{ownerWorkItem.WorkItemId}/draft"
        ];

        foreach (string target in targets)
        {
            var putResponse = await outsider.Client.PutAsJsonAsync(target, draftBody);
            Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);

            var getResponse = await outsider.Client.GetAsync(target);
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

            var deleteResponse = await outsider.Client.DeleteAsync(target);
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }
    }

    // --- Helpers ---

    private static async Task<DraftResponse> UpsertDraftAsync(AuthSession user, string path, object content)
    {
        var response = await user.Client.PutAsJsonAsync(path, new { draftContent = content });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsAsync<DraftResponse>(response);
    }

    private static async Task<GetDraftResponse> GetDraftAsync(AuthSession user, string path)
    {
        var response = await user.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsAsync<GetDraftResponse>(response);
    }

    private static async Task<T> ReadAsAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record DraftResponse(
        Guid Id,
        DraftKind Kind,
        Guid ScopeId,
        Guid? EntityId,
        JsonElement DraftContent,
        DateTime UpdatedAt);

    private sealed record GetDraftResponse(DraftResponse? Draft);
}
