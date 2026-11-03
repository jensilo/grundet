using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Images;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Grundet.Api.Features.Attachments;

/// <summary>
/// Upload and serving of project image blobs, shared by requirements and work items. The storage
/// owner key is the <c>projectId</c> (the owning requirement/work item may not exist yet at upload
/// time), so <see cref="IImageAttachmentStorage.DeleteOwnerAttachmentsAsync"/> is never called here
/// — it would wipe an entire project's images.
/// </summary>
public sealed class AttachmentService(
    IAttachmentStore attachmentStore,
    IImageAttachmentStorage imageStorage,
    IOptions<AttachmentOptions> options)
{
    public async Task<List<AttachmentResponse>> UploadAsync(
        AuthorizedProjectId projectId,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        if (files.Count == 0)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Attach at least one image.");
        }

        ImageAttachmentOptions imageOptions = CreateImageOptions();
        IReadOnlyList<StoredImage> stored = await imageStorage.StoreJpegsAsync(
            projectId.Value,
            files,
            imageOptions,
            cancellationToken);

        // Project-scoped content dedup: identical bytes (already stored, or repeated within this
        // batch) reuse the existing blob and discard the freshly written duplicate file. Hashes are
        // server-computed from the original bytes, so a caller can never claim a blob it did not
        // upload. Keyed on (ProjectId, Sha256) — never global, so a hash can't probe another tenant.
        string root = Path.GetFullPath(ConfiguredRoot());

        // sha256 -> the canonical persisted-or-pending row for that content. Seeded with rows already
        // stored in this project, then extended as new uploads are queued.
        var resolved = (await attachmentStore.GetByProjectAndShasAsync(
                projectId.Value,
                stored.Select(image => image.Sha256).Distinct().ToArray(),
                cancellationToken))
            .ToDictionary(attachment => attachment.Sha256);

        var now = DateTime.UtcNow;
        var toInsert = new List<Attachment>();
        foreach (StoredImage image in stored)
        {
            if (resolved.ContainsKey(image.Sha256))
            {
                DeleteBlobFiles(Path.GetFullPath(Path.Combine(root, image.StoragePath)));
                continue;
            }

            var attachment = new Attachment
            {
                Id = image.Id,
                ProjectId = projectId.Value,
                Sha256 = image.Sha256,
                OriginalFileName = image.OriginalFileName,
                ContentType = image.ContentType,
                StoragePath = image.StoragePath,
                OriginalSizeBytes = image.OriginalSizeBytes,
                StoredSizeBytes = image.StoredSizeBytes,
                Width = image.Width,
                Height = image.Height,
                CreatedAt = now
            };
            toInsert.Add(attachment);
            resolved[image.Sha256] = attachment; // dedup later files in the same batch
        }

        if (toInsert.Count > 0)
        {
            try
            {
                await attachmentStore.InsertAsync(toInsert, cancellationToken);
                await attachmentStore.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsProjectShaCollision(ex))
            {
                await ReconcileConcurrentDedupAsync(
                    projectId.Value, toInsert, resolved, root, cancellationToken);
            }
            catch
            {
                CleanupBlobs(toInsert, root);
                throw;
            }
        }

        // Built from the canonical rows so duplicates (in-batch or pre-existing) map to the reused row,
        // and input order is preserved.
        return stored.Select(image => Map(resolved[image.Sha256])).ToList();
    }

    // A concurrent upload committed the same (ProjectId, Sha256) between our dedup query and save, so
    // the unique index rejected the batch (which rolls back atomically). Detach the rolled-back rows,
    // reuse the ones that just committed, and re-insert only those still genuinely new.
    private async Task ReconcileConcurrentDedupAsync(
        Guid projectId,
        List<Attachment> toInsert,
        Dictionary<string, Attachment> resolved,
        string root,
        CancellationToken cancellationToken)
    {
        attachmentStore.Detach(toInsert);

        var committed = (await attachmentStore.GetByProjectAndShasAsync(
                projectId,
                toInsert.Select(attachment => attachment.Sha256).ToArray(),
                cancellationToken))
            .ToDictionary(attachment => attachment.Sha256);

        var stillNew = new List<Attachment>();
        foreach (Attachment attachment in toInsert)
        {
            if (committed.TryGetValue(attachment.Sha256, out Attachment? winner))
            {
                DeleteBlobFiles(Path.GetFullPath(Path.Combine(root, attachment.StoragePath)));
                resolved[attachment.Sha256] = winner;
            }
            else
            {
                stillNew.Add(attachment);
            }
        }

        if (stillNew.Count == 0)
        {
            return;
        }

        try
        {
            await attachmentStore.InsertAsync(stillNew, cancellationToken);
            await attachmentStore.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            CleanupBlobs(stillNew, root);
            throw;
        }
    }

    private static bool IsProjectShaCollision(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            && postgres.ConstraintName == "idx_attachment_project_sha256";

    private static void CleanupBlobs(IEnumerable<Attachment> attachments, string root)
    {
        foreach (Attachment attachment in attachments)
        {
            DeleteBlobFiles(Path.GetFullPath(Path.Combine(root, attachment.StoragePath)));
        }
    }

    public async Task<AttachmentContent> GetContentAsync(
        AuthorizedProjectId projectId,
        Guid attachmentId,
        ImageVariant variant,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        Attachment attachment = await attachmentStore.GetByProjectAndIdAsync(
            projectId.Value,
            attachmentId,
            cancellationToken)
            ?? throw HttpProblemException.NotFound(
                ProblemTypes.NotFound,
                $"Attachment '{attachmentId}' was not found.");

        string absolutePath = ResolveAbsolutePath(
            variant == ImageVariant.Thumb
                ? ImageStoragePaths.ToThumbnail(attachment.StoragePath)
                : attachment.StoragePath);

        // A thumbnail may be absent for a row that predates thumbnails; fall back to the full image
        // rather than 404 so the gallery still renders.
        if (variant == ImageVariant.Thumb && !File.Exists(absolutePath))
        {
            absolutePath = ResolveAbsolutePath(attachment.StoragePath);
        }

        if (!File.Exists(absolutePath))
        {
            throw HttpProblemException.NotFound(
                ProblemTypes.NotFound,
                $"Attachment '{attachmentId}' content is no longer available.");
        }

        var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        return new AttachmentContent(stream, attachment.ContentType, stream.Length);
    }

    // Re-derive the absolute path from the configured root + stored relative path. Never trust a
    // client-supplied path. Guard against traversal escaping the root.
    private string ResolveAbsolutePath(string storagePath)
    {
        string root = ConfiguredRoot();
        string fullRoot = Path.GetFullPath(root);
        string combined = Path.GetFullPath(Path.Combine(fullRoot, storagePath));

        string rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        return combined.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            ? combined
            : throw HttpProblemException.NotFound(
                ProblemTypes.NotFound,
                "Attachment content is no longer available.");
    }

    private ImageAttachmentOptions CreateImageOptions()
        => new(
            ConfiguredRoot(),
            options.Value.MaxFiles,
            options.Value.MaxOriginalBytes,
            options.Value.MaxImagePixels,
            options.Value.MaxDimension,
            options.Value.JpegQuality,
            GenerateThumbnail: true,
            options.Value.ThumbnailMaxDimension,
            options.Value.ThumbnailJpegQuality);

    private string ConfiguredRoot()
        => string.IsNullOrWhiteSpace(options.Value.AttachmentsRoot)
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Attachments are not configured on this server.")
            : options.Value.AttachmentsRoot;

    // Delete both renditions of a blob (full + its derived thumbnail).
    private static void DeleteBlobFiles(string fullPath)
    {
        TryDeleteFile(fullPath);
        TryDeleteFile(ImageStoragePaths.ToThumbnail(fullPath));
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static AttachmentResponse Map(Attachment attachment)
        => new(
            attachment.Id,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.Width,
            attachment.Height);
}

public sealed record AttachmentResponse(
    Guid Id,
    string? OriginalFileName,
    string ContentType,
    int Width,
    int Height);

public sealed record AttachmentContent(Stream Stream, string ContentType, long Length);
