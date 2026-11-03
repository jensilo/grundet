namespace Grundet.Api.Features.Labels;

public interface ILabelStore
{
    Task<IReadOnlyList<LabelSuggestion>> GetProjectLabelsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}

// Value is the most common original casing; Count is the number of distinct current
// entities (requirements + work items) carrying the label.
public sealed record LabelSuggestion(string Value, int Count);
