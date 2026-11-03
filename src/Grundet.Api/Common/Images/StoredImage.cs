namespace Grundet.Api.Common.Images;

public sealed record StoredImage(
    Guid Id,
    // Lowercase hex SHA-256 of the original upload bytes (pre-re-encode). Drives project-scoped
    // dedup. Always computed server-side from the received bytes — a client-supplied hash is never
    // trusted (it would be an access-control bypass to a blob the caller never uploaded).
    string Sha256,
    string? OriginalFileName,
    string ContentType,
    string StoragePath,
    long OriginalSizeBytes,
    long StoredSizeBytes,
    int Width,
    int Height);
