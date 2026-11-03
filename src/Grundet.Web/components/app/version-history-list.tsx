import type * as React from "react";

import { UserAvatar } from "@/components/app/user-avatar";
import { VersionChangesDetail } from "@/components/app/version-changes-detail";
import type { RequirementVersionEntry, WorkItemVersionEntry } from "@/lib/api/generated/types.gen";
import { changeTypeLabel } from "@/lib/changelog";
import { formatRelativeDate } from "@/lib/format-date";

type AnyVersionEntry = RequirementVersionEntry | WorkItemVersionEntry;

/**
 * Full per-item history: every version expanded with full field-level diffs.
 * Each version is anchored as `#v{n}` so individual versions are deep-linkable.
 */
export function VersionHistoryList({
  versions,
  projectId,
}: {
  versions: AnyVersionEntry[];
  projectId: string;
}): React.JSX.Element {
  // Sort by explicit version number so ordering never depends on API list direction.
  const ordered = [...versions].sort((a, b) => Number(b.versionNumber) - Number(a.versionNumber));

  return (
    <ol className="space-y-4">
      {ordered.map((version) => (
        <li
          key={version.versionId}
          id={`v${version.versionNumber}`}
          className="bg-card ring-foreground/10 scroll-mt-20 rounded-xl p-5 ring-1"
        >
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <span className="bg-muted text-muted-foreground rounded-sm px-1.5 py-0.5 font-mono text-xs">
              v{version.versionNumber}
            </span>
            <span className="text-foreground text-sm font-medium">
              {changeTypeLabel(version.changeType)}
            </span>
            <UserAvatar
              userId={version.changedByUserId}
              name={version.changedByDisplayName}
              size="default"
              className="size-5"
            />
            <span className="text-muted-foreground text-xs">
              {version.changedByDisplayName ?? "Unknown user"} ·{" "}
              {formatRelativeDate(version.changedAt)}
            </span>
          </div>
          <VersionChangesDetail
            changes={version.changes}
            projectId={projectId}
            isInitial={Number(version.versionNumber) === 1}
            reason={version.reason}
          />
        </li>
      ))}
    </ol>
  );
}
