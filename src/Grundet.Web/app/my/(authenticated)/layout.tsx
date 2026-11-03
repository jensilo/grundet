import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import type { ShellProject, ShellTeam } from "@/components/app/app-shell-types";
import { AppSidebar } from "@/components/app/app-sidebar";
import { AppTopbar } from "@/components/app/app-topbar";
import { CommandPaletteProvider } from "@/components/app/command-palette";
import { getMe } from "@/lib/api/generated/sdk.gen";
import { fetchTeamsWithProjects } from "@/lib/api/queries/teams";
import { createServerClient } from "@/lib/api/server";
import { UserProvider } from "@/providers/user-provider";

export default async function AppLayout({
  children,
}: {
  children: React.ReactNode;
}): Promise<React.JSX.Element> {
  const serverClient = await createServerClient();
  const [{ data, error }, teamsResult] = await Promise.all([
    getMe({ client: serverClient }),
    fetchTeamsWithProjects(serverClient),
  ]);

  if (error || !data) {
    redirect("/login");
  }

  const cookieStore = await cookies();
  const pendingInviteToken = cookieStore.get("grundet_pending_invite_token")?.value;

  if (pendingInviteToken && data.displayName) {
    redirect(`/invite/accept?token=${encodeURIComponent(pendingInviteToken)}`);
  }

  // Onboarding is unfinished until both the display name (Step 1) and the completion flag
  // (Step 2) are set — /onboarding resumes at the right step. There are no legacy accounts
  // predating the flag, so guarding on it never traps an existing user.
  if (!data.displayName || !data.onboardingCompletedAt) {
    redirect("/onboarding");
  }

  const user = {
    id: data.id,
    email: data.email,
    name: data.displayName ?? data.email,
  };

  const shellTeams: ShellTeam[] = teamsResult.teamsWithProjects
    .filter((entry) => !entry.failed)
    .map((entry) => ({
      id: entry.team.id,
      name: entry.team.name,
      projects: entry.projects.map((project) => ({ id: project.id, name: project.name })),
    }));

  const shellProjects: ShellProject[] = shellTeams.flatMap((team) =>
    team.projects.map((project) => ({
      id: project.id,
      name: project.name,
      teamId: team.id,
      teamName: team.name,
    }))
  );

  return (
    <UserProvider user={user}>
      <CommandPaletteProvider projects={shellProjects}>
        <div className="bg-background flex min-h-screen w-full">
          <AppSidebar
            projects={shellProjects}
            teams={shellTeams}
            user={{ id: user.id, name: user.name, email: user.email }}
          />
          <div className="flex min-w-0 flex-1 flex-col">
            <AppTopbar teams={shellTeams} projects={shellProjects} />
            <main className="flex-1 px-8 py-8">{children}</main>
          </div>
        </div>
      </CommandPaletteProvider>
    </UserProvider>
  );
}
