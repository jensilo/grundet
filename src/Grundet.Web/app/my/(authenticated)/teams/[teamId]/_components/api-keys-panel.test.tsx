import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createTeamApiKey, deleteTeamApiKey, revokeTeamApiKey } from "@/lib/api/generated/sdk.gen";
import type { TeamApiKeySummary, TeamMemberResponse } from "@/lib/api/generated/types.gen";

import { ApiKeysPanel } from "./api-keys-panel";

const { mockRefresh } = vi.hoisted(() => ({ mockRefresh: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: mockRefresh, push: vi.fn() }),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  createTeamApiKey: vi.fn(),
  revokeTeamApiKey: vi.fn(),
  deleteTeamApiKey: vi.fn(),
}));

const mockedCreate = vi.mocked(createTeamApiKey);
const mockedRevoke = vi.mocked(revokeTeamApiKey);
const mockedDelete = vi.mocked(deleteTeamApiKey);

const MEMBERS: TeamMemberResponse[] = [
  { userId: "user-alice", email: "alice@example.com", displayName: "Alice Smith", role: "Lead" },
  { userId: "user-bob", email: "bob@example.com", displayName: null, role: "Contributor" },
];

const ACTIVE_KEY: TeamApiKeySummary = {
  id: "key-1",
  teamId: "team-1",
  createdByUserId: "user-alice",
  role: "Lead",
  name: "Local MCP client",
  prefix: "gk_abc12",
  expiresAt: "2026-12-31T00:00:00.000Z",
  lastUsedAt: null,
  createdAt: "2026-06-01T00:00:00.000Z",
  revokedAt: null,
  isActive: true,
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("ApiKeysPanel", () => {
  it("renders safe metadata for a key and never a raw key or hash", () => {
    render(
      <ApiKeysPanel teamId="team-1" initialKeys={[ACTIVE_KEY]} members={MEMBERS} isLead={true} />
    );

    expect(screen.getByText("Local MCP client")).toBeInTheDocument();
    expect(screen.getByText("gk_abc12…")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
    // Created-by resolves via the members map.
    expect(screen.getByText("Alice Smith")).toBeInTheDocument();
    expect(screen.getByText("Never")).toBeInTheDocument();
    // The summary carries no secret; nothing key-like leaks into the DOM.
    expect(document.body.textContent).not.toContain("gk_abc12_secret");
  });

  it("falls back to email then 'Unknown' for created-by display name", () => {
    const byBob: TeamApiKeySummary = { ...ACTIVE_KEY, id: "key-2", createdByUserId: "user-bob" };
    const byGhost: TeamApiKeySummary = { ...ACTIVE_KEY, id: "key-3", createdByUserId: "user-x" };

    render(
      <ApiKeysPanel
        teamId="team-1"
        initialKeys={[byBob, byGhost]}
        members={MEMBERS}
        isLead={true}
      />
    );

    expect(screen.getByText("bob@example.com")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  it("shows an empty state when there are no keys", () => {
    render(<ApiKeysPanel teamId="team-1" initialKeys={[]} members={MEMBERS} isLead={true} />);
    expect(screen.getByText("No API keys yet")).toBeInTheDocument();
  });

  it("creates a key with the entered name and expiry, then reveals the raw key once", async () => {
    const user = userEvent.setup();
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true,
    });

    mockedCreate.mockResolvedValue({
      data: { rawKey: "gk_abc12_secret_raw_value", apiKey: ACTIVE_KEY },
      error: undefined,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    render(<ApiKeysPanel teamId="team-1" initialKeys={[]} members={MEMBERS} isLead={true} />);

    await user.type(screen.getByLabelText(/name/i), "Local MCP client");
    // Date inputs are unreliable with keyboard typing across locales; set directly.
    fireEvent.change(screen.getByLabelText("Expires"), { target: { value: "2026-12-31" } });
    await user.click(screen.getByRole("button", { name: "Create key" }));

    await waitFor(() => expect(mockedCreate).toHaveBeenCalledTimes(1));
    const sentBody = mockedCreate.mock.calls[0][0].body;
    expect(sentBody.name).toBe("Local MCP client");
    // The expiry must represent the END of the selected LOCAL calendar date, not
    // UTC midnight (which would be the previous day in western timezones and
    // expire the key early). Parsing the sent ISO back to local parts must yield
    // exactly the date the user selected, and it must not be midnight-UTC.
    const sent = new Date(sentBody.expiresAt ?? "");
    expect(sent.getFullYear()).toBe(2026);
    expect(sent.getMonth()).toBe(11); // December (0-indexed)
    expect(sent.getDate()).toBe(31);
    expect(sentBody.expiresAt).not.toBe("2026-12-31T00:00:00.000Z");
    expect(sentBody.expiresAt).toBe(new Date(2026, 11, 31, 23, 59, 59, 999).toISOString());

    // One-time reveal is shown and copyable.
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByDisplayValue("gk_abc12_secret_raw_value")).toBeInTheDocument();

    await user.click(within(dialog).getByRole("button", { name: "Copy" }));
    expect(writeText).toHaveBeenCalledWith("gk_abc12_secret_raw_value");

    // Dismissing clears the secret and refreshes.
    await user.click(within(dialog).getByRole("button", { name: "Done" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.queryByDisplayValue("gk_abc12_secret_raw_value")).not.toBeInTheDocument();
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("keeps the reveal dialog open when Escape is pressed while the raw key is shown", async () => {
    const user = userEvent.setup();
    mockedCreate.mockResolvedValue({
      data: { rawKey: "gk_abc12_secret_raw_value", apiKey: ACTIVE_KEY },
      error: undefined,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    render(<ApiKeysPanel teamId="team-1" initialKeys={[]} members={MEMBERS} isLead={true} />);

    await user.type(screen.getByLabelText(/name/i), "Escape test key");
    await user.click(screen.getByRole("button", { name: "Create key" }));

    // Wait for the reveal dialog to appear.
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByDisplayValue("gk_abc12_secret_raw_value")).toBeInTheDocument();

    // Pressing Escape must NOT clear the key or close the dialog.
    await user.keyboard("{Escape}");
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByDisplayValue("gk_abc12_secret_raw_value")).toBeInTheDocument();
    expect(mockRefresh).not.toHaveBeenCalled();

    // Explicit Done does close it and refresh.
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Done" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("sends a null expiry when the field is left empty", async () => {
    const user = userEvent.setup();
    mockedCreate.mockResolvedValue({
      data: { rawKey: "raw", apiKey: ACTIVE_KEY },
      error: undefined,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    render(<ApiKeysPanel teamId="team-1" initialKeys={[]} members={MEMBERS} isLead={true} />);

    await user.type(screen.getByLabelText(/name/i), "No expiry key");
    await user.click(screen.getByRole("button", { name: "Create key" }));

    await waitFor(() => expect(mockedCreate).toHaveBeenCalledTimes(1));
    expect(mockedCreate).toHaveBeenCalledWith({
      path: { teamId: "team-1" },
      body: { name: "No expiry key", expiresAt: null },
    });
  });

  it("surfaces a rate-limit message on 429", async () => {
    const user = userEvent.setup();
    mockedCreate.mockResolvedValue({
      data: undefined,
      error: { title: "Too Many Requests" },
      response: { status: 429 } as Response,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    render(<ApiKeysPanel teamId="team-1" initialKeys={[]} members={MEMBERS} isLead={true} />);

    await user.type(screen.getByLabelText(/name/i), "Spammy key");
    await user.click(screen.getByRole("button", { name: "Create key" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/too many keys/i);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("revokes a key after confirming", async () => {
    const user = userEvent.setup();
    mockedRevoke.mockResolvedValue({
      data: undefined,
      error: undefined,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    render(
      <ApiKeysPanel teamId="team-1" initialKeys={[ACTIVE_KEY]} members={MEMBERS} isLead={true} />
    );

    await user.click(screen.getByRole("button", { name: "Revoke" }));

    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Revoke key" }));

    await waitFor(() => expect(mockedRevoke).toHaveBeenCalledTimes(1));
    expect(mockedRevoke).toHaveBeenCalledWith({ path: { teamId: "team-1", keyId: "key-1" } });
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("does not offer revoke for an inactive key", () => {
    const revoked: TeamApiKeySummary = {
      ...ACTIVE_KEY,
      isActive: false,
      revokedAt: "2026-06-10T00:00:00.000Z",
    };

    render(
      <ApiKeysPanel teamId="team-1" initialKeys={[revoked]} members={MEMBERS} isLead={true} />
    );

    expect(screen.getByText("Revoked")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Revoke" })).not.toBeInTheDocument();
  });

  it("shows Delete for a revoked key when isLead and not for an active key", () => {
    const revoked: TeamApiKeySummary = {
      ...ACTIVE_KEY,
      id: "key-revoked",
      isActive: false,
      revokedAt: "2026-06-10T00:00:00.000Z",
    };

    render(
      <ApiKeysPanel
        teamId="team-1"
        initialKeys={[ACTIVE_KEY, revoked]}
        members={MEMBERS}
        isLead={true}
      />
    );

    // Active key shows Revoke, not Delete.
    expect(screen.getByRole("button", { name: "Revoke" })).toBeInTheDocument();
    // Revoked key shows Delete.
    expect(screen.getByRole("button", { name: "Delete" })).toBeInTheDocument();
  });

  it("does not show Delete for an inactive key when not isLead", () => {
    const revoked: TeamApiKeySummary = {
      ...ACTIVE_KEY,
      isActive: false,
      revokedAt: "2026-06-10T00:00:00.000Z",
    };

    render(
      <ApiKeysPanel teamId="team-1" initialKeys={[revoked]} members={MEMBERS} isLead={false} />
    );

    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });

  it("permanently deletes a revoked key after confirming", async () => {
    const user = userEvent.setup();
    const revoked: TeamApiKeySummary = {
      ...ACTIVE_KEY,
      isActive: false,
      revokedAt: "2026-06-10T00:00:00.000Z",
    };

    mockedDelete.mockResolvedValue({
      data: undefined,
      error: undefined,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    render(
      <ApiKeysPanel teamId="team-1" initialKeys={[revoked]} members={MEMBERS} isLead={true} />
    );

    await user.click(screen.getByRole("button", { name: "Delete" }));

    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Delete key" }));

    await waitFor(() => expect(mockedDelete).toHaveBeenCalledTimes(1));
    expect(mockedDelete).toHaveBeenCalledWith({
      path: { teamId: "team-1", keyId: "key-1" },
    });
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("surfaces a message on 409 when delete is called on an active key", async () => {
    const user = userEvent.setup();
    const revoked: TeamApiKeySummary = {
      ...ACTIVE_KEY,
      isActive: false,
      revokedAt: "2026-06-10T00:00:00.000Z",
    };

    mockedDelete.mockResolvedValue({
      data: undefined,
      error: { title: "Conflict" },
      response: { status: 409 } as Response,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    render(
      <ApiKeysPanel teamId="team-1" initialKeys={[revoked]} members={MEMBERS} isLead={true} />
    );

    await user.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Delete key" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/revoke it first/i);
  });
});
