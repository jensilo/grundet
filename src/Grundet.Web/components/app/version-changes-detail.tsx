import { ArrowRight } from "lucide-react";
import type * as React from "react";

import { AttachmentImage } from "@/components/app/attachment-image";
import { DiffBlock } from "@/components/app/diff-block";
import { ReferenceText } from "@/components/app/reference-text";
import type {
  AttachmentsDiff,
  CriterionChange,
  LabelsDiff,
  RequirementVersionChanges,
  SimpleFieldDiff,
  WorkItemVersionChanges,
} from "@/lib/api/generated/types.gen";
import { cn } from "@/lib/utils";

type AnyChanges = RequirementVersionChanges | WorkItemVersionChanges;

function isWorkChanges(changes: AnyChanges): changes is WorkItemVersionChanges {
  return "title" in changes;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
        {label}
      </p>
      {children}
    </div>
  );
}

function ScalarDiff({ diff }: { diff: SimpleFieldDiff }) {
  return (
    <p className="flex flex-wrap items-center gap-2 text-sm">
      <span className="text-muted-foreground line-through">{diff.before || "—"}</span>
      <ArrowRight className="text-muted-foreground/60 size-3.5 shrink-0" aria-hidden={true} />
      <span className="text-foreground font-medium">{diff.after || "—"}</span>
    </p>
  );
}

function LabelDiff({ diff }: { diff: LabelsDiff }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {diff.removed.map((label) => (
        <span
          key={`removed-${label}`}
          className="bg-destructive-50 text-destructive-700 inline-flex items-center rounded-sm px-2 py-0.5 text-xs font-medium line-through"
        >
          {label}
        </span>
      ))}
      {diff.added.map((label) => (
        <span
          key={`added-${label}`}
          className="bg-success-50 text-success-700 inline-flex items-center rounded-sm px-2 py-0.5 text-xs font-medium"
        >
          {label}
        </span>
      ))}
    </div>
  );
}

function AttachmentThumb({
  id,
  projectId,
  alt,
  tone,
}: {
  id: string;
  projectId: string;
  alt: string;
  tone: "removed" | "added";
}) {
  return (
    <div
      className={cn(
        "overflow-hidden rounded-md ring-2",
        tone === "removed" ? "ring-destructive-200" : "ring-success-200"
      )}
    >
      <AttachmentImage
        projectId={projectId}
        attachmentId={id}
        alt={alt}
        variant="thumb"
        className={cn("size-20 object-cover", tone === "removed" && "opacity-60 grayscale")}
      />
    </div>
  );
}

function AttachmentGroup({
  label,
  ids,
  projectId,
  alt,
  tone,
}: {
  label: string;
  ids: string[];
  projectId: string;
  alt: string;
  tone: "removed" | "added";
}) {
  return (
    <div className="space-y-1.5">
      <p className="text-muted-foreground text-xs font-medium">{label}</p>
      <div className="flex flex-wrap gap-2">
        {ids.map((id) => (
          <AttachmentThumb key={id} id={id} projectId={projectId} alt={alt} tone={tone} />
        ))}
      </div>
    </div>
  );
}

/**
 * The backend emits unpaired `added`/`removed` id lists. When the counts match we read the change
 * as a replacement and present each as an old → new row (mirroring `ScalarDiff`'s before → after
 * arrow language); otherwise we fall back to clearly-labelled Removed / Added groups.
 */
function AttachmentDiff({ diff, projectId }: { diff: AttachmentsDiff; projectId: string }) {
  const { added, removed } = diff;
  const isReplacement = added.length > 0 && added.length === removed.length;

  if (isReplacement) {
    return (
      <ul className="space-y-2">
        {removed.map((removedId, index) => (
          <li key={`${removedId}→${added[index]}`} className="flex flex-wrap items-center gap-3">
            <AttachmentThumb
              id={removedId}
              projectId={projectId}
              alt="Previous image"
              tone="removed"
            />
            <ArrowRight className="text-muted-foreground/60 size-4 shrink-0" aria-hidden={true} />
            <AttachmentThumb id={added[index]} projectId={projectId} alt="New image" tone="added" />
          </li>
        ))}
      </ul>
    );
  }

  return (
    <div className="space-y-3">
      {removed.length > 0 && (
        <AttachmentGroup
          label="Removed"
          ids={removed}
          projectId={projectId}
          alt="Removed image"
          tone="removed"
        />
      )}
      {added.length > 0 && (
        <AttachmentGroup
          label="Added"
          ids={added}
          projectId={projectId}
          alt="Added image"
          tone="added"
        />
      )}
    </div>
  );
}

const criterionVerb: Record<string, string> = {
  Added: "Added",
  Removed: "Removed",
  Edited: "Edited",
  Moved: "Moved",
};

function CriterionRow({ change, projectId }: { change: CriterionChange; projectId: string }) {
  const verb = criterionVerb[change.type] ?? change.type;

  if (change.type === "Edited" && change.before !== null && change.after !== null) {
    return (
      <li className="flex flex-col gap-1">
        <span className="text-muted-foreground text-xs font-medium">{verb}</span>
        <DiffBlock projectId={projectId} before={change.before} after={change.after} />
      </li>
    );
  }

  return (
    <li className="flex items-baseline gap-2 text-sm">
      <span className="text-muted-foreground shrink-0 text-xs font-medium">{verb}</span>
      <span className="text-foreground min-w-0">
        <ReferenceText projectId={projectId} text={change.after ?? change.before ?? ""} />
      </span>
    </li>
  );
}

/**
 * Full field-level rendering of a single version's changes — every changed
 * field shown in detail, not the collapsed summary used in the inline timeline.
 * Pure render (no hooks); usable from both server and client components.
 */
export function VersionChangesDetail({
  changes,
  projectId,
  isInitial = false,
  reason,
}: {
  changes: AnyChanges | null;
  projectId: string;
  // The very first version has nothing to diff against. A later version with no recorded changes is
  // distinct (a no-op save) — don't conflate the two by keying on `changes === null` alone.
  isInitial?: boolean;
  // The version's own change comment — per-version metadata, never diffed. Rendered as a note
  // independently of `changes`, so a reason-only version still shows it.
  reason?: string | null;
}) {
  const reasonNote = reason ? (
    <Field label="Reason">
      <p className="text-foreground text-sm italic">
        <ReferenceText projectId={projectId} text={reason} />
      </p>
    </Field>
  ) : null;

  if (changes === null) {
    return (
      <div className="space-y-4">
        {reasonNote}
        {!reason && (
          <p className="text-muted-foreground text-sm">
            {isInitial ? "First version – nothing to compare." : "No field-level changes recorded."}
          </p>
        )}
      </div>
    );
  }

  const sections: React.ReactNode[] = [];

  if (isWorkChanges(changes)) {
    if (changes.title) {
      sections.push(
        <Field key="title" label="Title">
          <DiffBlock
            projectId={projectId}
            before={changes.title.before}
            after={changes.title.after}
          />
        </Field>
      );
    }
    if (changes.description) {
      sections.push(
        <Field key="description" label="Description">
          <DiffBlock
            projectId={projectId}
            before={changes.description.before}
            after={changes.description.after}
          />
        </Field>
      );
    }
    if (changes.status) {
      sections.push(
        <Field key="status" label="Status">
          <ScalarDiff diff={changes.status} />
        </Field>
      );
    }
    if (changes.priority) {
      sections.push(
        <Field key="priority" label="Priority">
          <ScalarDiff diff={changes.priority} />
        </Field>
      );
    }
    if (changes.type) {
      sections.push(
        <Field key="type" label="Type">
          <ScalarDiff diff={changes.type} />
        </Field>
      );
    }
    if (changes.assignedToUserId) {
      sections.push(
        <Field key="assignee" label="Assignee">
          <ScalarDiff diff={changes.assignedToUserId} />
        </Field>
      );
    }
    if (changes.blockedReason) {
      sections.push(
        <Field key="blocked" label="Blocked reason">
          <DiffBlock
            projectId={projectId}
            before={changes.blockedReason.before}
            after={changes.blockedReason.after}
          />
        </Field>
      );
    }
    if (changes.labels) {
      sections.push(
        <Field key="labels" label="Labels">
          <LabelDiff diff={changes.labels} />
        </Field>
      );
    }
    if (changes.attachments) {
      sections.push(
        <Field key="images" label="Image(s)">
          <AttachmentDiff diff={changes.attachments} projectId={projectId} />
        </Field>
      );
    }
  } else {
    if (changes.text) {
      sections.push(
        <Field key="text" label="Text">
          <DiffBlock
            projectId={projectId}
            before={changes.text.before}
            after={changes.text.after}
          />
        </Field>
      );
    }
    if (changes.rationale) {
      sections.push(
        <Field key="rationale" label="Because">
          <DiffBlock
            projectId={projectId}
            before={changes.rationale.before}
            after={changes.rationale.after}
          />
        </Field>
      );
    }
    if (changes.status) {
      sections.push(
        <Field key="status" label="Status">
          <ScalarDiff diff={changes.status} />
        </Field>
      );
    }
    if (changes.priority) {
      sections.push(
        <Field key="priority" label="Priority">
          <ScalarDiff diff={changes.priority} />
        </Field>
      );
    }
    if (changes.feature) {
      sections.push(
        <Field key="feature" label="Feature">
          <ScalarDiff diff={changes.feature} />
        </Field>
      );
    }
    if (changes.acceptanceCriteria) {
      sections.push(
        <Field key="acceptance" label="Acceptance criteria">
          <ul className="space-y-2">
            {changes.acceptanceCriteria.changes.map((change, index) => (
              // Criterion changes are positional with no stable id — index key is correct here.
              // eslint-disable-next-line react/no-array-index-key
              <CriterionRow key={`${change.type}-${index}`} change={change} projectId={projectId} />
            ))}
          </ul>
        </Field>
      );
    }
    if (changes.labels) {
      sections.push(
        <Field key="labels" label="Labels">
          <LabelDiff diff={changes.labels} />
        </Field>
      );
    }
    if (changes.attachments) {
      sections.push(
        <Field key="images" label="Image(s)">
          <AttachmentDiff diff={changes.attachments} projectId={projectId} />
        </Field>
      );
    }
  }

  if (sections.length === 0) {
    return (
      <div className="space-y-4">
        {reasonNote}
        <p className="text-muted-foreground text-sm">No field-level changes recorded.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {reasonNote}
      {sections}
    </div>
  );
}
