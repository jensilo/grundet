import { FolderOpen, Users } from "lucide-react";
import Link from "next/link";

import { ProjectGuide } from "@/components/app/journey-guides";
import { PageError } from "@/components/app/page-error";
import { ProjectTile } from "@/components/app/project-tile";
import { TeamRoleBadge } from "@/components/app/team-role-badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { fetchTeamsWithProjects, type TeamWithProjects } from "@/lib/api/queries/teams";
import { createServerClient } from "@/lib/api/server";

// ---------------------------------------------------------------------------
// Sub-components (inline — not reused elsewhere)
// ---------------------------------------------------------------------------

function EmptyTeamsState(): React.JSX.Element {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <Users className="text-muted-foreground/40 mb-6 size-12" aria-hidden={true} />
      <h2 className="text-foreground mb-2 text-base font-semibold">No teams yet</h2>
      <p className="text-muted-foreground mb-6 max-w-[380px] text-sm">
        Teams are how you organise projects and collaborate with colleagues. Create your first team
        to get started.
      </p>
      <Button nativeButton={false} render={<Link href="/teams">Create team</Link>} />
    </div>
  );
}

function EmptyProjectsState({ teamId }: { teamId: string }): React.JSX.Element {
  return (
    <div className="flex flex-col items-center justify-center py-8 text-center">
      <FolderOpen className="text-muted-foreground/40 mb-4 size-10" aria-hidden={true} />
      <p className="text-foreground mb-1 text-sm font-medium">No projects yet</p>
      <p className="text-muted-foreground mb-4 max-w-[320px] text-sm">
        Add a project when there is product intent worth preserving and evolving.
      </p>
      <Button
        variant="outline"
        size="sm"
        nativeButton={false}
        render={<Link href={`/teams/${teamId}/projects`}>Manage projects</Link>}
      />
    </div>
  );
}

function TeamSection({
  team,
  projects,
  projectCount,
  failed,
}: TeamWithProjects): React.JSX.Element {
  // When the projects fetch failed we cannot trust the count — do not show a misleading
  // "0 projects". Show the error affordance instead. projectCount is the paginated total,
  // which may exceed projects.length (capped at the requested pageSize).
  const countLabel = projectCount === 1 ? "1 project" : `${projectCount} projects`;

  return (
    <Card>
      <CardHeader className="border-border border-b">
        <CardTitle>{team.name}</CardTitle>
        <CardDescription className="flex items-center gap-2">
          <TeamRoleBadge role={team.role} />
          {!failed ? <span className="text-muted-foreground text-sm">{countLabel}</span> : null}
        </CardDescription>
        <CardAction>
          <Link
            href={`/teams/${team.id}/projects`}
            className="text-muted-foreground hover:text-foreground text-sm transition-colors"
          >
            Manage projects
          </Link>
        </CardAction>
      </CardHeader>
      <CardContent>
        {failed ? (
          <PageError message="Could not load projects for this team. Please try again." />
        ) : projectCount === 0 ? (
          <EmptyProjectsState teamId={team.id} />
        ) : (
          <ul className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            {projects.map((project) => (
              <li key={project.id}>
                <ProjectTile project={project} className="h-full" />
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function ProjectsPage(): Promise<React.JSX.Element> {
  const serverClient = await createServerClient();
  const { teamsWithProjects, error } = await fetchTeamsWithProjects(serverClient);

  return (
    <div className="mx-auto max-w-[1080px] space-y-10">
      <header className="space-y-2">
        <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
          Projects
        </h1>
        <p className="text-muted-foreground max-w-[640px] text-[1.0625rem] leading-relaxed">
          Every project across your team workspaces. Open one to work with its intent, features,
          change history, and linked work.
        </p>
      </header>

      {error ? (
        <PageError message={error} />
      ) : teamsWithProjects.length === 0 ? (
        <EmptyTeamsState />
      ) : (
        <section className="space-y-4" aria-labelledby="projects-teams-heading">
          <h2 id="projects-teams-heading" className="sr-only">
            Teams and projects
          </h2>
          <div className="grid grid-cols-1 gap-4">
            {teamsWithProjects.map((teamWithProjects) => (
              <TeamSection key={teamWithProjects.team.id} {...teamWithProjects} />
            ))}
          </div>

          <ProjectGuide />
        </section>
      )}
    </div>
  );
}
