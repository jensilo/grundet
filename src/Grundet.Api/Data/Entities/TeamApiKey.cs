namespace Grundet.Api.Data.Entities;

/// <summary>
/// A per-Team API key that authenticates as its issuing user (<see cref="CreatedByUserId"/>),
/// scoped to <see cref="TeamId"/>. Accepted only on the MCP endpoint in alpha. Unlike
/// Requirement / WorkItem / WorkItemLink this is a mutable credential row, not a versioned
/// entity — it sits outside the append-only invariant by design. Only <see cref="KeyHash"/>
/// (SHA-256 of the raw key) is persisted; the raw key is shown exactly once on creation.
/// </summary>
public sealed class TeamApiKey
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }

    /// <summary>The acts-as / attribution identity. The key authenticates as this user.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Team role snapshot at creation. Metadata / forward-compat only — not enforced.</summary>
    public TeamRole Role { get; set; }

    public required string Name { get; set; }

    /// <summary>SHA-256(rawKey) as uppercase hex. Raw key never stored. Unique.</summary>
    public required string KeyHash { get; set; }

    /// <summary>First characters of the raw key, for display/identification only. Never used for auth lookup.</summary>
    public required string Prefix { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
}
