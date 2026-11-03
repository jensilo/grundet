"use client";

import { useEffect } from "react";

import { PageError } from "@/components/app/page-error";
import { Button } from "@/components/ui/button";

export default function DashboardError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}): React.JSX.Element {
  useEffect(() => {
    console.error("Dashboard failed to render", error.digest ?? "no digest");
  }, [error.digest]);

  return (
    <div className="mx-auto max-w-[720px] space-y-4">
      <div className="space-y-2">
        <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
          Could not load home
        </h1>
        <p className="text-muted-foreground text-sm">
          Grundet could not load your teams and projects right now.
        </p>
      </div>
      <PageError message="Please try again. If the problem continues, contact support." />
      <Button type="button" onClick={() => unstable_retry()}>
        Try again
      </Button>
    </div>
  );
}
