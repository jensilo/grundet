using System.Text.Json;

namespace Grundet.Api.Data.Entities;

public enum DraftKind
{
    Requirement = 1,
    WorkItem = 2
}

// User-private, in-progress form state for a create/edit composer.
// Generalizes over both Requirement (feature-scoped) and WorkItem (project-scoped) drafts.
// DraftContent is opaque jsonb owned by the frontend; the backend never validates its shape.
public sealed class Draft
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DraftKind Kind { get; set; }

    // FeatureId for Requirement drafts, ProjectId for WorkItem drafts.
    public Guid ScopeId { get; set; }

    // RequirementId / WorkItemId for edit drafts; null for a new (create) draft.
    public Guid? EntityId { get; set; }

    public required JsonDocument DraftContent { get; set; }
    public DateTime UpdatedAt { get; set; }
}
