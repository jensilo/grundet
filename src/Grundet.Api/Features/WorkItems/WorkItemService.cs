using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Validation;
using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;
using Grundet.Api.Features.Attachments;
using Grundet.Api.Features.Billing;
using Grundet.Api.Features.Drafts;
using Grundet.Api.Features.Requirements;
using AttachmentsDiff = Grundet.Api.Common.Diff.AttachmentsDiff;
using DiffFieldDiff = Grundet.Api.Common.Diff.FieldDiff;
using LabelsDiff = Grundet.Api.Common.Diff.LabelsDiff;
using SimpleFieldDiff = Grundet.Api.Common.Diff.SimpleFieldDiff;
using TextDiff = Grundet.Api.Common.Diff.TextDiff;

namespace Grundet.Api.Features.WorkItems;

public sealed class WorkItemService(
    IWorkItemStore workItemStore,
    IWorkItemLinkStore workItemLinkStore,
    IRequirementStore requirementStore,
    IAttachmentStore attachmentStore,
    IDraftStore draftStore,
    ITeamQuotaService teamQuotaService)
{
    public async Task<WorkItemResponse> CreateWorkItemAsync(
        AuthorizedProjectId projectId,
        Guid changedByUserId,
        CreateWorkItemCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        await teamQuotaService.EnsureCanCreateWorkItemAsync(projectId, cancellationToken);

        var priority = RequirePriority(command.Priority);
        var assignedToUserId = await ValidateAssignedToUserIdAsync(
            projectId.Value,
            command.AssignedToUserId,
            cancellationToken);
        int sequenceNumber = await workItemStore.ClaimNextSequenceNumberAsync(projectId.Value, cancellationToken)
            ?? throw HttpProblemException.Conflict(
                ProblemTypes.Conflict,
                $"Project counter for '{projectId.Value}' is not initialized.");

        Guid[] attachmentIds = await ResolveAttachmentIdsAsync(projectId.Value, command.AttachmentIds, cancellationToken);

        var now = DateTime.UtcNow;
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            ProjectId = projectId.Value,
            Title = TextInput.Required(command.Title, "Work item title", TextInput.NameMaxLength),
            Description = TextInput.Optional(command.Description, "Work item description", TextInput.DescriptionMaxLength),
            Status = command.Status ?? WorkItemStatus.Todo,
            Priority = priority,
            Labels = TextInput.NormalizeLabels(command.Labels),
            AttachmentIds = attachmentIds,
            AssignedToUserId = assignedToUserId,
            BlockedReason = NormalizeBlockedReason(command.BlockedReason),
            Reason = TextInput.Optional(command.Reason, "Reason", TextInput.ReasonMaxLength),
            SequenceNumber = sequenceNumber,
            Type = command.Type ?? WorkItemType.Task,
            ChangedAt = now,
            ChangedByUserId = changedByUserId,
            ChangeType = WorkItemChange.Created
        };

        // The work item must be persisted before its links: the WorkItemLinks RLS WITH CHECK
        // policy calls rls_can_access_work_item(), which queries the WorkItems table for the
        // target row. EF batches inserts into one round-trip, so a link inserted in the same
        // SaveChanges as its work item fails the policy (the row isn't visible yet -> 42501).
        // Persisting the work item first, then the links, inside one explicit transaction keeps
        // creation atomic while satisfying the link policy.
        return await workItemStore.ExecuteInTransactionAsync(
            async ct =>
            {
                await workItemStore.InsertVersionAsync(workItem, ct);
                await workItemStore.SaveChangesAsync(ct);

                await BuildCreationLinksAsync(workItem, projectId.Value, command.RequirementLinks, changedByUserId, now, ct);
                await draftStore.DeleteDraftIfExistsAsync(changedByUserId, DraftKind.WorkItem, projectId.Value, null, ct);
                await workItemStore.SaveChangesAsync(ct);

                return Map(workItem);
            },
            cancellationToken);
    }

    // The work item is brand new (no concurrent linkers yet), so no advisory lock is needed —
    // unlike the post-creation LinkRequirementAsync path which guards against TOCTOU duplicates.
    private async Task BuildCreationLinksAsync(
        WorkItem workItem,
        Guid projectId,
        IReadOnlyList<AuthorizedRequirementId> requirementLinks,
        Guid changedByUserId,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        if (requirementLinks.Count == 0)
        {
            return;
        }

        var seenRequirementIds = new HashSet<Guid>();

        foreach (var authorizedRequirementId in requirementLinks)
        {
            var requirement = await GetActiveRequirementOrThrowAsync(authorizedRequirementId.Value, cancellationToken);

            if (requirement.ProjectId != projectId)
            {
                throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    "Linked requirements must belong to the same project as the work item.");
            }

            if (!seenRequirementIds.Add(requirement.RequirementId))
            {
                continue;
            }

            await workItemLinkStore.InsertLinkVersionAsync(
                new WorkItemLink
                {
                    Id = Guid.NewGuid(),
                    WorkItemLinkId = Guid.NewGuid(),
                    WorkItemId = workItem.WorkItemId,
                    RequirementId = requirement.RequirementId,
                    ChangedAt = changedAt,
                    ChangedByUserId = changedByUserId,
                    ChangeType = WorkItemLinkChange.Linked
                },
                cancellationToken);
        }
    }

    public async Task<PagedResponse<WorkItemResponse>> ListWorkItemsAsync(
        AuthorizedProjectId projectId,
        ListWorkItemsCommand command,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        var query = new WorkItemListQuery(
            command.Statuses.Distinct().ToArray(),
            command.Priorities.Distinct().ToArray(),
            command.Types.Distinct().ToArray(),
            TextInput.NormalizeLabels(command.Labels),
            command.AssignedToUserId,
            command.UnassignedOnly,
            command.Blocked,
            command.FeatureId?.Value,
            NormalizeOptional(command.Query),
            command.Sort);
        var (items, total) = await workItemStore.GetCurrentByProjectAsync(
            projectId.Value,
            query,
            page,
            pageSize,
            cancellationToken);
        var mappedItems = items.Select(Map).ToList();

        var assigneeIds = mappedItems
            .Where(item => item.AssignedToUserId.HasValue)
            .Select(item => item.AssignedToUserId!.Value)
            .Distinct()
            .ToArray();

        if (assigneeIds.Length > 0)
        {
            var names = await workItemStore.GetUserDisplayNamesAsync(assigneeIds, cancellationToken);
            mappedItems = mappedItems
                .Select(item => item.AssignedToUserId is { } id
                    ? item with { AssignedToDisplayName = names.GetValueOrDefault(id) }
                    : item)
                .ToList();
        }

        return PagedResponse.Create(mappedItems, page, pageSize, total);
    }

    public async Task<WorkItemDetailResponse> GetWorkItemAsync(
        AuthorizedWorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();

        // Read the latest version (not the CurrentWorkItems view) so a soft-deleted work item still
        // resolves and renders as a tombstone, mirroring RequirementService.GetRequirementAsync.
        var workItem = await workItemStore.GetLatestAsync(workItemId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item '{workItemId.Value}' was not found.");
        var links = await workItemLinkStore.GetActiveLinksForWorkItemAsync(workItem.WorkItemId, cancellationToken);

        string? assignedToDisplayName = null;
        if (workItem.AssignedToUserId is { } assignedToUserId)
        {
            var displayNames = await workItemStore.GetUserDisplayNamesAsync([assignedToUserId], cancellationToken);
            assignedToDisplayName = displayNames.GetValueOrDefault(assignedToUserId);
        }

        return new WorkItemDetailResponse(
            Map(workItem),
            await EnrichLinksAsync(links, cancellationToken),
            assignedToDisplayName);
    }

    public async Task<WorkItemDetailResponse> GetWorkItemAsync(
        AuthorizedProjectId projectId,
        AuthorizedWorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        workItemId.ThrowIfDefault();

        await EnsureWorkItemBelongsToProjectAsync(workItemId.Value, projectId.Value, cancellationToken);
        return await GetWorkItemAsync(workItemId, cancellationToken);
    }

    public async Task<List<WorkItemResponse>> GetHistoryAsync(
        AuthorizedWorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();

        var history = await workItemStore.GetHistoryAsync(workItemId.Value, cancellationToken);

        return history.Count == 0
            ? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item '{workItemId.Value}' was not found.")
            : history.Select(Map).ToList();
    }

    public async Task<List<WorkItemVersionEntry>> GetVersionHistoryAsync(
        AuthorizedWorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();

        var versions = await workItemStore.GetVersionHistoryAsync(workItemId.Value, cancellationToken);
        if (versions.Count == 0)
        {
            throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item '{workItemId.Value}' was not found.");
        }

        var userIds = versions
            .Select(version => version.ChangedByUserId)
            .Concat(versions
                .Where(version => version.AssignedToUserId.HasValue)
                .Select(version => version.AssignedToUserId!.Value))
            .Distinct()
            .ToArray();

        var displayNames = await workItemStore.GetUserDisplayNamesAsync(userIds, cancellationToken);

        var entries = new List<WorkItemVersionEntry>(versions.Count);

        for (int index = 0; index < versions.Count; index++)
        {
            var current = versions[index];
            var changes = index == 0 ? null : BuildVersionChanges(versions[index - 1], current, displayNames);

            entries.Add(new WorkItemVersionEntry(
                current.Id,
                index + 1,
                current.ChangedAt,
                current.ChangedByUserId,
                displayNames.GetValueOrDefault(current.ChangedByUserId),
                current.ChangeType.ToString(),
                changes,
                current.Reason));
        }

        entries.Reverse();
        return entries;
    }

    public async Task<WorkItemResponse> UpdateWorkItemAsync(
        AuthorizedWorkItemId workItemId,
        Guid changedByUserId,
        UpdateWorkItemCommand command,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();

        // Convention: Requirement and WorkItem update+delete all run their read-check-write inside an
        // explicit transaction so the optimistic-concurrency version check (F2) is atomic and the
        // copy-forward version insert commits together with any cascade.
        return await workItemStore.ExecuteInTransactionAsync(async ct =>
        {
            var latest = await GetLatestOrThrowAsync(workItemId.Value, ct);
            EnsureWorkItemIsMutable(latest);
            EnsureExpectedVersion(latest, command.ExpectedVersionId);
            var priority = latest.Priority;
            if (command.PriorityWasProvided)
            {
                priority = RequirePriority(command.Priority);
            }

            var assignedToUserId = latest.AssignedToUserId;
            if (command.AssignedToUserIdWasProvided)
            {
                assignedToUserId = await ValidateAssignedToUserIdAsync(
                    latest.ProjectId,
                    command.AssignedToUserId,
                    ct);
            }

            // Omitted attachmentIds keep the current set (WasProvided); an explicit set is validated
            // against the project, mirroring Labels.
            Guid[] attachmentIds = command.AttachmentIdsWasProvided
                ? await ResolveAttachmentIdsAsync(latest.ProjectId, command.AttachmentIds, ct)
                : latest.AttachmentIds;

            var newVersion = new WorkItem
            {
                Id = Guid.NewGuid(),
                WorkItemId = latest.WorkItemId,
                ProjectId = latest.ProjectId,
                Title = command.TitleWasProvided
                    ? TextInput.Required(command.Title, "Work item title", TextInput.NameMaxLength)
                    : latest.Title,
                Description = command.DescriptionWasProvided
                    ? TextInput.Optional(command.Description, "Work item description", TextInput.DescriptionMaxLength)
                    : latest.Description,
                Status = command.StatusWasProvided && command.Status.HasValue ? command.Status.Value : latest.Status,
                Priority = priority,
                Labels = command.LabelsWasProvided ? TextInput.NormalizeLabels(command.Labels) : latest.Labels,
                AttachmentIds = attachmentIds,
                AssignedToUserId = assignedToUserId,
                // BlockedReason is carried-forward state (a work item stays blocked for the same
                // reason until changed), so it keeps the WasProvided semantics.
                BlockedReason = command.BlockedReasonWasProvided ? NormalizeBlockedReason(command.BlockedReason) : latest.BlockedReason,
                // Unlike BlockedReason, Reason describes *this* edit — never inherit the previous
                // version's reason, so it always resets to what the caller sent (null when absent).
                Reason = TextInput.Optional(command.Reason, "Reason", TextInput.ReasonMaxLength),
                SequenceNumber = latest.SequenceNumber,
                Type = command.TypeWasProvided && command.Type.HasValue ? command.Type.Value : latest.Type,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = changedByUserId,
                ChangeType = WorkItemChange.Updated
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

            await workItemStore.InsertVersionAsync(newVersion, ct);
            await draftStore.DeleteDraftIfExistsAsync(changedByUserId, DraftKind.WorkItem, latest.ProjectId, latest.WorkItemId, ct);
            await workItemStore.SaveChangesAsync(ct);
            return Map(newVersion);
        }, cancellationToken);
    }

    public async Task<WorkItemResponse> UpdateWorkItemAsync(
        AuthorizedProjectId projectId,
        AuthorizedWorkItemId workItemId,
        Guid changedByUserId,
        UpdateWorkItemCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        workItemId.ThrowIfDefault();

        await EnsureWorkItemBelongsToProjectAsync(workItemId.Value, projectId.Value, cancellationToken);
        return await UpdateWorkItemAsync(workItemId, changedByUserId, command, cancellationToken);
    }

    public async Task<WorkItemResponse> DeleteWorkItemAsync(
        AuthorizedWorkItemId workItemId,
        Guid changedByUserId,
        DeleteWorkItemCommand command,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();

        // Convention: Requirement and WorkItem update+delete all run their read-check-write inside an
        // explicit transaction so the optimistic-concurrency version check (F2) is atomic and the
        // deleted version plus every cascade unlink commit together.
        return await workItemStore.ExecuteInTransactionAsync(async ct =>
        {
            var latest = await GetLatestOrThrowAsync(workItemId.Value, ct);
            EnsureWorkItemIsMutable(latest);
            EnsureExpectedVersion(latest, command.ExpectedVersionId);

            var now = DateTime.UtcNow;
            var deletedVersion = new WorkItem
            {
                Id = Guid.NewGuid(),
                WorkItemId = latest.WorkItemId,
                ProjectId = latest.ProjectId,
                Title = latest.Title,
                Description = latest.Description,
                Status = latest.Status,
                Priority = latest.Priority,
                Labels = latest.Labels,
                AttachmentIds = latest.AttachmentIds,
                AssignedToUserId = latest.AssignedToUserId,
                BlockedReason = latest.BlockedReason,
                Reason = TextInput.Optional(command.Reason, "Reason", TextInput.ReasonMaxLength),
                SequenceNumber = latest.SequenceNumber,
                Type = latest.Type,
                ChangedAt = now,
                ChangedByUserId = changedByUserId,
                ChangeType = WorkItemChange.Deleted
            };

            await workItemStore.InsertVersionAsync(deletedVersion, ct);

            var activeLinks = await workItemLinkStore.GetActiveLinksForWorkItemAsync(latest.WorkItemId, ct);
            foreach (var link in activeLinks)
            {
                await workItemLinkStore.InsertLinkVersionAsync(CopyLinkVersion(link, changedByUserId, now, WorkItemLinkChange.Unlinked), ct);
            }

            await workItemStore.SaveChangesAsync(ct);
            return Map(deletedVersion);
        }, cancellationToken);
    }

    public async Task<WorkItemResponse> DeleteWorkItemAsync(
        AuthorizedProjectId projectId,
        AuthorizedWorkItemId workItemId,
        Guid changedByUserId,
        DeleteWorkItemCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        workItemId.ThrowIfDefault();

        await EnsureWorkItemBelongsToProjectAsync(workItemId.Value, projectId.Value, cancellationToken);
        return await DeleteWorkItemAsync(workItemId, changedByUserId, command, cancellationToken);
    }

    public async Task<List<AttachmentResponse>> GetWorkItemAttachmentsAsync(
        AuthorizedProjectId projectId,
        AuthorizedWorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        workItemId.ThrowIfDefault();

        await EnsureWorkItemBelongsToProjectAsync(workItemId.Value, projectId.Value, cancellationToken);
        var latest = await GetLatestOrThrowAsync(workItemId.Value, cancellationToken);

        if (latest.AttachmentIds.Length == 0)
        {
            return [];
        }

        var metadata = await attachmentStore.GetByProjectAndIdsAsync(
            projectId.Value,
            latest.AttachmentIds,
            cancellationToken);
        var byId = metadata.ToDictionary(attachment => attachment.Id);

        // Preserve the work item's stored attachment order; skip any id whose blob row is missing
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

    public async Task<WorkItemLinkResponse> LinkRequirementAsync(
        AuthorizedWorkItemId workItemId,
        AuthorizedRequirementId requirementId,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();
        requirementId.ThrowIfDefault();

        // Validate project-membership before acquiring the lock so we don't hold it
        // during the cheaper pre-checks.
        var workItem = await GetLatestOrThrowAsync(workItemId.Value, cancellationToken);
        EnsureWorkItemIsMutable(workItem);
        var requirement = await GetActiveRequirementOrThrowAsync(requirementId.Value, cancellationToken);

        if (!await requirementStore.RequirementBelongsToProjectAsync(requirement.RequirementId, workItem.ProjectId, cancellationToken))
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "Requirement must belong to the same project as the work item.");
        }

        // Acquire an advisory transaction lock keyed on workItemId to serialize
        // concurrent link requests for the same work item, preventing TOCTOU
        // duplicates on the (workItemId, requirementId) pair.
        return await workItemStore.ExecuteWithWorkItemLockAsync<WorkItemLinkResponse>(
            workItem.WorkItemId,
            async ct =>
            {
                var activeLinks = await workItemLinkStore.GetActiveLinksForWorkItemAsync(workItem.WorkItemId, ct);
                if (activeLinks.Any(l => l.RequirementId == requirement.RequirementId))
                {
                    throw HttpProblemException.Conflict(
                        ProblemTypes.Conflict,
                        "This requirement is already actively linked to the work item.");
                }

                var link = new WorkItemLink
                {
                    Id = Guid.NewGuid(),
                    WorkItemLinkId = Guid.NewGuid(),
                    WorkItemId = workItem.WorkItemId,
                    RequirementId = requirement.RequirementId,
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserId = changedByUserId,
                    ChangeType = WorkItemLinkChange.Linked
                };

                await workItemLinkStore.InsertLinkVersionAsync(link, ct);
                await workItemLinkStore.SaveChangesAsync(ct);
                return await EnrichLinkAsync(link, ct);
            },
            cancellationToken);
    }

    public async Task<WorkItemLinkResponse> UnlinkRequirementAsync(
        AuthorizedWorkItemId workItemId,
        AuthorizedWorkItemLinkId linkId,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();
        linkId.ThrowIfDefault();

        var latestLink = await GetLatestLinkForWorkItemOrThrowAsync(workItemId.Value, linkId.Value, cancellationToken);
        EnsureLinkIsActive(latestLink);

        var unlinkedVersion = CopyLinkVersion(latestLink, changedByUserId, DateTime.UtcNow, WorkItemLinkChange.Unlinked);
        await workItemLinkStore.InsertLinkVersionAsync(unlinkedVersion, cancellationToken);
        await workItemLinkStore.SaveChangesAsync(cancellationToken);
        return await EnrichLinkAsync(unlinkedVersion, cancellationToken);
    }

    public async Task<List<WorkItemLinkResponse>> GetLinkHistoryAsync(
        AuthorizedWorkItemId workItemId,
        AuthorizedWorkItemLinkId linkId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();
        linkId.ThrowIfDefault();

        _ = await GetLatestLinkForWorkItemOrThrowAsync(workItemId.Value, linkId.Value, cancellationToken);
        var history = await workItemLinkStore.GetLinkHistoryAsync(linkId.Value, cancellationToken);
        return await EnrichLinksAsync(history, cancellationToken);
    }

    private Task<List<WorkItemLinkResponse>> EnrichLinksAsync(
        IEnumerable<CurrentWorkItemLink> links,
        CancellationToken cancellationToken)
        => EnrichLinksAsync(links.Select(ToWorkItemLink).ToList(), cancellationToken);

    private async Task<List<WorkItemLinkResponse>> EnrichLinksAsync(
        List<WorkItemLink> links,
        CancellationToken cancellationToken)
    {
        if (links.Count == 0)
        {
            return [];
        }

        // Resolve every linked requirement's latest version in one round-trip rather than a
        // per-link GetLatestAsync — the latest version supplies the displayed ref/text and the
        // deleted flag.
        var latestByRequirementId = await requirementStore.GetLatestByRequirementIdsAsync(
            links.Select(link => link.RequirementId).Distinct().ToArray(),
            cancellationToken);

        return links
            .Select(link => MapLink(link, latestByRequirementId.GetValueOrDefault(link.RequirementId)))
            .ToList();
    }

    private async Task<WorkItemLinkResponse> EnrichLinkAsync(
        WorkItemLink link,
        CancellationToken cancellationToken)
        => MapLink(link, await requirementStore.GetLatestAsync(link.RequirementId, cancellationToken));

    private static WorkItemLinkResponse MapLink(WorkItemLink link, Requirement? latestRequirement)
        => Map(link, latestRequirement, latestRequirement?.ChangeType == RequirementChange.Deleted);

    private static WorkItemLink ToWorkItemLink(CurrentWorkItemLink link)
        => new()
        {
            Id = link.Id,
            WorkItemLinkId = link.WorkItemLinkId,
            WorkItemId = link.WorkItemId,
            RequirementId = link.RequirementId,
            ChangedAt = link.ChangedAt,
            ChangedByUserId = link.ChangedByUserId,
            ChangeType = link.ChangeType
        };

    private async Task<WorkItem> GetLatestOrThrowAsync(Guid workItemId, CancellationToken cancellationToken)
        => await workItemStore.GetLatestAsync(workItemId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item '{workItemId}' was not found.");

    // Optimistic-concurrency guard. Runs inside the update/delete transaction so the read-check-write
    // is atomic. The version identity is the per-version row Id, not the stable WorkItemId.
    private static void EnsureExpectedVersion(WorkItem latest, Guid? expectedVersionId)
    {
        if (expectedVersionId is { } expected && expected != latest.Id)
        {
            throw HttpProblemException.Conflict(
                ProblemTypes.Conflict,
                "This work item was changed by another update since you loaded it. Reload and try again.");
        }
    }

    // Validate that every requested attachment id exists in the work item's project (an authorized
    // filter surface — cross-tenant ids are rejected). Deduplicates while preserving caller order.
    // Mirrors RequirementService.ResolveAttachmentIdsAsync.
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

    private async Task EnsureWorkItemBelongsToProjectAsync(
        Guid workItemId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!await workItemStore.WorkItemBelongsToProjectAsync(workItemId, projectId, cancellationToken))
        {
            throw HttpProblemException.NotFound(
                ProblemTypes.NotFound,
                $"Work item '{workItemId}' was not found in project '{projectId}'.");
        }
    }

    private async Task<Requirement> GetActiveRequirementOrThrowAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var requirement = await requirementStore.GetLatestAsync(requirementId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Requirement '{requirementId}' was not found.");

        return requirement.ChangeType == RequirementChange.Deleted
            ? throw HttpProblemException.Conflict(ProblemTypes.Conflict, "Deleted requirements cannot be linked.")
            : requirement;
    }

    private async Task<WorkItemLink> GetLatestLinkForWorkItemOrThrowAsync(
        Guid workItemId,
        Guid linkId,
        CancellationToken cancellationToken)
    {
        var link = await workItemLinkStore.GetLatestLinkAsync(linkId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item link '{linkId}' was not found.");

        return link.WorkItemId != workItemId
            ? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item link '{linkId}' was not found for work item '{workItemId}'.")
            : link;
    }

    private static void EnsureWorkItemIsMutable(WorkItem workItem)
    {
        if (workItem.ChangeType == WorkItemChange.Deleted)
        {
            throw HttpProblemException.Conflict(ProblemTypes.Conflict, "Deleted work items cannot be modified.");
        }
    }

    private static void EnsureLinkIsActive(WorkItemLink link)
    {
        if (link.ChangeType == WorkItemLinkChange.Unlinked)
        {
            throw HttpProblemException.Conflict(ProblemTypes.Conflict, "Unlinked work item links cannot be modified.");
        }
    }

    private static WorkItemLink CopyLinkVersion(
        CurrentWorkItemLink link,
        Guid changedByUserId,
        DateTime changedAt,
        WorkItemLinkChange changeType)
        => new()
        {
            Id = Guid.NewGuid(),
            WorkItemLinkId = link.WorkItemLinkId,
            WorkItemId = link.WorkItemId,
            RequirementId = link.RequirementId,
            ChangedAt = changedAt,
            ChangedByUserId = changedByUserId,
            ChangeType = changeType
        };

    private static WorkItemLink CopyLinkVersion(
        WorkItemLink link,
        Guid changedByUserId,
        DateTime changedAt,
        WorkItemLinkChange changeType)
        => new()
        {
            Id = Guid.NewGuid(),
            WorkItemLinkId = link.WorkItemLinkId,
            WorkItemId = link.WorkItemId,
            RequirementId = link.RequirementId,
            ChangedAt = changedAt,
            ChangedByUserId = changedByUserId,
            ChangeType = changeType
        };

    private static WorkItemVersionChanges? BuildVersionChanges(
        WorkItem previous,
        WorkItem current,
        IReadOnlyDictionary<Guid, string> displayNames)
    {
        var title = DiffFieldDiff.ForText(previous.Title, current.Title);
        var description = DiffFieldDiff.ForNullableText(previous.Description, current.Description);
        var labels = DiffFieldDiff.ForLabels(previous.Labels, current.Labels);
        var status = DiffFieldDiff.ForEnum(previous.Status, current.Status);
        var priority = DiffFieldDiff.ForEnum(previous.Priority, current.Priority);
        var type = DiffFieldDiff.ForEnum(previous.Type, current.Type);
        var assignedToUserId = ForOptionalUser(previous.AssignedToUserId, current.AssignedToUserId, displayNames);
        var blockedReason = DiffFieldDiff.ForNullableText(previous.BlockedReason, current.BlockedReason);
        var attachments = DiffFieldDiff.ForAttachments(previous.AttachmentIds, current.AttachmentIds);

        return title is null
            && description is null
            && labels is null
            && status is null
            && priority is null
            && type is null
            && assignedToUserId is null
            && blockedReason is null
            && attachments is null
            ? null
            : new WorkItemVersionChanges(
            title,
            description,
            labels,
            status,
            priority,
            type,
            assignedToUserId,
            blockedReason,
            attachments);
    }

    private static SimpleFieldDiff? ForOptionalUser(
        Guid? before,
        Guid? after,
        IReadOnlyDictionary<Guid, string> displayNames)
        => before == after
            ? null
            : new SimpleFieldDiff(
                ResolveUserName(before, displayNames),
                ResolveUserName(after, displayNames));

    private static string ResolveUserName(Guid? userId, IReadOnlyDictionary<Guid, string> displayNames)
        => userId is { } id
            ? displayNames.GetValueOrDefault(id, id.ToString())
            : string.Empty;

    private static WorkItemResponse Map(CurrentWorkItem workItem)
        => new(
            workItem.Id,
            workItem.WorkItemId,
            workItem.ProjectId,
            workItem.Title,
            workItem.Description,
            workItem.Status,
            workItem.Priority,
            workItem.Labels,
            workItem.AssignedToUserId,
            workItem.BlockedReason,
            workItem.Reason,
            workItem.SequenceNumber,
            FormatReference(workItem.SequenceNumber),
            workItem.Type,
            workItem.ChangedAt,
            workItem.ChangedByUserId,
            workItem.ChangeType,
            IsReasonPrompted(workItem.Status, workItem.ChangeType),
            null);

    private static WorkItemResponse Map(WorkItem workItem)
        => new(
            workItem.Id,
            workItem.WorkItemId,
            workItem.ProjectId,
            workItem.Title,
            workItem.Description,
            workItem.Status,
            workItem.Priority,
            workItem.Labels,
            workItem.AssignedToUserId,
            workItem.BlockedReason,
            workItem.Reason,
            workItem.SequenceNumber,
            FormatReference(workItem.SequenceNumber),
            workItem.Type,
            workItem.ChangedAt,
            workItem.ChangedByUserId,
            workItem.ChangeType,
            IsReasonPrompted(workItem.Status, workItem.ChangeType),
            null);

    private static WorkItemLinkResponse Map(
        WorkItemLink link,
        Requirement? latestRequirement,
        bool isDeleted)
        => new(
            link.Id,
            link.WorkItemLinkId,
            link.WorkItemId,
            link.RequirementId,
            latestRequirement is null ? null : $"IN-{latestRequirement.SequenceNumber}",
            latestRequirement?.Text,
            link.ChangedAt,
            link.ChangedByUserId,
            link.ChangeType,
            isDeleted);

    private static string? NormalizeOptional(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeBlockedReason(string? value)
        => TextInput.Optional(value, "Blocked reason", TextInput.ReasonMaxLength);

    private static WorkItemPriority RequirePriority(WorkItemPriority? priority)
        => priority ?? throw HttpProblemException.BadRequest(
            ProblemTypes.Validation,
            "priority is required.");

    private static string FormatReference(int sequenceNumber) => $"WO-{sequenceNumber}";

    private async Task<Guid?> ValidateAssignedToUserIdAsync(
        Guid projectId,
        Guid? assignedToUserId,
        CancellationToken cancellationToken) => !assignedToUserId.HasValue
            ? null
            : await workItemStore.IsProjectTeamMemberAsync(projectId, assignedToUserId.Value, cancellationToken)
            ? assignedToUserId
            : throw HttpProblemException.BadRequest(
            ProblemTypes.Validation,
            "assignedToUserId must be a member of the project team.");

    private static bool IsReasonPrompted(WorkItemStatus status, WorkItemChange changeType)
        => changeType == WorkItemChange.Deleted
           || status == WorkItemStatus.Done
           || status == WorkItemStatus.WontDo;
}

public sealed record CreateWorkItemCommand(
    string Title,
    string? Description,
    WorkItemStatus? Status,
    WorkItemPriority? Priority,
    string[]? Labels,
    Guid? AssignedToUserId,
    string? BlockedReason,
    string? Reason,
    WorkItemType? Type,
    // Ordered image attachment ids (uploaded via the project attachments endpoint), validated
    // against the project before being written onto the version row.
    Guid[]? AttachmentIds,
    // Requirements to link to the new work item, authorized at the endpoint layer.
    IReadOnlyList<AuthorizedRequirementId> RequirementLinks);

public sealed record UpdateWorkItemCommand(
    string? Title,
    bool TitleWasProvided,
    string? Description,
    bool DescriptionWasProvided,
    WorkItemStatus? Status,
    bool StatusWasProvided,
    WorkItemPriority? Priority,
    bool PriorityWasProvided,
    string[]? Labels,
    bool LabelsWasProvided,
    Guid? AssignedToUserId,
    bool AssignedToUserIdWasProvided,
    string? BlockedReason,
    bool BlockedReasonWasProvided,
    string? Reason,
    bool ReasonWasProvided,
    WorkItemType? Type,
    bool TypeWasProvided,
    // Null/not-provided keeps the current set (WasProvided semantics, like Labels).
    Guid[]? AttachmentIds,
    bool AttachmentIdsWasProvided,
    /// <summary>Optimistic-concurrency guard: the version row Id the caller last loaded. Null skips
    /// the check; a mismatch against the current latest version throws 409.</summary>
    Guid? ExpectedVersionId = null);

public sealed record DeleteWorkItemCommand(string? Reason, Guid? ExpectedVersionId = null);

public sealed record ListWorkItemsCommand(
    IReadOnlyList<WorkItemStatus> Statuses,
    IReadOnlyList<WorkItemPriority> Priorities,
    IReadOnlyList<WorkItemType> Types,
    IReadOnlyList<string> Labels,
    Guid? AssignedToUserId,
    bool UnassignedOnly,
    bool? Blocked,
    AuthorizedFeatureId? FeatureId,
    string? Query,
    WorkItemBrowseSort Sort);

public enum WorkItemBrowseSort
{
    Priority,
    Updated,
    Created
}

public sealed record WorkItemResponse(
    Guid Id,
    Guid WorkItemId,
    Guid ProjectId,
    string Title,
    string? Description,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    string[] Labels,
    Guid? AssignedToUserId,
    string? BlockedReason,
    string? Reason,
    int SequenceNumber,
    string Ref,
    WorkItemType Type,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    WorkItemChange ChangeType,
    bool ReasonPrompted,
    // Only populated on the list path; all other producers leave this null.
    string? AssignedToDisplayName);

public sealed record WorkItemDetailResponse(
    WorkItemResponse WorkItem,
    List<WorkItemLinkResponse> Links,
    string? AssignedToDisplayName);

public sealed record WorkItemLinkResponse(
    Guid Id,
    Guid WorkItemLinkId,
    Guid WorkItemId,
    Guid RequirementId,
    string? RequirementRef,
    string? RequirementText,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    WorkItemLinkChange ChangeType,
    bool IsDeleted);

public sealed record WorkItemVersionEntry(
    Guid VersionId,
    int VersionNumber,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    string? ChangedByDisplayName,
    string ChangeType,
    WorkItemVersionChanges? Changes,
    string? Reason);

public sealed record WorkItemVersionChanges(
    TextDiff? Title,
    TextDiff? Description,
    LabelsDiff? Labels,
    SimpleFieldDiff? Status,
    SimpleFieldDiff? Priority,
    SimpleFieldDiff? Type,
    SimpleFieldDiff? AssignedToUserId,
    TextDiff? BlockedReason,
    AttachmentsDiff? Attachments);
