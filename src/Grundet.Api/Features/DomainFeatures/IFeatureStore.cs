using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.DomainFeatures;

public interface IFeatureStore
{
    Task AddFeatureAsync(Feature feature, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Feature> Items, int Total)> ListPageByProjectAsync(
        Guid projectId,
        int page,
        int pageSize,
        Guid? lastSeenId,
        string? query,
        CancellationToken cancellationToken = default);

    Task<Feature?> GetByProjectAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken = default);

    Task<Feature?> GetByProjectForUpdateAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task SoftDeleteFeatureAsync(Guid projectId, Guid featureId, Guid changedByUserId, DateTime utcNow, CancellationToken cancellationToken = default);
}
