using NpgsqlTypes;

namespace Grundet.Api.Data.Entities;

public sealed class Requirement
{
    public Guid Id { get; set; }
    public Guid RequirementId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FeatureId { get; set; }
    public int SequenceNumber { get; set; }
    public required string Text { get; set; }
    public RequirementStatus Status { get; set; }
    public RequirementPriority Priority { get; set; }
    public string[] Labels { get; set; } = [];
    public string[] AcceptanceCriteria { get; set; } = [];

    // Ordered references to Attachment blobs. Copied verbatim into every new version
    // row (like Labels/AcceptanceCriteria) so superseded versions keep rendering their image set.
    // The blobs are append-only and shared across versions; this array is the only link.
    public Guid[] AttachmentIds { get; set; } = [];
    public string? Rationale { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedByUserId { get; set; }
    public RequirementChange ChangeType { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }
}
