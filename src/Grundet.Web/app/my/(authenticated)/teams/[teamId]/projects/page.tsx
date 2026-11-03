import Link from "next/link";
import { notFound } from "next/navigation";

import { ProjectGuide } from "@/components/app/journey-guides";
import { PageError } from "@/components/app/page-error";
import { Button } from "@/components/ui/button";
import { getMe, getTeam, listProjects, listTeamMembers } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

import { ProjectsPanel } from "../_components/projects-panel";

export default async function TeamProjectsPage({
  params,
}: {
  params: Promise<{ teamId: string }>;
}): Promise<React.JSX.Element> {
  const { teamId } = await params;
  const serverClient = await createServerClient();

  const [
    { data: team, error: teamError },
    { data: projects, error: projectsError },
    { data: members },
    { data: me },
  ] = await Promise.all([
    getTeam({ client: serverClient, path: { teamId } }),
    listProjects({ client: serverClient, path: { teamId }, query: { pageSize: 200 } }),
    listTeamMembers({ client: serverClient, path: { teamId } }),
    getMe({ client: serverClient }),
  ]);

  if (teamError || !team) {
    notFound();
  }

  const isLead =
    members?.some((member) => member.userId === me?.id && member.role === "Lead") ?? false;

  return (
    <div className="mx-auto max-w-[1080px] space-y-6">
      <header className="space-y-4">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div className="max-w-[720px] space-y-2">
            <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
              Projects
            </h1>
            <p className="text-muted-foreground text-sm">
              Open existing projects or create a new one for {team.name}.
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            nativeButton={false}
            render={<Link href={`/teams/${team.id}`}>Team workspace</Link>}
          />
        </div>
      </header>

      {projectsError ? (
        <PageError message="Could not load projects. Please try again." />
      ) : (
        <ProjectsPanel
          teamId={teamId}
          initialProjects={projects?.items ?? []}
          canCreateProjects={true}
          canManageProjects={isLead}
        />
      )}

      <ProjectGuide />
    </div>
  );
}
