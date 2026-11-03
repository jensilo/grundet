import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  getTeam,
  getTeamLimits,
  getTeamPermissions,
  listTeamApiKeys,
  listTeamInvites,
  listTeamMembers,
} from "@/lib/api/generated/sdk.gen";
import type { Permission } from "@/lib/api/generated/types.gen";

import TeamDetailPage from "./page";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getTeam: vi.fn(),
  getTeamLimits: vi.fn(),
  getTeamPermissions: vi.fn(),
  listTeamApiKeys: vi.fn(),
  listTeamInvites: vi.fn(),
  listTeamMembers: vi.fn(),
}));

vi.mock("@/lib/api/server", () => ({
  createServerClient: vi.fn(async () => ({})),
}));

vi.mock("next/navigation", () => ({
  notFound: vi.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));

// Stub child panels so the gating logic is tested in isolation, not their internals.
vi.mock("./_components/members-panel", () => ({ MembersPanel: () => <div /> }));
vi.mock("./_components/usage-limits-panel", () => ({ UsageLimitsPanel: () => <div /> }));
vi.mock("./_components/connections-panel", () => ({ ConnectionsPanel: () => <div /> }));
vi.mock("./_components/rename-delete-actions", () => ({
  TeamSettingsPanel: () => <div />,
  TeamDangerZonePanel: () => <div />,
}));
vi.mock("./_components/api-keys-panel", () => ({
  ApiKeysPanel: () => <div data-testid="api-keys-panel" />,
}));

const mockedGetTeam = vi.mocked(getTeam);
const mockedGetTeamLimits = vi.mocked(getTeamLimits);
const mockedGetTeamPermissions = vi.mocked(getTeamPermissions);
const mockedListMembers = vi.mocked(listTeamMembers);
const mockedListInvites = vi.mocked(listTeamInvites);
const mockedListApiKeys = vi.mocked(listTeamApiKeys);

// Mocked SDK results are partial fixtures; cast through a loose result shape.
type MockResult = { data: unknown; error: undefined };
function result(data: unknown): MockResult {
  return { data, error: undefined };
}

const LEAD_PERMISSIONS: Permission[] = [
  "TeamManageSettings",
  "TeamDelete",
  "TeamInviteMembers",
  "TeamManageMembers",
];

function setup(permissions: Permission[]): void {
  mockedGetTeam.mockResolvedValue(
    result({ id: "team-1", name: "Acme" }) as Awaited<ReturnType<typeof getTeam>>
  );
  mockedGetTeamLimits.mockResolvedValue(
    result({ plan: "TeamS", aiUsageLabel: "x" }) as Awaited<ReturnType<typeof getTeamLimits>>
  );
  mockedGetTeamPermissions.mockResolvedValue(
    result({ permissions }) as Awaited<ReturnType<typeof getTeamPermissions>>
  );
  mockedListMembers.mockResolvedValue(result([]) as Awaited<ReturnType<typeof listTeamMembers>>);
  mockedListInvites.mockResolvedValue(result([]) as Awaited<ReturnType<typeof listTeamInvites>>);
  mockedListApiKeys.mockResolvedValue(result([]) as Awaited<ReturnType<typeof listTeamApiKeys>>);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("TeamDetailPage API keys gating", () => {
  it("renders the API keys panel and fetches keys for a Lead", async () => {
    setup(LEAD_PERMISSIONS);

    render(await TeamDetailPage({ params: Promise.resolve({ teamId: "team-1" }) }));

    expect(screen.getByTestId("api-keys-panel")).toBeInTheDocument();
    expect(mockedListApiKeys).toHaveBeenCalledTimes(1);
  });

  it("omits the panel and does not fetch keys for a non-Lead", async () => {
    setup([]);

    render(await TeamDetailPage({ params: Promise.resolve({ teamId: "team-1" }) }));

    expect(screen.queryByTestId("api-keys-panel")).not.toBeInTheDocument();
    expect(mockedListApiKeys).not.toHaveBeenCalled();
  });
});
