"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { ConfirmDialog } from "@/components/app/confirm-dialog";
import { EmptyState } from "@/components/app/empty-state";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { createTeam, deleteTeam, renameTeam } from "@/lib/api/generated/sdk.gen";
import type { TeamListItemResponse, TeamRole } from "@/lib/api/generated/types.gen";
import { formatRelativeDate } from "@/lib/format-date";
import { cn } from "@/lib/utils";

type MutationState = { type: "idle" } | { type: "submitting" } | { type: "error"; message: string };

type DeleteState = { type: "idle" } | { type: "deleting" } | { type: "error"; message: string };

function RoleBadge({ role }: { role: TeamRole }): React.JSX.Element {
  if (role === "Lead") {
    return (
      <span className="bg-primary/10 text-primary border-primary/20 inline-flex h-5 items-center rounded-sm border px-1.5 text-xs">
        Lead
      </span>
    );
  }
  return (
    <span className="bg-muted text-muted-foreground border-border inline-flex h-5 items-center rounded-sm border px-1.5 text-xs">
      Contributor
    </span>
  );
}

function CreateTeamForm({ onCancel }: { onCancel: () => void }): React.JSX.Element {
  const router = useRouter();
  const [name, setName] = useState("");
  const [mutationState, setMutationState] = useState<MutationState>({ type: "idle" });

  const isSubmitting = mutationState.type === "submitting";

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!name.trim()) return;

    setMutationState({ type: "submitting" });

    const { error } = await createTeam({ body: { name: name.trim() } });

    if (error) {
      setMutationState({ type: "error", message: "Failed to create team. Please try again." });
      return;
    }

    router.refresh();
    onCancel();
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="border-border bg-muted/50 space-y-4 rounded-md border p-4"
    >
      <div className="space-y-1.5">
        <p className="text-muted-foreground text-sm">
          Create a team workspace for collaborative product work. Your other workspaces stay
          separate.
        </p>
        <Label htmlFor="create-team-name">
          Name <span className="text-primary ml-0.5">*</span>
        </Label>
        <Input
          id="create-team-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Team name"
          disabled={isSubmitting}
          required
        />
      </div>

      {mutationState.type === "error" && (
        <p role="alert" className="text-destructive text-xs">
          {mutationState.message}
        </p>
      )}

      <div className="flex items-center gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Creating…" : "Create team"}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function TeamRow({ team }: { team: TeamListItemResponse }): React.JSX.Element {
  const router = useRouter();
  const [isEditing, setIsEditing] = useState(false);
  const [editName, setEditName] = useState(team.name);
  const [renameMutation, setRenameMutation] = useState<MutationState>({ type: "idle" });
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteState, setDeleteState] = useState<DeleteState>({ type: "idle" });

  async function handleRenameSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!editName.trim()) return;

    setRenameMutation({ type: "submitting" });

    const { error } = await renameTeam({
      path: { teamId: team.id },
      body: { name: editName.trim() },
    });

    if (error) {
      setRenameMutation({ type: "error", message: "Failed to rename team. Please try again." });
      return;
    }

    router.refresh();
    setIsEditing(false);
    setRenameMutation({ type: "idle" });
  }

  async function handleDeleteConfirm(): Promise<void> {
    setDeleteState({ type: "deleting" });

    const { error } = await deleteTeam({ path: { teamId: team.id } });

    if (error) {
      setDeleteState({ type: "error", message: "Failed to delete team. Please try again." });
      return;
    }

    setDeleteDialogOpen(false);
    setDeleteState({ type: "idle" });
    router.refresh();
  }

  function handleDeleteDialogChange(open: boolean): void {
    if (deleteState.type === "deleting") return;
    setDeleteDialogOpen(open);
    if (!open) {
      setDeleteState({ type: "idle" });
    }
  }

  const isDeleting = deleteState.type === "deleting";
  const isRenaming = renameMutation.type === "submitting";

  return (
    <li
      className={cn(
        "border-border flex items-start justify-between gap-4 border-b px-4 py-3 last:border-0",
        isDeleting && "opacity-50"
      )}
    >
      {isEditing ? (
        <form onSubmit={handleRenameSubmit} className="min-w-0 flex-1 space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor={`rename-team-${team.id}`}>
              Name <span className="text-primary ml-0.5">*</span>
            </Label>
            <Input
              id={`rename-team-${team.id}`}
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
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

          <div className="flex items-center gap-2">
            <Button type="submit" disabled={isRenaming}>
              {isRenaming ? "Saving…" : "Save team"}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setIsEditing(false);
                setEditName(team.name);
                setRenameMutation({ type: "idle" });
              }}
              disabled={isRenaming}
            >
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        <>
          <div className="min-w-0 flex-1 space-y-0.5">
            <div className="flex items-center gap-2">
              <p className="text-foreground text-sm font-medium">{team.name}</p>
              <RoleBadge role={team.role} />
            </div>
            <p className="text-muted-foreground text-xs">{formatRelativeDate(team.createdAt)}</p>
          </div>

          <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              nativeButton={false}
              render={<Link href={`/teams/${team.id}/projects`}>Projects</Link>}
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              nativeButton={false}
              render={<Link href={`/teams/${team.id}`}>Settings</Link>}
            />
            <Button type="button" variant="outline" size="sm" onClick={() => setIsEditing(true)}>
              Rename
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              disabled={isDeleting}
              onClick={() => setDeleteDialogOpen(true)}
            >
              Delete
            </Button>
          </div>
        </>
      )}

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
    </li>
  );
}

export function TeamList({ teams }: { teams: TeamListItemResponse[] }): React.JSX.Element {
  const [showCreate, setShowCreate] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-foreground text-base leading-[1.4] font-semibold">Teams</h2>
        {!showCreate && (
          <Button type="button" size="sm" onClick={() => setShowCreate(true)}>
            New team workspace
          </Button>
        )}
      </div>

      {showCreate && <CreateTeamForm onCancel={() => setShowCreate(false)} />}

      {teams.length === 0 && !showCreate ? (
        <EmptyState
          title="No teams yet"
          description="Teams are how you organise projects and collaborate with colleagues. Create your first team to get started."
        />
      ) : teams.length > 0 ? (
        <div className="border-border rounded-md border">
          <ul>
            {teams.map((team) => (
              <TeamRow key={team.id} team={team} />
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
