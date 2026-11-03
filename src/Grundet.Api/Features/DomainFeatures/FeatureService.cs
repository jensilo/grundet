using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Validation;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.DomainFeatures;

public sealed class FeatureService(IFeatureStore featureStore)
{
    public async Task<FeatureResponse> CreateFeatureAsync(
        AuthorizedProjectId projectId,
        CreateFeatureCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        var feature = new Feature
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId.Value,
            Name = TextInput.Required(command.Name, "Feature name", TextInput.NameMaxLength),
            Description = TextInput.Optional(command.Description, "Feature description", TextInput.DescriptionMaxLength),
            CreatedAt = DateTime.UtcNow
        };

        await featureStore.AddFeatureAsync(feature, cancellationToken);
        return Map(feature);
    }

    public async Task<PagedResponse<FeatureResponse>> ListFeaturesAsync(
        AuthorizedProjectId projectId,
        int page,
        int pageSize,
        Guid? lastSeenId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        var (features, total) = await featureStore.ListPageByProjectAsync(
            projectId.Value,
            page,
            pageSize,
            lastSeenId,
            query,
            cancellationToken);
        var items = features.Select(Map).ToList();
        Guid? responseLastSeenId = items.Count == 0 ? null : items[^1].Id;
        return PagedResponse.Create(items, page, pageSize, total, responseLastSeenId);
    }

    public async Task<FeatureResponse> GetFeatureAsync(
        AuthorizedProjectId projectId,
        AuthorizedFeatureId featureId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        featureId.ThrowIfDefault();

        var feature = await featureStore.GetByProjectAsync(projectId.Value, featureId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Feature '{featureId.Value}' was not found in project '{projectId.Value}'.");

        return Map(feature);
    }

    public async Task<FeatureResponse> UpdateFeatureAsync(
        AuthorizedProjectId projectId,
        AuthorizedFeatureId featureId,
        UpdateFeatureCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        featureId.ThrowIfDefault();

        var feature = await featureStore.GetByProjectForUpdateAsync(projectId.Value, featureId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Feature '{featureId.Value}' was not found in project '{projectId.Value}'.");

        if (command.Name is not null)
        {
            feature.Name = TextInput.Required(command.Name, "Feature name", TextInput.NameMaxLength);
        }

        if (command.DescriptionWasProvided)
        {
            feature.Description = TextInput.Optional(
                command.Description, "Feature description", TextInput.DescriptionMaxLength);
        }

        await featureStore.SaveChangesAsync(cancellationToken);
        return Map(feature);
    }

    public async Task DeleteFeatureAsync(
        AuthorizedProjectId projectId,
        AuthorizedFeatureId featureId,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        featureId.ThrowIfDefault();

        _ = await featureStore.GetByProjectAsync(projectId.Value, featureId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Feature '{featureId.Value}' was not found in project '{projectId.Value}'.");

        await featureStore.SoftDeleteFeatureAsync(projectId.Value, featureId.Value, changedByUserId, DateTime.UtcNow, cancellationToken);
    }

    private static FeatureResponse Map(Feature feature)
        => new(feature.Id, feature.ProjectId, feature.Name, feature.Description, feature.CreatedAt);

}

public sealed record CreateFeatureCommand(string Name, string? Description);

public sealed record UpdateFeatureCommand(string? Name, string? Description, bool DescriptionWasProvided);

public sealed record FeatureResponse(Guid Id, Guid ProjectId, string Name, string? Description, DateTime CreatedAt);
