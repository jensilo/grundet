import Link from "next/link";
import { notFound } from "next/navigation";

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
  getMe,
  getProjectWorkItem,
  getWorkItemAttachments,
  getWorkItemVersionHistory,
  listTeamMembers,
} from "@/lib/api/generated/sdk.gen";
import type {
  AttachmentResponse,
  TeamMemberResponse,
  WorkItemVersionEntry,
} from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";
import { formatRelativeDate } from "@/lib/format-date";

import { EditDeleteActions } from "./edit-delete-actions";
import { WorkItemAssigneeControl } from "./work-item-assignee-control";
import { WorkItemLinksSection } from "./work-item-links-section";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function WorkItemDetailPage({
  params,
}: {
  params: Promise<{ projectId: string; workItemId: string }>;
}): Promise<React.JSX.Element> {
  const { projectId, workItemId } = await params;

  const serverClient = await createServerClient();

  // Core detail, project, version history, current user, and attachments are independent.
  const [workItemResult, project, versionHistoryResult, meResult, attachmentsResult] =
    await Promise.all([
      getProjectWorkItem({ client: serverClient, path: { projectId, workItemId } }),
      findProject(serverClient, projectId),
      getWorkItemVersionHistory({ client: serverClient, path: { workItemId } }),
      getMe({ client: serverClient }),
      getWorkItemAttachments({ client: serverClient, path: { projectId, workItemId } }),
    ]);

  if (workItemResult.error || !workItemResult.data) {
    notFound();
  }

  if (!project) {
    notFound();
  }

  const { data: teamMembersData } = await listTeamMembers({
    client: serverClient,
    path: { teamId: project.teamId },
  });
  const teamMembers: TeamMemberResponse[] = teamMembersData ?? [];

  const { workItem, links } = workItemResult.data;
  const isDeleted = workItem.changeType === "Deleted";
  const versionHistory: WorkItemVersionEntry[] = versionHistoryResult.data ?? [];
  const historyError = !!versionHistoryResult.error;
  const currentUserId = meResult.data?.id ?? null;
  // A failed attachment load must not let a text-only edit drop the existing set — the composer
  // omits attachmentIds when this is false (the backend then preserves them).
  const attachmentsLoaded = !attachmentsResult.error;
  const attachments: AttachmentResponse[] = attachmentsResult.data ?? [];

  return (
    <div className="mx-auto max-w-[1200px] space-y-6">
      {/* Context/action bar */}
      <div className="flex items-center justify-between gap-4">
        <Link
          href={`/projects/${projectId}/work-items`}
          className="text-muted-foreground hover:text-foreground shrink-0 text-xs transition-colors"
        >
          ← Work
        </Link>
        <EditDeleteActions
          workItem={workItem}
          projectId={projectId}
          projectName={project.name}
          teamMembers={teamMembers}
          attachments={attachments}
          attachmentsLoaded={attachmentsLoaded}
          deleted={isDeleted}
        />
      </div>

      {isDeleted && (
        <div className="border-destructive/30 bg-destructive-50 text-destructive-700 rounded-lg border px-4 py-3 text-sm">
          This work item was deleted. It is kept for history and can no longer be changed.
        </div>
      )}

      {/* 2/3 + 1/3 grid */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Main column */}
        <div className="space-y-6 lg:col-span-2">
          {/* Hero block */}
          <div className="space-y-5">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-primary font-mono text-xs font-medium">{workItem.ref}</span>
              <span className="bg-border h-3.5 w-px" aria-hidden />
              {isDeleted && <Badge variant="destructive">Deleted</Badge>}
              <StatusBadge status={workItem.status} />
              <PriorityBadge priority={workItem.priority} />
              <span className="border-border text-muted-foreground inline-flex h-5 items-center rounded-sm border px-1.5 text-xs font-medium">
                {workItem.type}
              </span>
              {workItem.labels.map((l) => (
                <Link
                  key={l}
                  href={buildLabelFilterHref(`/projects/${projectId}/work-items`, l)}
                  className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 rounded-sm text-xs font-medium transition-colors focus-visible:ring-2 focus-visible:outline-none"
                >
                  <span className="text-muted-foreground/50">#</span>
                  {l}
                </Link>
              ))}
            </div>

            <p className="text-foreground text-xl leading-[1.45] font-medium">
              <ReferenceText projectId={projectId} text={workItem.title} />
            </p>

            {/* Blocked banner */}
            {workItem.blockedReason && (
              <div className="bg-warning-50 border-warning-500 rounded-r-md border-l-2 px-4 py-3">
                <p className="text-warning-700 text-xs font-medium tracking-[0.04em] uppercase">
                  Blocked
                </p>
                <p className="text-warning-700 mt-1.5 text-sm leading-relaxed">
                  <ReferenceText projectId={projectId} text={workItem.blockedReason} />
                </p>
              </div>
            )}
          </div>

          {/* Overview */}
          {(workItem.description || workItem.reason) && (
            <section className="bg-card ring-foreground/10 space-y-5 rounded-xl p-5 ring-1">
              {workItem.description && (
                <div>
                  <h2 className="text-muted-foreground mb-2 text-xs font-medium tracking-[0.04em] uppercase">
                    Description
                  </h2>
                  <p className="text-foreground max-w-prose text-sm leading-relaxed">
                    <ReferenceText projectId={projectId} text={workItem.description} />
                  </p>
                </div>
              )}
              {workItem.reason && (
                <div className="border-primary bg-accent/60 max-w-prose rounded-r-md border-l-2 py-3 pr-4 pl-4">
                  <p className="text-primary text-xs font-medium tracking-[0.04em] uppercase">
                    Why
                  </p>
                  <p className="text-foreground mt-1.5 text-sm leading-relaxed">
                    <ReferenceText projectId={projectId} text={workItem.reason} />
                  </p>
                </div>
              )}
            </section>
          )}

          <AttachmentGallery projectId={projectId} attachments={attachments} />

          {/* Linked intent */}
          <WorkItemLinksSection
            projectId={projectId}
            workItemId={workItem.workItemId}
            links={links}
            deleted={isDeleted}
          />
        </div>

        {/* Sidebar */}
        <aside className="space-y-6 lg:col-span-1">
          <section className="bg-card ring-foreground/10 rounded-xl p-5 ring-1">
            <h2 className="text-muted-foreground mb-4 text-xs font-medium tracking-[0.04em] uppercase">
              Details
            </h2>
            <dl className="space-y-3 text-sm">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted-foreground shrink-0">Project</dt>
                <dd className="text-foreground min-w-0 truncate text-right font-medium">
                  {project.name}
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted-foreground shrink-0">Last changed</dt>
                <dd className="text-foreground shrink-0">
                  {formatRelativeDate(workItem.changedAt)}
                </dd>
              </div>
            </dl>

            <div className="border-border mt-4 border-t pt-4">
              {currentUserId && !isDeleted ? (
                <WorkItemAssigneeControl
                  projectId={projectId}
                  workItemId={workItem.workItemId}
                  assignedToUserId={workItem.assignedToUserId}
                  teamMembers={teamMembers}
                  currentUserId={currentUserId}
                />
              ) : (
                <div className="flex items-center justify-between gap-3 text-sm">
                  <span className="text-muted-foreground">Assigned</span>
                  <span className="text-foreground font-medium">
                    {workItem.assignedToUserId ? "Assigned" : "Nobody yet"}
                  </span>
                </div>
              )}
            </div>
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
                reference={workItem.ref}
                title={workItem.title}
                historyHref={`/projects/${projectId}/work-items/${workItemId}/history`}
              />
            )}
          </section>
        </aside>
      </div>
    </div>
  );
}
