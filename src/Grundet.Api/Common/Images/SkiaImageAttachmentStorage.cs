using System.Security.Cryptography;
using Grundet.Api.Common.Errors;
using SkiaSharp;

namespace Grundet.Api.Common.Images;

public sealed class SkiaImageAttachmentStorage : IImageAttachmentStorage
{
    public async Task<IReadOnlyList<StoredImage>> StoreJpegsAsync(
        Guid ownerId,
        IReadOnlyList<IFormFile> files,
        ImageAttachmentOptions options,
        CancellationToken cancellationToken)
    {
        if (files.Count > options.MaxFiles)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"Attach at most {options.MaxFiles} images.");
        }

        string root = Path.GetFullPath(options.AttachmentsRoot);
        string ownerFolder = Path.Combine(root, ownerId.ToString());
        Directory.CreateDirectory(ownerFolder);

        var stored = new List<StoredImage>(files.Count);
        var writtenPaths = new List<string>(files.Count);

        try
        {
            foreach (IFormFile file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stored.Add(await StoreOneAsync(ownerFolder, ownerId, file, options, writtenPaths, cancellationToken));
            }

            return stored;
        }
        catch
        {
            foreach (string path in writtenPaths)
            {
                TryDelete(path);
            }

            throw;
        }
    }

    private static async Task<StoredImage> StoreOneAsync(
        string ownerFolder,
        Guid ownerId,
        IFormFile file,
        ImageAttachmentOptions options,
        List<string> writtenPaths,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > options.MaxOriginalBytes)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"Each image must be between 1 byte and {options.MaxOriginalBytes} bytes.");
        }

        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        // Hash the original bytes (not the re-encoded JPEG) so re-uploads of the same source file
        // dedup, regardless of any non-determinism in JPEG encoding across Skia versions.
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(memory.GetBuffer().AsSpan(0, (int)memory.Length)));
        memory.Position = 0;

        using var codec = SKCodec.Create(memory) ?? throw HttpProblemException.BadRequest(
            type: ProblemTypes.Validation,
            detail: "One image could not be decoded.");

        int sourceWidth = codec.Info.Width;
        int sourceHeight = codec.Info.Height;
        long pixels = (long)sourceWidth * sourceHeight;
        if (sourceWidth <= 0 || sourceHeight <= 0 || pixels > options.MaxImagePixels)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "One image is too large in pixel dimensions.");
        }

        memory.Position = 0;
        using var bitmap = SKBitmap.Decode(memory) ?? throw HttpProblemException.BadRequest(
            type: ProblemTypes.Validation,
            detail: "One image could not be decoded.");

        (int targetWidth, int targetHeight) = FitWithin(sourceWidth, sourceHeight, options.MaxDimension);

        var attachmentId = Guid.NewGuid();
        string fileName = $"{attachmentId}.jpg";
        string finalPath = Path.Combine(ownerFolder, fileName);
        WriteResizedJpeg(bitmap, targetWidth, targetHeight, options.JpegQuality, finalPath);
        writtenPaths.Add(finalPath);

        if (options.GenerateThumbnail)
        {
            (int thumbWidth, int thumbHeight) = FitWithin(sourceWidth, sourceHeight, options.ThumbnailMaxDimension);
            string thumbPath = Path.Combine(ownerFolder, $"{attachmentId}_thumb.jpg");
            WriteResizedJpeg(bitmap, thumbWidth, thumbHeight, options.ThumbnailJpegQuality, thumbPath);
            writtenPaths.Add(thumbPath);
        }

        var info = new FileInfo(finalPath);
        return new StoredImage(
            attachmentId,
            sha256,
            SanitizeOriginalFileName(file.FileName),
            "image/jpeg",
            $"{ownerId}/{fileName}",
            file.Length,
            info.Length,
            targetWidth,
            targetHeight);
    }

    // Scale (width, height) down so the longer side fits maxDimension, preserving aspect ratio.
    // Returns the source dimensions unchanged when already within bounds.
    private static (int Width, int Height) FitWithin(int width, int height, int maxDimension)
    {
        int longest = Math.Max(width, height);
        if (longest <= maxDimension)
        {
            return (width, height);
        }

        double scale = (double)maxDimension / longest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static void WriteResizedJpeg(SKBitmap source, int width, int height, int quality, string path)
    {
        using var resized = source.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default);
        using var image = SKImage.FromBitmap(resized ?? source);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality) ?? throw HttpProblemException.BadRequest(
            type: ProblemTypes.Validation,
            detail: "One image could not be converted to JPEG.");

        using var output = File.Create(path);
        encoded.SaveTo(output);
    }

    public Task DeleteOwnerAttachmentsAsync(
        Guid ownerId,
        ImageAttachmentOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string root = Path.GetFullPath(options.AttachmentsRoot);
        string ownerFolder = Path.Combine(root, ownerId.ToString());
        TryDeleteDirectory(ownerFolder);
        return Task.CompletedTask;
    }

    private static string? SanitizeOriginalFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string name = Path.GetFileName(fileName.Trim());
        return name.Length <= 255 ? name : name[..255];
    }

    private static void TryDelete(string path)
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
