using Grundet.Api.Data.Entities;

namespace Grundet.Api.Data.Views;

public sealed class CurrentWorkItemLink
{
    public Guid Id { get; set; }
    public Guid WorkItemLinkId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid RequirementId { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedByUserId { get; set; }
    public WorkItemLinkChange ChangeType { get; set; }
}
