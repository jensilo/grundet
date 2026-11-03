using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.AlphaWaitlist;

public sealed class AlphaWaitlistStore(AppDbContext dbContext) : IAlphaWaitlistStore
{
    public async Task<bool> UpsertPendingAsync(AlphaWaitlistEntry entry, CancellationToken cancellationToken = default)
    {
        var inserted = await dbContext.Database.SqlQuery<bool>($"""
            INSERT INTO "AlphaWaitlistEntries" (
                "Id",
                "EmailOriginal",
                "EmailNormalized",
                "Name",
                "AccountType",
                "TeamSize",
                "LineOfWork",
                "OrganizationName",
                "OrganizationCountry",
                "Reason",
                "Status",
                "CreatedAt",
                "UpdatedAt")
            VALUES (
                {entry.Id},
                {entry.EmailOriginal},
                {entry.EmailNormalized},
                {entry.Name},
                {entry.AccountType.ToString()},
                {entry.TeamSize?.ToString()},
                {entry.LineOfWork?.ToString()},
                {entry.OrganizationName},
                {entry.OrganizationCountry},
                {entry.Reason},
                {entry.Status.ToString()},
                {entry.CreatedAt},
                {entry.UpdatedAt})
            ON CONFLICT ("EmailNormalized") DO UPDATE
            SET "EmailOriginal" = EXCLUDED."EmailOriginal",
                "Name" = EXCLUDED."Name",
                "AccountType" = EXCLUDED."AccountType",
                "TeamSize" = EXCLUDED."TeamSize",
                "LineOfWork" = EXCLUDED."LineOfWork",
                "OrganizationName" = EXCLUDED."OrganizationName",
                "OrganizationCountry" = EXCLUDED."OrganizationCountry",
                "Reason" = EXCLUDED."Reason",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            WHERE "AlphaWaitlistEntries"."Status" = 'Pending'
            RETURNING (xmax = 0) AS "Value"
            """).ToListAsync(cancellationToken);

        return inserted.SingleOrDefault();
    }

    public async Task<bool> IsApprovedAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        => await dbContext.AlphaWaitlistEntries
            .AsNoTracking()
            .AnyAsync(
                entry => entry.EmailNormalized == normalizedEmail && entry.Status == AlphaWaitlistStatus.Approved,
                cancellationToken);

    public async Task ApproveViaTeamInviteAsync(
        string normalizedEmail,
        string originalEmail,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        string accountType = AlphaWaitlistAccountType.Personal.ToString();
        string approvedStatus = AlphaWaitlistStatus.Approved.ToString();
        string approvedBy = "system-team-invite";

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AlphaWaitlistEntries" (
                "Id",
                "EmailOriginal",
                "EmailNormalized",
                "Name",
                "AccountType",
                "Reason",
                "Status",
                "ApprovedAt",
                "ApprovedBy",
                "CreatedAt",
                "UpdatedAt")
            VALUES (
                {Guid.NewGuid()},
                {originalEmail},
                {normalizedEmail},
                {originalEmail},
                {accountType},
                {reason},
                {approvedStatus},
                {utcNow},
                {approvedBy},
                {utcNow},
                {utcNow})
            ON CONFLICT ("EmailNormalized") DO UPDATE
            SET "Status" = 'Approved',
                "Reason" = CASE
                    WHEN "AlphaWaitlistEntries"."Status" = 'Approved' THEN
                        COALESCE("AlphaWaitlistEntries"."Reason", '') || E'\n' || EXCLUDED."Reason"
                    ELSE EXCLUDED."Reason" || '; PreviousStatus=' || "AlphaWaitlistEntries"."Status"
                END,
                "ApprovedAt" = COALESCE("AlphaWaitlistEntries"."ApprovedAt", EXCLUDED."ApprovedAt"),
                "ApprovedBy" = COALESCE("AlphaWaitlistEntries"."ApprovedBy", EXCLUDED."ApprovedBy"),
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, cancellationToken);
    }
}
