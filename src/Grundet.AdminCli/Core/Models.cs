namespace Grundet.AdminCli.Core;

internal enum WaitlistStatus
{
    Pending,
    Approved,
    Declined
}

internal enum FeedbackKind
{
    Feedback,
    Bug
}

internal sealed record FeedbackEntry(
    Guid Id,
    Guid UserId,
    string UserEmail,
    FeedbackKind Kind,
    string Message,
    string PagePath,
    string? UserAgent,
    int? ViewportWidth,
    int? ViewportHeight,
    string? Locale,
    string? TimeZone,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? DeletedAt,
    int AttachmentCount);

internal sealed record FeedbackAttachmentEntry(
    Guid Id,
    Guid FeedbackSubmissionId,
    string? OriginalFileName,
    string ContentType,
    string StoragePath,
    long OriginalSizeBytes,
    long StoredSizeBytes,
    int Width,
    int Height,
    DateTime CreatedAt);

internal sealed record WaitlistEntry(
    Guid Id,
    string EmailOriginal,
    string EmailNormalized,
    string Name,
    string AccountType,
    string? TeamSize,
    string? LineOfWork,
    string? OrganizationName,
    string? OrganizationCountry,
    string? Reason,
    WaitlistStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? DeclinedAt,
    string? DeclinedBy);

// --- Waitlist requests/results ---

internal sealed record WaitlistListRequest(int Limit, WaitlistStatus? Status);

internal sealed record WaitlistListResult(IReadOnlyList<WaitlistEntry> Entries, int Limit, WaitlistStatus? Status);

/// <summary>Outcome of a single-row email lookup shared by show and change-status.</summary>
internal enum WaitlistLookup
{
    Found,
    NotFound,
    Ambiguous
}

internal sealed record WaitlistShowResult(
    WaitlistLookup Lookup,
    WaitlistEntry? Entry,
    string NormalizedEmail,
    int MatchCount,
    bool IncludeReason);

internal sealed record WaitlistChangeRequest(string Email, WaitlistStatus Target, string? Operator, bool Execute);

internal enum WaitlistChangeOutcome
{
    OperatorMissing,
    NotFound,
    Ambiguous,
    NotPending,
    DryRun,
    Committed,
    AffectedMismatch
}

internal sealed record WaitlistChangeResult(
    WaitlistChangeOutcome Outcome,
    string? DatabaseTarget,
    WaitlistEntry? Entry,
    WaitlistStatus Target,
    string? Operator,
    bool Execute,
    int AffectedRows,
    string NormalizedEmail,
    int MatchCount);

// --- Feedback requests/results ---

/// <summary>
/// Orthogonal list filters. Each is tri-state: <c>null</c> = don't filter on this dimension,
/// <c>true</c>/<c>false</c> = require that state. <see cref="Unread"/> true=unread / false=read;
/// <see cref="Deleted"/> true=deleted / false=active. The flag face maps its <c>--unread</c> /
/// <c>--include-deleted</c> options onto these (preserving exact behaviour); the TUI uses the
/// full range to offer an All / Unread / Read / Deleted status filter.
/// </summary>
internal sealed record FeedbackListRequest(int Limit, bool? Unread, bool? Deleted);

internal sealed record FeedbackListResult(IReadOnlyList<FeedbackEntry> Entries, int Limit, bool IncludeDeleted);

/// <summary>Outcome of a feedback id lookup. Mirrors <see cref="WaitlistLookup"/>; a unique
/// hex prefix resolves to <see cref="Found"/>, an ambiguous prefix fails closed as
/// <see cref="Ambiguous"/>.</summary>
internal enum FeedbackLookup
{
    Found,
    NotFound,
    Ambiguous
}

internal sealed record FeedbackShowResult(
    FeedbackLookup Lookup,
    FeedbackEntry? Entry,
    IReadOnlyList<FeedbackAttachmentEntry> Attachments,
    string IdEcho,
    int MatchCount);

internal sealed record FeedbackExportResult(
    FeedbackLookup Lookup,
    FeedbackEntry? Entry,
    string IdEcho,
    string? Target,
    IReadOnlyList<string> AttachmentMessages,
    int MatchCount);

internal enum FeedbackMutateOutcome
{
    OperatorMissing,
    NotFound,
    Ambiguous,
    DryRun,
    Committed,
    AffectedMismatch
}

internal sealed record FeedbackMutateResult(
    FeedbackMutateOutcome Outcome,
    string? DatabaseTarget,
    FeedbackEntry? Entry,
    string TargetState,
    string? Operator,
    bool Execute,
    int AffectedRows,
    string IdEcho);
