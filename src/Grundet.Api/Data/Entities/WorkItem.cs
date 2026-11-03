using NpgsqlTypes;

namespace Grundet.Api.Data.Entities;

public sealed class WorkItem
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid ProjectId { get; set; }
    public int SequenceNumber { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; }
    public WorkItemPriority Priority { get; set; }
    public WorkItemType Type { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? BlockedReason { get; set; }
    public string[] Labels { get; set; } = [];

    // Ordered references to Attachment blobs. Copied verbatim into every new version row, like
    // Labels; the version history is the changelog of which images were attached when.
    public Guid[] AttachmentIds { get; set; } = [];
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedByUserId { get; set; }
    public WorkItemChange ChangeType { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }
}
