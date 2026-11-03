import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it } from "vitest";

import { UserProvider } from "@/providers/user-provider";

import { GuideHint } from "./guide-hint";

const USER = { id: "user-1", email: "user@example.test", name: "User One" };
const STORAGE_KEY = `grundet.onboarding.v1.${USER.id}`;

function renderGuide(user = USER) {
  return render(
    <UserProvider user={user}>
      <GuideHint guideId="project" title="About projects">
        <p>A project is the product space for one product or initiative.</p>
      </GuideHint>
    </UserProvider>
  );
}

describe("GuideHint", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("keeps the explanation hidden until the user opts in", () => {
    renderGuide();

    expect(screen.getByRole("button", { name: /Show me how this works/i })).toBeInTheDocument();
    expect(screen.queryByText(/product space/i)).not.toBeInTheDocument();
  });

  it("reveals the explanation on demand and marks the guide as seen for this user", async () => {
    const user = userEvent.setup();
    renderGuide();

    await user.click(screen.getByRole("button", { name: /Show me how this works/i }));

    expect(screen.getByText("About projects")).toBeInTheDocument();
    expect(screen.getByText(/product space/i)).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? "{}")).toEqual({
      seenGuides: { project: true },
    });
  });

  it("does not inherit another user's seen-state on the same device", async () => {
    const user = userEvent.setup();
    const first = renderGuide();

    await user.click(screen.getByRole("button", { name: /Show me how this works/i }));
    await user.click(screen.getByRole("button", { name: /Hide the guide/i }));
    first.unmount();

    renderGuide({ id: "user-2", email: "other@example.test", name: "User Two" });

    // A fresh user sees the full text affordance, not the collapsed "?" replay trigger.
    const trigger = screen.getByRole("button", { name: /Show me how this works/i });
    expect(trigger).toHaveTextContent(/Show me how this works/i);
  });

  it("collapses to a quiet replay trigger after dismissal", async () => {
    const user = userEvent.setup();
    renderGuide();

    await user.click(screen.getByRole("button", { name: /Show me how this works/i }));
    await user.click(screen.getByRole("button", { name: /Hide the guide/i }));

    expect(screen.queryByText(/product space/i)).not.toBeInTheDocument();

    // Seen guides keep a discoverable "?" trigger — the guide stays replayable.
    const replayTrigger = screen.getByRole("button", { name: /Show me how this works/i });
    await user.click(replayTrigger);

    expect(screen.getByText(/product space/i)).toBeInTheDocument();
  });
});
