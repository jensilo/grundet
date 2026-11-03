using Grundet.Api.Data.Entities;
using NpgsqlTypes;

namespace Grundet.Api.Data.Views;

public sealed class CurrentRequirement
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
    public Guid[] AttachmentIds { get; set; } = [];
    public string? Rationale { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedByUserId { get; set; }
    public RequirementChange ChangeType { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }
}
