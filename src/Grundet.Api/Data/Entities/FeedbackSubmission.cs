namespace Grundet.Api.Data.Entities;

public enum FeedbackKind
{
    Feedback,
    Bug
}

public sealed class FeedbackSubmission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public FeedbackKind Kind { get; set; }
    public required string Message { get; set; }
    public required string PagePath { get; set; }
    public string? UserAgent { get; set; }
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
    public string? Locale { get; set; }
    public string? TimeZone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReadByOperator { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByOperator { get; set; }
}
