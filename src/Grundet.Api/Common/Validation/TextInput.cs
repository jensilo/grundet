using Grundet.Api.Common.Errors;

namespace Grundet.Api.Common.Validation;

/// <summary>
/// Trims and length-checks stored free-text fields. The caps are deliberately generous — they
/// stop runaway input (accidental paste, abuse) without constraining real prose. See ADR-007.
/// Limits are enforced here as the source of truth and mirrored by the frontend `maxLength`
/// attributes for immediate feedback.
/// </summary>
public static class TextInput
{
    public const int NameMaxLength = 200;
    public const int RequirementTextMaxLength = 1000;
    public const int DescriptionMaxLength = 4000;
    public const int AcceptanceCriterionMaxLength = 500;
    public const int ReasonMaxLength = 500;
    public const int LabelMaxLength = 50;
    public const int MaxLabelCount = 20;

    /// <summary>
    /// Trims, drops blanks, and case-insensitively deduplicates labels, then enforces the
    /// per-label length cap and the maximum label count (see ADR-007). Shared by the Requirement
    /// and WorkItem write paths so the caps are applied identically. Order is preserved.
    /// </summary>
    public static string[] NormalizeLabels(IEnumerable<string>? labels)
    {
        string[] normalized = labels?
            .Select(label => label.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (normalized.Length > MaxLabelCount)
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"A maximum of {MaxLabelCount} labels is allowed.");
        }

        foreach (string label in normalized)
        {
            if (label.Length > LabelMaxLength)
            {
                throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    $"Each label must be {LabelMaxLength} characters or fewer.");
            }
        }

        return normalized;
    }

    /// <summary>Trims a required field, rejecting empty or over-length input.</summary>
    public static string Required(string? value, string fieldName, int maxLength)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, $"{fieldName} is required.");
        }

        EnsureWithinLimit(trimmed, fieldName, maxLength);
        return trimmed;
    }

    /// <summary>Trims an optional field to null when blank, rejecting over-length input.</summary>
    public static string? Optional(string? value, string fieldName, int maxLength)
    {
        string? trimmed = value?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        EnsureWithinLimit(trimmed, fieldName, maxLength);
        return trimmed;
    }

    private static void EnsureWithinLimit(string value, string fieldName, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"{fieldName} must be {maxLength} characters or fewer.");
        }
    }
}
