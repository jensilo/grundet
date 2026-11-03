using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Feedback;

public sealed class CreateFeedbackRequest
{
    public FeedbackKind Kind { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PagePath { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
    public string? Locale { get; set; }
    public string? TimeZone { get; set; }
    public List<IFormFile> Screenshots { get; set; } = [];
}

public sealed record FeedbackResponse(Guid Id, DateTime CreatedAt);

public sealed record CreateFeedbackCommand(
    Guid UserId,
    FeedbackKind Kind,
    string Message,
    string PagePath,
    string? UserAgent,
    int? ViewportWidth,
    int? ViewportHeight,
    string? Locale,
    string? TimeZone,
    IReadOnlyList<IFormFile> Screenshots);
