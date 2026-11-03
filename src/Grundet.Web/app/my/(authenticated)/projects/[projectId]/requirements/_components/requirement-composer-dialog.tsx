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
import type { AttachmentResponse, RequirementResponse } from "@/lib/api/generated/types.gen";

import { RequirementComposer } from "./requirement-composer";

// The navbar deep-links to the list route with `?compose=intent`; the list-header dialog opts in
// via `openOnComposeParam` so that param opens it, and strips the param again on close.
const COMPOSE_PARAM = "compose";
const COMPOSE_VALUE = "intent";

type TriggerProps = {
  triggerLabel: string;
  triggerVariant?: VariantProps<typeof buttonVariants>["variant"];
  triggerSize?: VariantProps<typeof buttonVariants>["size"];
  triggerClassName?: string;
  triggerDisabled?: boolean;
  openOnComposeParam?: boolean;
};

type RequirementComposerDialogProps = TriggerProps & {
  projectId: string;
  projectName: string;
  currentFeature: { id: string; name: string };
  featureCount: number;
} & (
    | { mode: "create"; featureId: string }
    | {
        mode: "edit";
        initial: RequirementResponse;
        initialAttachments: AttachmentResponse[];
        attachmentsLoaded: boolean;
      }
  );

/**
 * Dialog host for the Intent composer. Renders the same `RequirementComposer` used by the
 * full-page `/new` and `/edit` routes, so there is one source of truth and no drift. On success
 * the dialog closes and the surrounding server data is refreshed in place — no navigation.
 */
export function RequirementComposerDialog(
  props: RequirementComposerDialogProps
): React.JSX.Element {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [internalOpen, setInternalOpen] = useState(false);
  const isEdit = props.mode === "edit";

  const composeMatches =
    props.openOnComposeParam === true && searchParams.get(COMPOSE_PARAM) === COMPOSE_VALUE;

  // Derived open: the navbar deep-links in via `?compose=intent`; closing strips that param so the
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
            <DialogTitle>{isEdit ? "Edit intent" : "New intent"}</DialogTitle>
            <DialogDescription>
              {isEdit
                ? "Saving creates a new version. The full history is preserved."
                : "Capture what should be true. Start with the intent — add the rest as you go."}
            </DialogDescription>
          </DialogHeader>

          {props.mode === "create" ? (
            <RequirementComposer
              mode="create"
              projectId={props.projectId}
              projectName={props.projectName}
              featureId={props.featureId}
              currentFeature={props.currentFeature}
              featureCount={props.featureCount}
              surface="dialog"
              onCancel={() => handleOpenChange(false)}
              onSuccess={() => {
                handleOpenChange(false);
                router.refresh();
              }}
            />
          ) : (
            <RequirementComposer
              mode="edit"
              projectId={props.projectId}
              projectName={props.projectName}
              initial={props.initial}
              currentFeature={props.currentFeature}
              featureCount={props.featureCount}
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
