namespace Grundet.Api.Data.Entities;

public sealed class WorkItemLink
{
    public Guid Id { get; set; }
    public Guid WorkItemLinkId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid RequirementId { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedByUserId { get; set; }
    public WorkItemLinkChange ChangeType { get; set; }
}
