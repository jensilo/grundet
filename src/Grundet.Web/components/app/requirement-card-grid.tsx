import Link from "next/link";
import type * as React from "react";

import { BrowseItemLabels } from "@/components/app/browse-item-labels";
import { type BrowseSearchParams, buildBrowseHref } from "@/components/app/browse-links";
import { PriorityBadge } from "@/components/app/priority-badge";
import { ReferenceText } from "@/components/app/reference-text";
import { StatusBadge } from "@/components/app/status-badge";
import { Card, CardContent, CardFooter, CardHeader } from "@/components/ui/card";
import type { RequirementResponse } from "@/lib/api/generated/types.gen";

export function RequirementCardGrid({
  requirements,
  projectId,
  featureNamesById,
  pathname,
  searchParams,
}: {
  requirements: RequirementResponse[];
  projectId: string;
  featureNamesById: Map<string, string>;
  pathname: string;
  searchParams: BrowseSearchParams;
}): React.JSX.Element {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
      {requirements.map((requirement) => (
        <RequirementCard
          key={requirement.requirementId}
          requirement={requirement}
          projectId={projectId}
          featureName={featureNamesById.get(requirement.featureId)}
          pathname={pathname}
          searchParams={searchParams}
        />
      ))}
    </div>
  );
}

function RequirementCard({
  requirement,
  projectId,
  featureName,
  pathname,
  searchParams,
}: {
  requirement: RequirementResponse;
  projectId: string;
  featureName?: string;
  pathname: string;
  searchParams: BrowseSearchParams;
}): React.JSX.Element {
  const baseHref = `/projects/${projectId}/requirements/${requirement.requirementId}`;
  const browseParams = new URLSearchParams();
  for (const [key, raw] of Object.entries(searchParams)) {
    const values = Array.isArray(raw) ? raw : raw ? [raw] : [];
    for (const value of values) {
      if (value) browseParams.append(key, value);
    }
  }
  const browseQuery = browseParams.toString();
  const href = browseQuery ? `${baseHref}?back=${encodeURIComponent(browseQuery)}` : baseHref;
  const criteriaCount = requirement.acceptanceCriteria.length;

  const rawStatuses = searchParams["status"];
  const selectedStatuses = Array.isArray(rawStatuses)
    ? rawStatuses
    : rawStatuses
      ? [rawStatuses]
      : [];
  const rawPriorities = searchParams["priority"];
  const selectedPriorities = Array.isArray(rawPriorities)
    ? rawPriorities
    : rawPriorities
      ? [rawPriorities]
      : [];
  const statusHref = buildBrowseHref(pathname, searchParams, {
    status: selectedStatuses.includes(requirement.status)
      ? selectedStatuses
      : [...selectedStatuses, requirement.status],
  });
  const priorityHref = buildBrowseHref(pathname, searchParams, {
    priority: selectedPriorities.includes(requirement.priority)
      ? selectedPriorities
      : [...selectedPriorities, requirement.priority],
  });

  return (
    <Card className="relative h-full gap-3 transition-shadow hover:shadow-sm">
      <Link href={href} aria-label={requirement.text} className="absolute inset-0" />
      <CardHeader className="gap-2">
        <div className="flex min-w-0 items-center justify-between gap-3">
          <div className="text-muted-foreground flex min-w-0 items-baseline gap-1.5 text-xs">
            <Link
              href={href}
              className="hover:text-foreground relative z-10 shrink-0 font-mono transition-colors"
            >
              {requirement.ref}
            </Link>
            {featureName && (
              <>
                <span aria-hidden={true}>·</span>
                <span className="truncate">{featureName}</span>
              </>
            )}
          </div>
          <div className="flex shrink-0 items-center gap-1.5">
            <Link
              href={statusHref}
              className="relative z-10 rounded-sm transition-opacity hover:opacity-80"
            >
              <StatusBadge status={requirement.status} />
            </Link>
            <Link
              href={priorityHref}
              className="relative z-10 rounded-sm transition-opacity hover:opacity-80"
            >
              <PriorityBadge priority={requirement.priority} />
            </Link>
          </div>
        </div>
        <ReferenceText
          projectId={projectId}
          text={requirement.text}
          className="text-foreground line-clamp-4 min-w-0 text-[0.9375rem] leading-snug font-medium"
          linkClassName="relative z-10"
        />
      </CardHeader>
      <CardContent className="flex flex-1 flex-col justify-end">
        <BrowseItemLabels
          labels={requirement.labels}
          pathname={pathname}
          searchParams={searchParams}
          className="relative z-10"
        />
      </CardContent>
      <CardFooter className="text-muted-foreground justify-between gap-3 text-xs">
        <span className="truncate">
          {criteriaCount === 1 ? "1 acceptance criterion" : `${criteriaCount} acceptance criteria`}
        </span>
        <span className="shrink-0">{formatChangedAt(requirement.changedAt)}</span>
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
