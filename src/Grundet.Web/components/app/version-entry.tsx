"use client";

import { ChevronDown, ChevronRight } from "lucide-react";

import { DiffBlock } from "@/components/app/diff-block";
import { ReferenceText } from "@/components/app/reference-text";
import { UserAvatar } from "@/components/app/user-avatar";
import type {
  RequirementVersionChanges,
  RequirementVersionEntry,
  WorkItemVersionChanges,
  WorkItemVersionEntry,
} from "@/lib/api/generated/types.gen";
import { relativeDate } from "@/lib/format-date";
import { cn } from "@/lib/utils";

export type AnyVersionEntry = RequirementVersionEntry | WorkItemVersionEntry;

interface VersionEntryProps {
  version: AnyVersionEntry;
  projectId: string;
  expanded: boolean;
  onToggle: () => void;
  isLatest: boolean;
  isLast: boolean;
}

function isWorkChanges(
  changes: RequirementVersionChanges | WorkItemVersionChanges
): changes is WorkItemVersionChanges {
  return "title" in changes;
}

const changeVerbs: Record<string, string> = {
  Created: "created this",
  Updated: "made changes",
  Deleted: "deleted this",
};

function FieldChip({ label, before, after }: { label: string; before: string; after: string }) {
  return (
    <span className="border-border bg-muted text-muted-foreground inline-flex items-center gap-1.5 rounded-sm border px-2 py-0.5">
      {label}:<span className="text-muted-foreground font-medium line-through">{before}</span>
      <span className="text-muted-foreground/60">→</span>
      <span className="text-foreground font-medium">{after}</span>
    </span>
  );
}

function RequirementChangesSection({
  changes,
  projectId,
}: {
  changes: RequirementVersionChanges;
  projectId: string;
}) {
  const hasScalars = !!(changes.status || changes.priority || changes.feature);

  const otherFields: string[] = [];
  if (changes.acceptanceCriteria) otherFields.push("Acceptance criteria");
  if (changes.labels) otherFields.push("Labels");
  if (changes.attachments) otherFields.push("Image(s)");

  return (
    <>
      {changes.text && (
        <DiffBlock projectId={projectId} before={changes.text.before} after={changes.text.after} />
      )}
      {changes.rationale && (
        <div className="space-y-1">
          <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
            Because
          </p>
          <DiffBlock
            projectId={projectId}
            before={changes.rationale.before}
            after={changes.rationale.after}
          />
        </div>
      )}
      {hasScalars && (
        <div className="flex flex-wrap items-center gap-2 text-xs">
          {changes.status && (
            <FieldChip label="Status" before={changes.status.before} after={changes.status.after} />
          )}
          {changes.priority && (
            <FieldChip
              label="Priority"
              before={changes.priority.before}
              after={changes.priority.after}
            />
          )}
          {changes.feature && (
            <FieldChip
              label="Feature"
              before={changes.feature.before}
              after={changes.feature.after}
            />
          )}
        </div>
      )}
      {otherFields.length > 0 && (
        <p className="text-muted-foreground text-xs">{otherFields.join(", ")} changed.</p>
      )}
    </>
  );
}

function WorkChangesSection({
  changes,
  projectId,
}: {
  changes: WorkItemVersionChanges;
  projectId: string;
}) {
  const hasScalars = !!(
    changes.status ||
    changes.priority ||
    changes.type ||
    changes.assignedToUserId
  );

  const otherFields: string[] = [];
  if (changes.description) otherFields.push("Description");
  if (changes.blockedReason) otherFields.push("Blocked reason");
  if (changes.labels) otherFields.push("Labels");
  if (changes.attachments) otherFields.push("Image(s)");

  return (
    <>
      {changes.title && (
        <DiffBlock
          projectId={projectId}
          before={changes.title.before}
          after={changes.title.after}
        />
      )}
      {hasScalars && (
        <div className="flex flex-wrap items-center gap-2 text-xs">
          {changes.status && (
            <FieldChip label="Status" before={changes.status.before} after={changes.status.after} />
          )}
          {changes.priority && (
            <FieldChip
              label="Priority"
              before={changes.priority.before}
              after={changes.priority.after}
            />
          )}
          {changes.type && (
            <FieldChip label="Type" before={changes.type.before} after={changes.type.after} />
          )}
          {changes.assignedToUserId && (
            <span className="border-border bg-muted text-muted-foreground inline-flex items-center rounded-sm border px-2 py-0.5">
              Assignment changed
            </span>
          )}
        </div>
      )}
      {otherFields.length > 0 && (
        <p className="text-muted-foreground text-xs">{otherFields.join(", ")} changed.</p>
      )}
    </>
  );
}

export function VersionEntry({
  version,
  projectId,
  expanded,
  onToggle,
  isLatest,
  isLast,
}: VersionEntryProps) {
  const authorName = version.changedByDisplayName ?? "Unknown user";
  const verb = changeVerbs[version.changeType] ?? version.changeType.toLowerCase();

  return (
    <li className="relative pb-1 pl-6 last:pb-0">
      {/* Timeline spine */}
      {!isLast && (
        <span aria-hidden className="bg-border absolute top-4 bottom-0 left-[5px] w-px" />
      )}
      {/* Timeline dot */}
      <span
        aria-hidden
        className={cn(
          "absolute top-[13px] left-0 size-[11px] rounded-full border-2",
          isLatest ? "border-primary bg-accent" : "border-border bg-card"
        )}
      />

      <button
        type="button"
        onClick={onToggle}
        className="group hover:bg-muted flex w-full items-center gap-2 rounded-md px-2 py-2 text-left transition-colors"
      >
        {/*
          20px avatar for this dense meta row. We override the default 32px via `size-5` rather
          than `size="sm"`: the sm/lg variants are higher-specificity `data-[size=...]` selectors
          that a plain className can't beat, whereas the default size is a bare `size-8` that
          tailwind-merge drops in favour of `size-5`.
        */}
        <UserAvatar
          userId={version.changedByUserId}
          name={version.changedByDisplayName}
          size="default"
          className="size-5"
        />
        <span className="min-w-0 flex-1 truncate text-sm">
          <span className="text-foreground font-medium">{authorName}</span>{" "}
          <span className="text-muted-foreground">{verb}</span>
        </span>
        <span className="text-muted-foreground shrink-0 text-xs">
          {relativeDate(version.changedAt)}
        </span>
        <span
          className={cn(
            "shrink-0 rounded-sm px-1.5 py-0.5 font-mono text-[0.6875rem]",
            isLatest ? "bg-accent text-accent-foreground" : "bg-muted text-muted-foreground"
          )}
        >
          v{version.versionNumber}
        </span>
        {expanded ? (
          <ChevronDown className="text-muted-foreground/60 size-3.5 shrink-0" />
        ) : (
          <ChevronRight className="text-muted-foreground/60 size-3.5 shrink-0" />
        )}
      </button>

      {expanded && (
        <div className="space-y-3 px-2 pt-1 pb-3">
          {version.reason && (
            <div className="space-y-1">
              <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
                Reason
              </p>
              <p className="text-foreground text-sm italic">
                <ReferenceText projectId={projectId} text={version.reason} />
              </p>
            </div>
          )}
          {version.changes === null ? (
            !version.reason && (
              <p className="text-muted-foreground text-xs">
                {Number(version.versionNumber) === 1
                  ? "First version – nothing to compare."
                  : "No field-level changes recorded."}
              </p>
            )
          ) : isWorkChanges(version.changes) ? (
            <WorkChangesSection changes={version.changes} projectId={projectId} />
          ) : (
            <RequirementChangesSection changes={version.changes} projectId={projectId} />
          )}
        </div>
      )}
    </li>
  );
}
