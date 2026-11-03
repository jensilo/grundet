using Grundet.Api.Common.Images;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Feedback;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Grundet.Api.Tests;

public sealed class FeedbackServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenStoreFailsAfterScreenshots_CleansStoredScreenshotFolder()
    {
        var store = new FailingFeedbackStore();
        var imageStorage = new TrackingImageStorage();
        var service = new FeedbackService(store, imageStorage, Options.Create(new FeedbackOptions
        {
            AttachmentsRoot = "/tmp/feedback-test",
            MaxFiles = 5,
            MaxOriginalBytes = 2_097_152,
            MaxImagePixels = 8_000_000,
            WeeklyLimit = 20
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateFeedbackCommand(
                Guid.NewGuid(),
                FeedbackKind.Bug,
                "The feedback service cleanup test message.",
                "/dashboard",
                null,
                null,
                null,
                null,
                null,
                [CreateScreenshot()]),
            CancellationToken.None));

        Assert.Equal("store failed", exception.Message);
        Assert.NotEqual(Guid.Empty, imageStorage.StoredOwnerId);
        Assert.Equal(imageStorage.StoredOwnerId, imageStorage.DeletedOwnerId);
    }

    private static FormFile CreateScreenshot()
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "screenshots", "screen.png");
    }

    private sealed class FailingFeedbackStore : IFeedbackStore
    {
        public Task<FeedbackSubmission?> CreateIfWithinWeeklyLimitAsync(
            FeedbackSubmission submission,
            IReadOnlyList<FeedbackAttachment> attachments,
            DateTime since,
            int weeklyLimit,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("store failed");
    }

    private sealed class TrackingImageStorage : IImageAttachmentStorage
    {
        public Guid StoredOwnerId { get; private set; }
        public Guid DeletedOwnerId { get; private set; }

        public Task<IReadOnlyList<StoredImage>> StoreJpegsAsync(
            Guid ownerId,
            IReadOnlyList<IFormFile> files,
            ImageAttachmentOptions options,
            CancellationToken cancellationToken)
        {
            StoredOwnerId = ownerId;
            IReadOnlyList<StoredImage> images =
            [
                new StoredImage(
                    Guid.NewGuid(),
                    new string('a', 64),
                    "screen.png",
                    "image/jpeg",
                    $"{ownerId}/attachment.jpg",
                    3,
                    2,
                    1,
                    1)
            ];
            return Task.FromResult(images);
        }

        public Task DeleteOwnerAttachmentsAsync(
            Guid ownerId,
            ImageAttachmentOptions options,
            CancellationToken cancellationToken)
        {
            DeletedOwnerId = ownerId;
            return Task.CompletedTask;
        }
    }
}
