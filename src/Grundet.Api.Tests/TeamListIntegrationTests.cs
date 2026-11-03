using System.Net;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

/// <summary>
/// Regression tests for GET /api/v1/teams and GET /api/v1/teams/{teamId}/members.
///
/// These endpoints join TeamMembers with Teams/Users and then sort the result.
/// EF Core cannot translate OrderBy when the sort key is a property on a record type
/// constructed inside the Join result selector — it needs an anonymous-type projection
/// so the sort maps directly to a column. These tests catch that class of failure, which
/// only manifests against a real Postgres database (not in-memory or SQLite providers).
/// </summary>
[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class TeamListIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task ListTeams_ReturnsTeam_WithLeadRole()
    {
        fixture.EmailSink.Clear();
        var user = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(user);

        var teams = await TestApi.ListTeamsAsync(user);

        var item = Assert.Single(teams, t => t.Id == team.Id);
        Assert.Equal(team.Name, item.Name);
        Assert.Equal(TeamRole.Lead, item.Role);
    }

    [Fact]
    public async Task ListTeams_ReturnsTeamsAlphabetically()
    {
        fixture.EmailSink.Clear();
        var user = await TestApi.SignInAsync(fixture);

        // Create out of alphabetical order to confirm sorting is applied server-side.
        await TestApi.CreateTeamAsync(user, "Zephyr");
        await TestApi.CreateTeamAsync(user, "Alpha");
        await TestApi.CreateTeamAsync(user, "Middle");

        var teams = await TestApi.ListTeamsAsync(user);

        Assert.Equal(["Alpha", "Middle", "My work", "Zephyr"], teams.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task ListTeams_ExcludesSoftDeletedTeams()
    {
        fixture.EmailSink.Clear();
        var user = await TestApi.SignInAsync(fixture);
        var active = await TestApi.CreateTeamAsync(user);
        var toDelete = await TestApi.CreateTeamAsync(user);

        var deleteResponse = await user.Client.DeleteAsync($"/api/v1/teams/{toDelete.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var teams = await TestApi.ListTeamsAsync(user);

        Assert.Contains(teams, t => t.Id == active.Id);
        Assert.DoesNotContain(teams, t => t.Id == toDelete.Id);
    }

    [Fact]
    public async Task ListTeamMembers_ReturnsAllMembers_WithCorrectRoles()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        string contributorEmail = fixture.CreateUniqueEmail();
        var contributor = await TestApi.SignInAsync(fixture, contributorEmail);

        var team = await TestApi.CreateTeamAsync(lead);
        await TestApi.InviteContributorAsync(lead, team.Id, contributorEmail);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        var members = await TestApi.ListTeamMembersAsync(lead, team.Id);

        Assert.Equal(2, members.Count);
        Assert.Single(members, m => m.UserId == lead.UserId && m.Role == TeamRole.Lead);
        Assert.Single(members, m => m.UserId == contributor.UserId && m.Role == TeamRole.Contributor);
    }
}
