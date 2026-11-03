using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grundet.Api.Tests.Infrastructure;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class TeamPermissionsIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly string[] CollaborativeLeadPermissions =
        ["TeamDelete", "TeamInviteMembers", "TeamManageMembers", "TeamManageSettings"];
    private static readonly string[] SoloLeadPermissions = ["TeamManageSettings"];

    [Fact]
    public async Task Permissions_ForCollaborativeLead_ReturnsFullSet()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead); // manual creation defaults to TeamS (collaborative)

        string[] permissions = await GetPermissionsAsync(lead, team.Id);

        Assert.Equal(
            CollaborativeLeadPermissions,
            permissions.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Permissions_ForSoloLead_ReturnsManageSettingsOnly()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var solo = Assert.Single(await TestApi.ListTeamsAsync(lead), team => team.Name == "My work");

        string[] permissions = await GetPermissionsAsync(lead, solo.Id);

        Assert.Equal(SoloLeadPermissions, permissions);
    }

    [Fact]
    public async Task Permissions_ForContributorOnCollaborativeTeam_ReturnsEmptySet()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var contributor = await TestApi.SignInAsync(fixture);
        string contributorEmail = await TestApi.InviteContributorAsync(lead, team.Id, contributor.Email);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        string[] permissions = await GetPermissionsAsync(contributor, team.Id);

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task Permissions_ForNonMember_Returns403()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var outsider = await TestApi.SignInAsync(fixture);

        var response = await outsider.Client.GetAsync($"/api/v1/teams/{team.Id}/permissions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_AreSerializedAsJsonStringArray()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        var response = await lead.Client.GetAsync($"/api/v1/teams/{team.Id}/permissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var permissionsElement = document.RootElement.GetProperty("permissions");
        Assert.Equal(JsonValueKind.Array, permissionsElement.ValueKind);
        foreach (var element in permissionsElement.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
        }

        Assert.Contains("TeamManageSettings", permissionsElement.EnumerateArray().Select(value => value.GetString()));
    }

    // --- Enforcement parity: a granted permission ⟺ its backend op won't 403/409 (and a withheld one would). ---

    [Fact]
    public async Task EnforcementParity_SoloLead_NoTeamDelete_DeleteReturns409()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var solo = Assert.Single(await TestApi.ListTeamsAsync(lead), team => team.Name == "My work");

        string[] permissions = await GetPermissionsAsync(lead, solo.Id);
        Assert.DoesNotContain("TeamDelete", permissions);

        var response = await lead.Client.DeleteAsync($"/api/v1/teams/{solo.Id}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EnforcementParity_SoloLead_NoTeamInvite_InviteReturns409()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var solo = Assert.Single(await TestApi.ListTeamsAsync(lead), team => team.Name == "My work");

        string[] permissions = await GetPermissionsAsync(lead, solo.Id);
        Assert.DoesNotContain("TeamInviteMembers", permissions);

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/teams/{solo.Id}/invites",
            new { email = fixture.CreateUniqueEmail(), role = "Contributor" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EnforcementParity_CollaborativeLead_HasTeamInvite_InviteSucceeds()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        string[] permissions = await GetPermissionsAsync(lead, team.Id);
        Assert.Contains("TeamInviteMembers", permissions);

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/teams/{team.Id}/invites",
            new { email = fixture.CreateUniqueEmail(), role = "Contributor" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task EnforcementParity_CollaborativeLead_HasTeamDelete_DeleteSucceeds()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        string[] permissions = await GetPermissionsAsync(lead, team.Id);
        Assert.Contains("TeamDelete", permissions);

        var response = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string[]> GetPermissionsAsync(AuthSession user, Guid teamId)
    {
        var response = await user.Client.GetAsync($"/api/v1/teams/{teamId}/permissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TeamPermissionsWireResponse>();
        Assert.NotNull(payload);
        return payload!.Permissions;
    }

    private sealed record TeamPermissionsWireResponse(string[] Permissions);
}
