namespace Grundet.Api.Features.Attachments;

/// <summary>
/// Storage and validation caps for project image attachments (shared by requirements and work
/// items). Deliberately more generous than Feedback's screenshot caps because an attachment may
/// stand in as the spec/chart/bug-evidence it represents. Re-encode caps
/// (<see cref="MaxDimension"/>/<see cref="JpegQuality"/>) are raised so detailed charts stay
/// legible — JPEG is still lossy for text-heavy images.
/// </summary>
public sealed class AttachmentOptions
{
    public int MaxFiles { get; set; } = 10;
    public long MaxOriginalBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxImagePixels { get; set; } = 30_000_000;
    public int MaxDimension { get; set; } = 2560;
    public int JpegQuality { get; set; } = 85;

    // Gallery-grid thumbnails. Written next to each full image on upload; the grid loads these
    // instead of CSS-scaling a multi-megapixel original. Sized for ~200px grid cells at 2x DPI while
    // keeping bytes small — full detail is served via the lightbox, not the thumbnail.
    public int ThumbnailMaxDimension { get; set; } = 360;
    public int ThumbnailJpegQuality { get; set; } = 65;

    public string? AttachmentsRoot { get; set; }
}
