"use client";

import { Archive, MoreHorizontal, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { ConfirmDialog } from "@/components/app/confirm-dialog";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { deleteProjectRequirement, saveProjectRequirement } from "@/lib/api/generated/sdk.gen";
import type {
  AttachmentResponse,
  RequirementResponse,
  RequirementStatus,
} from "@/lib/api/generated/types.gen";
import { mutationErrorMessage } from "@/lib/api/mutation-error";
import { cn } from "@/lib/utils";
import { notifyAffectedWork } from "@/lib/work-item-notices";

import { RequirementComposerDialog } from "../_components/requirement-composer-dialog";

// Archive / Won't do are reversible status changes; Delete is the soft, versioned tombstone.
// The everyday actions sit up front; Delete is demoted behind the overflow menu.
type ActionState =
  { type: "idle" } | { type: "status" } | { type: "deleting" } | { type: "error"; message: string };

function statusChangeMessage(error: unknown): string {
  const status =
    typeof error === "object" && error !== null && "status" in error
      ? (error as { status?: number }).status
      : undefined;

  if (status === 403) return "You do not have permission to change this intent.";
  if (status === 409) return "This intent can no longer be changed.";
  return "Could not update the intent. Please try again.";
}

export function EditDeleteActions({
  requirement,
  projectId,
  projectName,
  currentFeature,
  featureCount,
  attachments,
  attachmentsLoaded,
  deleted = false,
}: {
  requirement: RequirementResponse;
  projectId: string;
  projectName: string;
  currentFeature: { id: string; name: string };
  featureCount: number;
  attachments: AttachmentResponse[];
  attachmentsLoaded: boolean;
  deleted?: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const requirementId = requirement.requirementId;
  const currentStatus = requirement.status;
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [actionState, setActionState] = useState<ActionState>({ type: "idle" });
  // Hold the just-saved status locally so the matching action stays disabled until
  // router.refresh() delivers the new currentStatus prop. Without this, a slow refresh
  // re-enables the action and a second click writes another no-op immutable version.
  const [appliedStatus, setAppliedStatus] = useState<RequirementStatus | null>(null);

  const effectiveStatus = appliedStatus ?? currentStatus;
  const isBusy = actionState.type === "status" || actionState.type === "deleting";
  const isDeleting = actionState.type === "deleting";
  // A deleted intent is an immutable tombstone — no further mutations are possible.
  const controlsDisabled = isBusy || deleted;

  async function changeStatus(status: RequirementStatus): Promise<void> {
    setActionState({ type: "status" });

    const { data, error } = await saveProjectRequirement({
      path: { projectId, requirementId },
      body: { status },
    });

    if (error) {
      setActionState({ type: "error", message: statusChangeMessage(error) });
      return;
    }

    setAppliedStatus(status);
    setActionState({ type: "idle" });
    notifyAffectedWork(data?.workItemNotices);
    router.refresh();
  }

  async function handleDeleteConfirm(): Promise<void> {
    setActionState({ type: "deleting" });

    const { error } = await deleteProjectRequirement({
      path: { projectId, requirementId },
      body: { reason: null, expectedVersionId: requirement.id },
    });

    if (error) {
      setActionState({
        type: "error",
        message: mutationErrorMessage(error, "Failed to delete intent. Please try again."),
      });
      return;
    }

    router.push(`/projects/${projectId}/requirements`);
  }

  function handleDeleteDialogChange(open: boolean): void {
    if (isDeleting) return;
    setDeleteDialogOpen(open);
    if (!open && actionState.type === "error") {
      setActionState({ type: "idle" });
    }
  }

  return (
    <div className="flex flex-col items-end gap-1.5">
      <div className="flex items-center gap-2">
        <RequirementComposerDialog
          mode="edit"
          projectId={projectId}
          projectName={projectName}
          initial={requirement}
          currentFeature={currentFeature}
          featureCount={featureCount}
          initialAttachments={attachments}
          attachmentsLoaded={attachmentsLoaded}
          triggerLabel="Edit"
          triggerVariant="outline"
          triggerDisabled={controlsDisabled}
        />

        {effectiveStatus !== "WontDo" && (
          <Button
            type="button"
            variant="secondary"
            size="sm"
            disabled={controlsDisabled}
            onClick={() => void changeStatus("WontDo")}
          >
            Won&apos;t do
          </Button>
        )}

        <DropdownMenu>
          <DropdownMenuTrigger
            aria-label="More actions"
            disabled={controlsDisabled}
            className={cn(
              "text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-ring/30 inline-flex size-7 items-center justify-center rounded-md transition-colors focus-visible:ring-2 focus-visible:outline-none",
              controlsDisabled && "pointer-events-none opacity-50"
            )}
          >
            <MoreHorizontal className="size-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem
              disabled={effectiveStatus === "Archived"}
              onClick={() => void changeStatus("Archived")}
            >
              <Archive />
              Archive
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" onClick={() => setDeleteDialogOpen(true)}>
              <Trash2 />
              Delete…
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {actionState.type === "error" && !deleteDialogOpen && (
        <p role="alert" className="text-destructive text-xs">
          {actionState.message}
        </p>
      )}

      <ConfirmDialog
        open={deleteDialogOpen}
        onOpenChange={handleDeleteDialogChange}
        title="Delete intent?"
        description="Deleting removes this intent everywhere and cannot be undone. Prefer Won't do or Archive if you might need it again."
        confirmLabel="Delete intent"
        pendingLabel="Deleting…"
        pending={isDeleting}
        error={actionState.type === "error" ? actionState.message : undefined}
        onConfirm={handleDeleteConfirm}
      />
    </div>
  );
}
