"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

import { ConfirmDialog } from "@/components/app/confirm-dialog";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { deleteTeam, renameTeam } from "@/lib/api/generated/sdk.gen";
import type { ProblemDetails, TeamResponse } from "@/lib/api/generated/types.gen";
import { SOLO_WORKSPACE_REQUIRED } from "@/lib/api/mutation-error";

type MutationState = { type: "idle" } | { type: "submitting" } | { type: "error"; message: string };

type DeleteState = { type: "idle" } | { type: "deleting" } | { type: "error"; message: string };

function getDeleteTeamErrorMessage(error: ProblemDetails): string {
  if (error.type === SOLO_WORKSPACE_REQUIRED) {
    return "Solo workspaces cannot be deleted. Create a team workspace when you want to collaborate.";
  }

  return "Team could not be deleted. Check that you still have Lead access before trying again.";
}

export function TeamSettingsPanel({
  team,
  canManageTeam,
}: {
  team: TeamResponse;
  canManageTeam: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [name, setName] = useState(team.name);
  const [renameMutation, setRenameMutation] = useState<MutationState>({ type: "idle" });

  const isRenaming = renameMutation.type === "submitting";

  async function handleRenameSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!name.trim()) return;

    setRenameMutation({ type: "submitting" });

    const { error } = await renameTeam({
      path: { teamId: team.id },
      body: { name: name.trim() },
    });

    if (error) {
      setRenameMutation({ type: "error", message: "Failed to rename team. Please try again." });
      return;
    }

    router.refresh();
    setRenameMutation({ type: "idle" });
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Team settings</CardTitle>
        <CardDescription>Keep the workspace name clear for everyone on the team.</CardDescription>
      </CardHeader>
      <CardContent>
        {canManageTeam ? (
          <form onSubmit={handleRenameSubmit} className="space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="rename-team-name">
                Name <span className="text-primary ml-0.5">*</span>
              </Label>
              <Input
                id="rename-team-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Team name"
                disabled={isRenaming}
                required
              />
            </div>

            {renameMutation.type === "error" && (
              <p role="alert" className="text-destructive text-xs">
                {renameMutation.message}
              </p>
            )}

            <Button type="submit" disabled={isRenaming}>
              {isRenaming ? "Saving…" : "Save team settings"}
            </Button>
          </form>
        ) : (
          <p className="border-border bg-muted text-muted-foreground rounded-md border px-4 py-3 text-sm">
            Only team leads can rename this team.
          </p>
        )}
      </CardContent>
    </Card>
  );
}

export function TeamDangerZonePanel({
  team,
  canDeleteTeam,
  canManageTeam,
}: {
  team: TeamResponse;
  canDeleteTeam: boolean;
  canManageTeam: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteState, setDeleteState] = useState<DeleteState>({ type: "idle" });

  const isDeleting = deleteState.type === "deleting";

  async function handleDeleteConfirm(): Promise<void> {
    setDeleteState({ type: "deleting" });

    const { error } = await deleteTeam({ path: { teamId: team.id } });

    if (error) {
      setDeleteState({ type: "error", message: getDeleteTeamErrorMessage(error) });
      return;
    }

    router.push("/teams");
  }

  function handleDeleteDialogChange(open: boolean): void {
    if (isDeleting) return;
    setDeleteDialogOpen(open);
    if (!open) {
      setDeleteState({ type: "idle" });
    }
  }

  if (!canDeleteTeam) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>{canManageTeam ? "Personal workspace" : "Team administration"}</CardTitle>
          <CardDescription>
            {canManageTeam
              ? "Your solo workspace stays available for personal work. Create a team workspace when you want to collaborate and invite contributors."
              : "Only team leads can rename or delete this team."}
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Danger zone</CardTitle>
        <CardDescription>
          Destructive actions for this team workspace. Keep this at the bottom for a reason.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="border-border bg-muted/50 flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-1">
            <h2 className="text-foreground text-sm font-semibold">Delete team</h2>
            <p className="text-muted-foreground text-sm">
              Deleting a team removes its projects, features, intent, work, and collaboration data.
            </p>
          </div>
          <Button
            type="button"
            variant="destructive"
            disabled={isDeleting}
            onClick={() => setDeleteDialogOpen(true)}
          >
            Delete team
          </Button>
        </div>

        <ConfirmDialog
          open={deleteDialogOpen}
          onOpenChange={handleDeleteDialogChange}
          title={`Delete ${team.name}?`}
          description="This deletes the team workspace and all product data inside it. This cannot be undone."
          confirmLabel="Delete team"
          pendingLabel="Deleting…"
          pending={isDeleting}
          error={deleteState.type === "error" ? deleteState.message : undefined}
          onConfirm={handleDeleteConfirm}
        />
      </CardContent>
    </Card>
  );
}
