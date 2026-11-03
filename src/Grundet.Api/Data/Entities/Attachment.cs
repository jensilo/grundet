namespace Grundet.Api.Data.Entities;

/// <summary>
/// Immutable image blob metadata, scoped to a project and shared across owners (requirements and
/// work items reference it by id via their <c>AttachmentIds</c> arrays — never a FK to a version
/// row). Carries <see cref="ProjectId"/> so reference- and serve-time authorization need no join
/// back to the owner. <see cref="Sha256"/> is the hex digest of the original upload bytes; a unique
/// <c>(ProjectId, Sha256)</c> index dedups identical uploads within a project. Append-only: never
/// updated or deleted by the API.
/// </summary>
public sealed class Attachment
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Sha256 { get; set; }
    public string? OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public required string StoragePath { get; set; }
    public long OriginalSizeBytes { get; set; }
    public long StoredSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CreatedAt { get; set; }
}
