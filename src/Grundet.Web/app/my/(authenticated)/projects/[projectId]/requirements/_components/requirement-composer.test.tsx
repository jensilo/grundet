"use client";

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { type DraftAutosave, useDraftAutosave } from "@/components/app/composer/use-draft-autosave";
import {
  createRequirement,
  listFeatures,
  saveProjectRequirement,
} from "@/lib/api/generated/sdk.gen";
import type { RequirementResponse } from "@/lib/api/generated/types.gen";

import { RequirementComposer } from "./requirement-composer";

const mockedUseDraftAutosave = vi.mocked(useDraftAutosave);

function autosaveState(restored: boolean): DraftAutosave {
  return {
    restored,
    stale: false,
    saving: false,
    discard: vi.fn().mockResolvedValue(undefined),
    markSubmitted: vi.fn(),
  };
}

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock("@/components/app/composer/use-draft-autosave", () => ({
  useDraftAutosave: vi.fn().mockReturnValue({
    restored: false,
    stale: false,
    saving: false,
    discard: vi.fn(),
    markSubmitted: vi.fn(),
  }),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  createRequirement: vi.fn(),
  saveProjectRequirement: vi.fn(),
  // Feature search — called by the async FeaturePicker when it opens (edit/move tests).
  listFeatures: vi.fn(),
  // Attachment I/O — uploads happen on file selection; serve is used by thumbnails.
  createAttachments: vi.fn().mockResolvedValue({ data: [], error: undefined }),
  getAttachmentContent: vi.fn().mockResolvedValue({ data: new Blob(), error: undefined }),
  // Draft I/O — kept as a safety net in case the autosave mock isn't fully isolating.
  getNewRequirementDraft: vi.fn().mockResolvedValue({ data: { draft: null }, error: undefined }),
  upsertNewRequirementDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  deleteNewRequirementDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  getRequirementDraft: vi.fn().mockResolvedValue({ data: { draft: null }, error: undefined }),
  upsertRequirementDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  deleteRequirementDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  // Label suggestions — called on LabelChipInput focus in edit mode.
  getProjectLabels: vi.fn().mockResolvedValue({ data: { labels: [] }, error: undefined }),
}));

const mockedCreateRequirement = vi.mocked(createRequirement);
const mockedSaveProjectRequirement = vi.mocked(saveProjectRequirement);
const mockedListFeatures = vi.mocked(listFeatures);

function makeFeatureListResponse(items: { id: string; name: string }[]): unknown {
  return {
    data: { items },
    error: undefined,
    request: new Request("https://grundet.test"),
    response: new Response(),
  };
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

// Two-feature project for the move-to-feature tests — the picker fetches these on open.
const FEATURES = [
  { id: "feat-1", name: "Feature One" },
  { id: "feat-2", name: "Feature Two" },
];

const BASE_REQUIREMENT: RequirementResponse = {
  id: "req-1",
  requirementId: "req-1",
  featureId: "feat-1",
  sequenceNumber: 1,
  ref: "R-1",
  text: "The system shall validate user input.",
  status: "Draft",
  priority: "Should",
  labels: [],
  acceptanceCriteria: [],
  rationale: null,
  reason: null,
  changedAt: "2024-01-01T00:00:00Z",
  changedByUserId: "user-alice",
  changeType: "Created",
};

function makeOkCreate(requirementId = "new-req"): unknown {
  return {
    data: {
      requirement: { ...BASE_REQUIREMENT, requirementId },
    },
    error: undefined,
    request: new Request("https://grundet.test"),
    response: new Response(),
  };
}

function makeOkSave(): unknown {
  return {
    data: { requirement: BASE_REQUIREMENT },
    error: undefined,
    request: new Request("https://grundet.test"),
    response: new Response(),
  };
}

// ---------------------------------------------------------------------------
// Create mode tests
// ---------------------------------------------------------------------------

describe("RequirementComposer – create mode (WP6)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedCreateRequirement.mockResolvedValue(
      makeOkCreate() as Awaited<ReturnType<typeof createRequirement>>
    );
  });

  it("shows the rationale field prominently without any interaction", () => {
    render(
      <RequirementComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        featureId="feat-1"
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // The "Because" label must be visible without clicking any adder button.
    expect(screen.getByLabelText(/because/i)).toBeInTheDocument();
  });

  it("shows the project and feature context header (WP4)", () => {
    render(
      <RequirementComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        featureId="feat-1"
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByText(/creating in/i)).toBeInTheDocument();
    expect(screen.getByText("Core")).toBeInTheDocument();
    expect(screen.getByText("Checkout")).toBeInTheDocument();
  });

  it("does not show a reason field in create mode", () => {
    render(
      <RequirementComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        featureId="feat-1"
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.queryByLabelText(/reason for this change/i)).not.toBeInTheDocument();
  });

  it("sends rationale in the create payload", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        featureId="feat-1"
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await user.type(
      screen.getByRole("textbox", { name: /intent text/i }),
      "The system shall export data."
    );
    await user.type(screen.getByLabelText(/because/i), "Needed for compliance reporting.");
    await user.click(screen.getByRole("button", { name: /create intent/i }));

    await waitFor(() => {
      expect(mockedCreateRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({
            rationale: "Needed for compliance reporting.",
          }),
        })
      );
    });
  });

  it("sends null rationale when rationale field is left empty", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        featureId="feat-1"
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await user.type(
      screen.getByRole("textbox", { name: /intent text/i }),
      "The system shall handle errors."
    );
    await user.click(screen.getByRole("button", { name: /create intent/i }));

    await waitFor(() => {
      expect(mockedCreateRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ rationale: null }),
        })
      );
    });
  });
});

// ---------------------------------------------------------------------------
// Edit mode tests
// ---------------------------------------------------------------------------

describe("RequirementComposer – edit mode (WP6)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedSaveProjectRequirement.mockResolvedValue(
      makeOkSave() as Awaited<ReturnType<typeof saveProjectRequirement>>
    );
  });

  it("shows the rationale field seeded from the entity", () => {
    const entity: RequirementResponse = {
      ...BASE_REQUIREMENT,
      rationale: "This is the why for this requirement.",
    };

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={entity}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/because/i)).toHaveValue("This is the why for this requirement.");
  });

  it("shows the reason-for-this-change field in edit mode", () => {
    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/reason for this change/i)).toBeInTheDocument();
  });

  it("reason starts empty even when entity has a previous reason", () => {
    const entity: RequirementResponse = { ...BASE_REQUIREMENT, reason: "Previous change comment." };

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={entity}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Reason-for-change always starts blank — it's the user's per-version comment, not carried over.
    expect(screen.getByLabelText(/reason for this change/i)).toHaveValue("");
  });

  it("sends rationale and reason independently in the save payload", async () => {
    const user = userEvent.setup();
    const entity: RequirementResponse = {
      ...BASE_REQUIREMENT,
      rationale: "Original rationale.",
    };

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={entity}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Update rationale and provide a reason for this version.
    const rationaleField = screen.getByLabelText(/because/i);
    await user.clear(rationaleField);
    await user.type(rationaleField, "Updated rationale.");

    await user.type(screen.getByLabelText(/reason for this change/i), "Refined the product truth.");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedSaveProjectRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({
            rationale: "Updated rationale.",
            reason: "Refined the product truth.",
          }),
        })
      );
    });
  });

  it("sends null rationale when the field is cleared", async () => {
    const user = userEvent.setup();
    const entity: RequirementResponse = {
      ...BASE_REQUIREMENT,
      rationale: "Will be cleared.",
    };

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={entity}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const rationaleField = screen.getByLabelText(/because/i);
    await user.clear(rationaleField);

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedSaveProjectRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ rationale: null }),
        })
      );
    });
  });

  it("disables the reason field until a real change is made, then enables it", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const reasonField = screen.getByLabelText(/reason for this change/i);
    expect(reasonField).toBeDisabled();

    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Updated intent text.");

    expect(reasonField).toBeEnabled();
  });

  it("keeps Save disabled when only the reason is dirty after a reverted change", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const saveButton = screen.getByRole("button", { name: /save new version/i });
    expect(saveButton).toBeDisabled();

    // Change a field so the reason field enables, type a reason, then revert the field.
    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Updated intent text.");
    await user.type(screen.getByLabelText(/reason for this change/i), "Because reasons");
    await user.clear(textField);
    await user.type(textField, BASE_REQUIREMENT.text);

    // Only the reason is now dirty — no real change remains, so Save must stay disabled.
    expect(saveButton).toBeDisabled();
  });

  it("sends reason: null when the field is left blank so the new version doesn't inherit the prior reason", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Dirty the form so it can be submitted.
    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Updated intent text.");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      const body = mockedSaveProjectRequirement.mock.calls[0][0].body;
      expect(body.reason).toBeNull();
    });
  });
});

// ---------------------------------------------------------------------------
// Edit-mode progressive disclosure (app-convenience Item 1)
// ---------------------------------------------------------------------------

describe("RequirementComposer – edit-mode progressive disclosure", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedSaveProjectRequirement.mockResolvedValue(
      makeOkSave() as Awaited<ReturnType<typeof saveProjectRequirement>>
    );
  });

  it("hides empty optional fields behind the adder, keeping rationale and reason visible", () => {
    // BASE_REQUIREMENT carries only defaults: priority Should, status Draft, no labels/criteria.
    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Default-valued optionals are collapsed.
    expect(screen.queryByLabelText(/^priority$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^status$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^labels$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^acceptance criteria$/i)).not.toBeInTheDocument();

    // …and offered by the adder instead.
    expect(screen.getByRole("button", { name: /^priority$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^status$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^labels$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^acceptance criteria$/i })).toBeInTheDocument();

    // Always-visible edit fields stay put.
    expect(screen.getByLabelText(/because/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/reason for this change/i)).toBeInTheDocument();
  });

  it("reveals optional fields that hold non-default values, hiding their adder buttons", () => {
    const entity: RequirementResponse = {
      ...BASE_REQUIREMENT,
      priority: "Must",
      status: "Active",
      labels: ["backend"],
      acceptanceCriteria: ["The export completes within 5 seconds."],
    };

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={entity}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/^priority$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^status$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^labels$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^acceptance criteria$/i)).toBeInTheDocument();

    // Every optional is revealed, so the adder offers nothing.
    expect(screen.queryByRole("button", { name: /^priority$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^labels$/i })).not.toBeInTheDocument();
  });

  it("reveals a hidden optional field when its adder button is clicked", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.queryByLabelText(/^priority$/i)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /^priority$/i }));
    expect(screen.getByLabelText(/^priority$/i)).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Attachment tests
// ---------------------------------------------------------------------------

describe("RequirementComposer – image attachments", () => {
  beforeAll(() => {
    // jsdom has no object-URL implementation; the thumbnail renderer needs it.
    globalThis.URL.createObjectURL = vi.fn(() => "blob:mock");
    globalThis.URL.revokeObjectURL = vi.fn();
  });

  beforeEach(() => {
    vi.clearAllMocks();
    // clearAllMocks wipes call records but not return values; re-seed the default so a per-test
    // override (the discard case) can't leak into the next test.
    mockedUseDraftAutosave.mockReturnValue(autosaveState(false));
    mockedCreateRequirement.mockResolvedValue(
      makeOkCreate() as Awaited<ReturnType<typeof createRequirement>>
    );
    mockedSaveProjectRequirement.mockResolvedValue(
      makeOkSave() as Awaited<ReturnType<typeof saveProjectRequirement>>
    );
  });

  it("restores the original attachments on discard so a removed image is not dropped on save", async () => {
    const user = userEvent.setup();
    // A restored draft surfaces the Discard control.
    mockedUseDraftAutosave.mockReturnValue(autosaveState(true));

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        initialAttachments={[
          {
            id: "att-1",
            originalFileName: "spec.png",
            contentType: "image/jpeg",
            width: 100,
            height: 80,
          },
        ]}
        attachmentsLoaded
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Remove the existing image, then discard the draft.
    await user.click(screen.getByRole("button", { name: /remove image/i }));
    await user.click(screen.getByRole("button", { name: /discard draft/i }));

    // Make a real change so the save is permitted (a no-op save is blocked); the restored
    // attachments must still ride along in the payload.
    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Updated intent text.");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedSaveProjectRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          // Discard must restore att-1 — without the reset this would save [].
          body: expect.objectContaining({ attachmentIds: ["att-1"] }),
        })
      );
    });
  });

  it("sends an empty attachmentIds array on create when no images are added", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        featureId="feat-1"
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await user.type(
      screen.getByRole("textbox", { name: /intent text/i }),
      "The system shall render charts."
    );
    await user.click(screen.getByRole("button", { name: /create intent/i }));

    await waitFor(() => {
      expect(mockedCreateRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ attachmentIds: [] }),
        })
      );
    });
  });

  it("sends pre-populated attachment ids on save", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        initialAttachments={[
          {
            id: "att-1",
            originalFileName: "spec.png",
            contentType: "image/jpeg",
            width: 100,
            height: 80,
          },
        ]}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Edited with an existing image.");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedSaveProjectRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ attachmentIds: ["att-1"] }),
        })
      );
    });
  });

  it("omits attachmentIds on save when the attachment preload failed so existing images are preserved", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        // Preload failed: an empty list is indistinguishable from a real empty set.
        initialAttachments={[]}
        attachmentsLoaded={false}
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // A text-only edit — the user never touched attachments.
    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Edited text after a failed attachment load.");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      const body = mockedSaveProjectRequirement.mock.calls[0][0].body;
      // Crucial: the field must be absent (not []), so the backend keeps the existing set.
      expect(body).not.toHaveProperty("attachmentIds");
    });
  });

  it("sends attachmentIds on save when the attachment set loaded successfully", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        initialAttachments={[
          {
            id: "att-1",
            originalFileName: "spec.png",
            contentType: "image/jpeg",
            width: 100,
            height: 80,
          },
        ]}
        attachmentsLoaded
        currentFeature={{ id: "feat-1", name: "Checkout" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Edited text with a known attachment set.");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedSaveProjectRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ attachmentIds: ["att-1"] }),
        })
      );
    });
  });
});

// ---------------------------------------------------------------------------
// Move-to-feature tests (WP5)
// ---------------------------------------------------------------------------

describe("RequirementComposer – move to feature (WP5)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedSaveProjectRequirement.mockResolvedValue(
      makeOkSave() as Awaited<ReturnType<typeof saveProjectRequirement>>
    );
    // The picker fetches the project's features when it opens.
    mockedListFeatures.mockResolvedValue(
      makeFeatureListResponse(FEATURES) as Awaited<ReturnType<typeof listFeatures>>
    );
  });

  it("does not render the feature picker when the project has only one feature", () => {
    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Feature One" }}
        featureCount={1}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.queryByLabelText(/^feature$/i)).not.toBeInTheDocument();
  });

  it("shows an editing context header tracking the selected feature (WP4)", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Feature One" }}
        featureCount={2}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Scope to the header element — the feature name also appears in the picker trigger.
    const header = screen.getByText(/editing in/i).closest("div");
    expect(header).toHaveTextContent("Core");
    expect(header).toHaveTextContent("Feature One");

    // Moving to another feature updates the header live.
    await user.click(screen.getByLabelText(/^feature$/i));
    // base-ui opens the listbox in a portal on the next tick — findByRole retries.
    await user.click(await screen.findByRole("option", { name: "Feature Two" }));

    expect(header).toHaveTextContent("Feature Two");
  });

  it("renders the feature picker defaulting to the intent's current feature", () => {
    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Feature One" }}
        featureCount={2}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/^feature$/i)).toHaveTextContent("Feature One");
  });

  it("sends targetFeatureId when a different feature is selected", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Feature One" }}
        featureCount={2}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await user.click(screen.getByLabelText(/^feature$/i));
    // base-ui opens the listbox in a portal on the next tick — findByRole retries.
    await user.click(await screen.findByRole("option", { name: "Feature Two" }));

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedSaveProjectRequirement).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ targetFeatureId: "feat-2" }),
        })
      );
    });
  });

  it("does not send targetFeatureId when the feature is left unchanged", async () => {
    const user = userEvent.setup();

    render(
      <RequirementComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        initial={BASE_REQUIREMENT}
        currentFeature={{ id: "feat-1", name: "Feature One" }}
        featureCount={2}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Dirty a different field so the form is submittable without touching the feature.
    const textField = screen.getByRole("textbox", { name: /intent text/i });
    await user.clear(textField);
    await user.type(textField, "Updated intent text.");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      const body = mockedSaveProjectRequirement.mock.calls[0][0].body;
      expect(body).not.toHaveProperty("targetFeatureId");
    });
  });
});
