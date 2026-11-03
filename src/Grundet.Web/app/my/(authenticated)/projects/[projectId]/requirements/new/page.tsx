import { Layers } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";

import { RequirementComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/requirements/_components/requirement-composer-page";
import { EmptyState } from "@/components/app/empty-state";
import { IntentGuide } from "@/components/app/journey-guides";
import { Button } from "@/components/ui/button";
import { getProjectRequirement } from "@/lib/api/generated/sdk.gen";
import { getFeatureById, getFeatureCountAndFirst } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function NewRequirementPage({
  params,
  searchParams,
}: {
  params: Promise<{ projectId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}): Promise<React.JSX.Element> {
  const { projectId } = await params;
  const sp = await searchParams;

  const serverClient = await createServerClient();

  const [project, featuresResult] = await Promise.all([
    findProject(serverClient, projectId),
    getFeatureCountAndFirst(serverClient, projectId),
  ]);

  if (!project) {
    notFound();
  }

  if (featuresResult.count === 0 || !featuresResult.first) {
    return (
      <div className="mx-auto max-w-[720px] space-y-6">
        <header className="border-border space-y-1 border-b pb-5">
          <div className="text-muted-foreground text-xs">{project.name}</div>
          <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
            New intent
          </h1>
        </header>
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
      </div>
    );
  }

  // Resolve the active feature, in precedence order:
  //   1. explicit ?featureId= (a deliberate choice always wins)
  //   2. ?fromRequirement= — the intent the user was viewing when they hit "+ New intent"; we
  //      adopt its feature so the new sibling intent lands in the same context. Fails soft: an
  //      unresolvable/forbidden requirement just falls through to the first feature, never errors.
  //   3. first feature
  const rawFeatureId = sp["featureId"];
  const requestedFeatureId = typeof rawFeatureId === "string" ? rawFeatureId : undefined;

  let resolvedFeatureId = requestedFeatureId;
  if (!resolvedFeatureId) {
    const rawFromRequirement = sp["fromRequirement"];
    const fromRequirementId =
      typeof rawFromRequirement === "string" ? rawFromRequirement : undefined;
    if (fromRequirementId) {
      const { data } = await getProjectRequirement({
        client: serverClient,
        path: { projectId, requirementId: fromRequirementId },
      });
      if (data) {
        resolvedFeatureId = data.featureId;
      }
    }
  }

  // Default to the most-recent feature. A resolved target (explicit ?featureId= or adopted from
  // ?fromRequirement=) is fetched by id; an unresolvable/forbidden one falls back to the default.
  let activeFeature = featuresResult.first;
  if (resolvedFeatureId && resolvedFeatureId !== activeFeature.id) {
    activeFeature =
      (await getFeatureById(serverClient, projectId, resolvedFeatureId)) ?? activeFeature;
  }

  return (
    <div className="mx-auto max-w-[720px] space-y-6">
      <header className="border-border border-b pb-5">
        <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
          New intent
        </h1>
      </header>

      <RequirementComposerPage
        mode="create"
        featureId={activeFeature.id}
        currentFeature={activeFeature}
        featureCount={featuresResult.count}
        projectId={projectId}
        projectName={project.name}
      />
    </div>
  );
}
