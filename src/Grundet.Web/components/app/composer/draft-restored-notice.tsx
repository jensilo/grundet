"use client";

import { Button } from "@/components/ui/button";

/**
 * Subtle affordance shown when an in-progress draft is restored on open, with a way to discard
 * it. When `stale` is set, the underlying entity changed since the draft was started.
 */
export function DraftRestoredNotice({
  stale,
  onDiscard,
  disabled,
}: {
  stale: boolean;
  onDiscard: () => void;
  disabled?: boolean;
}): React.JSX.Element {
  return (
    <div className="border-border bg-muted/50 flex flex-wrap items-center justify-between gap-2 rounded-md border px-3 py-2">
      <p className="text-muted-foreground text-xs">
        Draft restored.
        {stale &&
          " The underlying item changed since this draft was started — review before saving."}
      </p>
      <Button
        type="button"
        variant="ghost"
        size="xs"
        disabled={disabled}
        className="text-muted-foreground hover:text-foreground"
        onClick={onDiscard}
      >
        Discard draft
      </Button>
    </div>
  );
}
