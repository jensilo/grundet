"use client";

import { MoreHorizontal, Unlink2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import {
  IntentLinkPicker,
  type SelectedIntent,
} from "@/components/app/composer/intent-link-picker";
import { InfoHint } from "@/components/app/info-hint";
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
import type { WorkItemLinkResponse } from "@/lib/api/generated/types.gen";
import { cn } from "@/lib/utils";

type Busy = { type: "idle" } | { type: "busy" } | { type: "error"; message: string };

export function WorkItemLinksSection({
  projectId,
  workItemId,
  links,
  deleted = false,
}: {
  projectId: string;
  workItemId: string;
  links: WorkItemLinkResponse[];
  deleted?: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [addOpen, setAddOpen] = useState(false);
  const [selection, setSelection] = useState<SelectedIntent[]>([]);
  const [addState, setAddState] = useState<Busy>({ type: "idle" });
  // The link whose row-level action is in flight, keyed by workItemLinkId.
  const [rowBusyId, setRowBusyId] = useState<string | null>(null);
  const [rowError, setRowError] = useState<string | null>(null);

  const linkedRequirementIds = links.map((link) => link.requirementId);

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

    // One call per intent — the work item's advisory lock serializes them on the backend anyway.
    for (const intent of selection) {
      const { error } = await linkRequirementToWorkItem({
        path: { workItemId },
        body: { requirementId: intent.requirementId },
      });
      if (error) {
        setAddState({
          type: "error",
          message: "Some intent could not be linked. Close and try again.",
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

  async function runRowAction(
    linkId: string,
    action: () => Promise<{ error?: unknown }>
  ): Promise<void> {
    setRowBusyId(linkId);
    setRowError(null);
    const { error } = await action();
    if (error) {
      setRowBusyId(null);
      setRowError("That change could not be applied. Please try again.");
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
            Linked intent
          </h2>
          <InfoHint label="How linking works" className="translate-y-px">
            This work links to the intent it makes true. When that intent changes, the link follows
            the latest version automatically – its history is kept in the changelog.
          </InfoHint>
          {links.length > 0 && (
            <span className="text-muted-foreground text-xs">· {links.length}</span>
          )}
        </div>
        {!deleted && (
          <Button type="button" variant="outline" size="sm" onClick={() => setAddOpen(true)}>
            Link intent
          </Button>
        )}
      </div>

      {links.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Not linked yet – connect this work to the intent it makes true.
        </p>
      ) : (
        <ul className="divide-border divide-y">
          {links.map((link) => {
            const rowBusy = rowBusyId === link.workItemLinkId;
            return (
              <li
                key={link.workItemLinkId}
                className={cn(
                  "flex items-baseline gap-3 py-2.5 first:pt-0 last:pb-0",
                  rowBusy && "opacity-50"
                )}
              >
                <Link
                  href={`/projects/${projectId}/requirements/${link.requirementId}`}
                  className="group flex min-w-0 flex-1 items-baseline gap-3"
                >
                  <span className="text-primary shrink-0 font-mono text-xs font-medium">
                    {link.requirementRef ?? link.requirementId.slice(0, 8)}
                  </span>
                  <span
                    className={
                      link.isDeleted
                        ? "text-muted-foreground min-w-0 flex-1 truncate text-sm line-through"
                        : "text-foreground min-w-0 flex-1 truncate text-sm group-hover:underline"
                    }
                  >
                    {link.requirementText ?? "Untitled intent"}
                  </span>
                </Link>

                {link.isDeleted && (
                  <span className="bg-muted text-muted-foreground inline-flex h-5 shrink-0 items-center rounded-sm px-1.5 text-xs font-medium">
                    Deleted
                  </span>
                )}

                {!deleted && (
                  <DropdownMenu>
                    <DropdownMenuTrigger
                      aria-label={`Actions for ${link.requirementRef ?? "intent"}`}
                      disabled={rowBusy}
                      className={cn(
                        "text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-ring/30 inline-flex size-6 shrink-0 items-center justify-center rounded-md transition-colors focus-visible:ring-2 focus-visible:outline-none",
                        rowBusy && "pointer-events-none opacity-50"
                      )}
                    >
                      <MoreHorizontal className="size-4" />
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem
                        variant="destructive"
                        onClick={() =>
                          void runRowAction(link.workItemLinkId, () =>
                            unlinkRequirementFromWorkItem({
                              path: { workItemId, linkId: link.workItemLinkId },
                            })
                          )
                        }
                      >
                        <Unlink2 />
                        Unlink
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                )}
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
            <DialogTitle>Link intent</DialogTitle>
            <DialogDescription>
              Connect this work to the intent it makes true. Link several at once.
            </DialogDescription>
          </DialogHeader>

          <IntentLinkPicker
            id="link-intent-picker"
            projectId={projectId}
            values={selection}
            disabled={addState.type === "busy"}
            excludeRequirementIds={linkedRequirementIds}
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
                  ? `Link ${selection.length} intent`
                  : "Link intent"}
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
