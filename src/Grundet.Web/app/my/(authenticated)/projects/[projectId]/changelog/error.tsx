"use client";

import { Button } from "@/components/ui/button";

export default function ChangelogError({
  unstable_retry,
}: {
  unstable_retry: () => void;
}): React.JSX.Element {
  return (
    <div className="mx-auto max-w-[720px] space-y-4">
      <div
        role="alert"
        className="border-border bg-muted text-muted-foreground rounded-md border px-4 py-3 text-sm"
      >
        Grundet could not load this project’s changelog right now.
      </div>
      <Button type="button" variant="outline" onClick={() => unstable_retry()}>
        Try again
      </Button>
    </div>
  );
}
