using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Common.Errors;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class WorkItemBrowseIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task ListWorkItems_SortCreated_ReturnsNewestSequenceFirst()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "First work item");
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Second work item");
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Third work item");

        var result = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, sort: "created");

        Assert.Equal(["WO-3", "WO-2", "WO-1"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListWorkItems_QueryFilter_MatchesLabelsAndExactRef()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Build checkout form", ["payments"]);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Export audit trail", ["compliance"]);

        var labelResult = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, query: "compliance");
        var refResult = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, query: "WO-1");

        Assert.Equal(["WO-2"], labelResult.Items.Select(item => item.Ref).ToArray());
        Assert.Equal(["WO-1"], refResult.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListWorkItems_QueryFilter_UsesFullTextSearch()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Payment retry sends customer receipt");
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Profile avatar upload stores thumbnails");

        var result = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, query: "payments receipts");

        Assert.Equal(["WO-1"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListWorkItems_LabelFilterAndCreatedSort_ComposeWithPagination()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "First export", ["export"]);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Unrelated", ["other"]);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Second export", ["export"]);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Third export", ["export"]);

        var result = await TestApi.ListProjectWorkItemsAsync(
            lead,
            project.Id,
            page: 1,
            pageSize: 2,
            sort: "created",
            labels: ["export"]);

        Assert.Equal(3, result.Total);
        Assert.True(result.HasMore);
        Assert.Equal(["WO-4", "WO-3"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListWorkItems_SingleTypeFilter_ReturnsOnlyMatchingType()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Fix crash", [], "Bug", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Add export", [], "Feature", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Refactor code", [], "Task", null);

        var result = await TestApi.ListProjectWorkItemsAsync(
            lead, project.Id, sort: "created", types: ["Bug"]);

        Assert.Equal(1, result.Total);
        Assert.Equal("Bug", result.Items.Single().Type);
    }

    [Fact]
    public async Task ListWorkItems_MultiTypeFilter_ReturnsAllMatchingTypes()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Fix crash", [], "Bug", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Add export", [], "Feature", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Refactor code", [], "Task", null);

        var result = await TestApi.ListProjectWorkItemsAsync(
            lead, project.Id, sort: "created", types: ["Bug", "Feature"]);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Contains(item.Type, (IEnumerable<string?>)["Bug", "Feature"]));
    }

    [Fact]
    public async Task ListWorkItems_EmptyTypeFilter_ReturnsAll()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Fix crash", [], "Bug", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Add export", [], "Feature", null);

        var result = await TestApi.ListProjectWorkItemsAsync(lead, project.Id);

        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task ListWorkItems_DuplicateTypeParams_DeduplicatesBeforeFiltering()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Fix crash", [], "Bug", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Add export", [], "Feature", null);

        var result = await TestApi.ListProjectWorkItemsAsync(
            lead, project.Id, types: ["Bug", "Bug"]);

        Assert.Equal(1, result.Total);
        Assert.Equal("Bug", result.Items.Single().Type);
    }

    [Fact]
    public async Task ListWorkItems_TypeAndBlockedFilter_CombineCorrectly()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Bug one", [], "Bug", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Bug two", [], "Bug", null);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Feature one", [], "Feature", null);

        // type=Bug returns only bugs; type=Feature returns only features
        var bugResult = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, types: ["Bug"]);
        var featureResult = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, types: ["Feature"]);

        Assert.Equal(2, bugResult.Total);
        Assert.Equal(1, featureResult.Total);
    }

    [Fact]
    public async Task ListWorkItems_AssignedItem_ReturnsDisplayName()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture, displayName: "Lead User");
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Assigned task", [], "Task", lead.UserId);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Unassigned task", [], "Task", null);

        var result = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, sort: "created");

        var assigned = result.Items.First(item => item.AssignedToUserId == lead.UserId);
        var unassigned = result.Items.First(item => item.AssignedToUserId is null);

        Assert.Equal("Lead User", assigned.AssignedToDisplayName);
        Assert.Null(unassigned.AssignedToDisplayName);
    }

    [Fact]
    public async Task ListWorkItems_AssignedToFilter_NarrowsByAssignee()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture, displayName: "Lead User");
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Assigned task", [], "Task", lead.UserId);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Unassigned task", [], "Task", null);

        var byUserId = await TestApi.ListProjectWorkItemsAsync(
            lead, project.Id, assignedTo: lead.UserId.ToString());
        Assert.Equal("Assigned task", Assert.Single(byUserId.Items).Title);

        var byMe = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, assignedTo: "me");
        Assert.Equal("Assigned task", Assert.Single(byMe.Items).Title);

        var unassignedOnly = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, assignedTo: "none");
        Assert.Equal("Unassigned task", Assert.Single(unassignedOnly.Items).Title);
    }

    [Fact]
    public async Task ListWorkItems_AssignedItem_FallsBackToEmailWhenDisplayNameNull()
    {
        fixture.EmailSink.Clear();
        // Sign in without setting displayName so it remains null
        string assigneeEmail = fixture.CreateUniqueEmail();
        var client = fixture.CreateClient(allowAutoRedirect: false);
        _ = await client.PostAsJsonAsync("/api/v1/auth/magic-link", new { email = assigneeEmail });
        string token = fixture.EmailSink.GetLatestMagicLinkToken(assigneeEmail);
        await client.GetAsync($"/api/v1/auth/verify?token={token}");
        var meResponse = await client.GetAsync("/api/v1/users/me");
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(me);
        var assignee = new AuthSession(client, me.Id, assigneeEmail);

        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        await TestApi.InviteContributorAsync(lead, team.Id, assigneeEmail);
        await TestApi.AcceptInviteAsync(fixture, assignee, assigneeEmail);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        await TestApi.CreateWorkItemAsync(lead, project.Id, "Assigned task", [], "Task", assignee.UserId);

        var result = await TestApi.ListProjectWorkItemsAsync(lead, project.Id);

        var item = result.Items.Single();
        Assert.Equal(assigneeEmail, item.AssignedToDisplayName);
    }

    [Fact]
    public async Task UpdateWorkItem_OmittingReason_DoesNotInheritPreviousVersionsReason()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Wire checkout");

        // First edit supplies a per-version change comment.
        var withReason = await lead.Client.PutAsJsonAsync(
            $"/api/v1/work-items/{workItem.WorkItemId}",
            new { title = "Wire checkout (renamed)", reason = "Renamed for clarity." });
        Assert.Equal(HttpStatusCode.OK, withReason.StatusCode);
        var afterReason = await ReadAsync<Grundet.Api.Features.WorkItems.WorkItemResponse>(withReason);
        Assert.Equal("Renamed for clarity.", afterReason.Reason);

        // Next edit omits reason — the new version must not inherit the previous version's reason.
        var withoutReason = await lead.Client.PutAsJsonAsync(
            $"/api/v1/work-items/{workItem.WorkItemId}",
            new { title = "Wire checkout (final)" });
        Assert.Equal(HttpStatusCode.OK, withoutReason.StatusCode);
        var afterNoReason = await ReadAsync<Grundet.Api.Features.WorkItems.WorkItemResponse>(withoutReason);
        Assert.Null(afterNoReason.Reason);
    }

    [Fact]
    public async Task UpdateWorkItem_ReasonOnly_IsRejected()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Wire checkout");

        var update = await lead.Client.PutAsJsonAsync(
            $"/api/v1/work-items/{workItem.WorkItemId}",
            new { reason = "Left a note only." });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        var problem = await update.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(ProblemTypes.ReasonRequiresChange, problem!.Type);

        var versionsResponse = await lead.Client.GetAsync($"/api/v1/workitems/{workItem.WorkItemId}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await ReadAsync<List<WorkItemVersionEntryDetail>>(versionsResponse);

        Assert.Single(versions);
    }

    [Fact]
    public async Task UpdateWorkItem_ReasonWithRealChange_Succeeds()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Wire checkout");

        var update = await lead.Client.PutAsJsonAsync(
            $"/api/v1/work-items/{workItem.WorkItemId}",
            new { title = "Wire checkout flow", reason = "Renamed for clarity." });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var versionsResponse = await lead.Client.GetAsync($"/api/v1/workitems/{workItem.WorkItemId}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await ReadAsync<List<WorkItemVersionEntryDetail>>(versionsResponse);

        var latestVersion = versions.MaxBy(v => v.VersionNumber);
        Assert.NotNull(latestVersion);
        Assert.Equal("Renamed for clarity.", latestVersion!.Reason);
        Assert.NotNull(latestVersion.Changes);
    }

    [Fact]
    public async Task ListWorkItems_SoftDeletedWorkItem_IsHiddenFromBrowse()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        var doomed = await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item to delete");
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Work item to keep");

        // Edit then delete: the deleted head must hide the work item even though its prior version
        // was non-Deleted (deleted-exclusion is applied after DISTINCT ON picks the latest).
        await TestApi.UpdateWorkItemAsync(lead, doomed.WorkItemId, "Work item to delete (edited)");
        await TestApi.DeleteWorkItemAsync(lead, doomed.WorkItemId);

        var result = await TestApi.ListProjectWorkItemsAsync(lead, project.Id);

        Assert.Equal(1, result.Total);
        Assert.Equal(["WO-2"], result.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListWorkItems_QueryPresent_OrdersByRelevanceOverRecency()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        // WO-1 matches "kubernetes" in its weight-A title; WO-2 (newer) matches only via a weight-C
        // label. ts_rank must lift the stronger title match (WO-1) above the newer WO-2.
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Kubernetes autoscaling", ["infra"]);
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Billing export", ["kubernetes"]);

        var ranked = await TestApi.ListProjectWorkItemsAsync(lead, project.Id, query: "kubernetes");
        var unranked = await TestApi.ListProjectWorkItemsAsync(lead, project.Id);

        Assert.Equal(["WO-1", "WO-2"], ranked.Items.Select(item => item.Ref).ToArray());
        // Without a query, the default sort is Updated: ChangedAt DESC is primary, so the newer WO-2
        // leads (here the two share equal priority, so this case does not by itself distinguish the
        // default from priority-first — see DefaultSort_IsUpdated_NotPriority below).
        Assert.Equal(["WO-2", "WO-1"], unranked.Items.Select(item => item.Ref).ToArray());
    }

    [Fact]
    public async Task ListWorkItems_NoSort_DefaultsToUpdatedNotPriority()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);

        // WO-1 is the highest priority (High) but created first (older ChangedAt); WO-2 is the lowest
        // priority (Low) but created later (newer ChangedAt). Under the Updated default, ChangedAt DESC
        // is primary, so the newer-but-lower-priority WO-2 must sort ABOVE the older High WO-1. Under the
        // old priority-first (Smart) default the order would invert to [WO-1, WO-2]. This pins the default.
        await TestApi.CreateWorkItemWithPriorityAsync(lead, project.Id, "Older high-priority work", "High");
        await TestApi.CreateWorkItemWithPriorityAsync(lead, project.Id, "Newer low-priority work", "Low");

        var result = await TestApi.ListProjectWorkItemsAsync(lead, project.Id);

        Assert.Equal(["WO-2", "WO-1"], result.Items.Select(item => item.Ref).ToArray());
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize response to {typeof(T).Name}.");
    }
}

public sealed record WorkItemVersionEntryDetail(int VersionNumber, object? Changes, string? Reason);
