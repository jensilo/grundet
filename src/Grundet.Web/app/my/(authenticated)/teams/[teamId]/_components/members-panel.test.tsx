import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { TeamInviteResponse, TeamMemberResponse } from "@/lib/api/generated/types.gen";

import { MembersPanel } from "./members-panel";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  changeTeamMemberRole: vi.fn(),
  inviteTeamMember: vi.fn(),
  removeTeamMember: vi.fn(),
  resendTeamInvite: vi.fn(),
  revokeTeamInvite: vi.fn(),
}));

const MEMBERS: TeamMemberResponse[] = [
  { userId: "user-1", email: "lead@example.com", displayName: "Lead Person", role: "Lead" },
  {
    userId: "user-2",
    email: "contributor@example.com",
    displayName: "Contributor Person",
    role: "Contributor",
  },
];

const INVITES: TeamInviteResponse[] = [
  {
    id: "invite-1",
    teamId: "team-1",
    email: "pending@example.com",
    role: "Contributor",
    expiresAt: "2026-12-31T00:00:00Z",
    createdAt: "2026-06-01T00:00:00Z",
  },
];

function renderPanel(
  overrides: Partial<{
    canManageTeam: boolean;
    canInviteMembers: boolean;
    canManageMembers: boolean;
  }>
): void {
  render(
    <MembersPanel
      teamId="team-1"
      initialMembers={MEMBERS}
      initialInvites={INVITES}
      canManageTeam={overrides.canManageTeam ?? false}
      canInviteMembers={overrides.canInviteMembers ?? false}
      canManageMembers={overrides.canManageMembers ?? false}
    />
  );
}

describe("MembersPanel", () => {
  it("renders no member/invite action buttons for a solo Lead (regression guard)", () => {
    // Solo Lead: can manage settings (rename / see invite section) but cannot invite or manage members.
    renderPanel({ canManageTeam: true, canInviteMembers: false, canManageMembers: false });

    // Invite section toggles open, but shows the solo-workspace message rather than the form.
    expect(screen.getByText(/this is a solo workspace/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^send invitation$/i })).not.toBeInTheDocument();

    // No member role/remove buttons.
    expect(screen.queryByRole("button", { name: /make contributor/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /make lead/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^remove$/i })).not.toBeInTheDocument();

    // No invite resend/revoke buttons.
    expect(screen.queryByRole("button", { name: /resend invitation/i })).not.toBeInTheDocument();
  });

  it("renders invite form and member/invite action buttons for a collaborative Lead", () => {
    renderPanel({ canManageTeam: true, canInviteMembers: true, canManageMembers: true });

    expect(screen.getByRole("button", { name: /^send invitation$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /make contributor/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /make lead/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /resend invitation/i })).toBeInTheDocument();
  });

  it("hides the invite section and all actions for a Contributor", () => {
    renderPanel({ canManageTeam: false, canInviteMembers: false, canManageMembers: false });

    expect(
      screen.getByText(/only team leads can invite members or change roles/i)
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^send invitation$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /make contributor/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /resend invitation/i })).not.toBeInTheDocument();
  });
});
