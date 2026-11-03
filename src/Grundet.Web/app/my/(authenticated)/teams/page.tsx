import { PageError } from "@/components/app/page-error";
import { listTeams } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

import { TeamList } from "./_components/team-list";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function TeamsPage(): Promise<React.JSX.Element> {
  const serverClient = await createServerClient();

  const { data: teams, error: teamsError } = await listTeams({ client: serverClient });

  return (
    <div className="mx-auto max-w-[1200px] space-y-6">
      <header className="space-y-1">
        <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
          Teams
        </h1>
        <p className="text-muted-foreground text-sm">Manage your teams and their members.</p>
      </header>

      {teamsError ? (
        <PageError message="Could not load teams. Please try again." />
      ) : (
        <TeamList teams={teams ?? []} />
      )}
    </div>
  );
}
