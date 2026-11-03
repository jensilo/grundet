using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grundet.Api.Tests.Infrastructure;
using Npgsql;
using Xunit;

namespace Grundet.Api.Tests;

/// <summary>
/// Phase 1 (per-Team API key lifecycle) integration tests, exercised through the real management
/// HTTP endpoints with RLS enforced (grundet_api role). The end-to-end auth + tenant-isolation
/// proof through the /mcp endpoint lands in Phase 2 alongside the MCP server.
/// </summary>
[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class TeamApiKeyIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Create_ReturnsRawKeyOnce_WithMetadata()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        var created = await CreateKeyAsync(lead, team.Id, "CI key");

        Assert.StartsWith("grundet_", created.RawKey);
        Assert.Equal(created.RawKey[..12], created.ApiKey.Prefix);
        Assert.Equal("Lead", created.ApiKey.Role);
        Assert.Equal(lead.UserId, created.ApiKey.CreatedByUserId);
        Assert.Equal(team.Id, created.ApiKey.TeamId);
        Assert.Equal("CI key", created.ApiKey.Name);
        Assert.True(created.ApiKey.IsActive);
        Assert.Null(created.ApiKey.RevokedAt);
        Assert.Null(created.ApiKey.LastUsedAt);
    }

    [Fact]
    public async Task List_ReturnsMetadata_NeverRawKeyOrHash()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var created = await CreateKeyAsync(lead, team.Id, "Visible key");

        var listResponse = await lead.Client.GetAsync($"/api/v1/teams/{team.Id}/api-keys");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        string body = await listResponse.Content.ReadAsStringAsync();

        // The raw key is shown exactly once on creation; it must never appear in a list payload,
        // nor may any hash field leak.
        Assert.DoesNotContain(created.RawKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain("keyHash", body, StringComparison.OrdinalIgnoreCase);

        var keys = JsonSerializer.Deserialize<List<ApiKeySummaryDto>>(body, JsonOptions)!;
        var item = Assert.Single(keys, k => k.Id == created.ApiKey.Id);
        Assert.Equal(created.ApiKey.Prefix, item.Prefix);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task Revoke_MarksKeyInactive_AndIsIdempotentlyNotFound()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var created = await CreateKeyAsync(lead, team.Id, "Doomed key");

        var revokeResponse = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/api-keys/{created.ApiKey.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var keys = await ListKeysAsync(lead, team.Id);
        var item = Assert.Single(keys, k => k.Id == created.ApiKey.Id);
        Assert.False(item.IsActive);
        Assert.NotNull(item.RevokedAt);

        // Revoking an already-revoked key finds no active key → 404.
        var secondRevoke = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/api-keys/{created.ApiKey.Id}");
        Assert.Equal(HttpStatusCode.NotFound, secondRevoke.StatusCode);
    }

    [Fact]
    public async Task PermanentDelete_RemovesRevokedKey_Row()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var created = await CreateKeyAsync(lead, team.Id, "To be deleted");

        // A key must be revoked before it can be permanently deleted.
        var revoke = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/api-keys/{created.ApiKey.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // Hard delete under the real grundet_api RLS role. Without the DELETE policy this would
        // affect zero rows and the service would surface a 404 (this test guards that regression).
        var delete = await lead.Client.DeleteAsync(
            $"/api/v1/teams/{team.Id}/api-keys/{created.ApiKey.Id}/permanent");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The row is actually gone: it no longer appears in the list...
        var keys = await ListKeysAsync(lead, team.Id);
        Assert.DoesNotContain(keys, k => k.Id == created.ApiKey.Id);

        // ...and a second permanent delete finds nothing → 404.
        var secondDelete = await lead.Client.DeleteAsync(
            $"/api/v1/teams/{team.Id}/api-keys/{created.ApiKey.Id}/permanent");
        Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
    }

    [Fact]
    public async Task PermanentDelete_ActiveKey_Returns409()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var created = await CreateKeyAsync(lead, team.Id, "Still active");

        // An active key cannot be permanently deleted; it must be revoked first.
        var delete = await lead.Client.DeleteAsync(
            $"/api/v1/teams/{team.Id}/api-keys/{created.ApiKey.Id}/permanent");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        // The key is untouched and still listed as active.
        var keys = await ListKeysAsync(lead, team.Id);
        var item = Assert.Single(keys, k => k.Id == created.ApiKey.Id);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task Revoke_UnknownKey_Returns404()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        var response = await lead.Client.DeleteAsync($"/api/v1/teams/{team.Id}/api-keys/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithPastExpiry_Returns400()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);

        var response = await lead.Client.PostAsJsonAsync(
            $"/api/v1/teams/{team.Id}/api-keys",
            new { name = "Expired", expiresAt = DateTime.UtcNow.AddDays(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsContributor_IsForbidden()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        string contributorEmail = fixture.CreateUniqueEmail();
        var contributor = await TestApi.SignInAsync(fixture, contributorEmail);
        var team = await TestApi.CreateTeamAsync(lead);
        await TestApi.InviteContributorAsync(lead, team.Id, contributorEmail);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        var response = await contributor.Client.PostAsJsonAsync(
            $"/api/v1/teams/{team.Id}/api-keys",
            new { name = "Sneaky key" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_AsContributor_IsForbidden()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        string contributorEmail = fixture.CreateUniqueEmail();
        var contributor = await TestApi.SignInAsync(fixture, contributorEmail);
        var team = await TestApi.CreateTeamAsync(lead);
        await TestApi.InviteContributorAsync(lead, team.Id, contributorEmail);
        await TestApi.AcceptInviteAsync(fixture, contributor, contributorEmail);

        var response = await contributor.Client.GetAsync($"/api/v1/teams/{team.Id}/api-keys");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Keys_AreIsolatedAcrossTeams()
    {
        fixture.EmailSink.Clear();
        var leadA = await TestApi.SignInAsync(fixture);
        var teamA = await TestApi.CreateTeamAsync(leadA);
        var createdA = await CreateKeyAsync(leadA, teamA.Id, "Team A key");

        var leadB = await TestApi.SignInAsync(fixture);
        var teamB = await TestApi.CreateTeamAsync(leadB);

        // Lead B cannot reach Team A's key management at all (not a member → 403).
        var crossList = await leadB.Client.GetAsync($"/api/v1/teams/{teamA.Id}/api-keys");
        Assert.Equal(HttpStatusCode.Forbidden, crossList.StatusCode);

        var crossRevoke = await leadB.Client.DeleteAsync($"/api/v1/teams/{teamA.Id}/api-keys/{createdA.ApiKey.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, crossRevoke.StatusCode);

        // Team B's own key list never contains Team A's key.
        var teamBKeys = await ListKeysAsync(leadB, teamB.Id);
        Assert.DoesNotContain(teamBKeys, k => k.Id == createdA.ApiKey.Id);
    }

    [Fact]
    public async Task ApiKey_IsNotAccepted_OnGeneralHttpApi()
    {
        fixture.EmailSink.Clear();
        var lead = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(lead);
        var created = await CreateKeyAsync(lead, team.Id, "Bearer probe");

        // A fresh cookie-less client presenting the raw key as a bearer token must NOT authenticate
        // against the general HTTP API — API keys are accepted only on /mcp in alpha (ADR-008).
        using var bearerClient = fixture.CreateClient();
        bearerClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", created.RawKey);

        var response = await bearerClient.GetAsync("/api/v1/teams");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KeyHash_HasUniqueIndex()
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT indisunique
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indexrelid
            WHERE c.relname = 'idx_team_api_key_hash';
            """,
            connection);

        object? result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        Assert.True((bool)result!);
    }

    private async Task<CreateApiKeyResponseDto> CreateKeyAsync(AuthSession user, Guid teamId, string name)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/v1/teams/{teamId}/api-keys",
            new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateApiKeyResponseDto>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task<List<ApiKeySummaryDto>> ListKeysAsync(AuthSession user, Guid teamId)
    {
        var response = await user.Client.GetAsync($"/api/v1/teams/{teamId}/api-keys");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<ApiKeySummaryDto>>(JsonOptions);
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

    private sealed record ApiKeySummaryDto(
        Guid Id,
        Guid TeamId,
        Guid CreatedByUserId,
        string Role,
        string Name,
        string Prefix,
        DateTime? ExpiresAt,
        DateTime? LastUsedAt,
        DateTime CreatedAt,
        DateTime? RevokedAt,
        bool IsActive);

    private sealed record CreateApiKeyResponseDto(string RawKey, ApiKeySummaryDto ApiKey);
}
