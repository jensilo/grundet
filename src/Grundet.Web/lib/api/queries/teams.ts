import { listProjects, listTeams } from "@/lib/api/generated/sdk.gen";
import type { ProjectResponse, TeamListItemResponse } from "@/lib/api/generated/types.gen";
import type { createServerClient } from "@/lib/api/server";

type ServerClient = Awaited<ReturnType<typeof createServerClient>>;

export type TeamWithProjects = {
  team: TeamListItemResponse;
  projects: ProjectResponse[];
  // Total project count from the paginated response — may exceed projects.length,
  // which is capped at the requested pageSize. Use this for count labels.
  projectCount: number;
  failed?: boolean;
};

export async function fetchTeamsWithProjects(serverClient: ServerClient): Promise<{
  teamsWithProjects: TeamWithProjects[];
  error: string | null;
}> {
  const { data: teams, error: teamsError } = await listTeams({ client: serverClient });

  if (teamsError || !teams) {
    return {
      teamsWithProjects: [],
      error: "Could not load your teams. Please try refreshing the page.",
    };
  }

  const teamsWithProjects: TeamWithProjects[] = await Promise.all(
    teams.map(async (team) => {
      const { data: projects, error: projectsError } = await listProjects({
        client: serverClient,
        path: { teamId: team.id },
        query: { pageSize: 200 },
      });
      if (projectsError || !projects) {
        return { team, projects: [], projectCount: 0, failed: true };
      }
      return { team, projects: projects.items, projectCount: Number(projects.total) };
    })
  );

  return { teamsWithProjects, error: null };
}
