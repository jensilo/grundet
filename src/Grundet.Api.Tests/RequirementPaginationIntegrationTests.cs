using System.Net;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class RequirementPaginationIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task ListRequirements_DefaultParams_ReturnsPagedEnvelope()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement A");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement B");

        var result = await TestApi.ListRequirementsAsync(lead, feature.Id);

        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ListRequirements_SecondPage_ReturnsCorrectItemsAndFullTotal()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 1");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 2");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 3");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 4");

        var firstPage = await TestApi.ListRequirementsAsync(lead, feature.Id, page: 1, pageSize: 2);
        var secondPage = await TestApi.ListRequirementsAsync(lead, feature.Id, page: 2, pageSize: 2);

        // First page
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(4, firstPage.Total);
        Assert.Equal(2, firstPage.Items.Count);

        // Second page returns items 3+ and the total reflects the full unpaged count
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(2, secondPage.PageSize);
        Assert.Equal(4, secondPage.Total);
        Assert.Equal(2, secondPage.Items.Count);

        // Items on the two pages are distinct
        var firstPageIds = firstPage.Items.Select(r => r.RequirementId).ToHashSet();
        var secondPageIds = secondPage.Items.Select(r => r.RequirementId).ToHashSet();
        Assert.Empty(firstPageIds.Intersect(secondPageIds));
    }

    [Fact]
    public async Task ListRequirements_PageBeyondLastPage_ReturnsEmptyItemsWithCorrectTotal()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Only Requirement");

        var result = await TestApi.ListRequirementsAsync(lead, feature.Id, page: 99, pageSize: 10);

        Assert.Equal(99, result.Page);
        Assert.Equal(1, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListRequirements_PageLessThanOne_Returns400()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await TestApi.ListRequirementsRawAsync(lead, feature.Id, "?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRequirements_NegativePage_Returns400()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await TestApi.ListRequirementsRawAsync(lead, feature.Id, "?page=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRequirements_PageSizeZero_Returns400()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await TestApi.ListRequirementsRawAsync(lead, feature.Id, "?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRequirements_NegativePageSize_Returns400()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await TestApi.ListRequirementsRawAsync(lead, feature.Id, "?pageSize=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRequirements_PageSizeExceedsMax_Returns400()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await TestApi.ListRequirementsRawAsync(lead, feature.Id, "?pageSize=201");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRequirements_NonIntegerPage_Returns400()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var response = await TestApi.ListRequirementsRawAsync(lead, feature.Id, "?page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRequirements_MaxPageSize_IsAccepted()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var result = await TestApi.ListRequirementsAsync(lead, feature.Id, page: 1, pageSize: 200);

        Assert.Equal(200, result.PageSize);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task ListRequirements_WithLabelFilter_TotalReflectsFilteredCount()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // 2 requirements with the "alpha" label, 2 without it.
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Alpha One", ["alpha"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Alpha Two", ["alpha"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Beta One", ["beta"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Beta Two", ["beta"]);

        // Page 1, pageSize 1 — total must reflect the label-filtered count (2), not the full count (4).
        var result = await TestApi.ListRequirementsAsync(
            lead, feature.Id, page: 1, pageSize: 1, labels: ["alpha"]);

        Assert.Equal(2, result.Total);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ListRequirements_WithLabelFilter_SecondPageReturnsRemainingItem()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Alpha One", ["alpha"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Alpha Two", ["alpha"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Beta One", ["beta"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Beta Two", ["beta"]);

        // Page 2, pageSize 1 — the second alpha-labelled item, total still 2.
        var result = await TestApi.ListRequirementsAsync(
            lead, feature.Id, page: 2, pageSize: 1, labels: ["alpha"]);

        Assert.Equal(2, result.Total);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ListRequirements_Paging_IsStableAcrossConsecutivePages()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 1");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 2");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 3");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement 4");

        var pageOne = await TestApi.ListRequirementsAsync(lead, feature.Id, page: 1, pageSize: 2);
        var pageTwo = await TestApi.ListRequirementsAsync(lead, feature.Id, page: 2, pageSize: 2);
        var full = await TestApi.ListRequirementsAsync(lead, feature.Id, page: 1, pageSize: 4);

        var combined = pageOne.Items
            .Concat(pageTwo.Items)
            .Select(item => item.RequirementId)
            .ToList();
        var fullIds = full.Items.Select(item => item.RequirementId).ToList();

        Assert.Equal(fullIds, combined);
    }

    [Fact]
    public async Task ListRequirements_RedundantFeatureIdQueryParam_Ignored()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var featureA = await TestApi.CreateFeatureAsync(lead, project.Id);
        var featureB = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, featureA.Id, "Feature A requirement");
        await TestApi.CreateRequirementAsync(lead, featureB.Id, "Feature B requirement");

        // ?featureId= is no longer a bound parameter on this endpoint.
        // The route {featureId} is authoritative; the query param is silently ignored.
        var result = await TestApi.ListRequirementsWithQueryStringAsync(
            lead,
            featureA.Id,
            $"?featureId={featureB.Id}");

        Assert.Contains(result.Items, i => i.Ref == "IN-1");
        Assert.DoesNotContain(result.Items, i => i.Ref == "IN-2");
    }

    [Fact]
    public async Task ListRequirements_ForeignTenantFeatureIdQueryParam_Ignored()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture, fixture.CreateUniqueEmail());

        var ownerTeam = await TestApi.CreateTeamAsync(lead);
        var ownerProject = await TestApi.CreateProjectAsync(lead, ownerTeam.Id);
        var ownerFeature = await TestApi.CreateFeatureAsync(lead, ownerProject.Id);

        await TestApi.CreateRequirementAsync(lead, ownerFeature.Id, "Owner requirement");

        var outsiderTeam = await TestApi.CreateTeamAsync(outsider);
        var outsiderProject = await TestApi.CreateProjectAsync(outsider, outsiderTeam.Id);
        var outsiderFeature = await TestApi.CreateFeatureAsync(outsider, outsiderProject.Id);

        // ?featureId= is no longer a bound parameter.
        // The route featureId is authorized separately, so a foreign-tenant ?featureId=
        // is simply ignored — it doesn't change which feature's data is returned.
        var result = await TestApi.ListRequirementsWithQueryStringAsync(
            outsider,
            outsiderFeature.Id,
            $"?featureId={ownerFeature.Id}");

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListProjectRequirements_NoFeatureFilter_ReturnsRequirementsAcrossProjectFeatures()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var featureA = await TestApi.CreateFeatureAsync(lead, project.Id);
        var featureB = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, featureA.Id, "Feature A requirement");
        await TestApi.CreateRequirementAsync(lead, featureB.Id, "Feature B requirement");

        var result = await TestApi.ListProjectRequirementsAsync(lead, project.Id);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ListProjectRequirements_FeatureFilter_ReturnsOnlyThatFeature()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var featureA = await TestApi.CreateFeatureAsync(lead, project.Id);
        var featureB = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, featureA.Id, "Feature A requirement");
        await TestApi.CreateRequirementAsync(lead, featureB.Id, "Feature B requirement");

        var result = await TestApi.ListProjectRequirementsAsync(lead, project.Id, featureId: featureB.Id);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("IN-2", result.Items[0].Ref);
    }

    [Fact]
    public async Task ListProjectRequirements_QueryFilter_ReturnsMatchingRequirements()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Privacy review must pass before rollout");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Migration can run after launch");

        var result = await TestApi.ListProjectRequirementsAsync(lead, project.Id, query: "privacy");

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("IN-1", result.Items[0].Ref);
    }

    [Fact]
    public async Task ListProjectRequirements_SortCreated_ReturnsNewestSequenceFirst()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "First requirement");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Second requirement");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Third requirement");

        var result = await TestApi.ListProjectRequirementsAsync(lead, project.Id, sort: "created");

        Assert.Equal(["IN-3", "IN-2", "IN-1"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListProjectRequirements_QueryFilter_MatchesLabelsAndExactRef()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Checkout accepts card payments", ["payments"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Audit log exports are retained", ["compliance"]);

        var labelResult = await TestApi.ListProjectRequirementsAsync(lead, project.Id, query: "compliance");
        var refResult = await TestApi.ListProjectRequirementsAsync(lead, project.Id, query: "IN-1");

        Assert.Equal(["IN-2"], labelResult.Items.Select(item => item.Ref).ToArray());
        Assert.Equal(["IN-1"], refResult.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListProjectRequirements_QueryFilter_UsesFullTextSearch()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Payment retry sends customer receipt");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Profile avatar upload stores thumbnails");

        var result = await TestApi.ListProjectRequirementsAsync(lead, project.Id, query: "payments receipts");

        Assert.Equal(["IN-1"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListProjectRequirements_LabelFilterAndCreatedSort_ComposeWithPagination()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "First export", ["export"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Unrelated", ["other"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Second export", ["export"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Third export", ["export"]);

        var result = await TestApi.ListProjectRequirementsAsync(
            lead,
            project.Id,
            page: 1,
            pageSize: 2,
            sort: "created",
            labels: ["export"]);

        Assert.Equal(3, result.Total);
        Assert.True(result.HasMore);
        Assert.Equal(["IN-4", "IN-3"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListProjectRequirements_SoftDeletedRequirement_IsHiddenFromBrowse()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        var doomed = await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement to delete");
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Requirement to keep");

        // Edit the doomed requirement first so its latest version before deletion is non-Deleted;
        // the deleted head must still hide it (the deleted-exclusion is applied after DISTINCT ON).
        await TestApi.UpdateRequirementAsync(lead, doomed.Requirement.RequirementId, "Requirement to delete (edited)");
        await TestApi.DeleteRequirementAsync(lead, doomed.Requirement.RequirementId);

        var result = await TestApi.ListProjectRequirementsAsync(lead, project.Id);

        Assert.Equal(1, result.Total);
        Assert.Equal(["IN-2"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListProjectRequirements_QueryPresent_OrdersByRelevanceOverRecency()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // IN-1 matches "kubernetes" in its weight-A text; IN-2 (created later, so newer) matches
        // only via a weight-C label. Without rank ordering the newer IN-2 would sort first; with
        // ts_rank the stronger text match (IN-1) must lead despite being older.
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Kubernetes autoscaling policy", ["infra"]);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Billing invoice export", ["kubernetes"]);

        var ranked = await TestApi.ListProjectRequirementsAsync(lead, project.Id, query: "kubernetes");
        var unranked = await TestApi.ListProjectRequirementsAsync(lead, project.Id);

        Assert.Equal(["IN-1", "IN-2"], ranked.Items.Select(item => item.Ref).ToArray());
        // Without a query, the default sort is Updated: ChangedAt DESC is primary, so the newer IN-2
        // leads (here the two share equal priority, so this case does not by itself distinguish the
        // default from priority-first — see DefaultSort_IsUpdated_NotPriority below).
        Assert.Equal(["IN-2", "IN-1"], unranked.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListProjectRequirements_NoSort_DefaultsToUpdatedNotPriority()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);

        // IN-1 is the highest priority (Must) but created first (older ChangedAt); IN-2 is the lowest
        // priority (Could) but created later (newer ChangedAt). Under the Updated default, ChangedAt DESC
        // is primary, so the newer-but-lower-priority IN-2 must sort ABOVE the older Must IN-1. Under the
        // old priority-first (Smart) default the order would invert to [IN-1, IN-2]. This pins the default.
        await TestApi.CreateRequirementWithPriorityAsync(lead, feature.Id, "Older high-priority intent", "Must");
        await TestApi.CreateRequirementWithPriorityAsync(lead, feature.Id, "Newer low-priority intent", "Could");

        var result = await TestApi.ListProjectRequirementsAsync(lead, project.Id);

        Assert.Equal(["IN-2", "IN-1"], result.Items.Select(item => item.Ref).ToArray());
    }
}
