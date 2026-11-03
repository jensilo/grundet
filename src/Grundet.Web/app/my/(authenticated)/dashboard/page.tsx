import { ArrowRight, Users } from "lucide-react";
import Link from "next/link";

import { ProjectGuide } from "@/components/app/journey-guides";
import { PageError } from "@/components/app/page-error";
import { TeamRoleBadge } from "@/components/app/team-role-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { getMe } from "@/lib/api/generated/sdk.gen";
import { fetchTeamsWithProjects, type TeamWithProjects } from "@/lib/api/queries/teams";
import { createServerClient } from "@/lib/api/server";

function GreetingHeader({ displayName }: { displayName: string | null }): React.JSX.Element {
  const name = displayName?.trim();
  const greeting = name ? `Welcome, ${name}` : "Welcome";

  return (
    <header className="space-y-2">
      <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
        {greeting}
      </h1>
      <p className="text-muted-foreground max-w-[640px] text-[1.0625rem] leading-relaxed">
        Your workspaces at a glance – pick up where a team left off, or open the Projects view to
        browse everything.
      </p>
    </header>
  );
}

function EmptyTeamsState(): React.JSX.Element {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <Users className="text-muted-foreground/40 mb-6 size-12" aria-hidden={true} />
      <h2 className="text-foreground mb-2 text-base font-semibold">Start your first team</h2>
      <p className="text-muted-foreground mb-6 max-w-[380px] text-sm">
        A team workspace gathers your projects, their intent, and the history behind every product
        decision.
      </p>
      <Button nativeButton={false} render={<Link href="/teams">Create team</Link>} />
    </div>
  );
}

// Home renders teams as a compact, scannable overview row — deliberately lighter than the
// Projects view, which expands every team into a full project-tile grid. Home orients; Projects
// is the working surface.
function TeamOverviewRow({ team, projectCount, failed }: TeamWithProjects): React.JSX.Element {
  const countLabel = failed
    ? "Projects unavailable"
    : projectCount === 1
      ? "1 project"
      : `${projectCount} projects`;

  return (
    <li className="hover:bg-muted/50 flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3 motion-safe:transition-colors">
      <Link
        href={`/teams/${team.id}`}
        className="hover:text-primary min-w-0 flex-1 truncate text-sm font-medium motion-safe:transition-colors"
      >
        {team.name}
      </Link>
      <TeamRoleBadge role={team.role} />
      <span className="text-muted-foreground text-sm">{countLabel}</span>
      <Link
        href={`/teams/${team.id}/projects`}
        className="text-primary hover:text-primary-hover inline-flex items-center gap-1 text-sm font-medium motion-safe:transition-colors"
      >
        Open
        <ArrowRight className="size-3.5" aria-hidden={true} />
      </Link>
    </li>
  );
}

export default async function DashboardPage(): Promise<React.JSX.Element> {
  const serverClient = await createServerClient();
  // Run the user and teams fetches in parallel so the greeting adds no sequential latency.
  const [{ data: me }, { teamsWithProjects, error }] = await Promise.all([
    getMe({ client: serverClient }),
    fetchTeamsWithProjects(serverClient),
  ]);

  return (
    <div className="mx-auto max-w-[1080px] space-y-10">
      <GreetingHeader displayName={me?.displayName ?? null} />

      {error ? (
        <PageError message={error} />
      ) : teamsWithProjects.length === 0 ? (
        <EmptyTeamsState />
      ) : (
        <section className="space-y-4" aria-labelledby="dashboard-teams-heading">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 id="dashboard-teams-heading" className="text-foreground text-base font-semibold">
              Your teams
            </h2>
            <Link
              href="/teams"
              className="text-muted-foreground hover:text-foreground text-sm transition-colors"
            >
              Manage all teams
            </Link>
          </div>
          <Card className="py-2">
            <CardContent className="px-0">
              <ul className="divide-border divide-y">
                {teamsWithProjects.map((teamWithProjects) => (
                  <TeamOverviewRow key={teamWithProjects.team.id} {...teamWithProjects} />
                ))}
              </ul>
            </CardContent>
          </Card>

          <ProjectGuide />
        </section>
      )}

      {/*
        Region C — future "Recent activity" feed slot.
        Intentionally not implemented in this slice. A cross-project / team-wide activity
        rollup depends on the aggregation machinery being built on feature/changelog-views,
        which defers the team-level rollup as a follow-up. When that lands and exposes a
        reusable team-level aggregate, render it here using the existing changelog/
        activity-timeline visual language (components/app/activity-timeline.tsx). Do not
        ship a dead-looking panel before the data exists.
      */}
    </div>
  );
}
