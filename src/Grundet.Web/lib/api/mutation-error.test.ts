import { describe, expect, it } from "vitest";

import type { ProblemDetails } from "@/lib/api/generated/types.gen";

import { mutationErrorMessage } from "./mutation-error";

const FALLBACK = "Could not save. Please try again.";

describe("mutationErrorMessage", () => {
  it("surfaces the backend detail for validation errors", () => {
    const error: ProblemDetails = {
      type: "https://grundet.app/errors/validation",
      detail: "assignedToUserId must be a member of the project team.",
    };

    expect(mutationErrorMessage(error, FALLBACK)).toBe(
      "assignedToUserId must be a member of the project team."
    );
  });

  it("surfaces the backend detail for quota errors", () => {
    const error: ProblemDetails = {
      type: "https://grundet.app/errors/quota-exceeded",
      detail: "Work item limit reached for this workspace plan.",
    };

    expect(mutationErrorMessage(error, FALLBACK)).toBe(
      "Work item limit reached for this workspace plan."
    );
  });

  it("surfaces the backend detail for conflict errors", () => {
    const error: ProblemDetails = {
      type: "https://grundet.app/errors/conflict",
      detail: "Deleted work items cannot be modified.",
    };

    expect(mutationErrorMessage(error, FALLBACK)).toBe("Deleted work items cannot be modified.");
  });

  it("surfaces the backend detail for a reason-requires-change error", () => {
    const error: ProblemDetails = {
      type: "https://grundet.app/errors/reason-requires-change",
      detail:
        "Make a change before adding a reason – a reason describes what changed in this version.",
    };

    expect(mutationErrorMessage(error, FALLBACK)).toBe(
      "Make a change before adding a reason – a reason describes what changed in this version."
    );
  });

  it("uses a specific message for forbidden errors without a detail", () => {
    const error: ProblemDetails = { type: "https://grundet.app/errors/forbidden" };

    expect(mutationErrorMessage(error, FALLBACK)).toBe("You do not have permission to do this.");
  });

  it("falls back to retry copy for unknown or transient errors", () => {
    expect(mutationErrorMessage(undefined, FALLBACK)).toBe(FALLBACK);
    expect(
      mutationErrorMessage({ type: "https://grundet.app/errors/too-many-requests" }, FALLBACK)
    ).toBe(FALLBACK);
  });
});
