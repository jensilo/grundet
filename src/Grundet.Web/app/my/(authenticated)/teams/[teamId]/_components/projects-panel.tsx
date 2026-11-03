"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";

import { ConfirmDialog } from "@/components/app/confirm-dialog";
import { EmptyState } from "@/components/app/empty-state";
import { ProjectTile } from "@/components/app/project-tile";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { createProject, deleteProject, updateProject } from "@/lib/api/generated/sdk.gen";
import type { ProjectResponse } from "@/lib/api/generated/types.gen";
import { TEXT_LIMITS } from "@/lib/text-limits";
import { cn } from "@/lib/utils";

type MutationState = { type: "idle" } | { type: "submitting" } | { type: "error"; message: string };

type DeleteState = { type: "idle" } | { type: "deleting" } | { type: "error"; message: string };

function CreateProjectForm({
  teamId,
  onCancel,
}: {
  teamId: string;
  onCancel: () => void;
}): React.JSX.Element {
  const router = useRouter();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [mutationState, setMutationState] = useState<MutationState>({ type: "idle" });

  const isSubmitting = mutationState.type === "submitting";

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!name.trim()) return;

    setMutationState({ type: "submitting" });

    const { error } = await createProject({
      path: { teamId },
      body: {
        name: name.trim(),
        description: description.trim() || null,
      },
    });

    if (error) {
      setMutationState({
        type: "error",
        message: "Failed to create project. Please try again.",
      });
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
        <Label htmlFor="create-project-name">
          Name <span className="text-primary ml-0.5">*</span>
        </Label>
        <Input
          id="create-project-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Project name"
          maxLength={TEXT_LIMITS.name}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="create-project-description">Description</Label>
        <Textarea
          id="create-project-description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="What product truth should this project preserve?"
          maxLength={TEXT_LIMITS.description}
          disabled={isSubmitting}
          rows={3}
        />
      </div>

      {mutationState.type === "error" && (
        <p role="alert" className="text-destructive text-xs">
          {mutationState.message}
        </p>
      )}

      <div className="flex items-center gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Creating…" : "Create project"}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function EditProjectForm({
  project,
  teamId,
  onCancel,
}: {
  project: ProjectResponse;
  teamId: string;
  onCancel: () => void;
}): React.JSX.Element {
  const router = useRouter();
  const [name, setName] = useState(project.name);
  const [description, setDescription] = useState(project.description ?? "");
  const [mutationState, setMutationState] = useState<MutationState>({ type: "idle" });

  const isSubmitting = mutationState.type === "submitting";

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!name.trim()) return;

    setMutationState({ type: "submitting" });

    const { error } = await updateProject({
      path: { teamId, projectId: project.id },
      body: { name: name.trim(), description: description.trim() || null },
    });

    if (error) {
      setMutationState({
        type: "error",
        message: "Failed to update project. Please try again.",
      });
      return;
    }

    router.refresh();
    onCancel();
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="space-y-1.5">
        <Label htmlFor={`edit-project-name-${project.id}`}>
          Name <span className="text-primary ml-0.5">*</span>
        </Label>
        <Input
          id={`edit-project-name-${project.id}`}
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Project name"
          maxLength={TEXT_LIMITS.name}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor={`edit-project-description-${project.id}`}>Description</Label>
        <Textarea
          id={`edit-project-description-${project.id}`}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="What product truth should this project preserve?"
          maxLength={TEXT_LIMITS.description}
          disabled={isSubmitting}
          rows={3}
        />
      </div>

      {mutationState.type === "error" && (
        <p role="alert" className="text-destructive text-xs">
          {mutationState.message}
        </p>
      )}

      <div className="flex items-center gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Save project"}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function ProjectRow({
  project,
  teamId,
  canManageProjects,
}: {
  project: ProjectResponse;
  teamId: string;
  canManageProjects: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [isEditing, setIsEditing] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteState, setDeleteState] = useState<DeleteState>({ type: "idle" });

  const isDeleting = deleteState.type === "deleting";

  async function handleDeleteConfirm(): Promise<void> {
    setDeleteState({ type: "deleting" });

    const { error } = await deleteProject({
      path: { teamId, projectId: project.id },
    });

    if (error) {
      setDeleteState({
        type: "error",
        message: "Failed to delete project. Please try again.",
      });
      return;
    }

    setDeleteDialogOpen(false);
    setDeleteState({ type: "idle" });
    router.refresh();
  }

  function handleDeleteDialogChange(open: boolean): void {
    if (isDeleting) return;
    setDeleteDialogOpen(open);
    if (!open) {
      setDeleteState({ type: "idle" });
    }
  }

  return (
    <li className={cn(isDeleting && "opacity-50")}>
      {isEditing ? (
        <div className="border-border bg-background rounded-lg border p-4">
          <EditProjectForm project={project} teamId={teamId} onCancel={() => setIsEditing(false)} />
        </div>
      ) : (
        <ProjectTile
          project={project}
          actions={
            canManageProjects ? (
              <>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setIsEditing(true)}
                >
                  Edit
                </Button>
                <Button
                  type="button"
                  variant="destructive"
                  size="sm"
                  onClick={() => setDeleteDialogOpen(true)}
                >
                  Delete
                </Button>
              </>
            ) : undefined
          }
        />
      )}

      <ConfirmDialog
        open={deleteDialogOpen}
        onOpenChange={handleDeleteDialogChange}
        title={`Delete ${project.name}?`}
        description="Deleting a project removes its features, intent, work, and history from the team workspace. This cannot be undone."
        confirmLabel="Delete project"
        pendingLabel="Deleting…"
        pending={isDeleting}
        error={deleteState.type === "error" ? deleteState.message : undefined}
        onConfirm={handleDeleteConfirm}
      />
    </li>
  );
}

export function ProjectsPanel({
  teamId,
  initialProjects,
  canCreateProjects,
  canManageProjects,
}: {
  teamId: string;
  initialProjects: ProjectResponse[];
  canCreateProjects: boolean;
  canManageProjects: boolean;
}): React.JSX.Element {
  const searchParams = useSearchParams();
  // Step 2's "Create your first project" CTA links here with ?new=1 so the create
  // dialog opens automatically on arrival, delivering on the CTA's label instead of
  // just landing on the (possibly empty) projects list. Read once via lazy init —
  // not a live subscription to the param.
  const [showCreate, setShowCreate] = useState(
    () => searchParams.get("new") === "1" && canCreateProjects
  );

  return (
    <div className="space-y-4">
      {!showCreate && canCreateProjects ? (
        <div className="flex justify-end">
          <Button type="button" size="sm" onClick={() => setShowCreate(true)}>
            New project
          </Button>
        </div>
      ) : null}

      {showCreate && canCreateProjects ? (
        <CreateProjectForm teamId={teamId} onCancel={() => setShowCreate(false)} />
      ) : null}

      {initialProjects.length === 0 && !showCreate ? (
        <EmptyState
          title="No projects yet"
          description={
            canCreateProjects
              ? "Create the first project to collect features and intent for this team."
              : "A team lead can create the first project when this workspace is ready."
          }
          action={
            canCreateProjects ? (
              <Button type="button" onClick={() => setShowCreate(true)}>
                Create project
              </Button>
            ) : undefined
          }
        />
      ) : initialProjects.length > 0 ? (
        <ul className="grid grid-cols-1 gap-3">
          {initialProjects.map((project) => (
            <ProjectRow
              key={project.id}
              project={project}
              teamId={teamId}
              canManageProjects={canManageProjects}
            />
          ))}
        </ul>
      ) : null}
    </div>
  );
}
