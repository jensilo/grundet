"use client";

import { useEffect, useRef, useState } from "react";

import { PageError } from "@/components/app/page-error";
import { ReferenceText } from "@/components/app/reference-text";
import { VersionChangesDetail } from "@/components/app/version-changes-detail";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  getRequirementVersionHistory,
  getWorkItemVersionHistory,
} from "@/lib/api/generated/sdk.gen";
import type { RequirementVersionEntry, WorkItemVersionEntry } from "@/lib/api/generated/types.gen";
import { formatRelativeDate } from "@/lib/format-date";

type AnyVersionEntry = RequirementVersionEntry | WorkItemVersionEntry;

export interface DiffDialogTarget {
  entityKind: "Intent" | "Work";
  logicalId: string;
  versionId: string;
  reference: string | null;
  title: string;
  changedByIsActiveMember: boolean;
}

type FetchState =
  { status: "loading" } | { status: "error" } | { status: "loaded"; versions: AnyVersionEntry[] };

async function fetchVersions(target: DiffDialogTarget): Promise<AnyVersionEntry[] | null> {
  if (target.entityKind === "Intent") {
    const { data, error } = await getRequirementVersionHistory({
      path: { requirementId: target.logicalId },
    });
    return error || !data ? null : data;
  }

  const { data, error } = await getWorkItemVersionHistory({
    path: { workItemId: target.logicalId },
  });
  return error || !data ? null : data;
}

export function VersionDiffDialog({
  target,
  projectId,
  onClose,
}: {
  target: DiffDialogTarget | null;
  projectId: string;
  onClose: () => void;
}): React.JSX.Element {
  // Fetched version lists are cached per logical id so reopening a row never refetches.
  const cache = useRef<Map<string, AnyVersionEntry[]>>(new Map());
  const [state, setState] = useState<FetchState>({ status: "loading" });

  useEffect(() => {
    if (!target) {
      return;
    }

    const cached = cache.current.get(target.logicalId);
    if (cached) {
      setState({ status: "loaded", versions: cached });
      return;
    }

    let active = true;
    setState({ status: "loading" });
    void fetchVersions(target).then((versions) => {
      if (!active) {
        return;
      }
      if (versions) {
        cache.current.set(target.logicalId, versions);
        setState({ status: "loaded", versions });
      } else {
        setState({ status: "error" });
      }
    });

    return () => {
      active = false;
    };
  }, [target]);

  const version =
    target && state.status === "loaded"
      ? state.versions.find((entry) => entry.versionId === target.versionId)
      : undefined;

  return (
    <Dialog open={target !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="pr-8 leading-snug">
            {target?.reference ? (
              <span className="text-primary mr-2 font-mono text-sm">{target.reference}</span>
            ) : null}
            {target ? <ReferenceText projectId={projectId} text={target.title} /> : null}
          </DialogTitle>
          {version && (
            <DialogDescription>
              {version.changedByDisplayName ?? "Unknown member"}
              {version.changedByDisplayName && target && !target.changedByIsActiveMember && (
                <span className="text-muted-foreground/80 italic"> · former member</span>
              )}{" "}
              · {formatRelativeDate(version.changedAt)} · v{version.versionNumber}
            </DialogDescription>
          )}
        </DialogHeader>

        <div className="max-h-[60vh] overflow-y-auto">
          {state.status === "loading" && (
            <p className="text-muted-foreground py-6 text-center text-sm">Loading changes…</p>
          )}
          {state.status === "error" && <PageError message="Could not load the changes." />}
          {state.status === "loaded" &&
            (version ? (
              <VersionChangesDetail
                changes={version.changes}
                projectId={projectId}
                isInitial={Number(version.versionNumber) === 1}
                reason={version.reason}
              />
            ) : (
              <p className="text-muted-foreground py-6 text-center text-sm">
                This version could not be found.
              </p>
            ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
