import Link from "next/link";
import { notFound } from "next/navigation";

import { PageError } from "@/components/app/page-error";
import { Button } from "@/components/ui/button";
import {
  getTeam,
  getTeamLimits,
  getTeamPermissions,
  listTeamApiKeys,
  listTeamInvites,
  listTeamMembers,
} from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

import { ApiKeysPanel } from "./_components/api-keys-panel";
import { ConnectionsPanel } from "./_components/connections-panel";
import { MembersPanel } from "./_components/members-panel";
import { TeamDangerZonePanel, TeamSettingsPanel } from "./_components/rename-delete-actions";
import { UsageLimitsPanel } from "./_components/usage-limits-panel";

export default async function TeamDetailPage({
  params,
}: {
  params: Promise<{ teamId: string }>;
}): Promise<React.JSX.Element> {
  const { teamId } = await params;
  const serverClient = await createServerClient();

  const [
    { data: team, error: teamError },
    { data: members, error: membersError },
    { data: permissionsData },
    { data: limits, error: limitsError },
  ] = await Promise.all([
    getTeam({ client: serverClient, path: { teamId } }),
    listTeamMembers({ client: serverClient, path: { teamId } }),
    getTeamPermissions({ client: serverClient, path: { teamId } }),
    getTeamLimits({ client: serverClient, path: { teamId } }),
  ]);

  if (teamError || !team) {
    notFound();
  }

  // Backend-computed viewer permissions are the only source of UI gating. A failed fetch fails
  // safe to an empty set (deny all UI) — permissions are a hint and the backend still enforces.
  const permissions = permissionsData?.permissions ?? [];
  const canManageTeam = permissions.includes("TeamManageSettings");
  const canDeleteTeam = permissions.includes("TeamDelete");
  const canInviteMembers = permissions.includes("TeamInviteMembers");
  const canManageMembers = permissions.includes("TeamManageMembers");

  const { data: invites, error: invitesError } = canInviteMembers
    ? await listTeamInvites({ client: serverClient, path: { teamId } })
    : { data: [], error: undefined };
  const { data: apiKeys, error: apiKeysError } = canManageTeam
    ? await listTeamApiKeys({ client: serverClient, path: { teamId } })
    : { data: [], error: undefined };

  return (
    <div className="mx-auto max-w-[1080px] space-y-6">
      <header className="space-y-4">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div className="max-w-[720px] space-y-2">
            <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
              {team.name}
            </h1>
            <p className="text-muted-foreground text-sm">
              Browse the team workspace, members, limits, and future connections.
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            nativeButton={false}
            render={<Link href={`/teams/${team.id}/projects`}>Manage projects</Link>}
          />
        </div>
      </header>

      <TeamSettingsPanel team={team} canManageTeam={canManageTeam} />

      {membersError || invitesError ? (
        <PageError message="Could not load members and invites. Please try again." />
      ) : (
        <MembersPanel
          teamId={teamId}
          initialMembers={members ?? []}
          initialInvites={invites ?? []}
          canManageTeam={canManageTeam}
          canInviteMembers={canInviteMembers}
          canManageMembers={canManageMembers}
        />
      )}

      {limitsError || !limits ? (
        <PageError message="Could not load usage and limits. Please try again." />
      ) : (
        <UsageLimitsPanel limits={limits} />
      )}

      <ConnectionsPanel />

      {canManageTeam ? (
        apiKeysError ? (
          <PageError message="Could not load API keys. Please try again." />
        ) : (
          <ApiKeysPanel
            teamId={teamId}
            initialKeys={apiKeys ?? []}
            members={members ?? []}
            isLead={canManageTeam}
          />
        )
      ) : null}

      <TeamDangerZonePanel
        team={team}
        canDeleteTeam={canDeleteTeam}
        canManageTeam={canManageTeam}
      />
    </div>
  );
}
