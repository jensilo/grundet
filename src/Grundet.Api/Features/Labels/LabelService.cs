using Grundet.Api.Common.Auth;

namespace Grundet.Api.Features.Labels;

public sealed class LabelService(ILabelStore labelStore)
{
    public async Task<IReadOnlyList<LabelSuggestion>> GetProjectLabelsAsync(
        AuthorizedProjectId projectId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        return await labelStore.GetProjectLabelsAsync(projectId.Value, cancellationToken);
    }
}
