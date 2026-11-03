import { notFound } from "next/navigation";

import { RequirementComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/requirements/_components/requirement-composer-page";
import { getProjectRequirement, getRequirementAttachments } from "@/lib/api/generated/sdk.gen";
import { getFeatureById, getFeatureCountAndFirst } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function EditRequirementPage({
  params,
}: {
  params: Promise<{ projectId: string; reqId: string }>;
}): Promise<React.JSX.Element> {
  const { projectId, reqId } = await params;

  const serverClient = await createServerClient();

  const [requirementResult, featuresResult, project, attachmentsResult] = await Promise.all([
    getProjectRequirement({ client: serverClient, path: { projectId, requirementId: reqId } }),
    getFeatureCountAndFirst(serverClient, projectId),
    findProject(serverClient, projectId),
    getRequirementAttachments({ client: serverClient, path: { projectId, requirementId: reqId } }),
  ]);

  if (requirementResult.error || !requirementResult.data) {
    notFound();
  }

  if (!project) {
    notFound();
  }

  const requirement = requirementResult.data;
  const currentFeature = (await getFeatureById(serverClient, projectId, requirement.featureId)) ?? {
    id: requirement.featureId,
    name: "",
  };
  // Distinguish "no attachments" from "load failed": a failed preload must not let a text-only save
  // drop the existing set (the composer omits attachmentIds when this is false).
  const attachmentsLoaded = !attachmentsResult.error;
  const attachments = attachmentsResult.data ?? [];

  return (
    <div className="mx-auto max-w-[720px] space-y-6">
      <header className="border-border space-y-1 border-b pb-5">
        <div className="text-muted-foreground font-mono text-xs">{requirement.ref}</div>
        <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
          Edit intent
        </h1>
      </header>

      <RequirementComposerPage
        mode="edit"
        initial={requirement}
        currentFeature={currentFeature}
        featureCount={featuresResult.count}
        initialAttachments={attachments}
        attachmentsLoaded={attachmentsLoaded}
        projectId={projectId}
        projectName={project.name}
      />
    </div>
  );
}
