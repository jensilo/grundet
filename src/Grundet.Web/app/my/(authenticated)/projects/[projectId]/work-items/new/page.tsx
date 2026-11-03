import { notFound } from "next/navigation";

import { WorkItemComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/work-items/_components/work-item-composer-page";
import { listTeamMembers } from "@/lib/api/generated/sdk.gen";
import type { TeamMemberResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";
import { createServerClient } from "@/lib/api/server";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function NewWorkItemPage({
  params,
}: {
  params: Promise<{ projectId: string }>;
}): Promise<React.JSX.Element> {
  const { projectId } = await params;

  const serverClient = await createServerClient();
  const project = await findProject(serverClient, projectId);

  if (!project) {
    notFound();
  }

  const { data: teamMembersData } = await listTeamMembers({
    client: serverClient,
    path: { teamId: project.teamId },
  });
  const teamMembers: TeamMemberResponse[] = teamMembersData ?? [];

  return (
    <div className="mx-auto max-w-[720px] space-y-6">
      <header className="border-border border-b pb-5">
        <h1 className="text-foreground text-2xl leading-[1.25] font-semibold tracking-[-0.01em]">
          New work item
        </h1>
      </header>

      <WorkItemComposerPage
        mode="create"
        projectId={projectId}
        projectName={project.name}
        teamMembers={teamMembers}
      />
    </div>
  );
}
