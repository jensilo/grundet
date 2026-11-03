using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class RequirementMoveIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task MoveRequirement_ToFeatureInSameProject_CreatesNewVersionRow_PreservingFieldsAndUpdatingFeature()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var sourceFeature = await TestApi.CreateFeatureAsync(lead, project.Id, "Source feature");
        var targetFeature = await TestApi.CreateFeatureAsync(lead, project.Id, "Target feature");

        const string rationale = "Belongs with the onboarding work.";
        var createResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{sourceFeature.Id}/requirements",
            new { text = "The system shall greet the user.", rationale });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<SaveRequirementMoveResponse>(createResponse);
        var requirementId = Guid.Parse(created.Requirement.RequirementId);

        var moveResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { targetFeatureId = targetFeature.Id });

        Assert.Equal(HttpStatusCode.OK, moveResponse.StatusCode);
        var moved = await ReadAsync<SaveRequirementMoveResponse>(moveResponse);
        Assert.Equal(targetFeature.Id.ToString(), moved.Requirement.FeatureId);

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.Requirements
                .Where(r => r.RequirementId == requirementId)
                .OrderBy(r => r.ChangedAt)
                .ThenBy(r => r.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);
            var original = history[0];
            var movedRow = history[1];

            Assert.Equal(sourceFeature.Id, original.FeatureId);
            Assert.Equal(targetFeature.Id, movedRow.FeatureId);
            // Same-project move: ProjectId and other fields preserved on the new version row.
            Assert.Equal(original.ProjectId, movedRow.ProjectId);
            Assert.Equal(original.SequenceNumber, movedRow.SequenceNumber);
            Assert.Equal(rationale, movedRow.Rationale);
            Assert.Equal(RequirementChange.Updated, movedRow.ChangeType);
        });

        // Version history diff shows the feature change with human-readable names.
        var versionsResponse = await lead.Client.GetAsync($"/api/v1/requirements/{requirementId}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await ReadAsync<List<RequirementVersionEntryMoveDetail>>(versionsResponse);
        var latest = versions.MaxBy(v => v.VersionNumber);
        Assert.NotNull(latest?.Changes?.Feature);
        Assert.Equal("Source feature", latest.Changes.Feature.Before);
        Assert.Equal("Target feature", latest.Changes.Feature.After);
    }

    [Fact]
    public async Task MoveRequirement_ToFeatureInDifferentProject_ReturnsBadRequest()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var sourceProject = await TestApi.CreateProjectAsync(lead, team.Id, "Source project");
        var otherProject = await TestApi.CreateProjectAsync(lead, team.Id, "Other project");
        var sourceFeature = await TestApi.CreateFeatureAsync(lead, sourceProject.Id);
        var otherFeature = await TestApi.CreateFeatureAsync(lead, otherProject.Id);

        var created = await TestApi.CreateRequirementAsync(lead, sourceFeature.Id, "The system shall do a thing.");
        var requirementId = created.Requirement.RequirementId;

        var moveResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { targetFeatureId = otherFeature.Id });

        Assert.Equal(HttpStatusCode.BadRequest, moveResponse.StatusCode);
    }

    [Fact]
    public async Task MoveRequirement_ToFeatureInAnotherTenant_ReturnsNotFound()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var sourceFeature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var created = await TestApi.CreateRequirementAsync(lead, sourceFeature.Id, "The system shall stay put.");
        var requirementId = created.Requirement.RequirementId;

        // A separate tenant's feature must not be a valid move target. RLS hides it
        // from this tenant's connection, so endpoint authorization returns 404 — no
        // cross-tenant existence leak.
        var outsider = await TestApi.SignInAsync(fixture);
        var outsiderTeam = await TestApi.CreateTeamAsync(outsider);
        var outsiderProject = await TestApi.CreateProjectAsync(outsider, outsiderTeam.Id);
        var outsiderFeature = await TestApi.CreateFeatureAsync(outsider, outsiderProject.Id);

        var moveResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { targetFeatureId = outsiderFeature.Id });

        Assert.Equal(HttpStatusCode.NotFound, moveResponse.StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }
}

public sealed record RequirementMoveDetailResponse(string RequirementId, string FeatureId);

public sealed record SaveRequirementMoveResponse(RequirementMoveDetailResponse Requirement);

public sealed record SimpleFieldDiffDetail(string Before, string After);

public sealed record RequirementVersionChangesMoveDetail(SimpleFieldDiffDetail? Feature);

public sealed record RequirementVersionEntryMoveDetail(
    int VersionNumber,
    RequirementVersionChangesMoveDetail? Changes);
