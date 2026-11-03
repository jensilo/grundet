using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Images;
using Grundet.Api.Data.Entities;
using Microsoft.Extensions.Options;

namespace Grundet.Api.Features.Feedback;

public sealed class FeedbackService(
    IFeedbackStore store,
    IImageAttachmentStorage imageStorage,
    IOptions<FeedbackOptions> options)
{
    public async Task<FeedbackSubmission> CreateAsync(CreateFeedbackCommand command, CancellationToken cancellationToken)
    {
        Validate(command);

        DateTime cutoff = DateTime.UtcNow.AddDays(-7);

        var submission = new FeedbackSubmission
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            Kind = command.Kind,
            Message = command.Message.Trim(),
            PagePath = SanitizePagePath(command.PagePath),
            UserAgent = NormalizeOptional(command.UserAgent, 512),
            ViewportWidth = command.ViewportWidth,
            ViewportHeight = command.ViewportHeight,
            Locale = NormalizeOptional(command.Locale, 64),
            TimeZone = NormalizeOptional(command.TimeZone, 128),
            CreatedAt = DateTime.UtcNow
        };

        ImageAttachmentOptions? imageOptions = CreateImageOptions(command.Screenshots);
        IReadOnlyList<StoredImage> storedImages = await StoreScreenshotsAsync(submission.Id, command.Screenshots, imageOptions, cancellationToken);
        FeedbackAttachment[] attachments = storedImages
            .Select(image => new FeedbackAttachment
            {
                Id = image.Id,
                FeedbackSubmissionId = submission.Id,
                OriginalFileName = image.OriginalFileName,
                ContentType = image.ContentType,
                StoragePath = image.StoragePath,
                OriginalSizeBytes = image.OriginalSizeBytes,
                StoredSizeBytes = image.StoredSizeBytes,
                Width = image.Width,
                Height = image.Height,
                CreatedAt = submission.CreatedAt
            })
            .ToArray();

        try
        {
            FeedbackSubmission? created = await store.CreateIfWithinWeeklyLimitAsync(
                submission,
                attachments,
                cutoff,
                options.Value.WeeklyLimit,
                cancellationToken);
            return created ?? throw HttpProblemException.TooManyRequests(
                type: ProblemTypes.TooManyRequests,
                detail: "You have sent the maximum number of feedback submissions for this week.",
                title: "Feedback limit reached");
        }
        catch
        {
            await DeleteStoredScreenshotsAsync(submission.Id, imageOptions, CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyList<StoredImage>> StoreScreenshotsAsync(
        Guid submissionId,
        IReadOnlyList<IFormFile> screenshots,
        ImageAttachmentOptions? imageOptions,
        CancellationToken cancellationToken)
        => imageOptions is null
            ? []
            : await imageStorage.StoreJpegsAsync(submissionId, screenshots, imageOptions, cancellationToken);

    private async Task DeleteStoredScreenshotsAsync(
        Guid submissionId,
        ImageAttachmentOptions? imageOptions,
        CancellationToken cancellationToken)
    {
        if (imageOptions is null)
        {
            return;
        }

        try
        {
            await imageStorage.DeleteOwnerAttachmentsAsync(submissionId, imageOptions, cancellationToken);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private ImageAttachmentOptions? CreateImageOptions(IReadOnlyList<IFormFile> screenshots)
    {
        if (screenshots.Count == 0)
        {
            return null;
        }

        string attachmentsRoot = string.IsNullOrWhiteSpace(options.Value.AttachmentsRoot)
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Feedback screenshots are not configured on this server.")
            : options.Value.AttachmentsRoot;

        return new ImageAttachmentOptions(
            attachmentsRoot,
            options.Value.MaxFiles,
            options.Value.MaxOriginalBytes,
            options.Value.MaxImagePixels);
    }

    private static void Validate(CreateFeedbackCommand command)
    {
        if (!Enum.IsDefined(command.Kind))
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Feedback kind must be feedback or bug.");
        }

        string message = command.Message.Trim();
        if (message.Length is < 10 or > 4000)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Feedback message must be between 10 and 4000 characters.");
        }

        if (string.IsNullOrWhiteSpace(command.PagePath) || command.PagePath.Length > 2048)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Page path is required and must be at most 2048 characters.");
        }

        if (command.PagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            command.PagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Page path must not include an origin.");
        }

        ValidateViewport(command.ViewportWidth, "viewport width");
        ValidateViewport(command.ViewportHeight, "viewport height");
    }

    private static void ValidateViewport(int? value, string fieldName)
    {
        if (value is not null && value is <= 0 or > 10000)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"Feedback {fieldName} is outside the accepted range.");
        }
    }

    private static string SanitizePagePath(string pagePath)
    {
        string trimmed = pagePath.Trim();
        int queryStart = trimmed.IndexOf('?');
        if (queryStart < 0)
        {
            return trimmed;
        }

        string path = trimmed[..queryStart];
        string query = trimmed[(queryStart + 1)..];
        string[] blockedKeys = ["access_token", "code", "id_token", "refresh_token", "secret", "state", "token"];
        string[] kept = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !IsBlockedQueryParameter(part, blockedKeys))
            .ToArray();

        return kept.Length == 0 ? path : $"{path}?{string.Join('&', kept)}";
    }

    private static bool IsBlockedQueryParameter(string parameter, string[] blockedKeys)
    {
        int equalsIndex = parameter.IndexOf('=');
        string key = equalsIndex < 0 ? parameter : parameter[..equalsIndex];
        string decodedKey = Uri.UnescapeDataString(key.Replace('+', ' '));

        return blockedKeys.Any(blockedKey => string.Equals(decodedKey, blockedKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
