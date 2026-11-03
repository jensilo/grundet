namespace Grundet.Api.Data.Entities;

public sealed class FeedbackAttachment
{
    public Guid Id { get; set; }
    public Guid FeedbackSubmissionId { get; set; }
    public string? OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public required string StoragePath { get; set; }
    public long OriginalSizeBytes { get; set; }
    public long StoredSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CreatedAt { get; set; }
}
