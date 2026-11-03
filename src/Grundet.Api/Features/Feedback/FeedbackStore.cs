using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.Feedback;

public sealed class FeedbackStore(AppDbContext db) : IFeedbackStore
{
    public async Task<FeedbackSubmission?> CreateIfWithinWeeklyLimitAsync(
        FeedbackSubmission submission,
        IReadOnlyList<FeedbackAttachment> attachments,
        DateTime since,
        int weeklyLimit,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({submission.UserId.ToString()}))",
            cancellationToken);

        int count = await db.FeedbackSubmissions
            .AsNoTracking()
            .Where(x => x.UserId == submission.UserId && x.CreatedAt >= since)
            .CountAsync(cancellationToken);
        if (count >= weeklyLimit)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        db.FeedbackSubmissions.Add(submission);
        db.FeedbackAttachments.AddRange(attachments);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return submission;
    }
}
