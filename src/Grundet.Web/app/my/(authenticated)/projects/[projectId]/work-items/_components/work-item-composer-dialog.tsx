"use client";

import type { VariantProps } from "class-variance-authority";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";

import { Button, buttonVariants } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import type {
  AttachmentResponse,
  TeamMemberResponse,
  WorkItemResponse,
} from "@/lib/api/generated/types.gen";

import { WorkItemComposer } from "./work-item-composer";

// The navbar deep-links to the list route with `?compose=work`; the list-header dialog opts in via
// `openOnComposeParam` so that param opens it, and strips the param again on close.
const COMPOSE_PARAM = "compose";
const COMPOSE_VALUE = "work";

type TriggerProps = {
  triggerLabel: string;
  triggerVariant?: VariantProps<typeof buttonVariants>["variant"];
  triggerSize?: VariantProps<typeof buttonVariants>["size"];
  triggerClassName?: string;
  triggerDisabled?: boolean;
  openOnComposeParam?: boolean;
};

type WorkItemComposerDialogProps = TriggerProps & {
  projectId: string;
  projectName: string;
  teamMembers: TeamMemberResponse[];
} & (
    | { mode: "create" }
    | {
        mode: "edit";
        initial: WorkItemResponse;
        initialAttachments: AttachmentResponse[];
        attachmentsLoaded: boolean;
      }
  );

/**
 * Dialog host for the Work composer. Renders the same `WorkItemComposer` used by the full-page
 * `/new` and `/edit` routes. On success the dialog closes and surrounding server data refreshes
 * in place — no navigation.
 */
export function WorkItemComposerDialog(props: WorkItemComposerDialogProps): React.JSX.Element {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [internalOpen, setInternalOpen] = useState(false);
  const isEdit = props.mode === "edit";

  const composeMatches =
    props.openOnComposeParam === true && searchParams.get(COMPOSE_PARAM) === COMPOSE_VALUE;

  // Derived open: the navbar deep-links in via `?compose=work`; closing strips that param so the
  // dialog reverts to its internal state and a later deep-link can re-open it. No effect needed.
  const open = internalOpen || composeMatches;

  function handleOpenChange(next: boolean): void {
    setInternalOpen(next);
    if (!next && composeMatches) {
      const params = new URLSearchParams(searchParams.toString());
      params.delete(COMPOSE_PARAM);
      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    }
  }

  return (
    <>
      <Button
        type="button"
        variant={props.triggerVariant}
        size={props.triggerSize ?? "sm"}
        className={props.triggerClassName}
        disabled={props.triggerDisabled}
        onClick={() => setInternalOpen(true)}
      >
        {props.triggerLabel}
      </Button>

      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{isEdit ? "Edit work item" : "New work item"}</DialogTitle>
            <DialogDescription>
              {isEdit
                ? "Saving creates a new version. The full history is preserved."
                : "Capture the work. Start with a title — add the rest as you go."}
            </DialogDescription>
          </DialogHeader>

          {props.mode === "create" ? (
            <WorkItemComposer
              mode="create"
              projectId={props.projectId}
              projectName={props.projectName}
              teamMembers={props.teamMembers}
              surface="dialog"
              onCancel={() => handleOpenChange(false)}
              onSuccess={() => {
                handleOpenChange(false);
                router.refresh();
              }}
            />
          ) : (
            <WorkItemComposer
              mode="edit"
              projectId={props.projectId}
              projectName={props.projectName}
              teamMembers={props.teamMembers}
              initial={props.initial}
              initialAttachments={props.initialAttachments}
              attachmentsLoaded={props.attachmentsLoaded}
              surface="dialog"
              onCancel={() => handleOpenChange(false)}
              onSuccess={() => {
                handleOpenChange(false);
                router.refresh();
              }}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
