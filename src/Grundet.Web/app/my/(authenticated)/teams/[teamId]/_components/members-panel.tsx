"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { ConfirmDialog } from "@/components/app/confirm-dialog";
import { EmptyState } from "@/components/app/empty-state";
import { UserAvatar } from "@/components/app/user-avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  changeTeamMemberRole,
  inviteTeamMember,
  removeTeamMember,
  resendTeamInvite,
  revokeTeamInvite,
} from "@/lib/api/generated/sdk.gen";
import type {
  ProblemDetails,
  TeamInviteResponse,
  TeamMemberResponse,
  TeamRole,
} from "@/lib/api/generated/types.gen";
import { QUOTA_EXCEEDED } from "@/lib/api/mutation-error";
import { cn } from "@/lib/utils";

type InviteState =
  | { type: "idle" }
  | { type: "submitting" }
  | { type: "success" }
  | { type: "error"; message: string };

type MemberDeleteState =
  { type: "idle" } | { type: "deleting" } | { type: "error"; message: string };

type RoleState = { type: "idle" } | { type: "changing" } | { type: "error"; message: string };

const selectClassName = cn(
  "border-input focus-visible:border-ring focus-visible:ring-ring/50",
  "bg-background h-8 w-full rounded-lg border px-2.5 py-1 text-sm",
  "transition-colors outline-none focus-visible:ring-3",
  "disabled:cursor-not-allowed disabled:opacity-50"
);

function getInviteErrorMessage(error: ProblemDetails): string {
  if (error.type === QUOTA_EXCEEDED) {
    if (
      error.detail ===
      "Solo workspaces do not allow member invites. Create a team workspace to collaborate."
    ) {
      return "This is a solo workspace. Create a team workspace to invite contributors.";
    }

    if (error.detail === "Member limit reached for this workspace plan.") {
      return "This team has reached its member limit, including pending invites. Remove a pending invite or contact support.";
    }
  }

  if (
    error.detail ===
    "A pending invite already exists for this email address. Use resend from the pending invite row."
  ) {
    return "A pending invite already exists for this email address. Use Resend invitation from the pending invite row.";
  }

  return "Invitation could not be sent. Check the email address and workspace limits before trying again.";
}

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

function MemberRow({
  member,
  teamId,
  canManage,
}: {
  member: TeamMemberResponse;
  teamId: string;
  canManage: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteState, setDeleteState] = useState<MemberDeleteState>({ type: "idle" });
  const [roleState, setRoleState] = useState<RoleState>({ type: "idle" });

  async function handleRemoveConfirm(): Promise<void> {
    setDeleteState({ type: "deleting" });

    const { error } = await removeTeamMember({
      path: { teamId, userId: member.userId },
    });

    if (error) {
      setDeleteState({ type: "error", message: "Failed to remove member. Please try again." });
      return;
    }

    setDeleteDialogOpen(false);
    setDeleteState({ type: "idle" });
    router.refresh();
  }

  async function handleChangeRole(): Promise<void> {
    const newRole: TeamRole = member.role === "Lead" ? "Contributor" : "Lead";

    setRoleState({ type: "changing" });

    const { error } = await changeTeamMemberRole({
      path: { teamId, userId: member.userId },
      body: { role: newRole },
    });

    if (error) {
      setRoleState({ type: "error", message: "Failed to change role. Please try again." });
      return;
    }

    router.refresh();
    setRoleState({ type: "idle" });
  }

  function handleDeleteDialogChange(open: boolean): void {
    if (deleteState.type === "deleting") return;
    setDeleteDialogOpen(open);
    if (!open) {
      setDeleteState({ type: "idle" });
    }
  }

  const isDeleting = deleteState.type === "deleting";
  const isChangingRole = roleState.type === "changing";
  const memberName = member.displayName ?? member.email;

  return (
    <li
      className={cn(
        "border-border flex items-start justify-between gap-4 border-b px-4 py-3 last:border-0",
        isDeleting && "opacity-50"
      )}
    >
      <div className="flex min-w-0 flex-1 items-start gap-2.5">
        <UserAvatar
          userId={member.userId}
          name={member.displayName}
          email={member.email}
          size="sm"
          className="mt-0.5"
        />
        <div className="min-w-0 space-y-0.5">
          <div className="flex items-center gap-2">
            <p className="text-foreground text-sm font-medium">{memberName}</p>
            <RoleBadge role={member.role} />
          </div>
          {member.displayName && <p className="text-muted-foreground text-xs">{member.email}</p>}
        </div>
      </div>

      {canManage ? (
        <div className="flex shrink-0 flex-col items-end gap-1">
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isChangingRole}
              onClick={handleChangeRole}
            >
              {isChangingRole
                ? "Changing…"
                : member.role === "Lead"
                  ? "Make Contributor"
                  : "Make Lead"}
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              disabled={isDeleting}
              onClick={() => setDeleteDialogOpen(true)}
            >
              Remove
            </Button>
          </div>
          {roleState.type === "error" && (
            <p role="alert" className="text-destructive text-xs">
              {roleState.message}
            </p>
          )}
        </div>
      ) : null}

      <ConfirmDialog
        open={deleteDialogOpen}
        onOpenChange={handleDeleteDialogChange}
        title={`Remove ${memberName}?`}
        description="This removes the member from the team workspace. They will lose access to the team projects and product intent."
        confirmLabel="Remove member"
        pendingLabel="Removing…"
        pending={isDeleting}
        error={deleteState.type === "error" ? deleteState.message : undefined}
        onConfirm={handleRemoveConfirm}
      />
    </li>
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(value));
}

function InviteRow({
  invite,
  teamId,
  canManage,
}: {
  invite: TeamInviteResponse;
  teamId: string;
  canManage: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [actionState, setActionState] = useState<
    | { type: "idle" }
    | { type: "resending" }
    | { type: "removing" }
    | { type: "error"; message: string }
  >({ type: "idle" });

  async function handleResend(): Promise<void> {
    setActionState({ type: "resending" });
    const { error } = await resendTeamInvite({ path: { teamId, inviteId: invite.id } });
    if (error) {
      setActionState({ type: "error", message: "Failed to resend invitation. Please try again." });
      return;
    }
    setActionState({ type: "idle" });
    router.refresh();
  }

  async function handleRemove(): Promise<void> {
    if (!window.confirm(`Remove invitation for ${invite.email}?`)) return;
    setActionState({ type: "removing" });
    const { error } = await revokeTeamInvite({ path: { teamId, inviteId: invite.id } });
    if (error) {
      setActionState({ type: "error", message: "Failed to remove invitation. Please try again." });
      return;
    }
    setActionState({ type: "idle" });
    router.refresh();
  }

  const isBusy = actionState.type === "resending" || actionState.type === "removing";

  return (
    <li
      className={cn(
        "border-border bg-muted/40 flex items-start justify-between gap-4 border-b px-4 py-3 last:border-0",
        isBusy && "opacity-50"
      )}
    >
      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex items-center gap-2">
          <p className="text-foreground text-sm font-medium">{invite.email}</p>
          <RoleBadge role={invite.role} />
          <span className="border-border bg-background text-muted-foreground inline-flex h-5 items-center rounded-sm border px-1.5 text-xs">
            Invited
          </span>
        </div>
        <p className="text-muted-foreground text-xs">
          Invited – waiting for them to accept. Expires {formatDate(invite.expiresAt)}.
        </p>
        {actionState.type === "error" && (
          <p role="alert" className="text-destructive text-xs">
            {actionState.message}
          </p>
        )}
      </div>

      {canManage ? (
        <div className="flex shrink-0 items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={isBusy}
            onClick={handleResend}
          >
            {actionState.type === "resending" ? "Resending…" : "Resend invitation"}
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            disabled={isBusy}
            onClick={handleRemove}
          >
            {actionState.type === "removing" ? "Removing…" : "Remove"}
          </Button>
        </div>
      ) : null}
    </li>
  );
}

function InviteForm({
  teamId,
  canInviteMembers,
}: {
  teamId: string;
  canInviteMembers: boolean;
}): React.JSX.Element {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<TeamRole>("Contributor");
  const [inviteState, setInviteState] = useState<InviteState>({ type: "idle" });

  useEffect(() => {
    if (inviteState.type !== "success") return;
    const id = setTimeout(() => setInviteState({ type: "idle" }), 3000);
    return () => clearTimeout(id);
  }, [inviteState.type]);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (!email.trim()) return;

    setInviteState({ type: "submitting" });

    const { error } = await inviteTeamMember({
      path: { teamId },
      body: { email: email.trim(), role },
    });

    if (error) {
      setInviteState({ type: "error", message: getInviteErrorMessage(error) });
      return;
    }

    setEmail("");
    setRole("Contributor");
    setInviteState({ type: "success" });
    router.refresh();
  }

  const isSubmitting = inviteState.type === "submitting";

  if (!canInviteMembers) {
    return (
      <div className="border-border bg-muted/50 space-y-2 rounded-md border p-4">
        <h3 className="text-foreground text-sm font-semibold">Invite contributors</h3>
        <p className="text-muted-foreground text-sm">
          This is a solo workspace. Create a team workspace when you want to collaborate and invite
          contributors.
        </p>
      </div>
    );
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="border-border bg-muted/50 space-y-4 rounded-md border p-4"
    >
      <h3 className="text-foreground text-sm font-semibold">Invite a member</h3>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="min-w-0 flex-1 space-y-1.5">
          <Label htmlFor="invite-email">
            Email <span className="text-primary ml-0.5">*</span>
          </Label>
          <Input
            id="invite-email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="colleague@example.com"
            disabled={isSubmitting}
            required
          />
        </div>

        <div className="w-40 shrink-0 space-y-1.5">
          <Label htmlFor="invite-role">Role</Label>
          <select
            id="invite-role"
            value={role}
            onChange={(e) => setRole(e.target.value as TeamRole)}
            disabled={isSubmitting}
            className={selectClassName}
          >
            <option value="Contributor">Contributor</option>
            <option value="Lead">Lead</option>
          </select>
        </div>

        <Button type="submit" disabled={isSubmitting} className="shrink-0 sm:self-end">
          {isSubmitting ? "Inviting…" : "Send invitation"}
        </Button>
      </div>

      {inviteState.type === "error" && (
        <p role="alert" className="text-destructive text-xs">
          {inviteState.message}
        </p>
      )}
      {inviteState.type === "success" && (
        <p role="status" className="text-success-700 text-xs">
          Invitation sent.
        </p>
      )}
    </form>
  );
}

export function MembersPanel({
  teamId,
  initialMembers,
  initialInvites,
  canManageTeam,
  canInviteMembers,
  canManageMembers,
}: {
  teamId: string;
  initialMembers: TeamMemberResponse[];
  initialInvites: TeamInviteResponse[];
  // Lead on any plan (TeamManageSettings): controls the invite-section toggle, faithful to the
  // former raw `isLead`. Semantically a "manage settings" capability, behaviorally "Lead, any plan".
  canManageTeam: boolean;
  // TeamInviteMembers: whether the InviteForm is active vs. the solo-workspace message.
  canInviteMembers: boolean;
  // TeamManageMembers: whether per-member / per-invite action buttons render.
  canManageMembers: boolean;
}): React.JSX.Element {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Members</CardTitle>
        <CardDescription>
          People who can read and contribute to this team workspace.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {canManageTeam ? (
          <InviteForm teamId={teamId} canInviteMembers={canInviteMembers} />
        ) : (
          <p className="border-border bg-muted text-muted-foreground rounded-md border px-4 py-3 text-sm">
            Only team leads can invite members or change roles.
          </p>
        )}

        {initialMembers.length === 0 && initialInvites.length === 0 ? (
          <EmptyState
            title="No members yet"
            description="Invite your colleagues to collaborate on this team."
          />
        ) : (
          <div className="border-border rounded-md border">
            <ul>
              {initialMembers.map((member) => (
                <MemberRow
                  key={member.userId}
                  member={member}
                  teamId={teamId}
                  canManage={canManageMembers}
                />
              ))}
              {initialInvites.map((invite) => (
                <InviteRow
                  key={invite.id}
                  invite={invite}
                  teamId={teamId}
                  canManage={canManageMembers}
                />
              ))}
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
