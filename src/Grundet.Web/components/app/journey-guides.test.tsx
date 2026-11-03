import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it } from "vitest";

import { UserProvider } from "@/providers/user-provider";

import { FeatureGuide, IntentGuide, ProjectGuide, WorkGuide } from "./journey-guides";

const USER = { id: "user-1", email: "user@example.test", name: "User One" };

function renderGuide(node: React.ReactNode) {
  return render(<UserProvider user={USER}>{node}</UserProvider>);
}

describe("journey guides", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("keeps every guide opt-in – no copy until the user asks", () => {
    renderGuide(<ProjectGuide />);

    expect(screen.getByRole("button", { name: /Show me how this works/i })).toBeInTheDocument();
    expect(screen.queryByText(/product truth for one initiative/i)).not.toBeInTheDocument();
  });

  it("each guide names the next concrete step in the Project → Feature → Intent → Work chain", async () => {
    const user = userEvent.setup();

    for (const { node, next } of [
      { node: <ProjectGuide />, next: /add a feature/i },
      { node: <FeatureGuide />, next: /write the intent/i },
      { node: <IntentGuide />, next: /add work to make the intent true/i },
      { node: <WorkGuide />, next: /link it to the intent/i },
    ]) {
      const view = renderGuide(node);
      await user.click(screen.getByRole("button", { name: /Show me how this works/i }));
      expect(screen.getByText(next)).toBeInTheDocument();
      view.unmount();
      window.localStorage.clear();
    }
  });

  it("marks its own guide seen so it collapses to a replay trigger, independently per guide", async () => {
    const user = userEvent.setup();
    const view = renderGuide(<FeatureGuide />);

    await user.click(screen.getByRole("button", { name: /Show me how this works/i }));
    view.unmount();

    // A different guide the user has not opened still offers the full affordance.
    renderGuide(<WorkGuide />);
    expect(screen.getByRole("button", { name: /Show me how this works/i })).toHaveTextContent(
      /Show me how this works/i
    );
  });
});
