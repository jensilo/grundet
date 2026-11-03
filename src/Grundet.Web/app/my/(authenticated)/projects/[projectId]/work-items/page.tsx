import { ListChecks, SearchX } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";
import type * as React from "react";

import { WorkItemComposerDialog } from "@/app/my/(authenticated)/projects/[projectId]/work-items/_components/work-item-composer-dialog";
import { BrowseFilterPanel } from "@/components/app/browse-filter-panel";
import {
  type BrowseSearchParams,
  buildBrowseHref,
  getSearchValue,
  getSearchValues,
} from "@/components/app/browse-links";
import { BrowsePagination } from "@/components/app/browse-pagination";
import { EmptyState } from "@/components/app/empty-state";
import { WorkGuide } from "@/components/app/journey-guides";
import { PageError } from "@/components/app/page-error";
import { WorkItemCardGrid } from "@/components/app/work-item-card-grid";
import { Button } from "@/components/ui/button";
import { listTeamMembers, listWorkItems } from "@/lib/api/generated/sdk.gen";
import type {
  PagedResponseOfWorkItemResponse,
  TeamMemberResponse,
} from "@/lib/api/generated/types.gen";
import { getFeatureById } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

const PAGE_SIZE = 24;
const STATUS_OPTIONS = [
  { value: "Todo", label: "To do" },
  { value: "InProgress", label: "In progress" },
  { value: "Done", label: "Done" },
  { value: "WontDo", label: "Won't Do" },
] as const;
const PRIORITY_OPTIONS = [
  { value: "High", label: "High" },
  { value: "Medium", label: "Medium" },
  { value: "Low", label: "Low" },
] as const;
const TYPE_OPTIONS = [
  { value: "Feature", label: "Feature" },
  { value: "Bug", label: "Bug" },
  { value: "Task", label: "Task" },
] as const;
const SORT_OPTIONS = ["priority", "updated", "created"] as const;

async function fetchWorkItems(
  serverClient: Awaited<ReturnType<typeof createServerClient>>,
  projectId: string,
  query: {
    featureId?: string;
    q?: string;
    status: string[];
    priority: string[];
    labels: string[];
    type: string[];
    assignedTo?: string;
    blocked?: boolean;
    page: number;
    sort: string;
  }
): Promise<{ paged: PagedResponseOfWorkItemResponse | null; error: string | null }> {
  const { data, error } = await listWorkItems({
    client: serverClient,
    path: { projectId },
    query: {
      featureId: query.featureId,
      q: query.q,
      status: query.status,
      priority: query.priority,
      labels: query.labels,
      type: query.type,
      assignedTo: query.assignedTo,
      blocked: query.blocked,
      page: query.page,
      pageSize: PAGE_SIZE,
      sort: query.sort,
    },
  });

  if (error || !data) {
    return { paged: null, error: "Could not load work." };
  }

  return { paged: data, error: null };
}

export default async function WorkItemsPage({
  params,
  searchParams,
}: {
  params: Promise<{ projectId: string }>;
  searchParams: Promise<BrowseSearchParams>;
}): Promise<React.JSX.Element> {
  const { projectId } = await params;
  const sp = await searchParams;
  const rawPage = sp["page"];
  const parsedPage = parseInt(typeof rawPage === "string" ? rawPage : "1", 10) || 1;
  const page = Math.min(100_000, Math.max(1, parsedPage));
  const q = getSearchValue(sp, "q");
  const requestedSort = getSearchValue(sp, "sort");
  const sort = SORT_OPTIONS.some((option) => option === requestedSort) ? requestedSort : "updated";
  const selectedStatuses = getSearchValues(
    sp,
    "status",
    STATUS_OPTIONS.map((option) => option.value)
  );
  const selectedPriorities = getSearchValues(
    sp,
    "priority",
    PRIORITY_OPTIONS.map((option) => option.value)
  );
  const selectedLabels = getSearchValues(sp, "labels");
  const selectedTypes = getSearchValues(
    sp,
    "type",
    TYPE_OPTIONS.map((option) => option.value)
  );
  const rawBlocked = getSearchValue(sp, "blocked");
  const blocked = rawBlocked === "true" ? true : rawBlocked === "false" ? false : undefined;
  const activeAssignee = getSearchValue(sp, "assignedTo") || undefined;

  const requestedFeatureId = getSearchValue(sp, "featureId");
  const activeFeatureId = requestedFeatureId || undefined;

  const serverClient = await createServerClient();
  const [project, activeFeature] = await Promise.all([
    findProject(serverClient, projectId),
    activeFeatureId
      ? getFeatureById(serverClient, projectId, activeFeatureId)
      : Promise.resolve(null),
  ]);

  if (!project) {
    notFound();
  }

  // An explicit ?featureId= that does not resolve is an invalid filter — 404 like the old validation.
  if (activeFeatureId && !activeFeature) {
    notFound();
  }

  const [{ paged, error: workItemsError }, teamMembersResult] = await Promise.all([
    fetchWorkItems(serverClient, projectId, {
      featureId: activeFeatureId,
      q: q || undefined,
      status: selectedStatuses,
      priority: selectedPriorities,
      labels: selectedLabels,
      type: selectedTypes,
      assignedTo: activeAssignee,
      blocked,
      page,
      sort,
    }),
    listTeamMembers({ client: serverClient, path: { teamId: project.teamId } }),
  ]);
  const teamMembers: TeamMemberResponse[] = teamMembersResult.data ?? [];
  const pathname = `/projects/${projectId}/work-items`;
  const hasActiveFilters = Boolean(
    q ||
    activeFeatureId ||
    selectedStatuses.length > 0 ||
    selectedPriorities.length > 0 ||
    selectedLabels.length > 0 ||
    selectedTypes.length > 0 ||
    activeAssignee ||
    blocked !== undefined
  );
  const labelOptions = Array.from(
    new Set([...(paged?.items.flatMap((item) => item.labels) ?? []), ...selectedLabels])
  )
    .sort((a, b) => a.localeCompare(b))
    .map((label) => ({ value: label, label }));

  return (
    <div className="mx-auto max-w-[1080px] space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0 space-y-1">
          <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
            Work
          </p>
          <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
            {project.name}
          </h1>
          {project.description && (
            <p className="text-muted-foreground line-clamp-1 text-sm">{project.description}</p>
          )}
        </div>
        <WorkItemComposerDialog
          mode="create"
          projectId={projectId}
          projectName={project.name}
          teamMembers={teamMembers}
          triggerLabel="New work item"
          triggerClassName="shrink-0"
          openOnComposeParam
        />
      </header>

      <BrowseFilterPanel
        kind="work"
        query={q}
        sort={sort}
        projectId={projectId}
        activeFeatureId={activeFeatureId}
        activeFeatureName={activeFeature?.name}
        statusOptions={[...STATUS_OPTIONS]}
        selectedStatuses={selectedStatuses}
        priorityOptions={[...PRIORITY_OPTIONS]}
        selectedPriorities={selectedPriorities}
        labelOptions={labelOptions}
        selectedLabels={selectedLabels}
        typeOptions={[...TYPE_OPTIONS]}
        selectedTypes={selectedTypes}
        blocked={blocked}
        assignees={teamMembers.map((member) => ({
          value: member.userId,
          label: member.displayName ?? member.email,
        }))}
        activeAssignee={activeAssignee}
      />

      <main className="min-w-0 space-y-3">
        {workItemsError ? (
          <PageError message={workItemsError} />
        ) : paged && paged.items.length > 0 ? (
          <>
            <p className="text-muted-foreground text-xs tabular-nums">
              {Number(paged.total) === 1 ? "1 work item" : `${Number(paged.total)} work items`}
            </p>
            <WorkItemCardGrid
              workItems={paged.items}
              projectId={projectId}
              pathname={pathname}
              searchParams={sp}
            />
            {Number(paged.totalPages) > 1 && (
              <BrowsePagination
                page={page}
                total={Number(paged.total)}
                totalPages={Number(paged.totalPages)}
                hasMore={paged.hasMore}
                makeHref={(nextPage) => buildBrowseHref(pathname, sp, { page: String(nextPage) })}
              />
            )}
            <WorkGuide />
          </>
        ) : (
          <div className="border-border rounded-xl border border-dashed">
            <EmptyState
              icon={hasActiveFilters ? SearchX : ListChecks}
              title={hasActiveFilters ? "No matching work" : "No work yet"}
              description={
                hasActiveFilters
                  ? "Try a broader search or remove a filter to see more of this project's work."
                  : "Create a work item to start making this project's intent true."
              }
            >
              {hasActiveFilters ? (
                <Button
                  variant="outline"
                  size="sm"
                  nativeButton={false}
                  render={<Link href={pathname}>Clear search and filters</Link>}
                />
              ) : (
                <>
                  <WorkItemComposerDialog
                    mode="create"
                    projectId={projectId}
                    projectName={project.name}
                    teamMembers={teamMembers}
                    triggerLabel="New work item"
                  />
                  <WorkGuide />
                </>
              )}
            </EmptyState>
          </div>
        )}
      </main>
    </div>
  );
}
