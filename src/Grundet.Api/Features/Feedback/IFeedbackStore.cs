using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Feedback;

public interface IFeedbackStore
{
    Task<FeedbackSubmission?> CreateIfWithinWeeklyLimitAsync(
        FeedbackSubmission submission,
        IReadOnlyList<FeedbackAttachment> attachments,
        DateTime since,
        int weeklyLimit,
        CancellationToken cancellationToken);
}
