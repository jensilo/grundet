"use client";

import { useState } from "react";

import { AttachmentUploader } from "@/components/app/attachment-uploader";
import { ComposerContextHeader } from "@/components/app/composer/composer-context-header";
import { AdderRow, ComposerField, ComposerSelect } from "@/components/app/composer/composer-fields";
import { DraftRestoredNotice } from "@/components/app/composer/draft-restored-notice";
import {
  IntentLinkPicker,
  type SelectedIntent,
} from "@/components/app/composer/intent-link-picker";
import { LabelChipInput } from "@/components/app/composer/label-chip-input";
import { useDraftAutosave } from "@/components/app/composer/use-draft-autosave";
import { UserAvatar } from "@/components/app/user-avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  createWorkItem,
  deleteNewWorkItemDraft,
  deleteWorkItemDraft,
  getNewWorkItemDraft,
  getWorkItemDraft,
  updateProjectWorkItem,
  upsertNewWorkItemDraft,
  upsertWorkItemDraft,
} from "@/lib/api/generated/sdk.gen";
import type {
  AttachmentResponse,
  TeamMemberResponse,
  WorkItemPriority,
  WorkItemResponse,
  WorkItemStatus,
  WorkItemType,
} from "@/lib/api/generated/types.gen";
import { mutationErrorMessage } from "@/lib/api/mutation-error";
import { TEXT_LIMITS } from "@/lib/text-limits";
import { cn } from "@/lib/utils";
import {
  idsEqualIgnoringOrder,
  labelsEqualIgnoringOrder,
  trimmedEqual,
} from "@/lib/version-change-comparison";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const SCHEMA_VERSION = 3;
const UNASSIGNED_SENTINEL = "__unassigned__";

const STATUS_OPTIONS: readonly { value: WorkItemStatus; label: string }[] = [
  { value: "Todo", label: "To do" },
  { value: "InProgress", label: "In progress" },
  { value: "Done", label: "Done" },
  { value: "WontDo", label: "Won't Do" },
];

const PRIORITY_OPTIONS: readonly { value: WorkItemPriority; label: string }[] = [
  { value: "Low", label: "Low" },
  { value: "Medium", label: "Medium" },
  { value: "High", label: "High" },
];

const TYPE_OPTIONS: readonly { value: WorkItemType; label: string }[] = [
  { value: "Bug", label: "Bug" },
  { value: "Feature", label: "Feature" },
  { value: "Task", label: "Task" },
];

const TYPE_DESCRIPTIONS: Record<WorkItemType, string> = {
  Bug: "Something is broken and needs fixing.",
  Feature: "New capability or user-facing value.",
  Task: "A unit of work that isn't a bug or a feature.",
};

type OptionalField =
  | "intents"
  | "type"
  | "priority"
  | "labels"
  | "description"
  | "status"
  | "reason"
  | "blockedReason"
  | "assignee";

const ADDER_FIELDS: readonly { key: OptionalField; label: string }[] = [
  { key: "intents", label: "linked intent" },
  { key: "type", label: "type" },
  { key: "priority", label: "priority" },
  { key: "labels", label: "labels" },
  { key: "description", label: "description" },
  { key: "status", label: "status" },
  { key: "reason", label: "reason" },
  { key: "blockedReason", label: "blocked reason" },
  { key: "assignee", label: "assignee" },
];

// ---------------------------------------------------------------------------
// Form payload (also the draft shape, owned by the frontend)
// ---------------------------------------------------------------------------

type WorkItemForm = {
  title: string;
  description: string;
  status: WorkItemStatus;
  priority: WorkItemPriority;
  type: WorkItemType;
  labels: string[];
  reason: string;
  blockedReason: string;
  assignedToUserId: string | null;
  // Intent (requirements) to link when the work item is created. Create mode only — on existing
  // work, links are managed on the detail page.
  intents: SelectedIntent[];
};

type CreateDefaults = {
  title?: string;
  type?: WorkItemType;
  intents?: SelectedIntent[];
};

function emptyForm(defaults?: CreateDefaults): WorkItemForm {
  return {
    title: defaults?.title ?? "",
    description: "",
    status: "Todo",
    priority: "Medium",
    type: defaults?.type ?? "Task",
    labels: [],
    reason: "",
    blockedReason: "",
    assignedToUserId: null,
    intents: defaults?.intents ?? [],
  };
}

function formFromEntity(entity: WorkItemResponse): WorkItemForm {
  return {
    title: entity.title,
    description: entity.description ?? "",
    status: entity.status,
    priority: entity.priority,
    type: entity.type,
    labels: entity.labels,
    reason: "",
    blockedReason: entity.blockedReason ?? "",
    assignedToUserId: entity.assignedToUserId,
    intents: [],
  };
}

function deriveRevealed(form: WorkItemForm): OptionalField[] {
  const revealed: OptionalField[] = [];
  if (form.intents.length > 0) revealed.push("intents");
  if (form.type !== "Task") revealed.push("type");
  if (form.priority !== "Medium") revealed.push("priority");
  if (form.labels.length > 0) revealed.push("labels");
  if (form.description.trim()) revealed.push("description");
  if (form.status !== "Todo") revealed.push("status");
  if (form.reason.trim()) revealed.push("reason");
  if (form.blockedReason.trim()) revealed.push("blockedReason");
  if (form.assignedToUserId !== null) revealed.push("assignee");
  return revealed;
}

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

type WorkItemComposerProps = {
  projectId: string;
  projectName: string;
  surface: "page" | "dialog";
  teamMembers: TeamMemberResponse[];
  onCancel: () => void;
  onSuccess: (workItemId: string) => void;
} & (
  | {
      mode: "create";
      // Optional prefill used by the "track a feature as work" bulk flow.
      initialTitle?: string;
      initialType?: WorkItemType;
      initialIntents?: SelectedIntent[];
      // The bulk flow is ephemeral and must not read or overwrite the shared project draft.
      enableDraft?: boolean;
    }
  | {
      mode: "edit";
      initial: WorkItemResponse;
      initialAttachments?: AttachmentResponse[];
      // Whether the current attachment set was successfully loaded. When false (preload failed) the
      // composer omits attachmentIds from the save payload so the backend preserves the existing set
      // rather than dropping it.
      attachmentsLoaded?: boolean;
    }
);

type MutationState = { type: "idle" } | { type: "submitting" } | { type: "error"; message: string };

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function WorkItemComposer(props: WorkItemComposerProps): React.JSX.Element {
  const isEdit = props.mode === "edit";
  const draftEnabled = props.mode === "create" ? props.enableDraft !== false : true;
  const [baseline] = useState<WorkItemForm>(() =>
    props.mode === "edit"
      ? formFromEntity(props.initial)
      : emptyForm({
          title: props.initialTitle,
          type: props.initialType,
          intents: props.initialIntents,
        })
  );

  const [form, setForm] = useState<WorkItemForm>(baseline);
  // Reveal only the optional fields that hold non-default/filled values — identical in create and
  // edit. Empty optionals stay hidden behind the adder until the user adds them. The version-change
  // `reason` is handled separately: always visible in edit (see below), never in the edit adder.
  const [revealed, setRevealed] = useState<Set<OptionalField>>(
    () => new Set(deriveRevealed(baseline))
  );
  const [mutation, setMutation] = useState<MutationState>({ type: "idle" });
  // Attachments are uploaded immediately and tracked outside the draft form: an in-progress upload
  // is intentionally not draft-persisted (orphaned blobs are accepted, see ADR-011). The baseline is
  // the original set, so Discard can restore it alongside the form fields.
  const [baselineAttachments] = useState<AttachmentResponse[]>(() =>
    props.mode === "edit" ? (props.initialAttachments ?? []) : []
  );
  const [attachments, setAttachments] = useState<AttachmentResponse[]>(baselineAttachments);

  const isSubmitting = mutation.type === "submitting";
  const isDirty = JSON.stringify(form) !== JSON.stringify(baseline);
  const canSubmit = form.title.trim().length > 0;

  // A failed attachment preload leaves the current set unknowable, so it is excluded from the
  // change comparison below (and omitted from the save payload).
  const attachmentsKnown = props.mode === "edit" ? props.attachmentsLoaded !== false : true;
  // Whether anything other than `reason` differs from the saved baseline.
  const hasOtherChanges =
    !trimmedEqual(form.title, baseline.title) ||
    !trimmedEqual(form.description, baseline.description) ||
    form.status !== baseline.status ||
    form.priority !== baseline.priority ||
    form.type !== baseline.type ||
    form.assignedToUserId !== baseline.assignedToUserId ||
    !trimmedEqual(form.blockedReason, baseline.blockedReason) ||
    !labelsEqualIgnoringOrder(form.labels, baseline.labels) ||
    (attachmentsKnown &&
      !idsEqualIgnoringOrder(
        attachments.map((attachment) => attachment.id),
        baselineAttachments.map((attachment) => attachment.id)
      ));

  const assignedMember = form.assignedToUserId
    ? props.teamMembers.find((m) => m.userId === form.assignedToUserId)
    : null;
  const assigneeDisplayValue = assignedMember
    ? (assignedMember.displayName ?? assignedMember.email)
    : "Unassigned";

  function setField<K extends keyof WorkItemForm>(key: K, value: WorkItemForm[K]): void {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function reveal(key: OptionalField): void {
    setRevealed((current) => new Set(current).add(key));
  }

  const draftIo = !draftEnabled
    ? {
        load: async () => null,
        save: async () => {},
        remove: async () => {},
      }
    : props.mode === "create"
      ? {
          load: async () => {
            const { data, error } = await getNewWorkItemDraft({
              path: { projectId: props.projectId },
            });
            // Throw on a failed load so the hook keeps autosave disabled rather than treating
            // the failure as "no draft" and overwriting an existing server draft.
            if (error) throw error;
            return data?.draft?.draftContent ?? null;
          },
          save: async (content: unknown) => {
            await upsertNewWorkItemDraft({
              path: { projectId: props.projectId },
              body: { draftContent: content },
            });
          },
          remove: async () => {
            await deleteNewWorkItemDraft({ path: { projectId: props.projectId } });
          },
        }
      : {
          load: async () => {
            const { data, error } = await getWorkItemDraft({
              path: { workItemId: props.initial.workItemId },
            });
            // Throw on a failed load so the hook keeps autosave disabled rather than treating
            // the failure as "no draft" and overwriting an existing server draft.
            if (error) throw error;
            return data?.draft?.draftContent ?? null;
          },
          save: async (content: unknown) => {
            await upsertWorkItemDraft({
              path: { workItemId: props.initial.workItemId },
              body: { draftContent: content },
            });
          },
          remove: async () => {
            await deleteWorkItemDraft({ path: { workItemId: props.initial.workItemId } });
          },
        };

  const autosave = useDraftAutosave<WorkItemForm>({
    schemaVersion: SCHEMA_VERSION,
    baseChangedAt: props.mode === "edit" ? props.initial.changedAt : null,
    io: draftIo,
    value: form,
    enabled: draftEnabled && isDirty && (isEdit || canSubmit),
    onRestore: (restored) => {
      setForm(restored);
      // Reflect exactly what the restored draft contains — replace rather than union so
      // manually-revealed-but-empty fields don't linger.
      setRevealed(new Set(deriveRevealed(restored)));
    },
  });

  async function handleDiscard(): Promise<void> {
    setForm(baseline);
    setRevealed(new Set(deriveRevealed(baseline)));
    setAttachments(baselineAttachments);
    await autosave.discard();
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (!canSubmit) return;
    // A reason alone is not a change. The Save button is disabled in this state; this guards the
    // Enter-to-submit path so we never send a no-op version.
    if (isEdit && !hasOtherChanges) return;
    setMutation({ type: "submitting" });

    if (props.mode === "create") {
      const { data, error } = await createWorkItem({
        path: { projectId: props.projectId },
        body: {
          title: form.title.trim(),
          description: form.description.trim() || null,
          status: form.status,
          priority: form.priority,
          type: form.type,
          labels: form.labels.length > 0 ? form.labels : null,
          reason: form.reason.trim() || null,
          blockedReason: form.blockedReason.trim() || null,
          assignedToUserId: form.assignedToUserId,
          attachmentIds: attachments.map((attachment) => attachment.id),
          requirementIds:
            form.intents.length > 0 ? form.intents.map((intent) => intent.requirementId) : null,
        },
      });
      if (error || !data) {
        setMutation({
          type: "error",
          message: mutationErrorMessage(
            error,
            "Could not create this work item. Please try again."
          ),
        });
        return;
      }
      autosave.markSubmitted();
      props.onSuccess(data.workItemId);
    } else {
      const { error } = await updateProjectWorkItem({
        path: { projectId: props.projectId, workItemId: props.initial.workItemId },
        body: {
          title: form.title.trim(),
          description: form.description.trim() || null,
          status: form.status,
          priority: form.priority,
          type: form.type,
          labels: form.labels,
          // Null the reason when nothing else changed — the backend rejects a reason-only save,
          // and the field is already disabled in that state.
          reason: hasOtherChanges ? form.reason.trim() || null : null,
          blockedReason: form.blockedReason.trim() || null,
          assignedToUserId: form.assignedToUserId,
          // Omit attachmentIds (rather than send []) when the set is unknown, so a failed preload
          // doesn't silently drop every existing attachment.
          ...(attachmentsKnown
            ? { attachmentIds: attachments.map((attachment) => attachment.id) }
            : {}),
          expectedVersionId: props.initial.id,
        },
      });
      if (error) {
        setMutation({
          type: "error",
          message: mutationErrorMessage(error, "Could not save this work item. Please try again."),
        });
        return;
      }
      autosave.markSubmitted();
      props.onSuccess(props.initial.workItemId);
    }
  }

  // Two fields are never offered by the edit-mode adder: the version-change `reason` is rendered
  // always-visible (below), and `intents` has no edit field at all — its picker renders only in
  // create mode (links on existing work are managed on the detail page). Offering either would show
  // an adder button that reveals nothing.
  const hiddenAdderFields = ADDER_FIELDS.filter(
    (field) =>
      !revealed.has(field.key) && !(isEdit && (field.key === "reason" || field.key === "intents"))
  );

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <ComposerContextHeader mode={isEdit ? "edit" : "create"} projectName={props.projectName} />

      {autosave.restored && (
        <DraftRestoredNotice
          stale={autosave.stale}
          onDiscard={handleDiscard}
          disabled={isSubmitting}
        />
      )}

      <ComposerField
        htmlFor="wi-title"
        label="Title"
        required
        helper={
          <>
            Reference other intent and work inline with <span className="font-mono">IN-</span> /{" "}
            <span className="font-mono">WO-</span> / <span className="font-mono">#N</span>.
          </>
        }
      >
        <Input
          id="wi-title"
          value={form.title}
          placeholder="What needs doing?"
          required
          maxLength={TEXT_LIMITS.name}
          disabled={isSubmitting}
          onChange={(event) => setField("title", event.target.value)}
        />
      </ComposerField>

      {!isEdit && revealed.has("intents") && (
        <ComposerField
          htmlFor="wi-intents"
          label="Linked intent"
          helper="Pin the intent this work makes true. Link several at once — each is version-pinned at creation."
        >
          <IntentLinkPicker
            id="wi-intents"
            projectId={props.projectId}
            values={form.intents}
            disabled={isSubmitting}
            onChange={(value) => setField("intents", value)}
          />
        </ComposerField>
      )}

      {revealed.has("type") && (
        <ComposerField htmlFor="wi-type" label="Type" helper={TYPE_DESCRIPTIONS[form.type]}>
          <ComposerSelect
            id="wi-type"
            value={form.type}
            options={TYPE_OPTIONS}
            disabled={isSubmitting}
            onValueChange={(value) => setField("type", value)}
          />
        </ComposerField>
      )}

      {revealed.has("priority") && (
        <ComposerField htmlFor="wi-priority" label="Priority">
          <ComposerSelect
            id="wi-priority"
            value={form.priority}
            options={PRIORITY_OPTIONS}
            disabled={isSubmitting}
            onValueChange={(value) => setField("priority", value)}
          />
        </ComposerField>
      )}

      {revealed.has("labels") && (
        <ComposerField
          htmlFor="wi-labels"
          label="Labels"
          helper="Lightweight tags for your team's own grouping — reuse existing ones where you can."
        >
          <LabelChipInput
            id="wi-labels"
            values={form.labels}
            projectId={props.projectId}
            disabled={isSubmitting}
            onChange={(value) => setField("labels", value)}
          />
        </ComposerField>
      )}

      {revealed.has("description") && (
        <ComposerField htmlFor="wi-description" label="Description">
          <Textarea
            id="wi-description"
            value={form.description}
            placeholder="Add detail, context, or links."
            rows={3}
            maxLength={TEXT_LIMITS.description}
            disabled={isSubmitting}
            onChange={(event) => setField("description", event.target.value)}
          />
        </ComposerField>
      )}

      {revealed.has("status") && (
        <ComposerField htmlFor="wi-status" label="Status">
          <ComposerSelect
            id="wi-status"
            value={form.status}
            options={STATUS_OPTIONS}
            disabled={isSubmitting}
            onValueChange={(value) => setField("status", value)}
          />
        </ComposerField>
      )}

      {/* Reason — the per-version changelog comment. Always visible in edit (a strong nudge to
          record why a new version is created); revealable via the adder in create. */}
      {(isEdit || revealed.has("reason")) && (
        <ComposerField
          htmlFor="wi-reason"
          label={isEdit ? "Reason for this change" : "Reason"}
          helper={
            isEdit
              ? hasOtherChanges
                ? "Describe why this version is being saved. It appears in the version history."
                : "Add a reason once you've made a change."
              : undefined
          }
        >
          <Textarea
            id="wi-reason"
            value={form.reason}
            placeholder={isEdit ? "What changed and why?" : "Why is this work needed?"}
            rows={2}
            maxLength={TEXT_LIMITS.reason}
            disabled={isSubmitting || (isEdit && !hasOtherChanges)}
            onChange={(event) => setField("reason", event.target.value)}
          />
        </ComposerField>
      )}

      {revealed.has("blockedReason") && (
        <ComposerField htmlFor="wi-blocked-reason" label="Blocked reason">
          <Input
            id="wi-blocked-reason"
            value={form.blockedReason}
            placeholder="What is blocking this item?"
            maxLength={TEXT_LIMITS.reason}
            disabled={isSubmitting}
            onChange={(event) => setField("blockedReason", event.target.value)}
          />
        </ComposerField>
      )}

      {revealed.has("assignee") && (
        <ComposerField htmlFor="wi-assignee" label="Assignee">
          <Select
            value={form.assignedToUserId ?? UNASSIGNED_SENTINEL}
            onValueChange={(next) =>
              setField("assignedToUserId", next === UNASSIGNED_SENTINEL ? null : next)
            }
            disabled={isSubmitting}
          >
            <SelectTrigger id="wi-assignee" className="w-full">
              <SelectValue>
                {assignedMember ? (
                  <>
                    <UserAvatar
                      userId={assignedMember.userId}
                      name={assignedMember.displayName}
                      email={assignedMember.email}
                      size="default"
                      className="size-5"
                    />
                    <span className="truncate">{assigneeDisplayValue}</span>
                  </>
                ) : (
                  assigneeDisplayValue
                )}
              </SelectValue>
            </SelectTrigger>
            <SelectContent align="start">
              <SelectItem value={UNASSIGNED_SENTINEL}>Unassigned</SelectItem>
              {props.teamMembers.map((member) => (
                <SelectItem key={member.userId} value={member.userId}>
                  <UserAvatar
                    userId={member.userId}
                    name={member.displayName}
                    email={member.email}
                    size="default"
                    className="size-5"
                  />
                  {member.displayName ?? member.email}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </ComposerField>
      )}

      {/* Images — uploaded immediately and referenced by id. Especially useful as bug evidence
          (screenshots, repro steps). Added and removed per version. */}
      <ComposerField
        htmlFor="wi-images"
        label="Images"
        helper="Optional — attach screenshots or evidence. Added and removed per version."
      >
        <div id="wi-images">
          <AttachmentUploader
            projectId={props.projectId}
            attachments={attachments}
            disabled={isSubmitting}
            onChange={setAttachments}
          />
        </div>
      </ComposerField>

      <AdderRow fields={hiddenAdderFields} onReveal={reveal} disabled={isSubmitting} />

      {isEdit && (
        <div className="border-border bg-muted rounded-md border px-4 py-3">
          <p className="text-muted-foreground text-xs">
            Saving creates a new version — the full history is preserved and this action cannot be
            undone.
          </p>
        </div>
      )}

      {mutation.type === "error" && <p className="text-destructive text-xs">{mutation.message}</p>}

      <div
        className={cn(
          "flex items-center gap-3",
          props.surface === "dialog" && "bg-muted/50 -mx-4 -mb-4 rounded-b-xl border-t p-4"
        )}
      >
        <Button type="submit" disabled={isSubmitting || !canSubmit || (isEdit && !hasOtherChanges)}>
          {isSubmitting
            ? isEdit
              ? "Saving…"
              : "Creating…"
            : isEdit
              ? "Save new version"
              : "Create work item"}
        </Button>
        <Button type="button" variant="outline" disabled={isSubmitting} onClick={props.onCancel}>
          Cancel
        </Button>
        {autosave.saving && <span className="text-muted-foreground text-xs">Saving draft…</span>}
      </div>
    </form>
  );
}
