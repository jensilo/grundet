using System.Text;

namespace Grundet.Api.Common.Diff;

public static class FieldDiff
{
    private const string ChangeAdded = "added";
    private const string ChangeRemoved = "removed";
    private const string ChangeEdited = "edited";
    private const string ChangeMoved = "moved";

    public static TextDiff? ForText(string? before, string? after) => string.Equals(before, after, StringComparison.Ordinal) ? null : new TextDiff(before ?? string.Empty, after ?? string.Empty);

    public static TextDiff? ForNullableText(string? before, string? after)
        => ForText(before, after);

    public static LabelsDiff? ForLabels(string[] before, string[] after)
    {
        before ??= [];
        after ??= [];

        string[] added = after
            .Except(before, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] removed = before
            .Except(after, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return added.Length == 0 && removed.Length == 0
            ? null
            : new LabelsDiff(added, removed);
    }

    public static AttachmentsDiff? ForAttachments(Guid[] before, Guid[] after)
    {
        before ??= [];
        after ??= [];

        var beforeSet = before.ToHashSet();
        var afterSet = after.ToHashSet();
        Guid[] added = after.Where(id => !beforeSet.Contains(id)).ToArray();
        Guid[] removed = before.Where(id => !afterSet.Contains(id)).ToArray();

        return added.Length == 0 && removed.Length == 0
            ? null
            : new AttachmentsDiff(added, removed);
    }

    public static AcceptanceCriteriaDiff? ForCriteria(string[] before, string[] after)
    {
        before ??= [];
        after ??= [];

        var beforeLines = before
            .Select((value, index) => new CriterionLine(index, value, Normalize(value), Tokenize(value)))
            .ToList();
        var afterLines = after
            .Select((value, index) => new CriterionLine(index, value, Normalize(value), Tokenize(value)))
            .ToList();

        var matchedBefore = new HashSet<int>();
        var matchedAfter = new HashSet<int>();
        var changes = new List<CriterionChange>();
        var afterByNormalized = BuildAfterIndexByNormalized(afterLines);

        foreach (var beforeLine in beforeLines)
        {
            if (!afterByNormalized.TryGetValue(beforeLine.Normalized, out var queue) || queue.Count == 0)
            {
                continue;
            }

            int afterIndex = queue.Dequeue();
            matchedBefore.Add(beforeLine.Index);
            matchedAfter.Add(afterIndex);

            if (beforeLine.Index != afterIndex)
            {
                changes.Add(new CriterionChange(
                    ChangeMoved,
                    beforeLine.Index,
                    afterIndex,
                    beforeLine.Original,
                    afterLines[afterIndex].Original));
            }
        }

        var unmatchedBefore = beforeLines
            .Where(line => !matchedBefore.Contains(line.Index))
            .ToList();
        var unmatchedAfter = afterLines
            .Where(line => !matchedAfter.Contains(line.Index))
            .ToList();

        var editedPairs = FindEditedPairs(unmatchedBefore, unmatchedAfter);
        foreach (var pair in editedPairs)
        {
            var beforeLine = unmatchedBefore.Single(line => line.Index == pair.BeforeIndex);
            var afterLine = unmatchedAfter.Single(line => line.Index == pair.AfterIndex);

            matchedBefore.Add(beforeLine.Index);
            matchedAfter.Add(afterLine.Index);
            changes.Add(new CriterionChange(
                ChangeEdited,
                beforeLine.Index,
                afterLine.Index,
                beforeLine.Original,
                afterLine.Original));
        }

        foreach (var removedLine in beforeLines.Where(line => !matchedBefore.Contains(line.Index)))
        {
            changes.Add(new CriterionChange(
                ChangeRemoved,
                removedLine.Index,
                null,
                removedLine.Original,
                null));
        }

        foreach (var addedLine in afterLines.Where(line => !matchedAfter.Contains(line.Index)))
        {
            changes.Add(new CriterionChange(
                ChangeAdded,
                null,
                addedLine.Index,
                null,
                addedLine.Original));
        }

        if (changes.Count == 0)
        {
            return null;
        }

        var summary = new DiffCriteriaSummary(
            Added: changes.Count(change => change.Type == ChangeAdded),
            Removed: changes.Count(change => change.Type == ChangeRemoved),
            Edited: changes.Count(change => change.Type == ChangeEdited),
            Moved: changes.Count(change => change.Type == ChangeMoved));

        return new AcceptanceCriteriaDiff(summary, changes.ToArray());
    }

    public static SimpleFieldDiff? ForEnum<T>(T before, T after)
        where T : struct, Enum
        => EqualityComparer<T>.Default.Equals(before, after)
            ? null
            : new SimpleFieldDiff(before.ToString(), after.ToString());

    private static Dictionary<string, Queue<int>> BuildAfterIndexByNormalized(List<CriterionLine> afterLines)
    {
        var map = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

        foreach (var afterLine in afterLines)
        {
            if (!map.TryGetValue(afterLine.Normalized, out var queue))
            {
                queue = new Queue<int>();
                map[afterLine.Normalized] = queue;
            }

            queue.Enqueue(afterLine.Index);
        }

        return map;
    }

    private static List<EditedPair> FindEditedPairs(
        IReadOnlyList<CriterionLine> beforeLines,
        IReadOnlyList<CriterionLine> afterLines)
    {
        var candidates = beforeLines
            .Select(beforeLine =>
            {
                var matches = afterLines
                    .Select(afterLine => new EditedCandidate(
                        beforeLine.Index,
                        afterLine.Index,
                        ComputeTokenOverlap(beforeLine.Tokens, afterLine.Tokens)))
                    .Where(candidate => candidate.Score > 0.6)
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.AfterIndex)
                    .ToList();

                return new EditedCandidateSet(beforeLine.Index, matches);
            })
            .Where(candidateSet => candidateSet.Candidates.Count == 1)
            .Select(candidateSet => candidateSet.Candidates[0])
            .ToList();

        return candidates
            .GroupBy(candidate => candidate.AfterIndex)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .OrderBy(candidate => candidate.BeforeIndex)
            .Select(candidate => new EditedPair(candidate.BeforeIndex, candidate.AfterIndex))
            .ToList();
    }

    private static double ComputeTokenOverlap(string[] beforeTokens, string[] afterTokens)
    {
        if (beforeTokens.Length == 0 || afterTokens.Length == 0)
        {
            return 0;
        }

        var beforeSet = beforeTokens
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var afterSet = afterTokens
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        int sharedCount = beforeSet.Intersect(afterSet, StringComparer.Ordinal).Count();

        return beforeSet.Count == 0
            ? 0
            : sharedCount / (double)beforeSet.Count;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        bool previousWasWhitespace = false;

        foreach (char character in trimmed)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    private static string[] Tokenize(string? value)
    {
        string normalized = Normalize(value);
        return normalized.Length == 0
            ? []
            : normalized.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed record CriterionLine(int Index, string Original, string Normalized, string[] Tokens);

    private sealed record EditedCandidate(int BeforeIndex, int AfterIndex, double Score);

    private sealed record EditedCandidateSet(int BeforeIndex, List<EditedCandidate> Candidates);

    private sealed record EditedPair(int BeforeIndex, int AfterIndex);
}
