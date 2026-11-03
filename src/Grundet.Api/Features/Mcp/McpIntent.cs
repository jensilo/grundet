using Grundet.Api.Features.Requirements;

namespace Grundet.Api.Features.Mcp;

/// <summary>
/// Stable, agent-facing shape of a single intent (requirement) version. Decoupled from the internal
/// <see cref="RequirementResponse"/> so the MCP contract can evolve independently of HTTP DTOs.
/// </summary>
public sealed record McpIntent(
    string Ref,
    Guid RequirementId,
    Guid FeatureId,
    int SequenceNumber,
    string Text,
    string Status,
    string Priority,
    string[] Labels,
    string[] AcceptanceCriteria,
    string? Rationale,
    string? Reason,
    DateTime ChangedAt,
    string ChangeType)
{
    public static McpIntent From(RequirementResponse requirement)
        => new(
            requirement.Ref,
            requirement.RequirementId,
            requirement.FeatureId,
            requirement.SequenceNumber,
            requirement.Text,
            requirement.Status.ToString(),
            requirement.Priority.ToString(),
            requirement.Labels,
            requirement.AcceptanceCriteria,
            requirement.Rationale,
            requirement.Reason,
            requirement.ChangedAt,
            requirement.ChangeType.ToString());
}
