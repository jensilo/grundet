"use client";

import { ArrowRight, History } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { ConfirmDialog } from "@/components/app/confirm-dialog";
import { EmptyState } from "@/components/app/empty-state";
import { FeatureGuide } from "@/components/app/journey-guides";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { createFeature, deleteFeature, updateFeature } from "@/lib/api/generated/sdk.gen";
import type { FeatureResponse, TeamMemberResponse } from "@/lib/api/generated/types.gen";
import { formatRelativeDate } from "@/lib/format-date";
import { TEXT_LIMITS } from "@/lib/text-limits";
import { cn } from "@/lib/utils";

import { FeatureBulkWorkDialog } from "./feature-bulk-work-dialog";

type MutationState = { type: "idle" } | { type: "submitting" } | { type: "error"; message: string };

type DeleteState = { type: "idle" } | { type: "deleting" } | { type: "error"; message: string };

export function CreateFeatureForm({
  projectId,
  onCancel,
}: {
  projectId: string;
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

    const { error } = await createFeature({
      path: { projectId },
      body: {
        name: name.trim(),
        description: description.trim() || null,
      },
    });

    if (error) {
      setMutationState({
        type: "error",
        message: "Failed to create feature. Please try again.",
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
        <Label htmlFor="create-feature-name">
          Name <span className="text-primary ml-0.5">*</span>
        </Label>
        <Input
          id="create-feature-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Feature name"
          maxLength={TEXT_LIMITS.name}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="create-feature-description">Description</Label>
        <Textarea
          id="create-feature-description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="What part of the product truth does this feature collect?"
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
          {isSubmitting ? "Creating…" : "Create feature"}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

export function EditFeatureForm({
  feature,
  projectId,
  onCancel,
}: {
  feature: FeatureResponse;
  projectId: string;
  onCancel: () => void;
}): React.JSX.Element {
  const router = useRouter();
  const [name, setName] = useState(feature.name);
  const [description, setDescription] = useState(feature.description ?? "");
  const [mutationState, setMutationState] = useState<MutationState>({ type: "idle" });

  const isSubmitting = mutationState.type === "submitting";

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!name.trim()) return;

    setMutationState({ type: "submitting" });

    const { error } = await updateFeature({
      path: { projectId, featureId: feature.id },
      body: {
        name: name.trim(),
        description: description.trim() || null,
      },
    });

    if (error) {
      setMutationState({
        type: "error",
        message: "Failed to update feature. Please try again.",
      });
      return;
    }

    router.refresh();
    onCancel();
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="space-y-1.5">
        <Label htmlFor={`edit-feature-name-${feature.id}`}>
          Name <span className="text-primary ml-0.5">*</span>
        </Label>
        <Input
          id={`edit-feature-name-${feature.id}`}
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Feature name"
          maxLength={TEXT_LIMITS.name}
          disabled={isSubmitting}
          required
        />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor={`edit-feature-description-${feature.id}`}>Description</Label>
        <Textarea
          id={`edit-feature-description-${feature.id}`}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="What part of the product truth does this feature collect?"
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
          {isSubmitting ? "Saving…" : "Save feature"}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

export function FeatureRow({
  feature,
  projectId,
  teamMembers,
}: {
  feature: FeatureResponse;
  projectId: string;
  teamMembers: TeamMemberResponse[];
}): React.JSX.Element {
  const router = useRouter();
  const [isEditing, setIsEditing] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteState, setDeleteState] = useState<DeleteState>({ type: "idle" });

  const isDeleting = deleteState.type === "deleting";

  async function handleDeleteConfirm(): Promise<void> {
    setDeleteState({ type: "deleting" });

    const { error } = await deleteFeature({
      path: { projectId, featureId: feature.id },
    });

    if (error) {
      setDeleteState({
        type: "error",
        message: "Failed to delete feature. Please try again.",
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
    <li
      className={cn(
        "border-border bg-background rounded-lg border p-4",
        isDeleting && "opacity-50"
      )}
    >
      {isEditing ? (
        <EditFeatureForm
          feature={feature}
          projectId={projectId}
          onCancel={() => setIsEditing(false)}
        />
      ) : (
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="min-w-0 flex-1 space-y-2">
            <h3 className="text-foreground min-w-0 truncate text-base font-semibold">
              {feature.name}
            </h3>
            <p className="text-muted-foreground line-clamp-2 text-sm">
              {feature.description ?? "No description yet."}
            </p>
            <p className="text-muted-foreground text-xs">
              Created {formatRelativeDate(feature.createdAt)}
            </p>
          </div>

          <div className="flex shrink-0 flex-wrap items-center gap-2">
            <Button
              variant="secondary"
              size="sm"
              nativeButton={false}
              render={
                <Link href={`/projects/${projectId}/requirements?featureId=${feature.id}&page=1`}>
                  Open intent
                  <ArrowRight className="size-3.5" aria-hidden={true} />
                </Link>
              }
            />
            <Button
              variant="outline"
              size="sm"
              nativeButton={false}
              render={
                <Link
                  href={`/projects/${projectId}/changelog?featureId=${feature.id}&entityTypes=Intent`}
                >
                  <History className="size-3.5" aria-hidden={true} />
                  Changelog
                </Link>
              }
            />
            <FeatureBulkWorkDialog
              projectId={projectId}
              featureId={feature.id}
              featureName={feature.name}
              teamMembers={teamMembers}
            />
            <Button type="button" variant="outline" size="sm" onClick={() => setIsEditing(true)}>
              Edit
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
        </div>
      )}

      <ConfirmDialog
        open={deleteDialogOpen}
        onOpenChange={handleDeleteDialogChange}
        title={`Delete ${feature.name}?`}
        description="Deleting a feature removes it from the project and hides the intent grouped under it. This cannot be undone."
        confirmLabel="Delete feature"
        pendingLabel="Deleting…"
        pending={isDeleting}
        error={deleteState.type === "error" ? deleteState.message : undefined}
        onConfirm={handleDeleteConfirm}
      />
    </li>
  );
}

export function FeatureList({
  features,
  projectId,
  teamMembers,
}: {
  features: FeatureResponse[];
  projectId: string;
  teamMembers: TeamMemberResponse[];
}): React.JSX.Element {
  const [showCreate, setShowCreate] = useState(false);

  return (
    <div className="space-y-4">
      {!showCreate && features.length > 0 && (
        <div className="flex justify-end">
          <Button type="button" size="sm" onClick={() => setShowCreate(true)}>
            New feature
          </Button>
        </div>
      )}

      {showCreate && (
        <CreateFeatureForm projectId={projectId} onCancel={() => setShowCreate(false)} />
      )}

      {features.length === 0 && !showCreate ? (
        <EmptyState
          title="No features yet"
          description="Add a feature to group intent around a meaningful product capability, then write the intent inside it."
        >
          <Button type="button" size="sm" onClick={() => setShowCreate(true)}>
            New feature
          </Button>
          <FeatureGuide />
        </EmptyState>
      ) : features.length > 0 ? (
        <>
          <ul className="grid grid-cols-1 gap-3">
            {features.map((feature) => (
              <FeatureRow
                key={feature.id}
                feature={feature}
                projectId={projectId}
                teamMembers={teamMembers}
              />
            ))}
          </ul>
          <FeatureGuide />
        </>
      ) : null}
    </div>
  );
}
