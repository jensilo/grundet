"use client";

import { useState } from "react";

import { AttachmentUploader } from "@/components/app/attachment-uploader";
import { AcceptanceCriteriaEditor } from "@/components/app/composer/acceptance-criteria-editor";
import { ComposerContextHeader } from "@/components/app/composer/composer-context-header";
import { AdderRow, ComposerField, ComposerSelect } from "@/components/app/composer/composer-fields";
import { DraftRestoredNotice } from "@/components/app/composer/draft-restored-notice";
import { FeaturePicker } from "@/components/app/composer/feature-picker";
import { LabelChipInput } from "@/components/app/composer/label-chip-input";
import { useDraftAutosave } from "@/components/app/composer/use-draft-autosave";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import {
  createRequirement,
  deleteNewRequirementDraft,
  deleteRequirementDraft,
  getNewRequirementDraft,
  getRequirementDraft,
  saveProjectRequirement,
  upsertNewRequirementDraft,
  upsertRequirementDraft,
} from "@/lib/api/generated/sdk.gen";
import type {
  AttachmentResponse,
  RequirementPriority,
  RequirementResponse,
  RequirementStatus,
} from "@/lib/api/generated/types.gen";
import { mutationErrorMessage } from "@/lib/api/mutation-error";
import { TEXT_LIMITS } from "@/lib/text-limits";
import { cn } from "@/lib/utils";
import {
  acceptanceCriteriaEqual,
  idsEqualIgnoringOrder,
  labelsEqualIgnoringOrder,
  trimmedEqual,
} from "@/lib/version-change-comparison";
import { notifyAffectedWork } from "@/lib/work-item-notices";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const SCHEMA_VERSION = 4; // bumped: form shape changed — featureName added alongside featureId
const MIN_TEXT_LENGTH = 3;

const STATUS_OPTIONS: readonly { value: RequirementStatus; label: string }[] = [
  { value: "Draft", label: "Draft" },
  { value: "Active", label: "Active" },
  { value: "Archived", label: "Archived" },
  { value: "WontDo", label: "Won't Do" },
];

const PRIORITY_OPTIONS: readonly { value: RequirementPriority; label: string }[] = [
  { value: "Must", label: "Must" },
  { value: "Should", label: "Should" },
  { value: "Could", label: "Could" },
  { value: "Wont", label: "Won't" },
];

type OptionalField = "priority" | "labels" | "acceptanceCriteria" | "status";

const ADDER_FIELDS: readonly { key: OptionalField; label: string }[] = [
  { key: "priority", label: "priority" },
  { key: "labels", label: "labels" },
  { key: "acceptanceCriteria", label: "acceptance criteria" },
  { key: "status", label: "status" },
];

// ---------------------------------------------------------------------------
// Form payload (also the draft shape, owned by the frontend)
// ---------------------------------------------------------------------------

type RequirementForm = {
  text: string;
  status: RequirementStatus;
  priority: RequirementPriority;
  labels: string[];
  acceptanceCriteria: string[];
  // Durable product truth — "why does this intent matter?" Included in search vector.
  rationale: string;
  // Per-version changelog comment — only collected at edit time, never at creation.
  reason: string;
  // The feature this intent belongs to. Changing it moves the intent to another feature (WP5).
  featureId: string;
  // Display name of the selected feature — kept in the form so the async picker and context header
  // render the right label after a draft restore without re-fetching it.
  featureName: string;
};

function emptyForm(): RequirementForm {
  return {
    text: "",
    status: "Draft",
    priority: "Should",
    labels: [],
    acceptanceCriteria: [],
    rationale: "",
    reason: "",
    featureId: "", // set from the active feature at construction
    featureName: "",
  };
}

function formFromEntity(entity: RequirementResponse, featureName: string): RequirementForm {
  return {
    text: entity.text,
    status: entity.status,
    priority: entity.priority,
    labels: entity.labels,
    acceptanceCriteria: entity.acceptanceCriteria,
    rationale: entity.rationale ?? "",
    reason: "", // always starts empty in edit — user provides the per-version comment
    featureId: entity.featureId,
    featureName,
  };
}

function deriveRevealed(form: RequirementForm): OptionalField[] {
  const revealed: OptionalField[] = [];
  if (form.priority !== "Should") revealed.push("priority");
  if (form.labels.length > 0) revealed.push("labels");
  if (form.acceptanceCriteria.length > 0) revealed.push("acceptanceCriteria");
  if (form.status !== "Draft") revealed.push("status");
  return revealed;
}

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

type RequirementComposerProps = {
  projectId: string;
  projectName: string;
  surface: "page" | "dialog";
  onCancel: () => void;
  onSuccess: (requirementId: string) => void;
  // The currently-selected feature (active feature in create, the intent's feature in edit). Its
  // name renders immediately; other features are searched on demand.
  currentFeature: { id: string; name: string };
  // Total features in the project — the picker is only shown when there is more than one.
  featureCount: number;
} & (
  | { mode: "create"; featureId: string }
  | {
      mode: "edit";
      initial: RequirementResponse;
      initialAttachments?: AttachmentResponse[];
      // Whether the current attachment set was successfully loaded. When false (preload failed) the
      // composer cannot tell "no attachments" from "unknown", so it omits attachmentIds from the
      // save payload entirely — the backend then preserves the existing set rather than dropping it.
      attachmentsLoaded?: boolean;
    }
);

type MutationState = { type: "idle" } | { type: "submitting" } | { type: "error"; message: string };

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function RequirementComposer(props: RequirementComposerProps): React.JSX.Element {
  const isEdit = props.mode === "edit";
  const [baseline] = useState<RequirementForm>(() =>
    props.mode === "edit"
      ? formFromEntity(props.initial, props.currentFeature.name)
      : { ...emptyForm(), featureId: props.featureId, featureName: props.currentFeature.name }
  );

  const [form, setForm] = useState<RequirementForm>(baseline);
  // Reveal only the optional fields that hold non-default/filled values — identical in create and
  // edit. Empty optionals stay hidden behind the adder until the user adds them.
  const [revealed, setRevealed] = useState<Set<OptionalField>>(
    () => new Set(deriveRevealed(baseline))
  );
  const [mutation, setMutation] = useState<MutationState>({ type: "idle" });
  const [textError, setTextError] = useState<string | null>(null);
  // Attachments are uploaded immediately and tracked outside the draft form: an in-progress upload
  // is intentionally not draft-persisted (orphaned blobs are accepted, see ADR-010). The baseline is
  // the original set, so Discard can restore it alongside the form fields.
  const [baselineAttachments] = useState<AttachmentResponse[]>(() =>
    props.mode === "edit" ? (props.initialAttachments ?? []) : []
  );
  const [attachments, setAttachments] = useState<AttachmentResponse[]>(baselineAttachments);

  const isSubmitting = mutation.type === "submitting";
  const isDirty = JSON.stringify(form) !== JSON.stringify(baseline);
  const canSubmit = form.text.trim().length >= MIN_TEXT_LENGTH;

  // A failed attachment preload leaves the current set unknowable, so it is excluded from the
  // change comparison below (and omitted from the save payload).
  const attachmentsKnown = props.mode === "edit" ? props.attachmentsLoaded !== false : true;
  // Whether anything other than `reason` differs from the saved baseline.
  const hasOtherChanges =
    !trimmedEqual(form.text, baseline.text) ||
    form.status !== baseline.status ||
    form.priority !== baseline.priority ||
    !labelsEqualIgnoringOrder(form.labels, baseline.labels) ||
    !acceptanceCriteriaEqual(form.acceptanceCriteria, baseline.acceptanceCriteria) ||
    !trimmedEqual(form.rationale, baseline.rationale) ||
    form.featureId !== baseline.featureId ||
    (attachmentsKnown &&
      !idsEqualIgnoringOrder(
        attachments.map((attachment) => attachment.id),
        baselineAttachments.map((attachment) => attachment.id)
      ));

  // Context-header feature name — tracks the live feature selection in both create and edit.
  const featureName = form.featureName;

  function setField<K extends keyof RequirementForm>(key: K, value: RequirementForm[K]): void {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function reveal(key: OptionalField): void {
    setRevealed((current) => new Set(current).add(key));
  }

  // Draft autosave/restore/discard.
  const draftIo =
    props.mode === "create"
      ? {
          load: async () => {
            const { data, error } = await getNewRequirementDraft({
              path: { featureId: props.featureId },
            });
            // Throw on a failed load so the hook keeps autosave disabled rather than treating
            // the failure as "no draft" and overwriting an existing server draft.
            if (error) throw error;
            return data?.draft?.draftContent ?? null;
          },
          save: async (content: unknown) => {
            await upsertNewRequirementDraft({
              path: { featureId: props.featureId },
              body: { draftContent: content },
            });
          },
          remove: async () => {
            await deleteNewRequirementDraft({ path: { featureId: props.featureId } });
          },
        }
      : {
          load: async () => {
            const { data, error } = await getRequirementDraft({
              path: { requirementId: props.initial.requirementId },
            });
            // Throw on a failed load so the hook keeps autosave disabled rather than treating
            // the failure as "no draft" and overwriting an existing server draft.
            if (error) throw error;
            return data?.draft?.draftContent ?? null;
          },
          save: async (content: unknown) => {
            await upsertRequirementDraft({
              path: { requirementId: props.initial.requirementId },
              body: { draftContent: content },
            });
          },
          remove: async () => {
            await deleteRequirementDraft({
              path: { requirementId: props.initial.requirementId },
            });
          },
        };

  const autosave = useDraftAutosave<RequirementForm>({
    schemaVersion: SCHEMA_VERSION,
    baseChangedAt: props.mode === "edit" ? props.initial.changedAt : null,
    io: draftIo,
    value: form,
    enabled: isDirty && (isEdit || canSubmit),
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
    setTextError(null);
    await autosave.discard();
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (!canSubmit) {
      setTextError(`Intent text must be at least ${MIN_TEXT_LENGTH} characters.`);
      return;
    }
    // A reason alone is not a change. The Save button is disabled in this state; this guards the
    // Enter-to-submit path so we never send a no-op version.
    if (isEdit && !hasOtherChanges) return;
    setTextError(null);
    setMutation({ type: "submitting" });

    if (props.mode === "create") {
      const { data, error } = await createRequirement({
        path: { featureId: form.featureId },
        body: {
          text: form.text.trim(),
          status: form.status,
          priority: form.priority,
          labels: form.labels.length > 0 ? form.labels : null,
          acceptanceCriteria: form.acceptanceCriteria.length > 0 ? form.acceptanceCriteria : null,
          rationale: form.rationale.trim() || null,
          attachmentIds: attachments.map((attachment) => attachment.id),
        },
      });
      if (error || !data) {
        setMutation({
          type: "error",
          message: mutationErrorMessage(error, "Could not create this intent. Please try again."),
        });
        return;
      }
      autosave.markSubmitted();
      props.onSuccess(data.requirement.requirementId);
    } else {
      // Edit always sends every field — the backend writes a new version row regardless.
      // targetFeatureId is only sent when the user picked a different feature (a move).
      const movedFeature = form.featureId !== props.initial.featureId;
      const { data, error } = await saveProjectRequirement({
        path: { projectId: props.projectId, requirementId: props.initial.requirementId },
        body: {
          text: form.text.trim(),
          status: form.status,
          priority: form.priority,
          labels: form.labels,
          acceptanceCriteria: form.acceptanceCriteria,
          rationale: form.rationale.trim() || null,
          // Null the reason when nothing else changed — the backend rejects a reason-only save,
          // and the field is already disabled in that state.
          reason: hasOtherChanges ? form.reason.trim() || null : null,
          // Omit attachmentIds (rather than send []) when the set is unknown, so a failed preload
          // doesn't silently drop every existing attachment.
          ...(attachmentsKnown
            ? { attachmentIds: attachments.map((attachment) => attachment.id) }
            : {}),
          ...(movedFeature ? { targetFeatureId: form.featureId } : {}),
          expectedVersionId: props.initial.id,
        },
      });
      if (error) {
        setMutation({
          type: "error",
          message: mutationErrorMessage(error, "Could not save this intent. Please try again."),
        });
        return;
      }
      autosave.markSubmitted();
      notifyAffectedWork(data?.workItemNotices);
      props.onSuccess(props.initial.requirementId);
    }
  }

  const hiddenAdderFields = ADDER_FIELDS.filter((field) => !revealed.has(field.key));

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <ComposerContextHeader
        mode={isEdit ? "edit" : "create"}
        projectName={props.projectName}
        featureName={featureName}
      />

      {autosave.restored && (
        <DraftRestoredNotice
          stale={autosave.stale}
          onDiscard={handleDiscard}
          disabled={isSubmitting}
        />
      )}

      <ComposerField
        htmlFor="req-text"
        label="Intent text"
        required
        helper={
          <>
            Small and testable — one statement of product truth. Reference other intent and work
            inline with <span className="font-mono">IN-</span> /{" "}
            <span className="font-mono">WO-</span> / <span className="font-mono">#N</span>.
          </>
        }
        error={textError}
      >
        <Textarea
          id="req-text"
          placeholder="Describe the intent in one testable statement."
          value={form.text}
          onChange={(event) => {
            setField("text", event.target.value);
            setTextError(null);
          }}
          required
          maxLength={TEXT_LIMITS.requirementText}
          rows={4}
          disabled={isSubmitting}
        />
      </ComposerField>

      {/* Rationale — always visible; durable product truth captured at creation and preserved across versions. */}
      <ComposerField
        htmlFor="req-rationale"
        label="Because"
        helper="Optional — why does this intent matter to the product?"
      >
        <Textarea
          id="req-rationale"
          placeholder="Explain why this intent matters to the product."
          value={form.rationale}
          onChange={(event) => setField("rationale", event.target.value)}
          maxLength={TEXT_LIMITS.description}
          rows={2}
          disabled={isSubmitting}
        />
      </ComposerField>

      {/* Images — uploaded immediately and referenced by id. An image can stand in as (or
          alongside) the spec/chart the intent describes. */}
      <ComposerField
        htmlFor="req-images"
        label="Images"
        helper="Optional — attach specs, charts, or mockups. Added and removed per version."
      >
        <div id="req-images">
          <AttachmentUploader
            projectId={props.projectId}
            attachments={attachments}
            disabled={isSubmitting}
            onChange={setAttachments}
          />
        </div>
      </ComposerField>

      {/* Feature picker — in create it chooses where the intent lands; in edit, changing it
          re-homes the intent under another feature in the same project while keeping one
          continuous version history (WP5). Hidden when the project has a single feature. */}
      {props.featureCount > 1 && (
        <ComposerField
          htmlFor="req-feature"
          label="Feature"
          helper={
            isEdit
              ? "Moving to another feature keeps the full version history — it stays one continuous changelog."
              : "Which feature this intent belongs to."
          }
        >
          <FeaturePicker
            id="req-feature"
            projectId={props.projectId}
            value={{ id: form.featureId, name: form.featureName }}
            disabled={isSubmitting}
            onChange={(feature) => {
              // The composer always has a selected feature — no clear entry, so null never fires.
              if (!feature) return;
              setForm((current) => ({
                ...current,
                featureId: feature.id,
                featureName: feature.name,
              }));
            }}
          />
        </ComposerField>
      )}

      {revealed.has("priority") && (
        <ComposerField htmlFor="req-priority" label="Priority">
          <ComposerSelect
            id="req-priority"
            value={form.priority}
            options={PRIORITY_OPTIONS}
            disabled={isSubmitting}
            onValueChange={(value) => setField("priority", value)}
          />
        </ComposerField>
      )}

      {revealed.has("labels") && (
        <ComposerField
          htmlFor="req-labels"
          label="Labels"
          helper="Lightweight tags for your team's own grouping — reuse existing ones where you can."
        >
          <LabelChipInput
            id="req-labels"
            values={form.labels}
            projectId={props.projectId}
            disabled={isSubmitting}
            onChange={(value) => setField("labels", value)}
          />
        </ComposerField>
      )}

      {revealed.has("acceptanceCriteria") && (
        <ComposerField
          htmlFor="req-criteria"
          label="Acceptance criteria"
          helper="One testable statement per row."
        >
          <AcceptanceCriteriaEditor
            id="req-criteria"
            values={form.acceptanceCriteria}
            disabled={isSubmitting}
            onChange={(value) => setField("acceptanceCriteria", value)}
          />
        </ComposerField>
      )}

      {revealed.has("status") && (
        <ComposerField htmlFor="req-status" label="Status">
          <ComposerSelect
            id="req-status"
            value={form.status}
            options={STATUS_OPTIONS}
            disabled={isSubmitting}
            onValueChange={(value) => setField("status", value)}
          />
        </ComposerField>
      )}

      <AdderRow fields={hiddenAdderFields} onReveal={reveal} disabled={isSubmitting} />

      {/* Reason for this change — edit mode only; per-version changelog comment, not collected at creation. */}
      {isEdit && (
        <ComposerField
          htmlFor="req-reason"
          label="Reason for this change"
          helper={
            hasOtherChanges
              ? "Describe why this version is being saved. It appears in the version history."
              : "Add a reason once you've made a change."
          }
        >
          <Textarea
            id="req-reason"
            placeholder="What changed and why?"
            value={form.reason}
            onChange={(event) => setField("reason", event.target.value)}
            maxLength={TEXT_LIMITS.reason}
            rows={2}
            disabled={isSubmitting || !hasOtherChanges}
          />
        </ComposerField>
      )}

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
              : "Create intent"}
        </Button>
        <Button type="button" variant="outline" disabled={isSubmitting} onClick={props.onCancel}>
          Cancel
        </Button>
        {autosave.saving && <span className="text-muted-foreground text-xs">Saving draft…</span>}
      </div>
    </form>
  );
}
