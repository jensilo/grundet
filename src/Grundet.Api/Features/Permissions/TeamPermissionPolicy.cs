using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Billing;

namespace Grundet.Api.Features.Permissions;

/// <summary>
/// Pure, testable mapping from <c>(team role, billing plan)</c> to a granted permission set.
/// This is the single source of truth that replaces the scattered client-side gating.
///
/// The policy mirrors the real enforcement predicates so a UI hint can never drift from the
/// 403/409 it predicts: it reuses the canonical <see cref="TeamPlanExtensions"/> predicates and
/// does not recompute or redefine the "solo" set. Permissions are a UI hint only — backend
/// enforcement remains the security boundary.
/// </summary>
public static class TeamPermissionPolicy
{
    public static Permission[] Evaluate(TeamRole role, TeamPlan plan)
    {
        if (role != TeamRole.Lead)
        {
            // Contributors have no team-management capabilities on any plan.
            return [];
        }

        var permissions = new List<Permission>
        {
            // Rename — Lead on any plan (TeamService.RenameTeamAsync has no plan gate).
            Permission.TeamManageSettings,
        };

        if (!plan.IsSoloPlan())
        {
            // Real gate: TeamService.DeleteTeamAsync → 409 (QuotaExceeded) on solo plans.
            permissions.Add(Permission.TeamDelete);
        }

        if (plan.AllowsCollaboration())
        {
            // Real gate: TeamQuotaService.EnsureCanInviteMemberAsync → 409 (QuotaExceeded) unless the plan allows collaboration.
            permissions.Add(Permission.TeamInviteMembers);

            // Change role / remove member (and resend/revoke invites). Gated WITH collaboration,
            // NOT on Lead alone: a solo Lead must not see member-management UI (preserves today's
            // behavior where members-panel computes canManageMembers = isLead && canInviteMembers).
            permissions.Add(Permission.TeamManageMembers);
        }

        return [.. permissions];
    }
}
