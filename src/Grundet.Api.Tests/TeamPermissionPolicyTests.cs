using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Permissions;
using Xunit;

namespace Grundet.Api.Tests;

/// <summary>
/// Pure unit tests for the full role × plan matrix. No Docker / DB required.
/// Asserts the policy mirrors the real enforcement gates, including the solo-Lead row where
/// TeamManageMembers / TeamInviteMembers / TeamDelete must be ABSENT.
/// </summary>
public sealed class TeamPermissionPolicyTests
{
    public static TheoryData<TeamRole, TeamPlan, Permission[]> Matrix()
    {
        var data = new TheoryData<TeamRole, TeamPlan, Permission[]>();

        // Lead on solo plans: rename only. The must-fix row — no member-management / invite / delete.
        foreach (var plan in new[] { TeamPlan.Solo, TeamPlan.Power, TeamPlan.Expert })
        {
            data.Add(TeamRole.Lead, plan, [Permission.TeamManageSettings]);
        }

        // Lead on collaborative plans: full set.
        foreach (var plan in new[] { TeamPlan.TeamS, TeamPlan.TeamM, TeamPlan.TeamL, TeamPlan.TeamAlpha, TeamPlan.TeamUnlimited })
        {
            data.Add(
                TeamRole.Lead,
                plan,
                [
                    Permission.TeamManageSettings,
                    Permission.TeamDelete,
                    Permission.TeamInviteMembers,
                    Permission.TeamManageMembers,
                ]);
        }

        // Contributors: empty on every plan.
        foreach (var plan in Enum.GetValues<TeamPlan>())
        {
            data.Add(TeamRole.Contributor, plan, []);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Evaluate_ReturnsExpectedPermissions(TeamRole role, TeamPlan plan, Permission[] expected)
    {
        var actual = TeamPermissionPolicy.Evaluate(role, plan);

        Assert.Equal(expected.OrderBy(p => p), actual.OrderBy(p => p));
    }

    [Fact]
    public void Evaluate_SoloLead_DoesNotGrantMemberManagementInviteOrDelete()
    {
        var permissions = TeamPermissionPolicy.Evaluate(TeamRole.Lead, TeamPlan.Solo);

        Assert.Contains(Permission.TeamManageSettings, permissions);
        Assert.DoesNotContain(Permission.TeamManageMembers, permissions);
        Assert.DoesNotContain(Permission.TeamInviteMembers, permissions);
        Assert.DoesNotContain(Permission.TeamDelete, permissions);
    }

    [Fact]
    public void Evaluate_CollaborativeLead_GrantsFullSet()
    {
        var permissions = TeamPermissionPolicy.Evaluate(TeamRole.Lead, TeamPlan.TeamS);

        Assert.Contains(Permission.TeamManageSettings, permissions);
        Assert.Contains(Permission.TeamDelete, permissions);
        Assert.Contains(Permission.TeamInviteMembers, permissions);
        Assert.Contains(Permission.TeamManageMembers, permissions);
    }
}
