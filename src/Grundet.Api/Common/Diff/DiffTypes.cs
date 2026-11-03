namespace Grundet.Api.Common.Diff;

public sealed record TextDiff(string Before, string After);

public sealed record LabelsDiff(string[] Added, string[] Removed);

// Added/removed attachment ids between two versions. The frontend renders thumbnails for each.
public sealed record AttachmentsDiff(Guid[] Added, Guid[] Removed);

public sealed record AcceptanceCriteriaDiff(
    DiffCriteriaSummary Summary,
    CriterionChange[] Changes);

public sealed record DiffCriteriaSummary(int Added, int Removed, int Edited, int Moved);

public sealed record CriterionChange(
    string Type,
    int? OldIndex,
    int? NewIndex,
    string? Before,
    string? After);

public sealed record SimpleFieldDiff(string Before, string After);
