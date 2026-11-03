using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Validation;
using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;
using Grundet.Api.Features.Attachments;
using Grundet.Api.Features.Billing;
using Grundet.Api.Features.Drafts;
using Grundet.Api.Features.WorkItems;
using AcceptanceCriteriaDiff = Grundet.Api.Common.Diff.AcceptanceCriteriaDiff;
using AttachmentsDiff = Grundet.Api.Common.Diff.AttachmentsDiff;
using DiffFieldDiff = Grundet.Api.Common.Diff.FieldDiff;
using LabelsDiff = Grundet.Api.Common.Diff.LabelsDiff;
using SimpleFieldDiff = Grundet.Api.Common.Diff.SimpleFieldDiff;
using TextDiff = Grundet.Api.Common.Diff.TextDiff;

namespace Grundet.Api.Features.Requirements;

public sealed class RequirementService(
    IRequirementStore requirementStore,
    IAttachmentStore attachmentStore,
    IDraftStore draftStore,
    ITeamQuotaService teamQuotaService)
{
    public async Task<SaveRequirementResponse> CreateRequirementAsync(
        AuthorizedFeatureId featureId,
        Guid changedByUserId,
        CreateRequirementCommand command,
        CancellationToken cancellationToken = default)
    {
        featureId.ThrowIfDefault();
        var projectId = await requirementStore.GetProjectIdForFeatureAsync(featureId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(
                ProblemTypes.NotFound,
                $"Feature '{featureId.Value}' was not found.");
        await teamQuotaService.EnsureCanCreateRequirementAsync(featureId, cancellationToken);
        int sequenceNumber = await requirementStore.ClaimNextSequenceNumberAsync(projectId, cancellationToken)
            ?? throw HttpProblemException.Conflict(
                ProblemTypes.Conflict,
                $"Project counter for '{projectId}' is not initialized.");

        Guid[] attachmentIds = await ResolveAttachmentIdsAsync(projectId, command.AttachmentIds, cancellationToken);

        var requirement = new Requirement
        {
            Id = Guid.NewGuid(),
            RequirementId = Guid.NewGuid(),
            ProjectId = projectId,
            FeatureId = featureId.Value,
            SequenceNumber = sequenceNumber,
            Text = TextInput.Required(command.Text, "Requirement text", TextInput.RequirementTextMaxLength),
            Status = command.Status ?? RequirementStatus.Draft,
            Priority = command.Priority ?? RequirementPriority.Must,
            Labels = TextInput.NormalizeLabels(command.Labels),
            AcceptanceCriteria = NormalizeAcceptanceCriteria(command.AcceptanceCriteria),
            AttachmentIds = attachmentIds,
            Rationale = TextInput.Optional(command.Rationale, "Requirement rationale", TextInput.DescriptionMaxLength),
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = changedByUserId,
            ChangeType = RequirementChange.Created
        };

        await requirementStore.InsertVersionAsync(requirement, cancellationToken);
        await draftStore.DeleteDraftIfExistsAsync(changedByUserId, DraftKind.Requirement, featureId.Value, null, cancellationToken);
        await requirementStore.SaveChangesAsync(cancellationToken);

        return new SaveRequirementResponse(Map(requirement), []);
    }

    public async Task<PagedResponse<RequirementResponse>> ListRequirementsAsync(
        AuthorizedFeatureId featureId,
        ListRequirementsCommand command,
        CancellationToken cancellationToken = default)
    {
        featureId.ThrowIfDefault();

        Guid projectId = await requirementStore.GetProjectIdForFeatureAsync(featureId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(
                ProblemTypes.NotFound,
                $"Feature '{featureId.Value}' was not found.");

        return await ListRequirementsCoreAsync(
            projectId,
            command with { FeatureId = featureId.Value },
            cancellationToken);
    }

    public async Task<PagedResponse<RequirementResponse>> ListProjectRequirementsAsync(
        AuthorizedProjectId projectId,
        ListRequirementsCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        return await ListRequirementsCoreAsync(projectId.Value, command, cancellationToken);
    }

    public async Task<RequirementResponse> GetRequirementAsync(
        AuthorizedRequirementId requirementId,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();

        var requirement = await GetLatestOrThrowAsync(requirementId.Value, cancellationToken);
        return Map(requirement);
    }

    public async Task<RequirementResponse> GetRequirementAsync(
        AuthorizedProjectId projectId,
        AuthorizedRequirementId requirementId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        requirementId.ThrowIfDefault();

        await EnsureRequirementBelongsToProjectAsync(requirementId.Value, projectId.Value, cancellationToken);
        return await GetRequirementAsync(requirementId, cancellationToken);
    }

    public async Task<List<AttachmentResponse>> GetRequirementAttachmentsAsync(
        AuthorizedProjectId projectId,
        AuthorizedRequirementId requirementId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        requirementId.ThrowIfDefault();

        await EnsureRequirementBelongsToProjectAsync(requirementId.Value, projectId.Value, cancellationToken);
        var latest = await GetLatestOrThrowAsync(requirementId.Value, cancellationToken);

        if (latest.AttachmentIds.Length == 0)
        {
            return [];
        }

        var metadata = await attachmentStore.GetByProjectAndIdsAsync(
            projectId.Value,
            latest.AttachmentIds,
            cancellationToken);
        var byId = metadata.ToDictionary(attachment => attachment.Id);

        // Preserve the requirement's stored attachment order; skip any id whose blob row is missing
        // (defensive — should not happen).
        return latest.AttachmentIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Select(attachment => new AttachmentResponse(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.Width,
                attachment.Height))
            .ToList();
    }

    public async Task<List<RequirementResponse>> GetHistoryAsync(
        AuthorizedRequirementId requirementId,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();

        var history = await requirementStore.GetHistoryAsync(requirementId.Value, cancellationToken);

        return history.Count == 0
            ? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Requirement '{requirementId.Value}' was not found.")
            : history.Select(Map).ToList();
    }

    public async Task<List<RequirementVersionEntry>> GetVersionHistoryAsync(
        AuthorizedRequirementId requirementId,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();

        var history = await requirementStore.GetVersionHistoryAsync(requirementId.Value, cancellationToken);

        if (history.Count == 0)
        {
            throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Requirement '{requirementId.Value}' was not found.");
        }

        var displayNames = await requirementStore.GetUserDisplayNamesAsync(
            history.Select(version => version.ChangedByUserId).Distinct().ToArray(),
            cancellationToken);

        var featureNames = await requirementStore.GetFeatureNamesAsync(
            history.Select(version => version.FeatureId).Distinct().ToArray(),
            cancellationToken);

        var entries = new List<RequirementVersionEntry>(history.Count);
        Requirement? previous = null;

        for (int index = 0; index < history.Count; index++)
        {
            var version = history[index];
            var changes = previous is null ? null : BuildVersionChanges(previous, version, featureNames);

            entries.Add(new RequirementVersionEntry(
                version.Id,
                index + 1,
                version.ChangedAt,
                version.ChangedByUserId,
                displayNames.GetValueOrDefault(version.ChangedByUserId),
                version.ChangeType.ToString(),
                changes,
                version.Reason));

            previous = version;
        }

        entries.Reverse();
        return entries;
    }

    public async Task<SaveRequirementResponse> SaveRequirementAsync(
        AuthorizedRequirementId requirementId,
        Guid changedByUserId,
        UpdateRequirementCommand command,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();

        return await requirementStore.ExecuteInTransactionAsync(async ct =>
        {
            var latest = await GetLatestOrThrowAsync(requirementId.Value, ct);
            EnsureRequirementIsMutable(latest);
            EnsureExpectedVersion(latest, command.ExpectedVersionId);
            var stagedSave = await StageRequirementUpdateAsync(latest, changedByUserId, command, ct);
            await requirementStore.SaveChangesAsync(ct);
            return stagedSave.Response;
        }, cancellationToken);
    }

    public async Task<SaveRequirementResponse> SaveRequirementAsync(
        AuthorizedProjectId projectId,
        AuthorizedRequirementId requirementId,
        Guid changedByUserId,
        UpdateRequirementCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        requirementId.ThrowIfDefault();

        await EnsureRequirementBelongsToProjectAsync(requirementId.Value, projectId.Value, cancellationToken);
        return await SaveRequirementAsync(requirementId, changedByUserId, command, cancellationToken);
    }

    public async Task<RequirementResponse> DeleteRequirementAsync(
        AuthorizedRequirementId requirementId,
        Guid changedByUserId,
        DeleteRequirementCommand command,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();

        return await requirementStore.ExecuteInTransactionAsync(async ct =>
        {
            var latest = await GetLatestOrThrowAsync(requirementId.Value, ct);
            EnsureRequirementIsMutable(latest);
            EnsureExpectedVersion(latest, command.ExpectedVersionId);

            var now = DateTime.UtcNow;
            var deletedVersion = new Requirement
            {
                Id = Guid.NewGuid(),
                RequirementId = latest.RequirementId,
                ProjectId = latest.ProjectId,
                FeatureId = latest.FeatureId,
                SequenceNumber = latest.SequenceNumber,
                Text = latest.Text,
                Status = latest.Status,
                Priority = latest.Priority,
                Labels = latest.Labels,
                AcceptanceCriteria = latest.AcceptanceCriteria,
                AttachmentIds = latest.AttachmentIds,
                Rationale = latest.Rationale,
                Reason = TextInput.Optional(command.Reason, "Reason", TextInput.ReasonMaxLength),
                ChangedAt = now,
                ChangedByUserId = changedByUserId,
                ChangeType = RequirementChange.Deleted
            };

            await requirementStore.InsertVersionAsync(deletedVersion, ct);
            // Links to a deleted intent stay active and surface as deleted via the latest-version
            // lookup at read time — no link rows are written here.
            await draftStore.DeleteDraftIfExistsAsync(changedByUserId, DraftKind.Requirement, latest.FeatureId, latest.RequirementId, ct);
            await requirementStore.SaveChangesAsync(ct);

            return Map(deletedVersion);
        }, cancellationToken);
    }

    public async Task<RequirementResponse> DeleteRequirementAsync(
        AuthorizedProjectId projectId,
        AuthorizedRequirementId requirementId,
        Guid changedByUserId,
        DeleteRequirementCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        requirementId.ThrowIfDefault();

        await EnsureRequirementBelongsToProjectAsync(requirementId.Value, projectId.Value, cancellationToken);
        return await DeleteRequirementAsync(requirementId, changedByUserId, command, cancellationToken);
    }

    public async Task<BatchSaveRequirementsResponse> BatchSaveRequirementsAsync(
        AuthorizedProjectId projectId,
        IReadOnlyList<AuthorizedRequirementSaveCommand> saves,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        return await requirementStore.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var responses = new List<SaveRequirementResponse>(saves.Count);
            var stagedLatestByRequirementId = new Dictionary<Guid, Requirement>();
            var seenWorkItemNoticeIds = new HashSet<Guid>();

            foreach (var save in saves)
            {
                save.RequirementId.ThrowIfDefault();

                if (!await requirementStore.RequirementBelongsToProjectAsync(
                        save.RequirementId.Value,
                        projectId.Value,
                        transactionCancellationToken))
                {
                    throw HttpProblemException.NotFound(
                        ProblemTypes.NotFound,
                        $"Requirement '{save.RequirementId.Value}' was not found in project '{projectId.Value}'.");
                }

                var latest = stagedLatestByRequirementId.TryGetValue(save.RequirementId.Value, out var stagedLatest)
                    ? stagedLatest
                    : await GetLatestOrThrowAsync(save.RequirementId.Value, transactionCancellationToken);
                var stagedSave = await StageRequirementUpdateAsync(
                    latest,
                    changedByUserId,
                    save.Command,
                    transactionCancellationToken);
                stagedLatestByRequirementId[save.RequirementId.Value] = stagedSave.NewVersion;
                responses.Add(stagedSave.Response with
                {
                    WorkItemNotices = stagedSave.Response.WorkItemNotices
                        .Where(notice => seenWorkItemNoticeIds.Add(notice.WorkItemId))
                        .ToList()
                });
            }

            await requirementStore.SaveChangesAsync(transactionCancellationToken);
            return new BatchSaveRequirementsResponse(responses);
        }, cancellationToken);
    }

    private async Task<StagedRequirementSave> StageRequirementUpdateAsync(
        Requirement latest,
        Guid changedByUserId,
        UpdateRequirementCommand command,
        CancellationToken cancellationToken)
    {
        EnsureRequirementIsMutable(latest);

        // Resolve the effective feature ID — may differ when a move is requested.
        Guid effectiveFeatureId = latest.FeatureId;
        if (command.TargetFeatureIdWasProvided && command.TargetFeatureId.HasValue)
        {
            var targetFeatureId = command.TargetFeatureId.Value;
            if (targetFeatureId.Value != latest.FeatureId)
            {
                var targetProjectId = await requirementStore.GetProjectIdForFeatureAsync(
                    targetFeatureId.Value,
                    cancellationToken)
                    ?? throw HttpProblemException.NotFound(
                        ProblemTypes.NotFound,
                        $"Feature '{targetFeatureId.Value}' was not found.");

                if (targetProjectId != latest.ProjectId)
                {
                    throw HttpProblemException.BadRequest(
                        ProblemTypes.Validation,
                        "Cross-project moves are not supported. The target feature must belong to the same project as the requirement.");
                }

                effectiveFeatureId = targetFeatureId.Value;
            }
        }

        Guid[] attachmentIds = command.AttachmentIdsWasProvided
            ? await ResolveAttachmentIdsAsync(latest.ProjectId, command.AttachmentIds, cancellationToken)
            : latest.AttachmentIds;

        var now = DateTime.UtcNow;
        var newVersion = new Requirement
        {
            Id = Guid.NewGuid(),
            RequirementId = latest.RequirementId,
            ProjectId = latest.ProjectId,
            FeatureId = effectiveFeatureId,
            SequenceNumber = latest.SequenceNumber,
            Text = command.TextWasProvided
                ? TextInput.Required(command.Text, "Requirement text", TextInput.RequirementTextMaxLength)
                : latest.Text,
            Status = command.StatusWasProvided && command.Status.HasValue ? command.Status.Value : latest.Status,
            Priority = command.PriorityWasProvided && command.Priority.HasValue ? command.Priority.Value : latest.Priority,
            Labels = command.LabelsWasProvided ? TextInput.NormalizeLabels(command.Labels) : latest.Labels,
            AcceptanceCriteria = command.AcceptanceCriteriaWasProvided
                ? NormalizeAcceptanceCriteria(command.AcceptanceCriteria)
                : latest.AcceptanceCriteria,
            AttachmentIds = attachmentIds,
            Rationale = command.RationaleWasProvided
                ? TextInput.Optional(command.Rationale, "Requirement rationale", TextInput.DescriptionMaxLength)
                : latest.Rationale,
            // Reason describes *this* edit — never inherit the previous version's reason, so it
            // always resets to what the caller sent (null when absent).
            Reason = TextInput.Optional(command.Reason, "Reason", TextInput.ReasonMaxLength),
            ChangedAt = now,
            ChangedByUserId = changedByUserId,
            ChangeType = RequirementChange.Updated
        };

        // A reason only makes sense alongside a real change; reuse the diff builder to detect a
        // no-op rather than duplicating its comparison logic.
        if (newVersion.Reason is not null &&
            BuildVersionChanges(latest, newVersion, new Dictionary<Guid, string>()) is null)
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.ReasonRequiresChange,
                "Make a change before adding a reason – a reason describes what changed in this version.");
        }

        await requirementStore.InsertVersionAsync(newVersion, cancellationToken);
        var notices = await GetAffectedWorkItemNoticesAsync(
            latest.RequirementId,
            latest.ProjectId,
            cancellationToken);

        // Draft is feature-scoped under the ORIGINAL feature — delete there, not the new target.
        await draftStore.DeleteDraftIfExistsAsync(
            changedByUserId,
            DraftKind.Requirement,
            latest.FeatureId,
            latest.RequirementId,
            cancellationToken);

        return new StagedRequirementSave(newVersion, new SaveRequirementResponse(Map(newVersion), notices));
    }

    // Work items linked to a requirement, surfaced after an edit so the UI can tell the editor
    // which work is affected. Reads the active links — it never mutates them.
    private async Task<List<WorkItemNoticeResponse>> GetAffectedWorkItemNoticesAsync(
        Guid requirementId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var activeLinks = await requirementStore.GetActiveLinksForRequirementAsync(requirementId, cancellationToken);

        var notices = await requirementStore.GetWorkItemNoticesAsync(
            projectId,
            activeLinks.Select(link => link.WorkItemId),
            cancellationToken);

        return notices
            .Select(notice => new WorkItemNoticeResponse(notice.WorkItemId, notice.Title, notice.Status))
            .ToList();
    }

    private async Task<PagedResponse<RequirementResponse>> ListRequirementsCoreAsync(
        Guid projectId,
        ListRequirementsCommand command,
        CancellationToken cancellationToken)
    {
        var query = new RequirementListQuery(
            command.Statuses.Distinct().ToArray(),
            command.Priorities.Distinct().ToArray(),
            TextInput.NormalizeLabels(command.Labels),
            command.FeatureId,
            NormalizeOptional(command.Query),
            command.Sort);
        var (items, total) = await requirementStore.GetCurrentByProjectAsync(
            projectId,
            query,
            command.Page,
            command.PageSize,
            cancellationToken);
        var mappedItems = items.Select(Map).ToList();
        return PagedResponse.Create(mappedItems, command.Page, command.PageSize, total);
    }

    public async Task<List<LinkedWorkItemResponse>> GetLinkedWorkItemsAsync(
        AuthorizedRequirementId requirementId,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();

        var rows = await requirementStore.GetLinkedWorkItemsAsync(requirementId.Value, cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var assigneeIds = rows
            .Where(row => row.AssignedToUserId.HasValue)
            .Select(row => row.AssignedToUserId!.Value)
            .Distinct()
            .ToArray();
        var displayNames = assigneeIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await requirementStore.GetUserDisplayNamesAsync(assigneeIds, cancellationToken);

        return rows
            .Select(row => new LinkedWorkItemResponse(
                row.WorkItemLinkId,
                row.WorkItemId,
                $"WO-{row.SequenceNumber}",
                row.Title,
                row.Status,
                row.Priority,
                row.Type,
                row.AssignedToUserId,
                row.AssignedToUserId is { } id ? displayNames.GetValueOrDefault(id) : null,
                row.BlockedReason))
            .ToList();
    }

    private async Task<Requirement> GetLatestOrThrowAsync(Guid requirementId, CancellationToken cancellationToken)
        => await requirementStore.GetLatestAsync(requirementId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Requirement '{requirementId}' was not found.");

    // Optimistic-concurrency guard. Runs inside the update/delete transaction so the read-check-write
    // is atomic. The version identity is the per-version row Id, not the stable RequirementId.
    private static void EnsureExpectedVersion(Requirement latest, Guid? expectedVersionId)
    {
        if (expectedVersionId is { } expected && expected != latest.Id)
        {
            throw HttpProblemException.Conflict(
                ProblemTypes.Conflict,
                "This requirement was changed by another update since you loaded it. Reload and try again.");
        }
    }

    private async Task EnsureRequirementBelongsToProjectAsync(
        Guid requirementId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!await requirementStore.RequirementBelongsToProjectAsync(requirementId, projectId, cancellationToken))
        {
            throw HttpProblemException.NotFound(
                ProblemTypes.NotFound,
                $"Requirement '{requirementId}' was not found in project '{projectId}'.");
        }
    }

    // Validates an ordered set of attachment ids against a project. Attachment ids are an authorized
    // filter surface (like ?featureId=): an id from another project, or an unknown id, is rejected so
    // a requirement can never reference a blob it does not own. Order is preserved; duplicates are
    // dropped (a requirement referencing the same image twice is meaningless).
    private async Task<Guid[]> ResolveAttachmentIdsAsync(
        Guid projectId,
        Guid[]? requestedIds,
        CancellationToken cancellationToken)
    {
        if (requestedIds is null || requestedIds.Length == 0)
        {
            return [];
        }

        var ordered = new List<Guid>(requestedIds.Length);
        var seen = new HashSet<Guid>();
        foreach (Guid id in requestedIds)
        {
            if (id == Guid.Empty)
            {
                throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    "Attachment ids must not be empty.");
            }

            if (seen.Add(id))
            {
                ordered.Add(id);
            }
        }

        var found = await attachmentStore.GetByProjectAndIdsAsync(projectId, ordered, cancellationToken);
        var foundIds = found.Select(attachment => attachment.Id).ToHashSet();
        return foundIds.Count == ordered.Count
            ? ordered.ToArray()
            : throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "One or more attachments do not exist or do not belong to this project.");
    }

    private static void EnsureRequirementIsMutable(Requirement requirement)
    {
        if (requirement.ChangeType == RequirementChange.Deleted)
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "Deleted requirements cannot be modified.");
        }
    }

    private static RequirementResponse Map(CurrentRequirement requirement)
        => new(
            requirement.Id,
            requirement.RequirementId,
            requirement.FeatureId,
            requirement.SequenceNumber,
            FormatReference(requirement.SequenceNumber),
            requirement.Text,
            requirement.Status,
            requirement.Priority,
            requirement.Labels,
            requirement.AcceptanceCriteria,
            requirement.Rationale,
            requirement.Reason,
            requirement.ChangedAt,
            requirement.ChangedByUserId,
            requirement.ChangeType);

    private static RequirementResponse Map(Requirement requirement)
        => new(
            requirement.Id,
            requirement.RequirementId,
            requirement.FeatureId,
            requirement.SequenceNumber,
            FormatReference(requirement.SequenceNumber),
            requirement.Text,
            requirement.Status,
            requirement.Priority,
            requirement.Labels,
            requirement.AcceptanceCriteria,
            requirement.Rationale,
            requirement.Reason,
            requirement.ChangedAt,
            requirement.ChangedByUserId,
            requirement.ChangeType);

    private static RequirementVersionChanges? BuildVersionChanges(
        Requirement previous,
        Requirement current,
        IReadOnlyDictionary<Guid, string> featureNames)
    {
        var text = DiffFieldDiff.ForText(previous.Text, current.Text);
        var acceptanceCriteria = DiffFieldDiff.ForCriteria(previous.AcceptanceCriteria, current.AcceptanceCriteria);
        var labels = DiffFieldDiff.ForLabels(previous.Labels, current.Labels);
        var status = DiffFieldDiff.ForEnum(previous.Status, current.Status);
        var priority = DiffFieldDiff.ForEnum(previous.Priority, current.Priority);
        var rationale = DiffFieldDiff.ForNullableText(previous.Rationale, current.Rationale);
        var feature = previous.FeatureId != current.FeatureId
            ? new SimpleFieldDiff(
                featureNames.GetValueOrDefault(previous.FeatureId, previous.FeatureId.ToString()),
                featureNames.GetValueOrDefault(current.FeatureId, current.FeatureId.ToString()))
            : null;
        var attachments = DiffFieldDiff.ForAttachments(previous.AttachmentIds, current.AttachmentIds);

        return text is null &&
            acceptanceCriteria is null &&
            labels is null &&
            status is null &&
            priority is null &&
            rationale is null &&
            feature is null &&
            attachments is null
            ? null
            : new RequirementVersionChanges(text, acceptanceCriteria, labels, status, priority, rationale, feature, attachments);
    }

    private static string? NormalizeOptional(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string[] NormalizeAcceptanceCriteria(IEnumerable<string>? acceptanceCriteria)
    {
        string[] criteria = acceptanceCriteria?
            .Select(criterion => criterion.Trim())
            .Where(criterion => !string.IsNullOrWhiteSpace(criterion))
            .ToArray() ?? [];

        foreach (string criterion in criteria)
        {
            if (criterion.Length > TextInput.AcceptanceCriterionMaxLength)
            {
                throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    $"Each acceptance criterion must be {TextInput.AcceptanceCriterionMaxLength} characters or fewer.");
            }
        }

        return criteria;
    }

    private static string FormatReference(int sequenceNumber) => $"IN-{sequenceNumber}";
}

public sealed record CreateRequirementCommand(
    string Text,
    RequirementStatus? Status,
    RequirementPriority? Priority,
    string[]? Labels,
    string[]? AcceptanceCriteria,
    string? Rationale,
    Guid[]? AttachmentIds);

public sealed record UpdateRequirementCommand(
    string? Text,
    bool TextWasProvided,
    RequirementStatus? Status,
    bool StatusWasProvided,
    RequirementPriority? Priority,
    bool PriorityWasProvided,
    string[]? Labels,
    bool LabelsWasProvided,
    string[]? AcceptanceCriteria,
    bool AcceptanceCriteriaWasProvided,
    string? Rationale,
    bool RationaleWasProvided,
    string? Reason,
    bool ReasonWasProvided,
    /// <summary>Ordered attachment ids for the new version — null/not provided keeps the current set.</summary>
    Guid[]? AttachmentIds,
    bool AttachmentIdsWasProvided,
    /// <summary>Authorized target feature — null/not provided means keep the current feature.</summary>
    AuthorizedFeatureId? TargetFeatureId,
    bool TargetFeatureIdWasProvided,
    /// <summary>Optimistic-concurrency guard: the version row Id the caller last loaded. Null skips
    /// the check; a mismatch against the current latest version throws 409.</summary>
    Guid? ExpectedVersionId = null);

public sealed record DeleteRequirementCommand(string? Reason, Guid? ExpectedVersionId = null);

public sealed record AuthorizedRequirementSaveCommand(
    AuthorizedRequirementId RequirementId,
    UpdateRequirementCommand Command);

public sealed record ListRequirementsCommand(
    IReadOnlyList<RequirementStatus> Statuses,
    IReadOnlyList<RequirementPriority> Priorities,
    IReadOnlyList<string> Labels,
    Guid? FeatureId,
    string? Query,
    int Page,
    int PageSize,
    RequirementBrowseSort Sort);

public enum RequirementBrowseSort
{
    Priority,
    Updated,
    Created
}

public sealed record RequirementResponse(
    Guid Id,
    Guid RequirementId,
    Guid FeatureId,
    int SequenceNumber,
    string Ref,
    string Text,
    RequirementStatus Status,
    RequirementPriority Priority,
    string[] Labels,
    string[] AcceptanceCriteria,
    string? Rationale,
    string? Reason,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    RequirementChange ChangeType);

public sealed record RequirementVersionEntry(
    Guid VersionId,
    int VersionNumber,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    string? ChangedByDisplayName,
    string ChangeType,
    RequirementVersionChanges? Changes,
    string? Reason);

public sealed record RequirementVersionChanges(
    TextDiff? Text,
    AcceptanceCriteriaDiff? AcceptanceCriteria,
    LabelsDiff? Labels,
    SimpleFieldDiff? Status,
    SimpleFieldDiff? Priority,
    TextDiff? Rationale,
    SimpleFieldDiff? Feature,
    AttachmentsDiff? Attachments);

public sealed record SaveRequirementResponse(
    RequirementResponse Requirement,
    List<WorkItemNoticeResponse> WorkItemNotices);

public sealed record WorkItemNoticeResponse(Guid WorkItemId, string Title, WorkItemStatus Status);

public sealed record LinkedWorkItemResponse(
    Guid WorkItemLinkId,
    Guid WorkItemId,
    string Ref,
    string Title,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    WorkItemType Type,
    Guid? AssignedToUserId,
    string? AssignedToDisplayName,
    string? BlockedReason);

public sealed record BatchSaveRequirementsResponse(List<SaveRequirementResponse> Saves);

internal sealed record StagedRequirementSave(Requirement NewVersion, SaveRequirementResponse Response);
