import { notFound } from "next/navigation";

import { WorkItemComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/work-items/_components/work-item-composer-page";
import {
  getProjectWorkItem,
  getWorkItemAttachments,
  listTeamMembers,
} from "@/lib/api/generated/sdk.gen";
import type { AttachmentResponse, TeamMemberResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function EditWorkItemPage({
  params,
}: {
  params: Promise<{ projectId: string; workItemId: string }>;
}): Promise<React.JSX.Element> {
  const { projectId, workItemId } = await params;

  const serverClient = await createServerClient();

  const [workItemResult, project, attachmentsResult] = await Promise.all([
    getProjectWorkItem({ client: serverClient, path: { projectId, workItemId } }),
    findProject(serverClient, projectId),
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

  const workItem = workItemResult.data.workItem;
  const attachmentsLoaded = !attachmentsResult.error;
  const attachments: AttachmentResponse[] = attachmentsResult.data ?? [];

  return (
    <div className="mx-auto max-w-[720px] space-y-6">
      <header className="border-border space-y-1 border-b pb-5">
        <div className="text-muted-foreground font-mono text-xs">{workItem.ref}</div>
        <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
          Edit work item
        </h1>
      </header>

      <WorkItemComposerPage
        mode="edit"
        initial={workItem}
        initialAttachments={attachments}
        attachmentsLoaded={attachmentsLoaded}
        projectId={projectId}
        projectName={project.name}
        teamMembers={teamMembers}
      />
    </div>
  );
}
