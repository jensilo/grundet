import Link from "next/link";
import type * as React from "react";

import { type BrowseSearchParams, buildBrowseHref } from "@/components/app/browse-links";
import { cn } from "@/lib/utils";

const VisibleLabelCount = 4;

export function BrowseItemLabels({
  labels,
  pathname,
  searchParams,
  className,
}: {
  labels: string[];
  pathname: string;
  searchParams: BrowseSearchParams;
  className?: string;
}): React.JSX.Element | null {
  if (labels.length === 0) {
    return null;
  }

  const visibleLabels = labels.slice(0, VisibleLabelCount);
  const hiddenLabels = labels.slice(VisibleLabelCount);

  return (
    <div className={cn("flex flex-wrap items-center gap-1.5", className)}>
      {visibleLabels.map((label) => (
        <LabelLink key={label} label={label} pathname={pathname} searchParams={searchParams} />
      ))}
      {hiddenLabels.length > 0 && (
        <details className="group/details basis-full">
          <summary className="text-muted-foreground hover:text-foreground inline-flex h-5 cursor-pointer list-none items-center rounded-sm px-1 text-xs font-medium transition-colors marker:hidden">
            +{hiddenLabels.length} more
          </summary>
          <div className="mt-1 flex flex-wrap gap-1.5">
            {hiddenLabels.map((label) => (
              <LabelLink
                key={label}
                label={label}
                pathname={pathname}
                searchParams={searchParams}
              />
            ))}
          </div>
        </details>
      )}
    </div>
  );
}

function LabelLink({
  label,
  pathname,
  searchParams,
}: {
  label: string;
  pathname: string;
  searchParams: BrowseSearchParams;
}): React.JSX.Element {
  const rawSelected = searchParams["labels"];
  const selectedLabels = Array.isArray(rawSelected)
    ? rawSelected
    : rawSelected
      ? [rawSelected]
      : [];
  const nextLabels = selectedLabels.includes(label) ? selectedLabels : [...selectedLabels, label];

  return (
    <Link
      href={buildBrowseHref(pathname, searchParams, { labels: nextLabels })}
      className={cn(
        "bg-muted text-muted-foreground hover:bg-accent hover:text-accent-foreground inline-flex h-5 max-w-full items-center rounded-sm px-1.5 text-xs transition-colors",
        selectedLabels.includes(label) && "bg-accent text-accent-foreground"
      )}
    >
      <span className="truncate">{label}</span>
    </Link>
  );
}
