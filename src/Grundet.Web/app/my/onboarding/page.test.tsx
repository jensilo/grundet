import { describe, expect, it, vi } from "vitest";

import OnboardingPage from "./page";

const redirectMock = vi.fn((url: string): never => {
  throw new Error(`REDIRECT:${url}`);
});

let pendingInviteToken: string | undefined;

const getMeMock = vi.fn();

vi.mock("next/navigation", () => ({
  redirect: (url: string) => redirectMock(url),
}));

vi.mock("next/headers", () => ({
  cookies: async () => ({
    get: (name: string) =>
      name === "grundet_pending_invite_token" && pendingInviteToken
        ? { value: pendingInviteToken }
        : undefined,
  }),
}));

vi.mock("@/lib/api/server", () => ({
  createServerClient: async () => ({}),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getMe: (...args: unknown[]) => getMeMock(...args),
}));

function setup({
  me,
  meError = false,
  inviteToken,
}: {
  me?: { displayName: string | null; onboardingCompletedAt: string | null };
  meError?: boolean;
  inviteToken?: string;
}) {
  pendingInviteToken = inviteToken;
  getMeMock.mockResolvedValue(
    meError
      ? { data: undefined, error: { title: "unauthorized" } }
      : { data: { id: "user-1", email: "user@example.test", ...me }, error: undefined }
  );
}

async function expectRedirect(to: string) {
  await expect(OnboardingPage()).rejects.toThrow(`REDIRECT:${to}`);
  expect(redirectMock).toHaveBeenCalledWith(to);
}

describe("OnboardingPage precedence", () => {
  it("redirects to /login when the user cannot be resolved", async () => {
    setup({ meError: true });

    await expectRedirect("/login");
  });

  it("shows Step 1 while the display name is missing – even with a pending invite", async () => {
    setup({
      me: { displayName: null, onboardingCompletedAt: null },
      inviteToken: "abc123",
    });

    const element = await OnboardingPage();

    expect(element.props.initialStep).toBe("name");
    expect(element.props.inviteHref).toBe("/invite/accept?token=abc123");
  });

  it("shows Step 2 with the invite context for a brand-new invited user", async () => {
    setup({
      me: { displayName: "Alex", onboardingCompletedAt: null },
      inviteToken: "abc123",
    });

    const element = await OnboardingPage();

    expect(element.props.initialStep).toBe("orientation");
    expect(element.props.inviteHref).toBe("/invite/accept?token=abc123");
  });

  it("resumes Step 2 while onboarding is not completed", async () => {
    setup({ me: { displayName: "Alex", onboardingCompletedAt: null } });

    const element = await OnboardingPage();

    expect(element.props.initialStep).toBe("orientation");
    expect(element.props.initialDisplayName).toBe("Alex");
    expect(element.props.inviteHref).toBeNull();
  });

  it("redirects completed users to the dashboard", async () => {
    setup({
      me: { displayName: "Alex", onboardingCompletedAt: "2026-07-01T10:00:00Z" },
    });

    await expectRedirect("/dashboard");
  });

  it("redirects an already-onboarded invitee straight to invite accept", async () => {
    setup({
      me: { displayName: "Alex", onboardingCompletedAt: "2026-07-01T10:00:00Z" },
      inviteToken: "abc123",
    });

    await expectRedirect("/invite/accept?token=abc123");
  });
});
