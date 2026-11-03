"use client";

import { Loader2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";

import type { SelectedIntent } from "@/components/app/composer/intent-link-picker";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { listProjectRequirements } from "@/lib/api/generated/sdk.gen";
import type { TeamMemberResponse } from "@/lib/api/generated/types.gen";

import { WorkItemComposer } from "../../work-items/_components/work-item-composer";

type LoadState =
  | { type: "idle" }
  | { type: "loading" }
  | { type: "ready"; intents: SelectedIntent[] }
  | { type: "error" };

/**
 * Bulk-link entry point: creates one Feature-type work item linked to every current intent under a
 * feature. It reuses the work composer prefilled with the feature's intent — the same multi-select
 * mechanism and the same one-transaction batch the composer uses for ad-hoc linking on create.
 */
export function FeatureBulkWorkDialog({
  projectId,
  featureId,
  featureName,
  teamMembers,
}: {
  projectId: string;
  featureId: string;
  featureName: string;
  teamMembers: TeamMemberResponse[];
}): React.JSX.Element {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [load, setLoad] = useState<LoadState>({ type: "idle" });

  async function handleOpen(): Promise<void> {
    setOpen(true);
    setLoad({ type: "loading" });

    // A feature can hold more current intents than a single page (max 200). Page through all of
    // them so the bulk selection covers the feature's full intent set — otherwise the created work
    // item would silently omit everything past the first page.
    const intents: SelectedIntent[] = [];
    let page = 1;
    for (;;) {
      const { data, error } = await listProjectRequirements({
        path: { projectId },
        query: { featureId, pageSize: "200", page: String(page) },
      });
      if (error || !data) {
        setLoad({ type: "error" });
        return;
      }
      for (const item of data.items) {
        intents.push({
          requirementId: item.requirementId,
          ref: item.ref,
          text: item.text,
        });
      }
      if (!data.hasMore) break;
      page += 1;
    }

    setLoad({ type: "ready", intents });
  }

  function handleOpenChange(next: boolean): void {
    setOpen(next);
    if (!next) setLoad({ type: "idle" });
  }

  return (
    <>
      <Button type="button" variant="outline" size="sm" onClick={() => void handleOpen()}>
        Track as work
      </Button>

      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Track {featureName} as work</DialogTitle>
            <DialogDescription>
              Creates one work item linked to this feature&apos;s intent. Adjust the selection or
              details before creating.
            </DialogDescription>
          </DialogHeader>

          {load.type === "loading" && (
            <div className="text-muted-foreground flex items-center gap-2 py-8 text-sm">
              <Loader2 className="size-4 animate-spin" aria-hidden />
              Gathering this feature&apos;s intent…
            </div>
          )}

          {load.type === "error" && (
            <p className="text-destructive py-8 text-sm">
              Could not load this feature&apos;s intent. Close and try again.
            </p>
          )}

          {load.type === "ready" && (
            <>
              <p className="text-muted-foreground text-xs">
                {load.intents.length === 0
                  ? "This feature has no intent yet — you can still create the work item and link intent later."
                  : load.intents.length === 1
                    ? "Linking 1 intent from this feature."
                    : `Linking ${load.intents.length} intent from this feature.`}
              </p>
              <WorkItemComposer
                mode="create"
                projectId={projectId}
                projectName={featureName}
                teamMembers={teamMembers}
                surface="dialog"
                enableDraft={false}
                initialTitle={featureName}
                initialType="Feature"
                initialIntents={load.intents}
                onCancel={() => handleOpenChange(false)}
                onSuccess={(workItemId) => {
                  handleOpenChange(false);
                  router.push(`/projects/${projectId}/work-items/${workItemId}`);
                }}
              />
            </>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
