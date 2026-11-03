using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class RequirementRationaleIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // ── Create with rationale ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateRequirement_WithRationale_PersistsRationaleAndReturnsItOnResponse()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        const string rationale = "This matters because it enables faster user onboarding.";
        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall support single-click onboarding.",
                rationale
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync<SaveRequirementDetailResponse>(response);
        Assert.Equal(rationale, body.Requirement.Rationale);

        await fixture.WithDbContextAsync(async db =>
        {
            var row = await db.Requirements
                .SingleAsync(r => r.RequirementId == Guid.Parse(body.Requirement.RequirementId));
            Assert.Equal(rationale, row.Rationale);
            Assert.Null(row.Reason);
            Assert.Equal(RequirementChange.Created, row.ChangeType);
        });
    }

    [Fact]
    public async Task CreateRequirement_WithoutRationale_PersistsNullRationale()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new { text = "The system shall validate inputs." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync<SaveRequirementDetailResponse>(response);
        Assert.Null(body.Requirement.Rationale);

        await fixture.WithDbContextAsync(async db =>
        {
            var row = await db.Requirements
                .SingleAsync(r => r.RequirementId == Guid.Parse(body.Requirement.RequirementId));
            Assert.Null(row.Rationale);
        });
    }

    // ── Update rationale ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRationale_CreatesNewVersionRow_AndDiffShowsRationaleChange()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var created = await TestApi.CreateRequirementAsync(lead, feature.Id, "The system shall export data.");
        Guid requirementId = created.Requirement.RequirementId;

        const string newRationale = "Needed for compliance reporting.";
        var updateResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { rationale = newRationale });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadAsync<SaveRequirementDetailResponse>(updateResponse);
        Assert.Equal(newRationale, updated.Requirement.Rationale);

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.Requirements
                .Where(r => r.RequirementId == requirementId)
                .OrderBy(r => r.ChangedAt)
                .ThenBy(r => r.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);
            var original = history[0];
            var updatedRow = history[1];

            Assert.Equal(RequirementChange.Created, original.ChangeType);
            Assert.Null(original.Rationale);

            Assert.Equal(RequirementChange.Updated, updatedRow.ChangeType);
            Assert.Equal(newRationale, updatedRow.Rationale);
        });

        // Version history should show the rationale change in the diff.
        var versionsResponse = await lead.Client.GetAsync($"/api/v1/requirements/{requirementId}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await ReadAsync<List<RequirementVersionEntryDetail>>(versionsResponse);
        var latestVersion = versions.MaxBy(v => v.VersionNumber);
        Assert.NotNull(latestVersion?.Changes?.Rationale);
        // Before is string.Empty because the previous version had no rationale (ForNullableText maps null → "").
        Assert.Equal(string.Empty, latestVersion.Changes.Rationale.Before);
        Assert.Equal(newRationale, latestVersion.Changes.Rationale.After);
    }

    [Fact]
    public async Task UpdateRationale_ComposerFullPayload_ResendingAllUnchangedFields_StillDiffsRationale()
    {
        // Reproduces the live composer's edit PUT: "Edit always sends every field" — text, status,
        // priority, labels, acceptanceCriteria, rationale, reason (null), attachmentIds — with ONLY
        // the rationale actually changed. Guards against a regression where re-sending every field
        // (and thus setting every *WasProvided flag) could suppress the rationale diff.
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // Create v1 with a full field set including an initial rationale.
        const string originalRationale = "Because the original reason still applies.";
        const string text = "The system shall export data.";
        string[] labels = ["reporting"];
        string[] acceptanceCriteria = ["Given data, when exported, then a CSV is produced."];

        var createResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text,
                status = "Active",
                priority = "Should",
                labels,
                acceptanceCriteria,
                rationale = originalRationale
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<SaveRequirementDetailResponse>(createResponse);
        var requirementId = Guid.Parse(created.Requirement.RequirementId);

        // Edit exactly as the composer does: EVERY field resent, only "Because" (rationale) changed.
        const string newRationale = "Because compliance reporting now requires it.";
        var updateResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}/requirements/{requirementId}",
            new
            {
                text,
                status = "Active",
                priority = "Should",
                labels,
                acceptanceCriteria,
                rationale = newRationale,
                reason = (string?)null,
                attachmentIds = Array.Empty<string>()
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var versionsResponse = await lead.Client.GetAsync($"/api/v1/requirements/{requirementId}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await ReadAsync<List<RequirementVersionEntryDetail>>(versionsResponse);
        var latestVersion = versions.MaxBy(v => v.VersionNumber);

        Assert.NotNull(latestVersion?.Changes?.Rationale);
        Assert.Equal(originalRationale, latestVersion!.Changes!.Rationale!.Before);
        Assert.Equal(newRationale, latestVersion.Changes.Rationale.After);
    }

    // ── Independence: Reason and Rationale don't affect each other ───────────

    [Fact]
    public async Task UpdatingReason_DoesNotChangeRationale_AndViceVersa()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // Create with rationale.
        const string originalRationale = "This is the why for this requirement.";
        var createResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall handle edge cases.",
                rationale = originalRationale
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<SaveRequirementDetailResponse>(createResponse);
        var requirementId = Guid.Parse(created.Requirement.RequirementId);

        // Update Text (a real change) plus Reason — Rationale must be preserved unchanged.
        // Reason-only updates (no other field change) are rejected — see
        // UpdateRequirement_ReasonOnly_IsRejected — so this pairs the reason with a real edit.
        var updateReasonResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { text = "The system shall handle edge cases robustly.", reason = "Fixed a typo." });
        Assert.Equal(HttpStatusCode.OK, updateReasonResponse.StatusCode);
        var afterReasonUpdate = await ReadAsync<SaveRequirementDetailResponse>(updateReasonResponse);
        Assert.Equal(originalRationale, afterReasonUpdate.Requirement.Rationale);
        Assert.Equal("Fixed a typo.", afterReasonUpdate.Requirement.Reason);

        // Update only Rationale — Reason must not be leaked to the new version.
        const string updatedRationale = "Actually, it matters because of regulatory risk.";
        var updateRationaleResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { rationale = updatedRationale });
        Assert.Equal(HttpStatusCode.OK, updateRationaleResponse.StatusCode);
        var afterRationaleUpdate = await ReadAsync<SaveRequirementDetailResponse>(updateRationaleResponse);
        Assert.Equal(updatedRationale, afterRationaleUpdate.Requirement.Rationale);
        // Reason is a per-version change comment: a new version that doesn't supply one must not
        // inherit the previous version's reason — it resets to null.
        Assert.Null(afterRationaleUpdate.Requirement.Reason);
    }

    [Fact]
    public async Task UpdateRequirement_ReasonOnly_IsRejected()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var created = await TestApi.CreateRequirementAsync(lead, feature.Id, "The system shall export data.");
        Guid requirementId = created.Requirement.RequirementId;

        var updateResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { reason = "Left a note only." });
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var problem = await updateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(ProblemTypes.ReasonRequiresChange, problem!.Type);

        var versionsResponse = await lead.Client.GetAsync($"/api/v1/requirements/{requirementId}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await ReadAsync<List<RequirementVersionEntryDetail>>(versionsResponse);

        Assert.Single(versions);
    }

    [Fact]
    public async Task UpdateRequirement_ReasonWithRealChange_Succeeds()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var created = await TestApi.CreateRequirementAsync(lead, feature.Id, "The system shall export data.");
        Guid requirementId = created.Requirement.RequirementId;

        var updateResponse = await lead.Client.PutAsJsonAsync(
            $"/api/v1/requirements/{requirementId}",
            new { text = "The system shall export data as CSV.", reason = "Clarified format." });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var versionsResponse = await lead.Client.GetAsync($"/api/v1/requirements/{requirementId}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await ReadAsync<List<RequirementVersionEntryDetail>>(versionsResponse);

        var latestVersion = versions.MaxBy(v => v.VersionNumber);
        Assert.NotNull(latestVersion);
        Assert.Equal("Clarified format.", latestVersion!.Reason);
        Assert.NotNull(latestVersion.Changes);
    }

    // ── Cascade/delete path copies Rationale ─────────────────────────────────

    [Fact]
    public async Task DeleteRequirement_WithRationale_CopiesRationaleToDeletedVersionRow()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        const string rationale = "This intent is critical for SOC-2 audit.";
        var createResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall retain audit logs for 12 months.",
                rationale
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<SaveRequirementDetailResponse>(createResponse);
        var requirementId = Guid.Parse(created.Requirement.RequirementId);

        var deleteResponse = await lead.Client.DeleteAsync(
            $"/api/v1/requirements/{requirementId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.Requirements
                .Where(r => r.RequirementId == requirementId)
                .OrderBy(r => r.ChangedAt)
                .ThenBy(r => r.Id)
                .ToListAsync();

            Assert.Equal(2, history.Count);
            var deletedRow = history.Single(r => r.ChangeType == RequirementChange.Deleted);
            Assert.Equal(rationale, deletedRow.Rationale);
        });
    }

    [Fact]
    public async Task DeleteFeature_CascadesRationaleToDeletedRequirementVersionRow()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        const string rationale = "Critical for the data-retention guarantee.";
        var createResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall purge stale records nightly.",
                rationale
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<SaveRequirementDetailResponse>(createResponse);
        var requirementId = Guid.Parse(created.Requirement.RequirementId);

        // Deleting the parent feature cascades a Deleted version row onto each
        // requirement via FeatureStore — that row must copy Rationale (versioning
        // invariant: all fields copied into the new version row).
        var deleteResponse = await lead.Client.DeleteAsync(
            $"/api/v1/projects/{project.Id}/features/{feature.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await fixture.WithDbContextAsync(async db =>
        {
            var history = await db.Requirements
                .Where(r => r.RequirementId == requirementId)
                .OrderBy(r => r.ChangedAt)
                .ThenBy(r => r.Id)
                .ToListAsync();

            var deletedRow = history.Single(r => r.ChangeType == RequirementChange.Deleted);
            Assert.Equal(rationale, deletedRow.Rationale);
        });
    }

    // ── Browse/list path returns Rationale (not just get-by-id) ──────────────

    [Fact]
    public async Task ListProjectRequirements_WithRationale_ReturnsRationaleOnBrowseItem()
    {
        // Regression test for a bug where RequirementStore.GetCurrentByProjectAsync (the
        // list/browse query) always returned Rationale = null, even though the single-get
        // path returned it correctly. This asserts Rationale on the BROWSE item itself.
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        const string rationale = "This is distinctive and must survive the browse path too.";
        var createResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall notify on export completion.",
                rationale
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<SaveRequirementDetailResponse>(createResponse);
        var requirementId = Guid.Parse(created.Requirement.RequirementId);

        var browseResponse = await lead.Client.GetAsync($"/api/v1/projects/{project.Id}/requirements");
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);
        var browsePage = await ReadAsync<PagedRequirementDetailResponse>(browseResponse);
        var browseItem = browsePage.Items.Single(item => item.RequirementId == requirementId.ToString());

        Assert.Equal(rationale, browseItem.Rationale);
        Assert.Equal(created.Requirement.Rationale, browseItem.Rationale);
    }

    // ── Search finds intent by rationale text ────────────────────────────────

    [Fact]
    public async Task SearchRequirements_WithRationaleText_FindsMatchingRequirement()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        const string uniqueRationaleToken = "xqzuniquerationale9182";
        var createResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "The system shall support exporting to PDF.",
                rationale = $"Needed because of {uniqueRationaleToken} compliance obligations."
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var searchResults = await TestApi.SearchRequirementsAsync(lead, project.Id, uniqueRationaleToken);

        Assert.Single(searchResults);
        Assert.Equal("requirement", searchResults[0].Type);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }
}

public sealed record RequirementDetailResponse(
    string Id,
    string RequirementId,
    string FeatureId,
    int SequenceNumber,
    string Ref,
    string Text,
    string? Rationale,
    string? Reason);

public sealed record SaveRequirementDetailResponse(RequirementDetailResponse Requirement);

public sealed record PagedRequirementDetailResponse(IReadOnlyList<RequirementDetailResponse> Items);

public sealed record RequirementVersionChangesDetail(
    RationaleTextDiff? Rationale);

public sealed record RationaleTextDiff(string? Before, string? After);

public sealed record RequirementVersionEntryDetail(
    string VersionId,
    int VersionNumber,
    RequirementVersionChangesDetail? Changes,
    string? Reason);
