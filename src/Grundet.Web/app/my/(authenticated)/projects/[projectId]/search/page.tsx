import Link from "next/link";
import { notFound } from "next/navigation";

import { EmptyState } from "@/components/app/empty-state";
import { PageError } from "@/components/app/page-error";
import { StatusBadge } from "@/components/app/status-badge";
import { searchProject } from "@/lib/api/generated/sdk.gen";
import type { SearchGroupedResponse, SearchResultResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

import { SearchForm } from "./_components/search-form";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function resultHref(projectId: string, result: SearchResultResponse): string {
  switch (result.type) {
    case "requirement":
      return `/projects/${projectId}/requirements/${result.id}`;
    case "workitem":
      return `/projects/${projectId}/work-items/${result.id}`;
    default:
      return `/projects/${projectId}/features`;
  }
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function ResultRow({
  projectId,
  result,
}: {
  projectId: string;
  result: SearchResultResponse;
}): React.JSX.Element {
  const href = resultHref(projectId, result);
  return (
    <li className="border-border flex items-center justify-between gap-3 border-b px-4 py-2.5 last:border-0">
      <div className="min-w-0 flex-1">
        <Link href={href} className="text-foreground text-sm font-medium hover:underline">
          {result.title}
        </Link>
        {result.subTitle && <p className="text-muted-foreground text-xs">{result.subTitle}</p>}
      </div>
      {result.status && <StatusBadge status={result.status} />}
    </li>
  );
}

function ResultSection({
  title,
  results,
  projectId,
}: {
  title: string;
  results: SearchResultResponse[];
  projectId: string;
}): React.JSX.Element | null {
  if (results.length === 0) return null;
  return (
    <div className="space-y-2">
      <h2 className="text-foreground text-sm font-semibold">{title}</h2>
      <div className="border-border rounded-md border">
        <ul>
          {results.map((r) => (
            <ResultRow key={`${r.type}:${r.id}`} projectId={projectId} result={r} />
          ))}
        </ul>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function SearchPage({
  params,
  searchParams,
}: {
  params: Promise<{ projectId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}): Promise<React.JSX.Element> {
  const { projectId } = await params;
  const sp = await searchParams;

  const rawQ = sp["q"];
  const q = typeof rawQ === "string" ? rawQ.trim() : "";

  const serverClient = await createServerClient();
  const project = await findProject(serverClient, projectId);

  if (!project) {
    notFound();
  }

  let results: SearchGroupedResponse | null = null;
  let fetchError: string | null = null;

  if (q) {
    const result = await searchProject({
      client: serverClient,
      path: { projectId },
      query: { q },
    });

    if (result.error || !result.data) {
      fetchError = "Could not load search results. Please try again.";
    } else {
      results = result.data;
    }
  }

  const totalResults = results
    ? results.requirements.length + results.workItems.length + results.features.length
    : 0;

  return (
    <div className="mx-auto max-w-[1200px] space-y-6">
      <header className="space-y-3">
        <div className="space-y-1">
          <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
            Search
          </h1>
          <p className="text-muted-foreground text-xs">{project.name}</p>
        </div>
        <SearchForm projectId={projectId} defaultValue={q} />
      </header>

      {fetchError && <PageError message={fetchError} />}

      {!q && (
        <EmptyState
          title="Search this project"
          description="Find intent, work, and features by title, keyword, or ID."
        />
      )}

      {q && results && totalResults === 0 && (
        <p className="text-muted-foreground py-16 text-center text-sm">
          No results for &ldquo;{q}&rdquo;.
        </p>
      )}

      {results && totalResults > 0 && (
        <>
          <ResultSection title="Intent" results={results.requirements} projectId={projectId} />
          <ResultSection title="Work" results={results.workItems} projectId={projectId} />
          <ResultSection title="Features" results={results.features} projectId={projectId} />
        </>
      )}
    </div>
  );
}
