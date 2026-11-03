"use client";

import { CircleHelp } from "lucide-react";
import { useId, useState, useSyncExternalStore } from "react";

import { Button } from "@/components/ui/button";
import {
  type GuideId,
  isGuideSeen,
  markGuideSeen,
  subscribeToGuideState,
} from "@/lib/onboarding-guides";
import { useUser } from "@/providers/user-provider";

// Opt-in contextual guide for true-empty states. Never auto-opens; the parent decides
// where it appears (only on genuinely-empty surfaces, never filtered-empty ones). After
// the first view the trigger collapses to a quiet "?" so it stays replayable without
// competing with the empty state's own call to action.
export function GuideHint({
  guideId,
  title,
  label = "Show me how this works",
  children,
}: {
  guideId: GuideId;
  title: string;
  label?: string;
  children: React.ReactNode;
}): React.JSX.Element {
  const panelId = useId();
  const [open, setOpen] = useState(false);
  // Seen-markers are scoped to the signed-in user so a new user on a shared device
  // starts fresh. GuideHint only renders inside the authenticated layout's UserProvider.
  const user = useUser();
  const userId = user?.id ?? null;
  // Server snapshot is always "unseen" so SSR and the first client render agree.
  const seen = useSyncExternalStore(
    subscribeToGuideState,
    () => isGuideSeen(guideId, userId),
    () => false
  );

  function toggle(): void {
    const next = !open;
    setOpen(next);
    if (next) {
      markGuideSeen(guideId, userId);
    }
  }

  const compact = seen && !open;

  return (
    <div className="flex w-full flex-col items-center gap-3">
      <Button
        type="button"
        variant="ghost"
        size={compact ? "icon-sm" : "sm"}
        className="text-muted-foreground hover:text-foreground"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        aria-label={compact ? label : undefined}
        onClick={toggle}
      >
        <CircleHelp className="size-4" aria-hidden={true} />
        {compact ? null : open ? "Hide the guide" : label}
      </Button>

      {open ? (
        <div
          id={panelId}
          className="border-border bg-muted/50 motion-safe:animate-in motion-safe:fade-in-0 w-full max-w-[420px] rounded-lg border p-4 text-left motion-safe:duration-200"
        >
          <p className="text-foreground text-sm font-medium">{title}</p>
          <div className="text-muted-foreground mt-1.5 space-y-2 text-sm leading-relaxed">
            {children}
          </div>
        </div>
      ) : null}
    </div>
  );
}
