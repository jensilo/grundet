namespace Grundet.Api.Common.Images;

/// <summary>
/// Which rendition of a stored image to serve. <see cref="Thumb"/> is a small, low-quality JPEG for
/// gallery grids; <see cref="Full"/> is the re-encoded original for the lightbox.
/// </summary>
public enum ImageVariant
{
    Full,
    Thumb
}

public static class ImageStoragePaths
{
    private const string JpegSuffix = ".jpg";
    private const string ThumbSuffix = "_thumb.jpg";

    /// <summary>
    /// The thumbnail blob lives next to the full JPEG by convention (<c>{id}.jpg</c> →
    /// <c>{id}_thumb.jpg</c>) rather than in a stored column: the path is fully derivable and there
    /// are no legacy rows to backfill.
    /// </summary>
    public static string ToThumbnail(string fullStoragePath)
        => fullStoragePath.EndsWith(JpegSuffix, StringComparison.Ordinal)
            ? string.Concat(fullStoragePath.AsSpan(0, fullStoragePath.Length - JpegSuffix.Length), ThumbSuffix)
            : fullStoragePath + ThumbSuffix;
}
