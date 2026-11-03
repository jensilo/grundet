import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { type DraftAutosave, useDraftAutosave } from "@/components/app/composer/use-draft-autosave";
import { createWorkItem, updateProjectWorkItem } from "@/lib/api/generated/sdk.gen";
import type { TeamMemberResponse, WorkItemResponse } from "@/lib/api/generated/types.gen";

import { WorkItemComposer } from "./work-item-composer";

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

// Mock the autosave hook — we don't want async draft behaviour in these tests.
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
  createWorkItem: vi.fn(),
  updateProjectWorkItem: vi.fn(),
  // Attachment I/O — uploads happen on file selection; serve backs existing-image thumbnails.
  createAttachments: vi.fn().mockResolvedValue({ data: [], error: undefined }),
  getAttachmentContent: vi.fn().mockResolvedValue({ data: new Blob(), error: undefined }),
  // Draft I/O — called by the real hook if not mocked above; kept here as a safety net.
  getNewWorkItemDraft: vi.fn().mockResolvedValue({ data: { draft: null }, error: undefined }),
  upsertNewWorkItemDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  deleteNewWorkItemDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  getWorkItemDraft: vi.fn().mockResolvedValue({ data: { draft: null }, error: undefined }),
  upsertWorkItemDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  deleteWorkItemDraft: vi.fn().mockResolvedValue({ data: null, error: undefined }),
  // Label suggestions — called on LabelChipInput focus in edit mode.
  getProjectLabels: vi.fn().mockResolvedValue({ data: { labels: [] }, error: undefined }),
  // Intent search — called by IntentLinkPicker when the "linked intent" field is opened.
  listProjectRequirements: vi.fn().mockResolvedValue({
    data: {
      items: [{ requirementId: "req-1", ref: "IN-1", text: "Users can log in" }],
    },
    error: undefined,
  }),
}));

const mockedCreateWorkItem = vi.mocked(createWorkItem);
const mockedUpdateProjectWorkItem = vi.mocked(updateProjectWorkItem);

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const TEAM_MEMBERS: TeamMemberResponse[] = [
  { userId: "user-alice", email: "alice@example.com", displayName: "Alice Smith", role: "Lead" },
  { userId: "user-bob", email: "bob@example.com", displayName: "Bob Jones", role: "Contributor" },
  { userId: "user-carol", email: "carol@example.com", displayName: null, role: "Contributor" },
];

const BASE_WORK_ITEM: WorkItemResponse = {
  id: "wi-1",
  workItemId: "wi-1",
  projectId: "proj-1",
  title: "Fix the login bug",
  description: null,
  status: "Todo",
  priority: "Medium",
  type: "Task",
  labels: [],
  assignedToUserId: null,
  assignedToDisplayName: null,
  blockedReason: null,
  reason: null,
  sequenceNumber: 1,
  ref: "WO-1",
  changedAt: "2024-01-01T00:00:00Z",
  changedByUserId: "user-alice",
  changeType: "Created",
  reasonPrompted: false,
};

function fakeCreatedItem(overrides: Partial<WorkItemResponse> = {}): WorkItemResponse {
  return { ...BASE_WORK_ITEM, ...overrides };
}

function makeOkResponse<T>(data: T): unknown {
  return {
    data,
    error: undefined,
    request: new Request("https://grundet.test"),
    response: new Response(),
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("WorkItemComposer – assignee (WP1)", () => {
  beforeEach(() => {
    mockedCreateWorkItem.mockResolvedValue(
      makeOkResponse(fakeCreatedItem({ workItemId: "new-wi" })) as Awaited<
        ReturnType<typeof createWorkItem>
      >
    );
    mockedUpdateProjectWorkItem.mockResolvedValue(
      makeOkResponse(BASE_WORK_ITEM) as Awaited<ReturnType<typeof updateProjectWorkItem>>
    );
  });

  it("shows the project context header without a feature (WP4)", () => {
    render(
      <WorkItemComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const header = screen.getByText(/creating in/i).closest("div");
    expect(header).toHaveTextContent("Core");
    // Work items are project-scoped — no feature separator in the header.
    expect(header).not.toHaveTextContent("/");
  });

  it("create: sends assignedToUserId null when no assignee is selected", async () => {
    const user = userEvent.setup();

    render(
      <WorkItemComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await user.type(screen.getByRole("textbox", { name: /title/i }), "My work item");
    await user.click(screen.getByRole("button", { name: /create work item/i }));

    await waitFor(() => {
      expect(mockedCreateWorkItem).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ assignedToUserId: null }),
        })
      );
    });
  });

  it("create: links a selected intent and sends requirementIds", async () => {
    const user = userEvent.setup();

    render(
      <WorkItemComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await user.type(screen.getByRole("textbox", { name: /title/i }), "My work item");

    // Reveal the linked-intent field, search, and pick the only result.
    await user.click(screen.getByRole("button", { name: /linked intent/i }));
    await user.click(screen.getByRole("combobox"));
    await user.click(await screen.findByRole("option", { name: /IN-1/ }));

    await user.click(screen.getByRole("button", { name: /create work item/i }));

    await waitFor(() => {
      expect(mockedCreateWorkItem).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ requirementIds: ["req-1"] }),
        })
      );
    });
  });

  it("create: sends requirementIds null when no intent is linked", async () => {
    const user = userEvent.setup();

    render(
      <WorkItemComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await user.type(screen.getByRole("textbox", { name: /title/i }), "My work item");
    await user.click(screen.getByRole("button", { name: /create work item/i }));

    await waitFor(() => {
      expect(mockedCreateWorkItem).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ requirementIds: null }),
        })
      );
    });
  });

  it("edit: seeds assignedToUserId from the entity and shows assignee display name", () => {
    const entity: WorkItemResponse = { ...BASE_WORK_ITEM, assignedToUserId: "user-alice" };

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={entity}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // The assignee field is always revealed in edit mode and must show Alice's name.
    expect(screen.getByText("Alice Smith")).toBeInTheDocument();
  });

  it("edit: sends assignedToUserId when saving", async () => {
    const user = userEvent.setup();
    const entity: WorkItemResponse = { ...BASE_WORK_ITEM, assignedToUserId: "user-alice" };

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={entity}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Make the form dirty by changing the title, then save.
    const titleInput = screen.getByRole("textbox", { name: /title/i });
    await user.clear(titleInput);
    await user.type(titleInput, "Updated title");
    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedUpdateProjectWorkItem).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ assignedToUserId: "user-alice" }),
        })
      );
    });
  });

  it("edit: sends assignedToUserId null when entity has no assignee", async () => {
    const user = userEvent.setup();

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={BASE_WORK_ITEM}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const titleInput = screen.getByRole("textbox", { name: /title/i });
    await user.clear(titleInput);
    await user.type(titleInput, "Updated title");
    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedUpdateProjectWorkItem).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ assignedToUserId: null }),
        })
      );
    });
  });

  it("edit: falls back to email when member has no displayName", () => {
    const entity: WorkItemResponse = { ...BASE_WORK_ITEM, assignedToUserId: "user-carol" };

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={entity}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Carol has no displayName — email is the fallback.
    expect(screen.getByText("carol@example.com")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Edit-mode progressive disclosure (app-convenience Item 1)
// ---------------------------------------------------------------------------

describe("WorkItemComposer – edit-mode progressive disclosure", () => {
  beforeEach(() => {
    mockedUpdateProjectWorkItem.mockResolvedValue(
      makeOkResponse(BASE_WORK_ITEM) as Awaited<ReturnType<typeof updateProjectWorkItem>>
    );
  });

  it("hides empty optional fields behind the adder, but keeps the version reason always visible", () => {
    // BASE_WORK_ITEM holds only defaults: Task / Medium / Todo, no labels, description, blocked
    // reason or assignee.
    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={BASE_WORK_ITEM}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // Default-valued optionals are collapsed.
    expect(screen.queryByLabelText(/^type$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^priority$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^status$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^labels$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^description$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^blocked reason$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^assignee$/i)).not.toBeInTheDocument();

    // The version-change reason stays visible — a strong nudge to record why a version is saved.
    expect(screen.getByLabelText(/reason for this change/i)).toBeInTheDocument();
    // …and it is never offered by the adder in edit mode (only its always-visible field exists).
    expect(screen.queryByRole("button", { name: /^reason$/i })).not.toBeInTheDocument();

    // The remaining optionals are offered by the adder.
    expect(screen.getByRole("button", { name: /^type$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^description$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^blocked reason$/i })).toBeInTheDocument();
  });

  it("reveals optional fields that hold non-default values, hiding their adder buttons", () => {
    const entity: WorkItemResponse = {
      ...BASE_WORK_ITEM,
      type: "Bug",
      priority: "High",
      status: "InProgress",
      labels: ["api"],
      description: "Investigate the 500 on submit.",
      blockedReason: "Waiting on infra.",
      assignedToUserId: "user-alice",
    };

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={entity}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/^type$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^priority$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^status$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^labels$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^description$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^blocked reason$/i)).toBeInTheDocument();

    // All optionals revealed and reason is always-visible, so the adder offers nothing.
    expect(screen.queryByRole("button", { name: /^type$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^description$/i })).not.toBeInTheDocument();
  });

  it("disables the reason field until a real change is made, then enables it", async () => {
    const user = userEvent.setup();

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={BASE_WORK_ITEM}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const reasonField = screen.getByLabelText(/reason for this change/i);
    expect(reasonField).toBeDisabled();

    const titleInput = screen.getByRole("textbox", { name: /title/i });
    await user.clear(titleInput);
    await user.type(titleInput, "Updated title");

    expect(reasonField).toBeEnabled();
  });

  it("keeps Save disabled when only the reason is dirty after a reverted change", async () => {
    const user = userEvent.setup();

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={BASE_WORK_ITEM}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const saveButton = screen.getByRole("button", { name: /save new version/i });
    expect(saveButton).toBeDisabled();

    // Change a field so the reason field enables, type a reason, then revert the field.
    const titleInput = screen.getByRole("textbox", { name: /title/i });
    await user.clear(titleInput);
    await user.type(titleInput, "Updated title");
    await user.type(screen.getByLabelText(/reason for this change/i), "Because reasons");
    await user.clear(titleInput);
    await user.type(titleInput, BASE_WORK_ITEM.title);

    // Only the reason is now dirty — no real change remains, so Save must stay disabled.
    expect(saveButton).toBeDisabled();
  });

  it("does not show the version reason field in create mode until added", () => {
    render(
      <WorkItemComposer
        mode="create"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        surface="page"
        onCancel={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    // In create the reason is a normal adder-gated optional — hidden until revealed.
    expect(screen.queryByLabelText(/^reason$/i)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^reason$/i })).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Attachment tests — kept last so the per-test restored-draft override can't leak.
// ---------------------------------------------------------------------------

describe("WorkItemComposer – image attachments", () => {
  beforeAll(() => {
    // jsdom has no object-URL implementation; the thumbnail renderer needs it.
    globalThis.URL.createObjectURL = vi.fn(() => "blob:mock");
    globalThis.URL.revokeObjectURL = vi.fn();
  });

  beforeEach(() => {
    vi.clearAllMocks();
    mockedUseDraftAutosave.mockReturnValue(autosaveState(false));
    mockedUpdateProjectWorkItem.mockResolvedValue(
      makeOkResponse(BASE_WORK_ITEM) as Awaited<ReturnType<typeof updateProjectWorkItem>>
    );
  });

  it("restores the original attachments on discard so a removed image is not dropped on save", async () => {
    const user = userEvent.setup();
    // A restored draft surfaces the Discard control.
    mockedUseDraftAutosave.mockReturnValue(autosaveState(true));

    render(
      <WorkItemComposer
        mode="edit"
        projectId="proj-1"
        projectName="Core"
        teamMembers={TEAM_MEMBERS}
        initial={BASE_WORK_ITEM}
        initialAttachments={[
          {
            id: "att-1",
            originalFileName: "evidence.png",
            contentType: "image/jpeg",
            width: 100,
            height: 80,
          },
        ]}
        attachmentsLoaded
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
    const titleInput = screen.getByRole("textbox", { name: /title/i });
    await user.clear(titleInput);
    await user.type(titleInput, "Updated title");

    await user.click(screen.getByRole("button", { name: /save new version/i }));

    await waitFor(() => {
      expect(mockedUpdateProjectWorkItem).toHaveBeenCalledWith(
        expect.objectContaining({
          // Discard must restore att-1 — without the reset this would save [].
          body: expect.objectContaining({ attachmentIds: ["att-1"] }),
        })
      );
    });
  });
});
