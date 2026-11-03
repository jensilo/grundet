"use client";

import { MoreHorizontal, Unlink2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { SelectedWork, WorkLinkPicker } from "@/components/app/composer/work-link-picker";
import { InfoHint } from "@/components/app/info-hint";
import { StatusBadge } from "@/components/app/status-badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  linkRequirementToWorkItem,
  unlinkRequirementFromWorkItem,
} from "@/lib/api/generated/sdk.gen";
import type { LinkedWorkItemResponse } from "@/lib/api/generated/types.gen";
import { cn } from "@/lib/utils";

type AddState = { type: "idle" } | { type: "busy" } | { type: "error"; message: string };

export function IntentWorkSection({
  projectId,
  requirementId,
  workItems,
}: {
  projectId: string;
  requirementId: string;
  workItems: LinkedWorkItemResponse[];
}): React.JSX.Element {
  const router = useRouter();
  const [addOpen, setAddOpen] = useState(false);
  const [selection, setSelection] = useState<SelectedWork[]>([]);
  const [addState, setAddState] = useState<AddState>({ type: "idle" });
  const [rowBusyId, setRowBusyId] = useState<string | null>(null);
  const [rowError, setRowError] = useState<string | null>(null);

  const linkedWorkItemIds = workItems.map((work) => work.workItemId);

  function handleAddOpenChange(next: boolean): void {
    if (addState.type === "busy") return;
    setAddOpen(next);
    if (!next) {
      setSelection([]);
      setAddState({ type: "idle" });
    }
  }

  async function handleAddConfirm(): Promise<void> {
    if (selection.length === 0) return;
    setAddState({ type: "busy" });

    // Each work item links itself to this intent through the existing per-link endpoint.
    for (const work of selection) {
      const { error } = await linkRequirementToWorkItem({
        path: { workItemId: work.workItemId },
        body: { requirementId },
      });
      if (error) {
        setAddState({
          type: "error",
          message: "Some work could not be linked. Close and try again.",
        });
        router.refresh();
        return;
      }
    }

    setAddOpen(false);
    setSelection([]);
    setAddState({ type: "idle" });
    router.refresh();
  }

  async function handleUnlink(work: LinkedWorkItemResponse): Promise<void> {
    setRowBusyId(work.workItemLinkId);
    setRowError(null);
    const { error } = await unlinkRequirementFromWorkItem({
      path: { workItemId: work.workItemId, linkId: work.workItemLinkId },
    });
    if (error) {
      setRowBusyId(null);
      setRowError("That work could not be unlinked. Please try again.");
      return;
    }
    setRowBusyId(null);
    router.refresh();
  }

  return (
    <section className="bg-card ring-foreground/10 rounded-xl p-5 ring-1">
      <div className="mb-4 flex items-center justify-between gap-3">
        <div className="flex items-center gap-1.5">
          <h2 className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
            Linked work
          </h2>
          <InfoHint label="How linking works" className="translate-y-px">
            Work that makes this intent true. Each linked item follows the latest version of this
            intent automatically.
          </InfoHint>
          {workItems.length > 0 && (
            <span className="text-muted-foreground text-xs">· {workItems.length}</span>
          )}
        </div>
        <Button type="button" variant="outline" size="sm" onClick={() => setAddOpen(true)}>
          Link work
        </Button>
      </div>

      {workItems.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          No work linked yet – connect the work that makes this intent true.
        </p>
      ) : (
        <ul className="divide-border divide-y">
          {workItems.map((work) => {
            const rowBusy = rowBusyId === work.workItemLinkId;
            return (
              <li
                key={work.workItemLinkId}
                className={cn(
                  "flex items-center gap-3 py-2.5 first:pt-0 last:pb-0",
                  rowBusy && "opacity-50"
                )}
              >
                <Link
                  href={`/projects/${projectId}/work-items/${work.workItemId}`}
                  className="group flex min-w-0 flex-1 items-center gap-3"
                >
                  <span className="text-primary shrink-0 font-mono text-xs font-medium">
                    {work.ref}
                  </span>
                  <span className="text-foreground min-w-0 flex-1 truncate text-sm group-hover:underline">
                    {work.title}
                  </span>
                </Link>

                {work.assignedToDisplayName && (
                  <span className="text-muted-foreground hidden shrink-0 truncate text-xs sm:block">
                    {work.assignedToDisplayName}
                  </span>
                )}
                <StatusBadge status={work.status} />

                <DropdownMenu>
                  <DropdownMenuTrigger
                    aria-label={`Actions for ${work.ref}`}
                    disabled={rowBusy}
                    className={cn(
                      "text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-ring/30 inline-flex size-6 shrink-0 items-center justify-center rounded-md transition-colors focus-visible:ring-2 focus-visible:outline-none",
                      rowBusy && "pointer-events-none opacity-50"
                    )}
                  >
                    <MoreHorizontal className="size-4" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem variant="destructive" onClick={() => void handleUnlink(work)}>
                      <Unlink2 />
                      Unlink
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </li>
            );
          })}
        </ul>
      )}

      {rowError && (
        <p role="alert" className="text-destructive mt-3 text-xs">
          {rowError}
        </p>
      )}

      <Dialog open={addOpen} onOpenChange={handleAddOpenChange}>
        <DialogContent className="overflow-visible sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Link work</DialogTitle>
            <DialogDescription>
              Connect the work that makes this intent true. Link several at once.
            </DialogDescription>
          </DialogHeader>

          <WorkLinkPicker
            id="link-work-picker"
            projectId={projectId}
            values={selection}
            disabled={addState.type === "busy"}
            excludeWorkItemIds={linkedWorkItemIds}
            onChange={setSelection}
          />

          {addState.type === "error" && (
            <p role="alert" className="text-destructive text-xs">
              {addState.message}
            </p>
          )}

          <div className="bg-muted/50 -mx-4 -mb-4 flex items-center gap-3 rounded-b-xl border-t p-4">
            <Button
              type="button"
              disabled={selection.length === 0 || addState.type === "busy"}
              onClick={() => void handleAddConfirm()}
            >
              {addState.type === "busy"
                ? "Linking…"
                : selection.length > 1
                  ? `Link ${selection.length} work items`
                  : "Link work"}
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={addState.type === "busy"}
              onClick={() => handleAddOpenChange(false)}
            >
              Cancel
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </section>
  );
}
