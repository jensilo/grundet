import { History, SearchX } from "lucide-react";
import { notFound } from "next/navigation";
import type * as React from "react";

import {
  type BrowseSearchParams,
  buildBrowseHref,
  getSearchValue,
  getSearchValues,
} from "@/components/app/browse-links";
import { BrowsePagination } from "@/components/app/browse-pagination";
import { ChangelogFilterPanel } from "@/components/app/changelog-filter-panel";
import { ChangelogTimeline } from "@/components/app/changelog-timeline";
import { EmptyState } from "@/components/app/empty-state";
import { PageError } from "@/components/app/page-error";
import { getProjectChangelog, getProjectChangelogAuthors } from "@/lib/api/generated/sdk.gen";
import type { PagedResponseOfChangelogEntryResponse } from "@/lib/api/generated/types.gen";
import { getFeatureById } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";
import { CHANGE_TYPE_OPTIONS, ENTITY_KIND_OPTIONS } from "@/lib/changelog";

const PAGE_SIZE = 50;
const ENTITY_VALUES = ENTITY_KIND_OPTIONS.map((option) => option.value);
const CHANGE_VALUES = CHANGE_TYPE_OPTIONS.map((option) => option.value);

export default async function ChangelogPage({
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

  const selectedEntityTypes = getSearchValues(sp, "entityTypes", ENTITY_VALUES);
  const selectedChangeTypes = getSearchValues(sp, "changeTypes", CHANGE_VALUES);
  const activeFeatureId = getSearchValue(sp, "featureId") || undefined;
  const activeAuthorId = getSearchValue(sp, "authorId") || undefined;
  const from = getSearchValue(sp, "from");
  const to = getSearchValue(sp, "to");

  const serverClient = await createServerClient();
  const project = await findProject(serverClient, projectId);

  if (!project) {
    notFound();
  }

  const [changelogResult, activeFeature, authorsResult] = await Promise.all([
    getProjectChangelog({
      client: serverClient,
      path: { projectId },
      query: {
        entityTypes: selectedEntityTypes.length > 0 ? selectedEntityTypes : undefined,
        changeTypes: selectedChangeTypes.length > 0 ? selectedChangeTypes : undefined,
        featureId: activeFeatureId,
        authorId: activeAuthorId,
        from: from || undefined,
        to: to || undefined,
        page: String(page),
        pageSize: String(PAGE_SIZE),
      },
    }),
    activeFeatureId
      ? getFeatureById(serverClient, projectId, activeFeatureId)
      : Promise.resolve(null),
    getProjectChangelogAuthors({ client: serverClient, path: { projectId } }),
  ]);

  const paged: PagedResponseOfChangelogEntryResponse | null = changelogResult.data ?? null;
  const changelogError = !!changelogResult.error;

  // Authors include people who have left the team so their past changes stay filterable.
  const authorOptions = (authorsResult.data ?? []).map((author) => ({
    value: author.userId,
    label: author.isActiveMember ? author.displayName : `${author.displayName} – former member`,
  }));

  const pathname = `/projects/${projectId}/changelog`;
  const hasActiveFilters = Boolean(
    selectedEntityTypes.length > 0 ||
    selectedChangeTypes.length > 0 ||
    activeFeatureId ||
    activeAuthorId ||
    from ||
    to
  );

  return (
    <div className="mx-auto max-w-[1080px] space-y-6">
      <header className="space-y-1">
        <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
          Changelog
        </p>
        <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
          {project.name}
        </h1>
        <p className="text-muted-foreground text-sm">
          Every change to intent, work, and links in this project – newest first.
        </p>
      </header>

      <ChangelogFilterPanel
        projectId={projectId}
        authors={authorOptions}
        activeFeatureId={activeFeatureId}
        activeFeatureName={activeFeature?.name}
        activeAuthorId={activeAuthorId}
        selectedEntityTypes={selectedEntityTypes}
        selectedChangeTypes={selectedChangeTypes}
        from={from}
        to={to}
      />

      <main className="min-w-0 space-y-4">
        {changelogError ? (
          <PageError message="Could not load the changelog." />
        ) : paged && paged.items.length > 0 ? (
          <>
            <ChangelogTimeline entries={paged.items} projectId={projectId} />
            {Number(paged.totalPages) > 1 && (
              <BrowsePagination
                page={page}
                total={Number(paged.total)}
                totalPages={Number(paged.totalPages)}
                hasMore={paged.hasMore}
                makeHref={(nextPage) => buildBrowseHref(pathname, sp, { page: String(nextPage) })}
              />
            )}
          </>
        ) : (
          <div className="border-border rounded-xl border border-dashed">
            <EmptyState
              icon={hasActiveFilters ? SearchX : History}
              title={hasActiveFilters ? "No matching changes" : "No changes yet"}
              description={
                hasActiveFilters
                  ? "Try a broader date range or remove a filter to see more of this project's history."
                  : "As the team edits intent and work, every version will appear here."
              }
            />
          </div>
        )}
      </main>
    </div>
  );
}
