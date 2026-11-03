using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Grundet.Api.Tests;

/// <summary>
/// Phase 2 end-to-end tests through the real /mcp endpoint: the full ApiKey auth + RLS + team-scope
/// path, driven by the official MCP client against the in-memory server. These are the authoritative
/// isolation proofs (direct tool-method calls would bypass auth and RLS).
/// </summary>
[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class McpIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly Uri McpEndpoint = new("https://localhost/mcp");

    [Fact]
    public async Task ListIntent_ReturnsTeamRequirements()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "The login page must support passkeys");
        string rawKey = await CreateKeyAsync(lead, team.Id);

        await using var client = await ConnectAsync(rawKey);
        var (isError, json) = await CallAsync(client, "list_intent", new() { ["projectId"] = project.Id.ToString() });

        Assert.False(isError);
        Assert.Contains("The login page must support passkeys", json, StringComparison.Ordinal);
        Assert.Contains("IN-1", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIntent_ByReference_ReturnsRequirement()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        await TestApi.CreateRequirementAsync(lead, feature.Id, "Export must produce a signed PDF");
        string rawKey = await CreateKeyAsync(lead, team.Id);

        await using var client = await ConnectAsync(rawKey);
        var (isError, json) = await CallAsync(client, "get_intent", new()
        {
            ["projectId"] = project.Id.ToString(),
            ["reference"] = "IN-1"
        });

        Assert.False(isError);
        Assert.Contains("Export must produce a signed PDF", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIntentHistory_ReturnsAllVersions()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        var feature = await TestApi.CreateFeatureAsync(lead, project.Id);
        var created = await TestApi.CreateRequirementAsync(lead, feature.Id, "First wording");
        await TestApi.UpdateRequirementAsync(lead, created.Requirement.RequirementId, "Second wording");
        string rawKey = await CreateKeyAsync(lead, team.Id);

        await using var client = await ConnectAsync(rawKey);
        var (isError, json) = await CallAsync(client, "get_intent_history", new()
        {
            ["projectId"] = project.Id.ToString(),
            ["reference"] = "IN-1"
        });

        Assert.False(isError);
        Assert.Contains("First wording", json, StringComparison.Ordinal);
        Assert.Contains("Second wording", json, StringComparison.Ordinal);
        // Advertised oldest-to-newest: the original wording must precede the later edit.
        Assert.True(
            json.IndexOf("First wording", StringComparison.Ordinal) < json.IndexOf("Second wording", StringComparison.Ordinal),
            "History must be returned oldest-to-newest.");
    }

    [Fact]
    public async Task CrossTeam_ProjectFromAnotherTeam_IsDenied()
    {
        fixture.EmailSink.Clear();
        var leadA = await TestApi.SignInAsync(fixture);
        var teamA = await TestApi.CreateTeamAsync(leadA);
        string keyA = await CreateKeyAsync(leadA, teamA.Id);

        var leadB = await TestApi.SignInAsync(fixture);
        var teamB = await TestApi.CreateTeamAsync(leadB);
        var projectB = await TestApi.CreateProjectAsync(leadB, teamB.Id);
        var featureB = await TestApi.CreateFeatureAsync(leadB, projectB.Id);
        await TestApi.CreateRequirementAsync(leadB, featureB.Id, "Team B secret intent");

        // Key for team A must not be able to read team B's project, even via its real GUID.
        await using var client = await ConnectAsync(keyA);
        var (isError, json) = await CallAsync(client, "list_intent", new() { ["projectId"] = projectB.Id.ToString() });

        Assert.True(isError);
        Assert.DoesNotContain("Team B secret intent", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DualMembership_KeyDoesNotReachOtherTeamsProject()
    {
        fixture.EmailSink.Clear();

        // The attacker is a member of BOTH teams but holds a key scoped only to teamA. A bare
        // project-team membership check would pass for teamB's project — only the key-team binding denies it.
        string attackerEmail = fixture.CreateUniqueEmail();
        var attacker = await TestApi.SignInAsync(fixture, attackerEmail);
        var teamA = await TestApi.CreateTeamAsync(attacker);
        string keyA = await CreateKeyAsync(attacker, teamA.Id);

        var leadB = await TestApi.SignInAsync(fixture);
        var teamB = await TestApi.CreateTeamAsync(leadB);
        var projectB = await TestApi.CreateProjectAsync(leadB, teamB.Id);
        var featureB = await TestApi.CreateFeatureAsync(leadB, projectB.Id);
        await TestApi.CreateRequirementAsync(leadB, featureB.Id, "Team B secret intent");

        // Attacker joins teamB too, so the issuer genuinely is a member of teamB.
        await TestApi.InviteContributorAsync(leadB, teamB.Id, attackerEmail);
        await TestApi.AcceptInviteAsync(fixture, attacker, attackerEmail);

        await using var client = await ConnectAsync(keyA);
        var (isError, json) = await CallAsync(client, "list_intent", new() { ["projectId"] = projectB.Id.ToString() });

        Assert.True(isError);
        Assert.DoesNotContain("Team B secret intent", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIntent_MalformedReference_IsError()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        string rawKey = await CreateKeyAsync(lead, team.Id);

        await using var client = await ConnectAsync(rawKey);
        var (isError, _) = await CallAsync(client, "get_intent", new()
        {
            ["projectId"] = project.Id.ToString(),
            ["reference"] = "IN-abc"
        });

        Assert.True(isError);
    }

    [Fact]
    public async Task GetIntent_NonExistentReference_IsError()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var project = await TestApi.CreateProjectAsync(lead, team.Id);
        string rawKey = await CreateKeyAsync(lead, team.Id);

        await using var client = await ConnectAsync(rawKey);
        var (isError, _) = await CallAsync(client, "get_intent", new()
        {
            ["projectId"] = project.Id.ToString(),
            ["reference"] = "IN-9999"
        });

        Assert.True(isError);
    }

    [Fact]
    public async Task RevokedKey_IsRejectedAtConnect()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var (rawKey, keyId) = await CreateKeyWithIdAsync(lead, team.Id);

        var revoke = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/api-keys/{keyId}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, revoke.StatusCode);

        // The revoked key no longer authenticates; the MCP handshake (initialize) fails.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var client = await ConnectAsync(rawKey);
        });
    }

    [Fact]
    public async Task ExpiredKey_IsRejectedAtConnect()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var (rawKey, keyId) = await CreateKeyWithIdAsync(lead, team.Id);

        // Creation rejects a past expiry (400), so expire the key directly in the DB instead.
        await fixture.WithDbContextAsync(async db =>
        {
            var row = await db.TeamApiKeys.SingleAsync(item => item.Id == keyId);
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        // The expired key no longer authenticates; the MCP handshake (initialize) fails.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var client = await ConnectAsync(rawKey);
        });
    }

    [Fact]
    public async Task MalformedKey_IsRejectedAtConnect()
        => await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var client = await ConnectAsync("grundet_not-a-real-key");
        });

    [Fact]
    public async Task IssuerRemovedFromTeam_LosesAccess()
    {
        fixture.EmailSink.Clear();
        var leadA = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(leadA);
        var project = await TestApi.CreateProjectAsync(leadA, team.Id);
        var feature = await TestApi.CreateFeatureAsync(leadA, project.Id);
        await TestApi.CreateRequirementAsync(leadA, feature.Id, "Issuer still here");
        string rawKey = await CreateKeyAsync(leadA, team.Id);

        // Promote a second member to Lead so the issuer can be removed without leaving the team leaderless.
        string secondEmail = fixture.CreateUniqueEmail();
        var second = await TestApi.SignInAsync(fixture, secondEmail);
        await TestApi.InviteContributorAsync(leadA, team.Id, secondEmail);
        await TestApi.AcceptInviteAsync(fixture, second, secondEmail);
        var promote = await leadA.Client.PutAsJsonAsync(
            $"/api/v1/teams/{team.Id}/members/{second.UserId}/role", new { role = "Lead" });
        Assert.Equal(System.Net.HttpStatusCode.OK, promote.StatusCode);

        // The new Lead removes the issuer from the team. The key is still active as a credential...
        await TestApi.RemoveTeamMemberAsync(second, team.Id, leadA.UserId);

        // ...but live membership is gone, so the tool call is denied (no cross-team/ghost access).
        await using var client = await ConnectAsync(rawKey);
        var (isError, _) = await CallAsync(client, "list_intent", new() { ["projectId"] = project.Id.ToString() });
        Assert.True(isError);
    }

    [Fact]
    public async Task StatelessRequests_DoNotLeakUserContext()
    {
        fixture.EmailSink.Clear();
        var leadA = await TestApi.SignInAsync(fixture);
        var teamA = await TestApi.CreateTeamAsync(leadA);
        var projectA = await TestApi.CreateProjectAsync(leadA, teamA.Id);
        var featureA = await TestApi.CreateFeatureAsync(leadA, projectA.Id);
        await TestApi.CreateRequirementAsync(leadA, featureA.Id, "Alpha-only intent");
        string keyA = await CreateKeyAsync(leadA, teamA.Id);

        var leadB = await TestApi.SignInAsync(fixture);
        var teamB = await TestApi.CreateTeamAsync(leadB);
        var projectB = await TestApi.CreateProjectAsync(leadB, teamB.Id);
        var featureB = await TestApi.CreateFeatureAsync(leadB, projectB.Id);
        await TestApi.CreateRequirementAsync(leadB, featureB.Id, "Beta-only intent");
        string keyB = await CreateKeyAsync(leadB, teamB.Id);

        // Interleave consecutive stateless requests as two different keys/users.
        await using var clientA = await ConnectAsync(keyA);
        await using var clientB = await ConnectAsync(keyB);

        var (errorA1, jsonA1) = await CallAsync(clientA, "list_intent", new() { ["projectId"] = projectA.Id.ToString() });
        var (errorB, jsonB) = await CallAsync(clientB, "list_intent", new() { ["projectId"] = projectB.Id.ToString() });
        var (errorA2, jsonA2) = await CallAsync(clientA, "list_intent", new() { ["projectId"] = projectA.Id.ToString() });

        Assert.False(errorA1);
        Assert.False(errorB);
        Assert.False(errorA2);

        Assert.Contains("Alpha-only intent", jsonA1, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta-only intent", jsonA1, StringComparison.Ordinal);

        Assert.Contains("Beta-only intent", jsonB, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha-only intent", jsonB, StringComparison.Ordinal);

        // A's second request still sees only A's data — no bleed from B's interleaved request.
        Assert.Contains("Alpha-only intent", jsonA2, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta-only intent", jsonA2, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListProjects_ReturnsTeamProjects()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var alpha = await TestApi.CreateProjectAsync(lead, team.Id, "Checkout");
        var beta = await TestApi.CreateProjectAsync(lead, team.Id, "Billing");
        string rawKey = await CreateKeyAsync(lead, team.Id);

        await using var client = await ConnectAsync(rawKey);
        var (isError, json) = await CallAsync(client, "list_projects", new());

        Assert.False(isError);
        Assert.Contains("Checkout", json, StringComparison.Ordinal);
        Assert.Contains("Billing", json, StringComparison.Ordinal);
        Assert.Contains(alpha.Id.ToString(), json, StringComparison.Ordinal);
        Assert.Contains(beta.Id.ToString(), json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListProjects_DuplicateNames_BothAppearWithDistinctIds()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        // Project names are not unique within a team; the tool must surface both so the skill can
        // disambiguate rather than collapsing the name to a single id server-side.
        var first = await TestApi.CreateProjectAsync(lead, team.Id, "Platform");
        var second = await TestApi.CreateProjectAsync(lead, team.Id, "Platform");
        string rawKey = await CreateKeyAsync(lead, team.Id);

        await using var client = await ConnectAsync(rawKey);
        var (isError, json) = await CallAsync(client, "list_projects", new());

        Assert.False(isError);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Contains(first.Id.ToString(), json, StringComparison.Ordinal);
        Assert.Contains(second.Id.ToString(), json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListProjects_OnlyReturnsTheKeysTeamProjects()
    {
        fixture.EmailSink.Clear();
        var leadA = await TestApi.SignInAsync(fixture);
        var teamA = await TestApi.CreateTeamAsync(leadA);
        var projectA = await TestApi.CreateProjectAsync(leadA, teamA.Id, "Alpha-only project");
        string keyA = await CreateKeyAsync(leadA, teamA.Id);

        var leadB = await TestApi.SignInAsync(fixture);
        var teamB = await TestApi.CreateTeamAsync(leadB);
        var projectB = await TestApi.CreateProjectAsync(leadB, teamB.Id, "Beta-only project");

        // The team-A key must list only team A's projects — team B's is invisible, even though the
        // key holder could belong to other teams.
        await using var client = await ConnectAsync(keyA);
        var (isError, json) = await CallAsync(client, "list_projects", new());

        Assert.False(isError);
        Assert.Contains(projectA.Id.ToString(), json, StringComparison.Ordinal);
        Assert.DoesNotContain(projectB.Id.ToString(), json, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta-only project", json, StringComparison.Ordinal);
    }

    private async Task<McpClient> ConnectAsync(string rawKey)
    {
        var httpClient = fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = McpEndpoint,
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport);
    }

    private static async Task<(bool IsError, string Json)> CallAsync(
        McpClient client, string tool, Dictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        string text = string.Join(
            "\n",
            result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        string structured = result.StructuredContent?.GetRawText() ?? string.Empty;
        return (result.IsError == true, text + structured);
    }

    private static async Task<string> CreateKeyAsync(AuthSession user, Guid teamId)
        => (await CreateKeyWithIdAsync(user, teamId)).RawKey;

    private static async Task<(string RawKey, Guid KeyId)> CreateKeyWithIdAsync(AuthSession user, Guid teamId)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/teams/{teamId}/api-keys", new { name = "MCP test key" });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        string rawKey = root.GetProperty("rawKey").GetString()!;
        Guid keyId = root.GetProperty("apiKey").GetProperty("id").GetGuid();
        return (rawKey, keyId);
    }
}
