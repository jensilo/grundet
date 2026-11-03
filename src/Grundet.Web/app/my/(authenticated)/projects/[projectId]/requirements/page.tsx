import { FileText, Layers, SearchX } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";
import type * as React from "react";

import { RequirementComposerDialog } from "@/app/my/(authenticated)/projects/[projectId]/requirements/_components/requirement-composer-dialog";
import { BrowseFilterPanel } from "@/components/app/browse-filter-panel";
import {
  type BrowseSearchParams,
  buildBrowseHref,
  getSearchValue,
  getSearchValues,
} from "@/components/app/browse-links";
import { BrowsePagination } from "@/components/app/browse-pagination";
import { EmptyState } from "@/components/app/empty-state";
import { IntentGuide } from "@/components/app/journey-guides";
import { PageError } from "@/components/app/page-error";
import { RequirementCardGrid } from "@/components/app/requirement-card-grid";
import { Button } from "@/components/ui/button";
import { listProjectRequirements } from "@/lib/api/generated/sdk.gen";
import type {
  FeatureResponse,
  PagedResponseOfRequirementResponse,
} from "@/lib/api/generated/types.gen";
import { listAllFeatures } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

const PAGE_SIZE = 24;
const STATUS_OPTIONS = [
  { value: "Draft", label: "Draft" },
  { value: "Active", label: "Active" },
  { value: "Archived", label: "Archived" },
  { value: "WontDo", label: "Won't Do" },
] as const;
const PRIORITY_OPTIONS = [
  { value: "Must", label: "Must" },
  { value: "Should", label: "Should" },
  { value: "Could", label: "Could" },
  { value: "Wont", label: "Won't" },
] as const;
const SORT_OPTIONS = ["priority", "updated", "created"] as const;

async function fetchFeatures(
  serverClient: Awaited<ReturnType<typeof createServerClient>>,
  projectId: string
): Promise<{ features: FeatureResponse[]; error: string | null }> {
  const { features, error } = await listAllFeatures(serverClient, projectId);

  if (error) {
    return { features: [], error: "Could not load features." };
  }

  return { features, error: null };
}

async function fetchRequirements(
  serverClient: Awaited<ReturnType<typeof createServerClient>>,
  projectId: string,
  query: {
    featureId?: string;
    q?: string;
    status: string[];
    priority: string[];
    labels: string[];
    page: number;
    sort: string;
  }
): Promise<{ paged: PagedResponseOfRequirementResponse | null; error: string | null }> {
  const { data, error } = await listProjectRequirements({
    client: serverClient,
    path: { projectId },
    query: {
      featureId: query.featureId,
      q: query.q,
      status: query.status,
      priority: query.priority,
      labels: query.labels,
      page: String(query.page),
      pageSize: String(PAGE_SIZE),
      sort: query.sort,
    },
  });

  if (error || !data) {
    return { paged: null, error: "Could not load intent." };
  }

  return { paged: data, error: null };
}

export default async function RequirementsPage({
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

  const serverClient = await createServerClient();
  const project = await findProject(serverClient, projectId);

  if (!project) {
    notFound();
  }

  const { features, error: featuresError } = await fetchFeatures(serverClient, projectId);

  if (featuresError || features.length === 0) {
    return (
      <div className="mx-auto max-w-[1080px] space-y-6">
        <header className="flex flex-wrap items-end justify-between gap-4">
          <div className="min-w-0 space-y-1">
            <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
              Intent
            </p>
            <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
              {project.name}
            </h1>
          </div>
          <Button
            variant="outline"
            size="sm"
            nativeButton={false}
            render={<Link href={`/projects/${projectId}/features`}>Manage features</Link>}
          />
        </header>
        {featuresError ? (
          <PageError message={featuresError} />
        ) : (
          <div className="border-border rounded-xl border border-dashed">
            <EmptyState
              icon={Layers}
              title="No features yet"
              description="Add a feature to this project to start writing intent."
            >
              <Button
                size="sm"
                nativeButton={false}
                render={<Link href={`/projects/${projectId}/features`}>Manage features</Link>}
              />
              <IntentGuide />
            </EmptyState>
          </div>
        )}
      </div>
    );
  }

  const requestedFeatureId = getSearchValue(sp, "featureId");
  const activeFeatureId = requestedFeatureId || undefined;
  if (activeFeatureId && !features.some((feature) => feature.id === activeFeatureId)) {
    notFound();
  }

  const { paged, error: reqError } = await fetchRequirements(serverClient, projectId, {
    featureId: activeFeatureId,
    q: q || undefined,
    status: selectedStatuses,
    priority: selectedPriorities,
    labels: selectedLabels,
    page,
    sort,
  });
  const pathname = `/projects/${projectId}/requirements`;
  const hasActiveFilters = Boolean(
    q ||
    activeFeatureId ||
    selectedStatuses.length > 0 ||
    selectedPriorities.length > 0 ||
    selectedLabels.length > 0
  );
  const featureNamesById = new Map(features.map((feature) => [feature.id, feature.name]));
  // The composer's current-feature label: the active filter feature, else the first feature. The
  // page already holds the full list (for the filter facet + card name map), so derive locally.
  const composerActiveFeature =
    (activeFeatureId && features.find((feature) => feature.id === activeFeatureId)) || features[0];
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
            Intent
          </p>
          <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
            {project.name}
          </h1>
          {project.description && (
            <p className="text-muted-foreground line-clamp-1 text-sm">{project.description}</p>
          )}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            nativeButton={false}
            render={<Link href={`/projects/${projectId}/features`}>Manage features</Link>}
          />
          <RequirementComposerDialog
            mode="create"
            projectId={projectId}
            projectName={project.name}
            featureId={composerActiveFeature.id}
            currentFeature={{ id: composerActiveFeature.id, name: composerActiveFeature.name }}
            featureCount={features.length}
            triggerLabel="New intent"
            openOnComposeParam
          />
        </div>
      </header>

      <BrowseFilterPanel
        kind="intent"
        query={q}
        sort={sort}
        projectId={projectId}
        activeFeatureId={activeFeatureId}
        activeFeatureName={activeFeatureId ? featureNamesById.get(activeFeatureId) : undefined}
        statusOptions={[...STATUS_OPTIONS]}
        selectedStatuses={selectedStatuses}
        priorityOptions={[...PRIORITY_OPTIONS]}
        selectedPriorities={selectedPriorities}
        labelOptions={labelOptions}
        selectedLabels={selectedLabels}
      />

      <main className="min-w-0 space-y-3">
        {reqError ? (
          <PageError message={reqError} />
        ) : paged && paged.items.length > 0 ? (
          <>
            <p className="text-muted-foreground text-xs tabular-nums">
              {Number(paged.total) === 1 ? "1 intent item" : `${Number(paged.total)} intent items`}
            </p>
            <RequirementCardGrid
              requirements={paged.items}
              projectId={projectId}
              featureNamesById={featureNamesById}
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
            <IntentGuide />
          </>
        ) : (
          <div className="border-border rounded-xl border border-dashed">
            <EmptyState
              icon={hasActiveFilters ? SearchX : FileText}
              title={hasActiveFilters ? "No matching intent" : "No intent yet"}
              description={
                hasActiveFilters
                  ? "Try a broader search or remove a filter to see more of this project's intent."
                  : "Write the first intent item to capture what should be true for this project."
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
                  <RequirementComposerDialog
                    mode="create"
                    projectId={projectId}
                    projectName={project.name}
                    featureId={features[0].id}
                    currentFeature={{ id: features[0].id, name: features[0].name }}
                    featureCount={features.length}
                    triggerLabel="New intent"
                  />
                  <IntentGuide />
                </>
              )}
            </EmptyState>
          </div>
        )}
      </main>
    </div>
  );
}
