namespace Grundet.Api.Features.Feedback;

public sealed class FeedbackOptions
{
    public int WeeklyLimit { get; set; } = 20;
    public int MaxFiles { get; set; } = 5;
    public long MaxOriginalBytes { get; set; } = 2 * 1024 * 1024;
    public long MaxImagePixels { get; set; } = 8_000_000;
    public string? AttachmentsRoot { get; set; }
}
