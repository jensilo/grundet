namespace Grundet.Api.Data.Entities;

public sealed class TeamUsageSnapshot
{
    public Guid TeamId { get; set; }
    public TeamPlan Plan { get; set; }
    public int ProjectCount { get; set; }
    public int MemberCount { get; set; }
    public int RequirementCount { get; set; }
    public int WorkItemCount { get; set; }
    public RequirementWorkItemLimitStatus RequirementWorkItemStatus { get; set; }
    public DateTime? OverLimitSince { get; set; }
    public DateTime? EnforcementStartsAt { get; set; }
    public DateTime CalculatedAt { get; set; }
}
