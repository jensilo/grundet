using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Billing;

public static class TeamPlanExtensions
{
    public static bool IsSoloPlan(this TeamPlan plan)
        => plan is TeamPlan.Solo or TeamPlan.Power or TeamPlan.Expert;

    public static bool IsTeamPlan(this TeamPlan plan)
        => plan is TeamPlan.TeamS or TeamPlan.TeamM or TeamPlan.TeamL or TeamPlan.TeamAlpha or TeamPlan.TeamUnlimited;

    public static bool IsAdminGrantedPlan(this TeamPlan plan)
        => plan is TeamPlan.TeamAlpha or TeamPlan.TeamUnlimited;

    public static bool AllowsCollaboration(this TeamPlan plan)
        => plan.IsTeamPlan();
}
