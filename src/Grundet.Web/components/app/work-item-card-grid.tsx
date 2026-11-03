import Link from "next/link";
import type * as React from "react";

import { BrowseItemLabels } from "@/components/app/browse-item-labels";
import { type BrowseSearchParams, buildBrowseHref } from "@/components/app/browse-links";
import { ReferenceText } from "@/components/app/reference-text";
import { StatusBadge } from "@/components/app/status-badge";
import { UserAvatar } from "@/components/app/user-avatar";
import { Card, CardContent, CardFooter, CardHeader } from "@/components/ui/card";
import type { WorkItemResponse } from "@/lib/api/generated/types.gen";

export function WorkItemCardGrid({
  workItems,
  projectId,
  pathname,
  searchParams,
}: {
  workItems: WorkItemResponse[];
  projectId: string;
  pathname: string;
  searchParams: BrowseSearchParams;
}): React.JSX.Element {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
      {workItems.map((workItem) => (
        <WorkItemCard
          key={workItem.workItemId}
          workItem={workItem}
          projectId={projectId}
          pathname={pathname}
          searchParams={searchParams}
        />
      ))}
    </div>
  );
}

function WorkItemCard({
  workItem,
  projectId,
  pathname,
  searchParams,
}: {
  workItem: WorkItemResponse;
  projectId: string;
  pathname: string;
  searchParams: BrowseSearchParams;
}): React.JSX.Element {
  const href = `/projects/${projectId}/work-items/${workItem.workItemId}`;

  return (
    <Card className="relative h-full gap-3 transition-shadow hover:shadow-sm">
      <Link href={href} aria-label={workItem.title} className="absolute inset-0" />
      <CardHeader className="gap-2">
        <div className="flex items-center justify-between gap-3">
          <div className="text-muted-foreground flex min-w-0 items-baseline gap-1.5 text-xs">
            <Link
              href={href}
              className="hover:text-foreground relative z-10 shrink-0 font-mono transition-colors"
            >
              {workItem.ref}
            </Link>
            <span aria-hidden={true}>·</span>
            <span className="truncate">{workItem.type}</span>
          </div>
          <div className="flex shrink-0 items-center gap-1.5">
            <StatusBadge status={workItem.status} />
            <span className="bg-muted text-muted-foreground inline-flex h-5 items-center rounded-sm px-1.5 text-xs font-medium tracking-[0.02em]">
              {workItem.priority}
            </span>
          </div>
        </div>
        <ReferenceText
          projectId={projectId}
          text={workItem.title}
          className="text-foreground line-clamp-3 text-[0.9375rem] leading-snug font-medium"
          linkClassName="relative z-10"
        />
      </CardHeader>
      <CardContent className="flex flex-1 flex-col gap-3">
        {workItem.description && (
          <p className="text-muted-foreground line-clamp-2 text-sm">{workItem.description}</p>
        )}
        {(workItem.blockedReason || workItem.labels.length > 0) && (
          <div className="mt-auto flex flex-wrap items-center gap-1.5">
            {workItem.blockedReason && (
              <Link
                href={buildBrowseHref(pathname, searchParams, { blocked: "true" })}
                className="bg-destructive-50 text-destructive-700 hover:bg-destructive-100 relative z-10 inline-flex h-5 items-center rounded-sm px-1.5 text-xs font-medium tracking-[0.02em] transition-colors"
              >
                Blocked
              </Link>
            )}
            <BrowseItemLabels
              labels={workItem.labels}
              pathname={pathname}
              searchParams={searchParams}
              className="relative z-10"
            />
          </div>
        )}
      </CardContent>
      <CardFooter className="text-muted-foreground justify-between gap-3 text-xs">
        <span className="flex min-w-0 items-center gap-1.5">
          {workItem.assignedToUserId && (
            <UserAvatar
              userId={workItem.assignedToUserId}
              name={workItem.assignedToDisplayName}
              size="default"
              className="size-5"
            />
          )}
          <span className="truncate" title={workItem.assignedToDisplayName ?? "Unassigned"}>
            {workItem.assignedToDisplayName ?? "Unassigned"}
          </span>
        </span>
        <span className="shrink-0">{formatChangedAt(workItem.changedAt)}</span>
      </CardFooter>
    </Card>
  );
}

function formatChangedAt(changedAt: string): string {
  const date = new Date(changedAt);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffHours = diffMs / (1000 * 60 * 60);

  if (diffHours < 1) {
    return "< 1h ago";
  }

  if (diffHours < 24) {
    return `${Math.floor(diffHours)}h ago`;
  }

  const diffDays = diffHours / 24;
  if (diffDays < 7) {
    return `${Math.floor(diffDays)}d ago`;
  }

  return date.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
}
