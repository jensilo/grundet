import type { ProblemDetails } from "@/lib/api/generated/types.gen";

// Canonical backend ProblemTypes (see src/Grundet.Api/Common/Errors/ProblemTypes.cs).
// Mapping on the stable `type` URI — not HTTP status or incidental text — keeps this
// resilient to copy changes on either side.
export const VALIDATION = "https://grundet.app/errors/validation";
export const FORBIDDEN = "https://grundet.app/errors/forbidden";
export const CONFLICT = "https://grundet.app/errors/conflict";
export const NOT_FOUND = "https://grundet.app/errors/not-found";
export const QUOTA_EXCEEDED = "https://grundet.app/errors/quota-exceeded";
export const REASON_REQUIRES_CHANGE = "https://grundet.app/errors/reason-requires-change";
export const SOLO_WORKSPACE_REQUIRED = "https://grundet.app/errors/solo-workspace-required";

/**
 * Maps a backend {@link ProblemDetails} to user-facing copy. Backend `detail` for
 * validation, quota, conflict, and not-found errors is authored to be user-safe and
 * actionable, so it is surfaced directly rather than hidden behind generic retry copy.
 *
 * The `fallback` is used only for unknown or likely-transient failures (network/5xx),
 * where "please try again" is the honest message. Never pass generic retry copy for a
 * non-retryable state — that is exactly what this helper exists to avoid.
 */
export function mutationErrorMessage(error: ProblemDetails | undefined, fallback: string): string {
  switch (error?.type) {
    case VALIDATION:
    case QUOTA_EXCEEDED:
    case CONFLICT:
    case NOT_FOUND:
    case REASON_REQUIRES_CHANGE:
      return error.detail ?? fallback;
    case FORBIDDEN:
      return error.detail ?? "You do not have permission to do this.";
    default:
      return fallback;
  }
}
