import { ChevronLeft, ChevronRight } from "lucide-react";
import Link from "next/link";
import type * as React from "react";

import { Button } from "@/components/ui/button";

function PaginationButton({
  href,
  disabled,
  children,
}: {
  href: string;
  disabled: boolean;
  children: React.ReactNode;
}): React.JSX.Element {
  if (disabled) {
    return (
      <Button variant="outline" size="sm" disabled>
        {children}
      </Button>
    );
  }

  return (
    <Button
      variant="outline"
      size="sm"
      nativeButton={false}
      render={<Link href={href}>{children}</Link>}
    />
  );
}

export function BrowsePagination({
  page,
  total,
  totalPages,
  hasMore,
  makeHref,
}: {
  page: number;
  total: number;
  totalPages: number;
  hasMore: boolean;
  makeHref: (page: number) => string;
}): React.JSX.Element {
  const hasPrev = page > 1;
  const resolvedTotalPages = totalPages > 0 ? totalPages : 1;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 pt-2">
      <p className="text-muted-foreground text-xs tabular-nums">
        Page {page} of {resolvedTotalPages} · {total} total
      </p>
      <div className="flex items-center gap-2">
        <PaginationButton href={makeHref(page - 1)} disabled={!hasPrev}>
          <ChevronLeft className="size-3.5" aria-hidden={true} />
          Previous
        </PaginationButton>
        <PaginationButton href={makeHref(page + 1)} disabled={!hasMore}>
          Next
          <ChevronRight className="size-3.5" aria-hidden={true} />
        </PaginationButton>
      </div>
    </div>
  );
}
