import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OnboardingOrientation } from "./onboarding-orientation";

const pushMock = vi.fn();
const completeOnboardingMock = vi.fn();
const listTeamsMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  completeOnboarding: (...args: unknown[]) => completeOnboardingMock(...args),
  listTeams: (...args: unknown[]) => listTeamsMock(...args),
}));

describe("OnboardingOrientation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    completeOnboardingMock.mockResolvedValue({ data: {}, error: undefined });
    listTeamsMock.mockResolvedValue({ data: [{ id: "t1" }], error: undefined });
  });

  it("greets the user and shows the Project → Feature → Intent → Work overview", () => {
    render(<OnboardingOrientation displayName="Alex" inviteHref={null} />);

    expect(screen.getByRole("heading", { name: "Welcome, Alex" })).toBeInTheDocument();
    expect(screen.getByText("Project")).toBeInTheDocument();
    expect(screen.getByText("Feature")).toBeInTheDocument();
    expect(screen.getByText("Intent")).toBeInTheDocument();
    expect(screen.getByText("Work")).toBeInTheDocument();
  });

  it("marks onboarding complete once, then routes to the team's project surface", async () => {
    const user = userEvent.setup();
    render(<OnboardingOrientation displayName="Alex" inviteHref={null} />);

    await user.click(screen.getByRole("button", { name: /Create your first project/i }));

    expect(completeOnboardingMock).toHaveBeenCalledTimes(1);
    expect(pushMock).toHaveBeenCalledWith("/teams/t1/projects?new=1");
  });

  it("falls back to the dashboard when teams cannot be resolved", async () => {
    listTeamsMock.mockResolvedValue({ data: undefined, error: { title: "boom" } });
    const user = userEvent.setup();
    render(<OnboardingOrientation displayName="Alex" inviteHref={null} />);

    await user.click(screen.getByRole("button", { name: /Create your first project/i }));

    expect(pushMock).toHaveBeenCalledWith("/dashboard");
  });

  it("falls back to the dashboard when the user has multiple teams", async () => {
    listTeamsMock.mockResolvedValue({ data: [{ id: "t1" }, { id: "t2" }], error: undefined });
    const user = userEvent.setup();
    render(<OnboardingOrientation displayName="Alex" inviteHref={null} />);

    await user.click(screen.getByRole("button", { name: /Create your first project/i }));

    expect(pushMock).toHaveBeenCalledWith("/dashboard");
  });

  it("marks onboarding complete once, then routes to the dashboard on skip", async () => {
    const user = userEvent.setup();
    render(<OnboardingOrientation displayName="Alex" inviteHref={null} />);

    await user.click(screen.getByRole("button", { name: /Skip for now/i }));

    expect(completeOnboardingMock).toHaveBeenCalledTimes(1);
    expect(listTeamsMock).not.toHaveBeenCalled();
    expect(pushMock).toHaveBeenCalledWith("/dashboard");
  });

  it("still navigates when the completion call fails – onboarding never traps the user", async () => {
    completeOnboardingMock.mockRejectedValue(new Error("network down"));
    const user = userEvent.setup();
    render(<OnboardingOrientation displayName="Alex" inviteHref={null} />);

    await user.click(screen.getByRole("button", { name: /Skip for now/i }));

    expect(pushMock).toHaveBeenCalledWith("/dashboard");
  });

  describe("invited variant", () => {
    const inviteHref = "/invite/accept?token=abc123";

    it("routes the primary action through the invite-accept path, not project creation", async () => {
      const user = userEvent.setup();
      render(<OnboardingOrientation displayName="Alex" inviteHref={inviteHref} />);

      await user.click(screen.getByRole("button", { name: /Go to your team/i }));

      expect(completeOnboardingMock).toHaveBeenCalledTimes(1);
      expect(listTeamsMock).not.toHaveBeenCalled();
      expect(pushMock).toHaveBeenCalledWith(inviteHref);
    });

    it("also routes skip through the invite-accept path so the invite is never lost", async () => {
      const user = userEvent.setup();
      render(<OnboardingOrientation displayName="Alex" inviteHref={inviteHref} />);

      await user.click(screen.getByRole("button", { name: /Skip for now/i }));

      expect(completeOnboardingMock).toHaveBeenCalledTimes(1);
      expect(pushMock).toHaveBeenCalledWith(inviteHref);
    });
  });
});
