import Link from "next/link";
import { notFound } from "next/navigation";

import { AcceptanceCriterion } from "@/components/app/acceptance-criterion";
import { AttachmentGallery } from "@/components/app/attachment-gallery";
import { buildLabelFilterHref } from "@/components/app/browse-links";
import { PageError } from "@/components/app/page-error";
import { PriorityBadge } from "@/components/app/priority-badge";
import { ReferenceText } from "@/components/app/reference-text";
import { StatusBadge } from "@/components/app/status-badge";
import { VersionHistoryDialog } from "@/components/app/version-history-dialog";
import { VersionTimeline } from "@/components/app/version-timeline";
import { Badge } from "@/components/ui/badge";
import {
  getProjectRequirement,
  getRequirementAttachments,
  getRequirementVersionHistory,
  getRequirementWorkItems,
} from "@/lib/api/generated/sdk.gen";
import type {
  AttachmentResponse,
  LinkedWorkItemResponse,
  RequirementVersionEntry,
} from "@/lib/api/generated/types.gen";
import { getFeatureById, getFeatureCountAndFirst } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";
import { formatRelativeDate } from "@/lib/format-date";

import { EditDeleteActions } from "./edit-delete-actions";
import { IntentWorkSection } from "./intent-work-section";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function RequirementDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ projectId: string; reqId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}): Promise<React.JSX.Element> {
  const { projectId, reqId } = await params;
  const sp = await searchParams;
  const backParam = typeof sp["back"] === "string" ? sp["back"] : undefined;
  const backHref = backParam
    ? `/projects/${projectId}/requirements?${decodeURIComponent(backParam)}`
    : `/projects/${projectId}/requirements`;

  const serverClient = await createServerClient();

  // Requirement, feature count, project, version history, linked work, and attachments are independent.
  const [
    requirementResult,
    featureCountResult,
    project,
    versionHistoryResult,
    linkedWorkResult,
    attachmentsResult,
  ] = await Promise.all([
    getProjectRequirement({ client: serverClient, path: { projectId, requirementId: reqId } }),
    getFeatureCountAndFirst(serverClient, projectId),
    findProject(serverClient, projectId),
    getRequirementVersionHistory({ client: serverClient, path: { requirementId: reqId } }),
    getRequirementWorkItems({ client: serverClient, path: { requirementId: reqId } }),
    getRequirementAttachments({ client: serverClient, path: { projectId, requirementId: reqId } }),
  ]);

  if (requirementResult.error || !requirementResult.data) {
    notFound();
  }

  if (!project) {
    notFound();
  }

  const requirement = requirementResult.data;
  const isDeleted = requirement.changeType === "Deleted";
  const currentFeature = await getFeatureById(serverClient, projectId, requirement.featureId);
  const featureName = currentFeature?.name ?? null;
  const versionHistory: RequirementVersionEntry[] = versionHistoryResult.data ?? [];
  const historyError = !!versionHistoryResult.error;
  const linkedWork: LinkedWorkItemResponse[] = linkedWorkResult.data ?? [];
  // A failed attachment load must not let a text-only edit drop the existing set — the composer
  // omits attachmentIds when this is false (the backend then preserves them).
  const attachmentsLoaded = !attachmentsResult.error;
  const attachments: AttachmentResponse[] = attachmentsResult.data ?? [];
  const lastChangedBy =
    versionHistory.find((v) => v.changedByUserId === requirement.changedByUserId)
      ?.changedByDisplayName ?? null;

  return (
    <div className="mx-auto max-w-[1200px] space-y-6">
      {/* Context/action bar */}
      <div className="flex items-center justify-between gap-4">
        <Link
          href={backHref}
          className="text-muted-foreground hover:text-foreground shrink-0 text-xs transition-colors"
        >
          ← Intent
        </Link>
        <EditDeleteActions
          requirement={requirement}
          projectId={projectId}
          projectName={project.name}
          currentFeature={currentFeature ?? { id: requirement.featureId, name: "" }}
          featureCount={featureCountResult.count}
          attachments={attachments}
          attachmentsLoaded={attachmentsLoaded}
          deleted={isDeleted}
        />
      </div>

      {isDeleted && (
        <div className="border-destructive/30 bg-destructive-50 text-destructive-700 rounded-lg border px-4 py-3 text-sm">
          This intent was deleted. It is kept for history and can no longer be changed.
        </div>
      )}

      {/* 2/3 + 1/3 grid */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Main column */}
        <div className="space-y-6 lg:col-span-2">
          {/* Hero block */}
          <div className="space-y-5">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-primary font-mono text-xs font-medium">{requirement.ref}</span>
              <span className="bg-border h-3.5 w-px" aria-hidden />
              {isDeleted && <Badge variant="destructive">Deleted</Badge>}
              <StatusBadge status={requirement.status} />
              <PriorityBadge priority={requirement.priority} />
              {requirement.labels.map((l) => (
                <Link
                  key={l}
                  href={buildLabelFilterHref(`/projects/${projectId}/requirements`, l)}
                  className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 rounded-sm text-xs font-medium transition-colors focus-visible:ring-2 focus-visible:outline-none"
                >
                  <span className="text-muted-foreground/50">#</span>
                  {l}
                </Link>
              ))}
            </div>

            <p className="text-foreground text-xl leading-[1.45] font-medium">
              <ReferenceText projectId={projectId} text={requirement.text} />
            </p>

            {requirement.rationale && (
              <div className="border-primary bg-accent/60 rounded-r-md border-l-2 py-3 pr-4 pl-4">
                <p className="text-primary text-xs font-medium tracking-[0.04em] uppercase">
                  Because
                </p>
                <p className="text-foreground mt-1.5 text-sm leading-relaxed">
                  <ReferenceText projectId={projectId} text={requirement.rationale} />
                </p>
              </div>
            )}
          </div>

          <section className="bg-card ring-foreground/10 rounded-xl p-5 ring-1">
            <div className="mb-4 flex items-baseline justify-between gap-3">
              <h2 className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
                Acceptance criteria
              </h2>
              {requirement.acceptanceCriteria.length > 0 && (
                <span className="text-muted-foreground text-xs">
                  {requirement.acceptanceCriteria.length}
                </span>
              )}
            </div>
            {requirement.acceptanceCriteria.length > 0 ? (
              <AcceptanceCriterion items={requirement.acceptanceCriteria} projectId={projectId} />
            ) : (
              <p className="text-muted-foreground text-sm">
                None yet – add criteria so the team knows when this intent is true.
              </p>
            )}
          </section>

          <AttachmentGallery projectId={projectId} attachments={attachments} />

          <IntentWorkSection
            projectId={projectId}
            requirementId={requirement.requirementId}
            workItems={linkedWork}
          />
        </div>

        {/* Sidebar */}
        <aside className="space-y-6 lg:col-span-1">
          <section className="bg-card ring-foreground/10 rounded-xl p-5 ring-1">
            <h2 className="text-muted-foreground mb-4 text-xs font-medium tracking-[0.04em] uppercase">
              Details
            </h2>
            <dl className="space-y-3 text-sm">
              {featureName && (
                <div className="flex items-center justify-between gap-3">
                  <dt className="text-muted-foreground shrink-0">Feature</dt>
                  <dd className="text-foreground min-w-0 truncate text-right font-medium">
                    {featureName}
                  </dd>
                </div>
              )}
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted-foreground shrink-0">Project</dt>
                <dd className="text-foreground min-w-0 truncate text-right font-medium">
                  {project.name}
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted-foreground shrink-0">Last changed</dt>
                <dd className="text-foreground shrink-0">
                  {formatRelativeDate(requirement.changedAt)}
                </dd>
              </div>
              {lastChangedBy && (
                <div className="flex items-center justify-between gap-3">
                  <dt className="text-muted-foreground shrink-0">Changed by</dt>
                  <dd className="text-foreground min-w-0 truncate text-right font-medium">
                    {lastChangedBy}
                  </dd>
                </div>
              )}
            </dl>
          </section>

          <section className="bg-card ring-foreground/10 rounded-xl p-5 ring-1">
            <div className="mb-4 flex items-baseline justify-between gap-3">
              <h2 className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
                Activity
              </h2>
              {versionHistory.length > 0 && (
                <span className="text-muted-foreground text-xs">
                  {versionHistory.length} {versionHistory.length === 1 ? "version" : "versions"}
                </span>
              )}
            </div>
            {historyError ? (
              <PageError message="Activity could not be loaded." />
            ) : (
              <VersionTimeline versions={versionHistory} projectId={projectId} />
            )}
            {versionHistory.length > 0 && (
              <VersionHistoryDialog
                versions={versionHistory}
                projectId={projectId}
                reference={requirement.ref}
                title={requirement.text}
                historyHref={`/projects/${projectId}/requirements/${reqId}/history`}
              />
            )}
          </section>
        </aside>
      </div>
    </div>
  );
}
