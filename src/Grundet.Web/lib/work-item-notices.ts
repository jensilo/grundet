"use client";

import type { WorkItemNoticeResponse } from "@/lib/api/generated/types.gen";
import { toast } from "@/lib/toast";

export type AffectedWorkMessage = { title: string; description: string };

/**
 * Builds the "linked work may be affected" notice from the work-item notices the
 * backend returns on every Intent mutation. Returns null when nothing is linked —
 * the toast is suppressed entirely so an edit with no downstream work stays quiet.
 */
export function affectedWorkMessage(
  notices: WorkItemNoticeResponse[] | undefined
): AffectedWorkMessage | null {
  if (!notices || notices.length === 0) return null;

  const count = notices.length;
  return {
    title: "Intent saved",
    description:
      count === 1
        ? `1 linked work item may be affected – ${notices[0].title}.`
        : `${count} linked work items may be affected.`,
  };
}

/** Raises the affected-work toast, or does nothing when no work is linked. */
export function notifyAffectedWork(notices: WorkItemNoticeResponse[] | undefined): void {
  const message = affectedWorkMessage(notices);
  if (!message) return;
  toast({ type: "success", ...message });
}
