using Grundet.Api.Common.Email;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.AlphaWaitlist;

public sealed class AlphaWaitlistService(IAlphaWaitlistStore store, IEmailService emailService)
{
    public const string GenericSuccessMessage = "Thanks — if alpha access becomes available, we'll contact you.";
    public const string PrivateAlphaMessage = "Grundet is currently in private alpha. Join the alpha waitlist to request access.";

    public async Task SubmitAsync(SubmitAlphaWaitlistCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);
        var now = DateTime.UtcNow;
        var entry = new AlphaWaitlistEntry
        {
            Id = Guid.NewGuid(),
            EmailOriginal = command.EmailOriginal,
            EmailNormalized = command.EmailNormalized,
            Name = command.Name,
            AccountType = command.AccountType,
            TeamSize = command.TeamSize,
            LineOfWork = command.LineOfWork,
            OrganizationName = command.OrganizationName,
            OrganizationCountry = command.OrganizationCountry,
            Reason = command.Reason,
            Status = AlphaWaitlistStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        bool inserted = await store.UpsertPendingAsync(entry, cancellationToken);
        if (inserted)
        {
            await emailService.SendAsync(
                command.EmailNormalized,
                "We received your Grundet alpha request",
                EmailTemplates.BuildAlphaWaitlistConfirmationEmail(),
                cancellationToken);
        }
    }

    public Task<bool> IsApprovedEmailAsync(string email, CancellationToken cancellationToken = default)
        => store.IsApprovedAsync(EmailNormalizer.Normalize(email), cancellationToken);

    public Task ApproveTeamInviteEmailAsync(string email, string reason, DateTime utcNow, CancellationToken cancellationToken = default)
        => store.ApproveViaTeamInviteAsync(EmailNormalizer.Normalize(email), email, reason, utcNow, cancellationToken);

    private static void Validate(SubmitAlphaWaitlistCommand command)
    {
        if (command.AccountType == AlphaWaitlistAccountType.Business &&
            (command.TeamSize is null ||
             command.LineOfWork is null ||
             string.IsNullOrWhiteSpace(command.OrganizationName) ||
             string.IsNullOrWhiteSpace(command.OrganizationCountry)))
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Business alpha requests require team size, line of work, organization name, and organization country.");
        }
    }
}
