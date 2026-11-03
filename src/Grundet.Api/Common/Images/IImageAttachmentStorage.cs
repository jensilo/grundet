namespace Grundet.Api.Common.Images;

public interface IImageAttachmentStorage
{
    Task<IReadOnlyList<StoredImage>> StoreJpegsAsync(
        Guid ownerId,
        IReadOnlyList<IFormFile> files,
        ImageAttachmentOptions options,
        CancellationToken cancellationToken);

    Task DeleteOwnerAttachmentsAsync(
        Guid ownerId,
        ImageAttachmentOptions options,
        CancellationToken cancellationToken);
}

public sealed record ImageAttachmentOptions(
    string AttachmentsRoot,
    int MaxFiles,
    long MaxOriginalBytes,
    long MaxImagePixels,
    // Re-encode caps. Defaults preserve the original Feedback behaviour (1280px / quality 82).
    // Requirement attachments raise these so a spec/chart stays legible. Note: JPEG re-encoding
    // still degrades text-heavy charts regardless of dimension — preserving the original (PNG)
    // would be a larger, deliberate change.
    int MaxDimension = 1280,
    int JpegQuality = 82,
    // When true, a small thumbnail JPEG ({id}_thumb.jpg) is written next to each full image for
    // gallery grids. Off by default so Feedback (which never serves thumbnails) is unchanged.
    bool GenerateThumbnail = false,
    int ThumbnailMaxDimension = 400,
    int ThumbnailJpegQuality = 80);
