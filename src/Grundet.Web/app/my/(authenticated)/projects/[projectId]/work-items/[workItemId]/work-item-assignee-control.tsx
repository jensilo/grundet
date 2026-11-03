"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

import { UserAvatar } from "@/components/app/user-avatar";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { updateProjectWorkItem } from "@/lib/api/generated/sdk.gen";
import type { TeamMemberResponse } from "@/lib/api/generated/types.gen";

const UNASSIGNED = "__unassigned__";

function memberLabel(member: TeamMemberResponse): string {
  return member.displayName ?? member.email;
}

/**
 * Inline assignee picker for the work detail sidebar — change the assignee or "Assign to me" without
 * opening the full editor. Each change is a normal versioned update (no reason prompt).
 */
export function WorkItemAssigneeControl({
  projectId,
  workItemId,
  assignedToUserId,
  teamMembers,
  currentUserId,
}: {
  projectId: string;
  workItemId: string;
  assignedToUserId: string | null;
  teamMembers: TeamMemberResponse[];
  currentUserId: string;
}): React.JSX.Element {
  const router = useRouter();
  // Hold the just-applied value so the control reflects the change until router.refresh() lands.
  const [applied, setApplied] = useState<string | null | undefined>(undefined);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const effective = applied === undefined ? assignedToUserId : applied;
  const effectiveMember = effective
    ? (teamMembers.find((member) => member.userId === effective) ?? null)
    : null;
  const selfIsMember = teamMembers.some((member) => member.userId === currentUserId);
  const canAssignSelf = selfIsMember && effective !== currentUserId;

  async function assign(next: string | null): Promise<void> {
    if (next === effective) return;
    setBusy(true);
    setError(null);
    const { error: mutationError } = await updateProjectWorkItem({
      path: { projectId, workItemId },
      body: { assignedToUserId: next },
    });
    if (mutationError) {
      setBusy(false);
      setError("Could not update the assignee. Please try again.");
      return;
    }
    setApplied(next);
    setBusy(false);
    router.refresh();
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <span className="text-muted-foreground text-sm">Assigned</span>
        {canAssignSelf && (
          <Button
            type="button"
            variant="ghost"
            size="xs"
            disabled={busy}
            className="text-primary hover:text-primary -mr-1 px-1.5"
            onClick={() => void assign(currentUserId)}
          >
            Assign to me
          </Button>
        )}
      </div>
      <Select
        value={effective ?? UNASSIGNED}
        disabled={busy}
        onValueChange={(next) => void assign(next === UNASSIGNED ? null : next)}
      >
        <SelectTrigger className="w-full" aria-label="Assignee">
          <SelectValue>
            {effective ? (
              <>
                <UserAvatar
                  userId={effective}
                  name={effectiveMember?.displayName}
                  email={effectiveMember?.email}
                  size="default"
                  className="size-5"
                />
                <span className="truncate">
                  {effectiveMember?.displayName ?? effectiveMember?.email ?? "Unknown member"}
                </span>
              </>
            ) : (
              "Nobody yet"
            )}
          </SelectValue>
        </SelectTrigger>
        <SelectContent align="start">
          <SelectItem value={UNASSIGNED}>Nobody yet</SelectItem>
          {teamMembers.map((member) => (
            <SelectItem key={member.userId} value={member.userId}>
              <UserAvatar
                userId={member.userId}
                name={member.displayName}
                email={member.email}
                size="default"
                className="size-5"
              />
              {memberLabel(member)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {error && (
        <p role="alert" className="text-destructive text-xs">
          {error}
        </p>
      )}
    </div>
  );
}
