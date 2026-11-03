using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Billing;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class BillingQuotaIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly DateTime FixedNowUtc = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] IntegrationLabels = ["integration"];
    private static readonly string[] DefaultAcceptanceCriteria = ["AC"];
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Signup_CreatesExactlyOneSoloWorkspace_WithLeadMembership()
    {
        fixture.EmailSink.Clear();
        var user = await TestApi.SignInAsync(fixture);

        var teams = await fixture.WithDbContextAsync(async db => await db.Teams
            .Where(team => db.TeamMembers.Any(member => member.TeamId == team.Id
                && member.UserId == user.UserId
                && member.Role == TeamRole.Lead))
            .Select(team => new
            {
                team.Id,
                team.Name,
                team.Plan,
                LeadMemberships = db.TeamMembers.Count(member => member.TeamId == team.Id
                    && member.UserId == user.UserId
                    && member.Role == TeamRole.Lead)
            })
            .ToListAsync());

        var soloTeam = Assert.Single(teams, team => team.Plan == TeamPlan.Solo);
        Assert.Equal("My work", soloTeam.Name);
        Assert.Equal(1, soloTeam.LeadMemberships);
    }

    [Fact]
    public async Task ManualTeamCreation_DefaultsToTeamS_NotSolo()
    {
        fixture.EmailSink.Clear();
        var user = await TestApi.SignInAsync(fixture);

        var created = await TestApi.CreateTeamAsync(user);

        var plan = await fixture.WithDbContextAsync(async db => await db.Teams
            .Where(team => team.Id == created.Id)
            .Select(team => team.Plan)
            .SingleAsync());

        Assert.Equal(TeamPlan.TeamS, plan);
    }

    [Fact]
    public async Task ManualTeamCreation_WhenAlphaWorkspaceCreationEnabled_DefaultsToTeamAlpha()
    {
        fixture.EmailSink.Clear();
        var user = await TestApi.SignInAsync(fixture, createTeamAlphaWorkspaces: true);

        var created = await TestApi.CreateTeamAsync(user);

        var plans = await fixture.WithDbContextAsync(async db => await db.Teams
            .Where(team => db.TeamMembers.Any(member => member.TeamId == team.Id
                && member.UserId == user.UserId
                && member.Role == TeamRole.Lead))
            .Select(team => new { team.Id, team.Name, team.Plan })
            .ToListAsync());

        var soloWorkspace = Assert.Single(plans, team => team.Name == "My work");
        Assert.Equal(TeamPlan.Solo, soloWorkspace.Plan);
        var createdWorkspace = Assert.Single(plans, team => team.Id == created.Id);
        Assert.Equal(TeamPlan.TeamAlpha, createdWorkspace.Plan);
    }

    [Fact]
    public async Task SoloWorkspace_RejectsInvites_WithQuotaProblemType()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        string contributorEmail = fixture.CreateUniqueEmail();
        var teams = await TestApi.ListTeamsAsync(lead);
        var solo = Assert.Single(teams, team => team.Name == "My work");

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/teams/{solo.Id}/invites",
            new { email = contributorEmail, role = "Contributor" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(ProblemTypes.QuotaExceeded, problem!.Type);
    }

    [Fact]
    public async Task ProjectCreation_AtTeamSLimit_ReturnsQuotaProblemType()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        for (int index = 0; index < 6; index++)
        {
            await TestApi.CreateProjectAsync(lead, team.Id, $"Project {index}");
        }

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/teams/{team.Id}/projects",
            new { name = "Project over quota", description = "Should fail" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(ProblemTypes.QuotaExceeded, problem!.Type);
    }

    [Fact]
    public async Task RecalculateUsageAsync_UsesTenantScopedCounts_AndApproachingLimitAtEightyPercent()
    {
        var teamId = Guid.NewGuid();
        var store = new FakeQuotaStore
        {
            Plan = TeamPlan.Solo,
            Usage = new TeamUsageCounts(2, 1, 800, 0)
        };
        var service = new TeamQuotaService(store);

        var summary = await service.RecalculateUsageAsync(AuthorizeTeamId(teamId), FixedNowUtc);

        Assert.Equal(RequirementWorkItemLimitStatus.ApproachingLimit, summary.RequirementWorkItemStatus);
        Assert.Null(summary.OverLimitSince);
        Assert.Equal(800, summary.Usage!.TotalRequirementsAndWorkItems);
        Assert.Equal(teamId, store.SavedSnapshot!.TeamId);
    }

    [Fact]
    public async Task RecalculateUsageAsync_StartsGracePeriod_WhenUsageIsOverLimit()
    {
        var teamId = Guid.NewGuid();
        var store = new FakeQuotaStore
        {
            Plan = TeamPlan.Solo,
            Usage = new TeamUsageCounts(2, 1, 1001, 0)
        };
        var service = new TeamQuotaService(store);

        var summary = await service.RecalculateUsageAsync(AuthorizeTeamId(teamId), FixedNowUtc);

        Assert.Equal(RequirementWorkItemLimitStatus.OverLimitGrace, summary.RequirementWorkItemStatus);
        Assert.Equal(FixedNowUtc, summary.OverLimitSince);
        Assert.Equal(FixedNowUtc.AddDays(14), summary.EnforcementStartsAt);
    }

    [Fact]
    public async Task RecalculateUsageAsync_ActivatesEnforcement_AfterGracePeriod()
    {
        var teamId = Guid.NewGuid();
        var overLimitSince = FixedNowUtc.AddDays(-15);
        var store = new FakeQuotaStore
        {
            Plan = TeamPlan.Solo,
            ExistingSnapshot = new TeamUsageSnapshot
            {
                TeamId = teamId,
                Plan = TeamPlan.Solo,
                ProjectCount = 1,
                MemberCount = 1,
                RequirementCount = 1001,
                WorkItemCount = 0,
                RequirementWorkItemStatus = RequirementWorkItemLimitStatus.OverLimitGrace,
                OverLimitSince = overLimitSince,
                EnforcementStartsAt = overLimitSince.AddDays(14),
                CalculatedAt = overLimitSince
            },
            Usage = new TeamUsageCounts(1, 1, 1001, 0)
        };
        var service = new TeamQuotaService(store);

        var summary = await service.RecalculateUsageAsync(AuthorizeTeamId(teamId), FixedNowUtc);

        Assert.Equal(RequirementWorkItemLimitStatus.EnforcementActive, summary.RequirementWorkItemStatus);
        Assert.Equal(overLimitSince, summary.OverLimitSince);
        Assert.Equal(overLimitSince.AddDays(14), summary.EnforcementStartsAt);
    }

    [Fact]
    public async Task RequirementAndWorkItemCreation_IsBlockedOnlyWhenCachedEnforcementIsActive()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        await UpsertUsageSnapshotAsync(team.Id, RequirementWorkItemLimitStatus.OverLimitGrace);

        await TestApi.CreateRequirementAsync(lead, feature.Id, "Allowed during grace");
        await TestApi.CreateWorkItemAsync(lead, project.Id, "Allowed during grace");
        await UpsertUsageSnapshotAsync(team.Id, RequirementWorkItemLimitStatus.EnforcementActive);

        var requirementResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/features/{feature.Id}/requirements",
            new
            {
                text = "Blocked requirement",
                status = "Draft",
                priority = "Must",
                labels = IntegrationLabels,
                acceptanceCriteria = DefaultAcceptanceCriteria,
                reason = (string?)null
            });
        var workItemResponse = await lead.Client.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/work-items",
            new
            {
                title = "Blocked work item",
                description = "Integration test work item",
                status = "Todo",
                priority = "Medium",
                labels = IntegrationLabels,
                assignedToUserId = (Guid?)null,
                blockedReason = (string?)null,
                reason = (string?)null,
                type = "Task"
            });

        Assert.Equal(HttpStatusCode.Conflict, requirementResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, workItemResponse.StatusCode);
        Assert.Equal(ProblemTypes.QuotaExceeded, (await requirementResponse.Content.ReadFromJsonAsync<ProblemDetails>())!.Type);
        Assert.Equal(ProblemTypes.QuotaExceeded, (await workItemResponse.Content.ReadFromJsonAsync<ProblemDetails>())!.Type);
    }

    [Fact]
    public async Task RequirementAndWorkItemUpdates_RemainAllowed_WhenCachedEnforcementIsActive()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var requirement = await TestApi.CreateRequirementAsync(lead, feature.Id, "Original requirement");
        var workItem = await TestApi.CreateWorkItemAsync(lead, project.Id, "Original work item");
        await UpsertUsageSnapshotAsync(team.Id, RequirementWorkItemLimitStatus.EnforcementActive);

        var updatedRequirement = await TestApi.UpdateRequirementAsync(lead, requirement.Requirement.RequirementId, "Updated requirement");
        var updatedWorkItem = await TestApi.UpdateWorkItemAsync(lead, workItem.WorkItemId, "Updated work item");

        Assert.Equal(requirement.Requirement.RequirementId, updatedRequirement.Requirement.RequirementId);
        Assert.Equal(workItem.WorkItemId, updatedWorkItem.WorkItemId);
    }

    [Fact]
    public async Task GetTeamLimits_ReturnsLatestUsageSnapshot()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        await UpsertUsageSnapshotAsync(team.Id, RequirementWorkItemLimitStatus.ApproachingLimit, requirementCount: 800);

        var httpResponse = await lead.Client.GetAsync($"/api/v1/teams/{team.Id}/limits");
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var response = await httpResponse.Content.ReadFromJsonAsync<TeamUsageSummaryResponse>(JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(TeamPlan.TeamS, response!.Plan);
        Assert.Equal(RequirementWorkItemLimitStatus.ApproachingLimit, response.RequirementWorkItemStatus);
        Assert.Equal(800, response.Usage!.Requirements);
        Assert.Equal("Usage is calculated once per day.", response.CalculationCadence);
    }

    private Task UpsertUsageSnapshotAsync(
        Guid teamId,
        RequirementWorkItemLimitStatus status,
        int requirementCount = 10_001)
        => fixture.WithDbContextAsync(async db =>
        {
            var team = await db.Teams.SingleAsync(item => item.Id == teamId);
            var snapshot = await db.TeamUsageSnapshots.SingleOrDefaultAsync(item => item.TeamId == teamId);
            if (snapshot is null)
            {
                snapshot = new TeamUsageSnapshot { TeamId = teamId };
                db.TeamUsageSnapshots.Add(snapshot);
            }

            snapshot.Plan = team.Plan;
            snapshot.ProjectCount = 1;
            snapshot.MemberCount = 1;
            snapshot.RequirementCount = requirementCount;
            snapshot.WorkItemCount = 0;
            snapshot.RequirementWorkItemStatus = status;
            snapshot.OverLimitSince = status == RequirementWorkItemLimitStatus.WithinLimit ? null : FixedNowUtc;
            snapshot.EnforcementStartsAt = status == RequirementWorkItemLimitStatus.EnforcementActive ? FixedNowUtc.AddDays(-1) : FixedNowUtc.AddDays(14);
            snapshot.CalculatedAt = FixedNowUtc;
            await db.SaveChangesAsync();
        });

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static AuthorizedTeamId AuthorizeTeamId(Guid teamId)
        => (AuthorizedTeamId)Activator.CreateInstance(
            typeof(AuthorizedTeamId),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: new object[] { teamId },
            culture: null)!;

    private sealed class FakeQuotaStore : ITeamQuotaStore
    {
        public TeamPlan Plan { get; init; }
        public TeamUsageCounts Usage { get; init; } = new(0, 0, 0, 0);
        public TeamUsageSnapshot? ExistingSnapshot { get; init; }
        public TeamUsageSnapshot? SavedSnapshot { get; private set; }

        public Task<TeamPlan?> GetTeamPlanAsync(Guid teamId, CancellationToken cancellationToken = default)
            => Task.FromResult<TeamPlan?>(Plan);

        public Task<int> CountProjectsAsync(Guid teamId, CancellationToken cancellationToken = default)
            => Task.FromResult(Usage.Projects);

        public Task<ProjectTeamUsage?> GetProjectTeamUsageAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectTeamUsage?>(null);

        public Task<ProjectTeamUsage?> GetFeatureTeamUsageAsync(Guid featureId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectTeamUsage?>(null);

        public Task<RequirementWorkItemLimitStatus?> GetProjectRequirementWorkItemStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<RequirementWorkItemLimitStatus?>(null);

        public Task<RequirementWorkItemLimitStatus?> GetFeatureRequirementWorkItemStatusAsync(Guid featureId, CancellationToken cancellationToken = default)
            => Task.FromResult<RequirementWorkItemLimitStatus?>(null);

        public Task<TeamUsageSnapshot?> GetUsageSnapshotAsync(Guid teamId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingSnapshot);

        public Task<TeamUsageCounts> CountUsageAsync(Guid teamId, CancellationToken cancellationToken = default)
            => Task.FromResult(Usage);

        public Task<TeamUsageCounts> CountUsageForRecalculationAsync(Guid teamId, CancellationToken cancellationToken = default)
            => Task.FromResult(Usage);

        public Task UpsertUsageSnapshotAsync(TeamUsageSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            SavedSnapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<TeamMemberUsage?> GetTeamMemberUsageAsync(Guid teamId, DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<TeamMemberUsage?>(new TeamMemberUsage(Plan, Usage.Members));

        public Task<bool> TryAcquireUsageRecalculationLockAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task ReleaseUsageRecalculationLockAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> HasAnyStaleExistingUsageSnapshotAsync(DateTime staleBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<Guid>> ListTeamIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}

public sealed record TeamUsageSummaryResponse(
    TeamPlan Plan,
    TeamLimitsResponse Limits,
    TeamUsageCountsResponse? Usage,
    DateTime? CalculatedAt,
    RequirementWorkItemLimitStatus RequirementWorkItemStatus,
    DateTime? OverLimitSince,
    DateTime? EnforcementStartsAt,
    string CalculationCadence,
    string RecommendedAction);

public sealed record TeamLimitsResponse(
    int? MaxProjects,
    int? MaxTotalRequirementsAndWorkItems,
    int? MaxMembers,
    string AiUsageLabel,
    bool IsUnlimited);

public sealed record TeamUsageCountsResponse(
    int Projects,
    int Members,
    int Requirements,
    int WorkItems,
    int TotalRequirementsAndWorkItems);
