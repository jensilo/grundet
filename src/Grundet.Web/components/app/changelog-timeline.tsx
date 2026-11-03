"use client";

import Link from "next/link";
import { useState } from "react";

import { LocalDayLabel } from "@/components/app/local-date";
import { UserAvatar } from "@/components/app/user-avatar";
import { type DiffDialogTarget, VersionDiffDialog } from "@/components/app/version-diff-dialog";
import type { ChangelogEntryResponse } from "@/lib/api/generated/types.gen";
import {
  type ChangeTone,
  changeTypeLabel,
  changeTypeTone,
  ENTITY_FALLBACK_ICON,
  ENTITY_ICONS,
  groupEntriesByDay,
  isExpandableKind,
} from "@/lib/changelog";
import { relativeDate } from "@/lib/format-date";
import { cn } from "@/lib/utils";

const toneClasses: Record<ChangeTone, string> = {
  success: "bg-success-50 text-success-700",
  destructive: "bg-destructive-50 text-destructive-700",
  neutral: "bg-muted text-muted-foreground",
};

function requirementHref(projectId: string, logicalId: string): string {
  return `/projects/${projectId}/requirements/${logicalId}`;
}

function workItemHref(projectId: string, logicalId: string): string {
  return `/projects/${projectId}/work-items/${logicalId}`;
}

// Href for the row's title. Intent/Work rows address their own item; Link rows
// (Linked/Unlinked) carry the requirement they connect, so the title (IN-…) links there.
function titleHref(projectId: string, entry: ChangelogEntryResponse): string | null {
  if (entry.entityKind === "Intent") {
    return requirementHref(projectId, entry.logicalId);
  }
  if (entry.entityKind === "Work") {
    return workItemHref(projectId, entry.logicalId);
  }
  if (entry.entityKind === "Link" && entry.linkedRequirementId) {
    return requirementHref(projectId, entry.linkedRequirementId);
  }
  return null;
}

// Href for the mono reference chip. Intent/Work rows point it at their own item; Link rows
// (Linked/Unlinked) point it at the work item end (WO-…), distinct from the title's IN-… link.
function referenceHref(projectId: string, entry: ChangelogEntryResponse): string | null {
  if (entry.entityKind === "Intent") {
    return requirementHref(projectId, entry.logicalId);
  }
  if (entry.entityKind === "Work") {
    return workItemHref(projectId, entry.logicalId);
  }
  if (entry.entityKind === "Link" && entry.linkedWorkItemId) {
    return workItemHref(projectId, entry.linkedWorkItemId);
  }
  return null;
}

function ChangelogRow({
  entry,
  projectId,
  onInspect,
}: {
  entry: ChangelogEntryResponse;
  projectId: string;
  onInspect: (target: DiffDialogTarget) => void;
}): React.JSX.Element {
  const Icon = ENTITY_ICONS[entry.entityKind] ?? ENTITY_FALLBACK_ICON;
  const href = titleHref(projectId, entry);
  const refHref = referenceHref(projectId, entry);
  const expandableKind = isExpandableKind(entry.entityKind) ? entry.entityKind : null;

  return (
    <li className="flex items-start gap-3 py-3">
      <span
        aria-hidden={true}
        className="bg-muted text-muted-foreground mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full"
      >
        <Icon className="size-4" />
      </span>

      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <span
            className={cn(
              "inline-flex items-center rounded-sm px-1.5 py-0.5 text-xs font-medium",
              toneClasses[changeTypeTone(entry.changeType)]
            )}
          >
            {changeTypeLabel(entry.changeType)}
          </span>
          {entry.reference &&
            (refHref ? (
              <Link
                href={refHref}
                className="text-primary hover:text-primary/80 font-mono text-xs font-medium transition-colors"
              >
                {entry.reference}
              </Link>
            ) : (
              <span className="text-primary font-mono text-xs font-medium">{entry.reference}</span>
            ))}
          {href ? (
            <Link
              href={href}
              className="text-foreground hover:text-primary min-w-0 truncate text-sm font-medium transition-colors"
            >
              {entry.title}
            </Link>
          ) : (
            <span className="text-foreground min-w-0 truncate text-sm font-medium">
              {entry.title}
            </span>
          )}
        </div>
        <div className="text-muted-foreground flex items-center gap-1.5 text-xs">
          <UserAvatar
            userId={entry.changedByUserId}
            name={entry.changedByName}
            size="default"
            className="size-5"
          />
          <p className="min-w-0 truncate">
            {entry.changedByName ?? "Unknown member"}
            {entry.changedByName && !entry.changedByIsActiveMember && (
              <span className="text-muted-foreground/80 italic"> · former member</span>
            )}{" "}
            · {relativeDate(entry.changedAt)}
          </p>
        </div>
      </div>

      {expandableKind && (
        <button
          type="button"
          onClick={() =>
            onInspect({
              entityKind: expandableKind,
              logicalId: entry.logicalId,
              versionId: entry.entryId,
              reference: entry.reference,
              title: entry.title,
              changedByIsActiveMember: entry.changedByIsActiveMember,
            })
          }
          className="border-border text-foreground hover:bg-muted shrink-0 rounded-md border px-2 py-1 text-xs font-medium transition-colors"
        >
          See changes
        </button>
      )}
    </li>
  );
}

export function ChangelogTimeline({
  entries,
  projectId,
}: {
  entries: ChangelogEntryResponse[];
  projectId: string;
}): React.JSX.Element {
  const [target, setTarget] = useState<DiffDialogTarget | null>(null);
  const groups = groupEntriesByDay(entries);

  return (
    <>
      <div className="space-y-6">
        {groups.map((group) => (
          <section key={group.key}>
            <h2 className="text-muted-foreground mb-1 text-xs font-medium tracking-[0.04em] uppercase">
              <LocalDayLabel iso={group.iso} />
            </h2>
            <ul className="divide-border divide-y">
              {group.entries.map((entry) => (
                <ChangelogRow
                  key={entry.entryId}
                  entry={entry}
                  projectId={projectId}
                  onInspect={setTarget}
                />
              ))}
            </ul>
          </section>
        ))}
      </div>

      <VersionDiffDialog target={target} projectId={projectId} onClose={() => setTarget(null)} />
    </>
  );
}
