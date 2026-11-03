"use client";

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { RequirementComposerDialog } from "./requirement-composer-dialog";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

const replace = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace, refresh: vi.fn(), push: vi.fn() }),
  usePathname: () => "/projects/proj-1/requirements",
  useSearchParams: () => searchParams,
}));

// Stub the composer body — this suite only exercises the dialog's open/close + compose-param wiring,
// not the form itself (covered by requirement-composer.test.tsx).
vi.mock("./requirement-composer", () => ({
  RequirementComposer: ({ onCancel }: { onCancel: () => void }) => (
    <button type="button" onClick={onCancel}>
      stub-cancel
    </button>
  ),
}));

const CREATE_PROPS = {
  mode: "create",
  projectId: "proj-1",
  projectName: "Core",
  featureId: "feat-1",
  currentFeature: { id: "feat-1", name: "Checkout" },
  featureCount: 1,
  triggerLabel: "New intent",
} as const;

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("RequirementComposerDialog – compose param (WP2)", () => {
  beforeAll(() => {
    // Base UI Dialog needs these in jsdom.
    Element.prototype.hasPointerCapture = vi.fn(() => false);
    Element.prototype.setPointerCapture = vi.fn();
    Element.prototype.releasePointerCapture = vi.fn();
    Element.prototype.scrollIntoView = vi.fn();
    globalThis.ResizeObserver = class {
      observe(): void {}
      unobserve(): void {}
      disconnect(): void {}
    };
  });

  beforeEach(() => {
    vi.clearAllMocks();
    searchParams = new URLSearchParams();
  });

  it("opens automatically when ?compose=intent is present and opted in", () => {
    searchParams = new URLSearchParams("compose=intent");
    render(<RequirementComposerDialog {...CREATE_PROPS} openOnComposeParam />);

    expect(screen.getByText("stub-cancel")).toBeInTheDocument();
  });

  it("ignores the compose param when not opted in", () => {
    searchParams = new URLSearchParams("compose=intent");
    render(<RequirementComposerDialog {...CREATE_PROPS} />);

    expect(screen.queryByText("stub-cancel")).not.toBeInTheDocument();
  });

  it("strips the compose param on close, preserving other params", async () => {
    const user = userEvent.setup();
    searchParams = new URLSearchParams("compose=intent&status=Active");
    render(<RequirementComposerDialog {...CREATE_PROPS} openOnComposeParam />);

    await user.click(screen.getByText("stub-cancel"));

    expect(replace).toHaveBeenCalledWith("/projects/proj-1/requirements?status=Active", {
      scroll: false,
    });
  });
});
