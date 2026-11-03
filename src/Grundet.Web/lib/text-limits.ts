// Mirrors backend Grundet.Api.Common.Validation.TextInput (see docs/adr/007-input-length-limits.md).
// Server-side validation is the source of truth; these drive the input `maxLength` attributes for
// immediate feedback so a length rejection never comes as a surprise on submit.
export const TEXT_LIMITS = {
  name: 200,
  requirementText: 1000,
  description: 4000,
  acceptanceCriterion: 500,
  reason: 500,
  feedback: 4000,
  labels: {
    perLabelMaxLength: 50,
    maxCount: 20,
  },
} as const;
