import { NextRequest } from "next/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { GET } from "./route";

const cookieDelete = vi.fn();
const cookieSet = vi.fn();

vi.mock("next/headers", () => ({
  cookies: vi.fn(async () => ({
    get: vi.fn(),
    set: cookieSet,
    delete: cookieDelete,
  })),
}));

vi.mock("next/navigation", () => ({
  redirect: vi.fn((path: string) => {
    throw new Error(`REDIRECT:${path}`);
  }),
}));

vi.mock("@/lib/api/server", () => ({
  createServerClient: vi.fn(async () => ({})),
}));

const getMe = vi.fn();
const acceptInvite = vi.fn();
const completeOnboarding = vi.fn();

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getMe: (...args: unknown[]) => getMe(...args),
  acceptInvite: (...args: unknown[]) => acceptInvite(...args),
  completeOnboarding: (...args: unknown[]) => completeOnboarding(...args),
}));

function request(token: string): NextRequest {
  return new NextRequest(`https://my.grundet.test/invite/accept?token=${token}`);
}

describe("GET /invite/accept", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getMe.mockResolvedValue({ data: { id: "user-1" } });
    completeOnboarding.mockResolvedValue({ data: undefined });
  });

  it("lands the invitee on the joined team's projects page", async () => {
    acceptInvite.mockResolvedValue({ data: { teamId: "team-42" } });

    await expect(GET(request("tok"))).rejects.toThrow("REDIRECT:/teams/team-42/projects");
    expect(cookieDelete).toHaveBeenCalledWith("grundet_pending_invite_token");
  });

  it("still lands on the team when the best-effort completion call fails", async () => {
    acceptInvite.mockResolvedValue({ data: { teamId: "team-42" } });
    completeOnboarding.mockRejectedValue(new Error("boom"));

    await expect(GET(request("tok"))).rejects.toThrow("REDIRECT:/teams/team-42/projects");
  });

  it("parks the token and redirects to login when unauthenticated", async () => {
    getMe.mockResolvedValue({ data: undefined });

    await expect(GET(request("tok"))).rejects.toThrow("REDIRECT:/login");
    expect(cookieSet).toHaveBeenCalled();
    expect(acceptInvite).not.toHaveBeenCalled();
  });

  it("routes invalid tokens to the auth error page", async () => {
    acceptInvite.mockResolvedValue({
      data: undefined,
      error: { type: "https://grundet.app/errors/invalid-token" },
    });

    await expect(GET(request("tok"))).rejects.toThrow("REDIRECT:/auth/error?reason=invalid_invite");
    expect(cookieDelete).toHaveBeenCalledWith("grundet_pending_invite_token");
  });
});
