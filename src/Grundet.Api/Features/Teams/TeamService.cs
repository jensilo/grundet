using System.Security.Cryptography;
using System.Text;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Email;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.AlphaWaitlist;
using Grundet.Api.Features.Billing;

namespace Grundet.Api.Features.Teams;

public sealed class TeamService(ITeamStore teamStore, IEmailService emailService, IConfiguration configuration, ITeamQuotaService teamQuotaService, AlphaWaitlistService alphaWaitlistService)
{
    private readonly int _inviteExpiryDays = Math.Max(1, configuration.GetValue<int?>("Invites:ExpiryDays") ?? 14);
    private readonly string? _appBaseUrl = configuration.GetValue<string>("MagicLink:PublicAppBaseUrl")?.TrimEnd('/');
    private readonly bool _createTeamAlphaWorkspaces = configuration.GetValue<bool?>("Billing:CreateTeamAlphaWorkspaces") ?? false;

    public async Task<TeamResponse> CreateTeamAsync(CreateTeamCommand command, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        string name = NormalizeName(command.Name, "Team name");
        var plan = GetDefaultCreatedTeamPlan();
        if (plan.IsSoloPlan() && await teamStore.HasSoloWorkspaceAsync(currentUserId, cancellationToken))
        {
            throw HttpProblemException.Conflict(
                ProblemTypes.SoloWorkspaceAlreadyExists,
                "You already have a personal workspace.");
        }

        var now = DateTime.UtcNow;
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            Plan = plan,
            CreatedAt = now
        };
        var leadMember = new TeamMember
        {
            TeamId = team.Id,
            UserId = currentUserId,
            Role = TeamRole.Lead
        };

        await teamStore.AddTeamWithLeadAsync(team, leadMember, cancellationToken);
        return new TeamResponse(team.Id, team.Name, team.CreatedAt);
    }

    public async Task<List<TeamListItemResponse>> ListTeamsAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var teams = await teamStore.ListTeamsForUserAsync(currentUserId, cancellationToken);
        return teams
            .Select(team => new TeamListItemResponse(team.Id, team.Name, team.CreatedAt, team.Role))
            .ToList();
    }

    public async Task<TeamResponse> GetTeamAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var team = await teamStore.GetTeamAsync(validatedTeamId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{validatedTeamId}' was not found.");

        return new TeamResponse(team.Id, team.Name, team.CreatedAt);
    }

    public async Task<TeamResponse> RenameTeamAsync(
        AuthorizedTeamId teamId,
        RenameTeamCommand command,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        string name = NormalizeName(command.Name, "Team name");
        var team = await teamStore.GetTeamForUpdateAsync(validatedTeamId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{validatedTeamId}' was not found.");

        team.Name = name;
        await teamStore.SaveChangesAsync(cancellationToken);
        return new TeamResponse(team.Id, team.Name, team.CreatedAt);
    }

    public async Task DeleteTeamAsync(
        AuthorizedTeamId teamId,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedUserId = RequireUserId(changedByUserId, "Changed-by user id");
        var team = await teamStore.GetTeamAsync(validatedTeamId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{validatedTeamId}' was not found.");

        if (team.Plan.IsSoloPlan())
        {
            throw HttpProblemException.Conflict(
                ProblemTypes.SoloWorkspaceRequired,
                "Solo workspaces cannot be deleted. Create a team workspace to collaborate.");
        }

        await teamStore.SoftDeleteTeamAsync(validatedTeamId, validatedUserId, DateTime.UtcNow, cancellationToken);
    }

    public async Task<List<TeamMemberResponse>> ListMembersAsync(
        AuthorizedTeamId teamId,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var members = await teamStore.ListMembersAsync(validatedTeamId, cancellationToken);
        return members
            .Select(member => new TeamMemberResponse(member.UserId, member.Email, member.DisplayName, member.Role))
            .ToList();
    }

    public async Task<TeamInviteResponse> InviteMemberAsync(
        AuthorizedTeamId teamId,
        InviteMemberCommand command,
        Guid invitedByUserId,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedInvitedByUserId = RequireUserId(invitedByUserId, "Invited-by user id");
        string email = EmailNormalizer.Normalize(command.Email);
        string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string tokenHash = ComputeTokenHash(rawToken);
        var now = DateTime.UtcNow;
        var invite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TeamId = validatedTeamId,
            Email = email,
            Role = command.Role,
            TokenHash = tokenHash,
            ExpiresAt = now.AddDays(_inviteExpiryDays),
            CreatedAt = now
        };

        var inviter = await teamStore.GetUserAsync(validatedInvitedByUserId, cancellationToken)
            ?? throw HttpProblemException.Unauthorized("Authenticated user could not be resolved.");
        var team = await teamStore.GetTeamAsync(validatedTeamId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{validatedTeamId}' was not found.");

        await teamStore.ExecuteWithTeamMembershipLockAsync(
            validatedTeamId,
            async ct =>
            {
                await teamQuotaService.EnsureCanInviteMemberAsync(teamId, ct);

                if (await teamStore.GetPendingInviteForEmailAsync(validatedTeamId, email, now, ct) is not null)
                {
                    throw HttpProblemException.Conflict(
                        type: ProblemTypes.Conflict,
                        detail: "A pending invite already exists for this email address. Use resend from the pending invite row.");
                }

                await teamStore.AddInviteAsync(invite, ct);

                string reason = BuildTeamInviteAlphaReason(inviter, team, invite, now);
                await alphaWaitlistService.ApproveTeamInviteEmailAsync(email, reason, now, ct);
            },
            cancellationToken);

        string inviteUrl = BuildInviteUrl(rawToken);
        await SendInviteEmailAsync(email, inviteUrl, team, inviter, cancellationToken);

        return new TeamInviteResponse(invite.Id, invite.TeamId, invite.Email, invite.Role, invite.ExpiresAt, invite.CreatedAt);
    }

    public async Task<List<TeamInviteResponse>> ListPendingInvitesAsync(AuthorizedTeamId teamId, CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var invites = await teamStore.ListPendingInvitesAsync(validatedTeamId, DateTime.UtcNow, cancellationToken);
        return invites.Select(invite => new TeamInviteResponse(invite.Id, invite.TeamId, invite.Email, invite.Role, invite.ExpiresAt, invite.CreatedAt)).ToList();
    }

    public async Task<TeamInviteResponse> ResendInviteAsync(AuthorizedTeamId teamId, Guid inviteId, Guid resentByUserId, CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedInviteId = RequireGuid(inviteId, "Invite id");
        var validatedResentByUserId = RequireUserId(resentByUserId, "Resent-by user id");
        var inviter = await teamStore.GetUserAsync(validatedResentByUserId, cancellationToken)
            ?? throw HttpProblemException.Unauthorized("Authenticated user could not be resolved.");
        var team = await teamStore.GetTeamAsync(validatedTeamId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{validatedTeamId}' was not found.");
        string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string tokenHash = ComputeTokenHash(rawToken);
        var now = DateTime.UtcNow;
        TeamInvite invite = await teamStore.ExecuteWithTeamMembershipLockAsync(
            validatedTeamId,
            async ct =>
            {
                var pendingInvite = await teamStore.GetPendingInviteForUpdateAsync(validatedTeamId, validatedInviteId, now, ct)
                    ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Pending invite '{validatedInviteId}' was not found.");
                pendingInvite.TokenHash = tokenHash;
                pendingInvite.ExpiresAt = now.AddDays(_inviteExpiryDays);
                await teamStore.SaveChangesAsync(ct);
                return pendingInvite;
            },
            cancellationToken);

        string inviteUrl = BuildInviteUrl(rawToken);
        await SendInviteEmailAsync(invite.Email, inviteUrl, team, inviter, cancellationToken);
        return new TeamInviteResponse(invite.Id, invite.TeamId, invite.Email, invite.Role, invite.ExpiresAt, invite.CreatedAt);
    }

    public Task RevokeInviteAsync(AuthorizedTeamId teamId, Guid inviteId, Guid revokedByUserId, CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedInviteId = RequireGuid(inviteId, "Invite id");
        var validatedRevokedByUserId = RequireUserId(revokedByUserId, "Revoked-by user id");
        var now = DateTime.UtcNow;
        return teamStore.ExecuteWithTeamMembershipLockAsync(
            validatedTeamId,
            async ct =>
            {
                var invite = await teamStore.GetPendingInviteForUpdateAsync(validatedTeamId, validatedInviteId, now, ct)
                    ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Pending invite '{validatedInviteId}' was not found.");
                invite.RevokedAt = now;
                invite.RevokedByUserId = validatedRevokedByUserId;
                await teamStore.SaveChangesAsync(ct);
            },
            cancellationToken);
    }

    public async Task<AcceptInviteResponse> AcceptInviteAsync(
        string rawToken,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw HttpProblemException.BadRequest(ProblemTypes.InvalidToken, "Invite token is required.");
        }

        var validatedUserId = RequireUserId(currentUserId, "Current user id");
        string tokenHash = ComputeTokenHash(rawToken.Trim());
        return await teamStore.ExecuteInTransactionAsync(
            async ct =>
            {
                var now = DateTime.UtcNow;
                var invite = await teamStore.GetPendingInviteByTokenHashForUpdateAsync(tokenHash, now, ct)
                    ?? throw HttpProblemException.BadRequest(ProblemTypes.InvalidToken, "Invite token is invalid or expired.");

                if (invite.ExpiresAt <= now)
                {
                    throw HttpProblemException.BadRequest(ProblemTypes.InvalidToken, "Invite token is invalid or expired.");
                }

                var user = await teamStore.GetUserAsync(validatedUserId, ct)
                    ?? throw HttpProblemException.Unauthorized("Authenticated user could not be resolved.");

                if (!string.Equals(user.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
                {
                    throw HttpProblemException.Forbidden(
                        type: ProblemTypes.Forbidden,
                        detail: "This invite belongs to a different email address.");
                }

                if (await teamStore.GetMemberForUpdateAsync(invite.TeamId, validatedUserId, ct) is null)
                {
                    await teamStore.AddMemberAsync(
                        new TeamMember
                        {
                            TeamId = invite.TeamId,
                            UserId = validatedUserId,
                            Role = invite.Role
                        },
                        ct);
                }

                invite.AcceptedAt = now;
                await teamStore.SaveChangesAsync(ct);

                var team = await teamStore.GetTeamAsync(invite.TeamId, ct)
                    ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team '{invite.TeamId}' was not found.");

                return new AcceptInviteResponse(team.Id, team.Name, invite.Role);
            },
            cancellationToken);
    }

    public Task RemoveMemberAsync(
        AuthorizedTeamId teamId,
        AuthorizedTeamMemberUserId memberUserId,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedMemberUserId = RequireAuthorizedTeamMemberUserId(memberUserId);

        return teamStore.ExecuteWithTeamMembershipLockAsync(
            validatedTeamId,
            async ct =>
            {
                var member = await teamStore.GetMemberForUpdateAsync(validatedTeamId, validatedMemberUserId, ct)
                    ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team member '{validatedMemberUserId}' was not found.");

                if (member.Role == TeamRole.Lead)
                {
                    await EnsureNotLastLeadAsync(validatedTeamId, ct);
                }

                teamStore.RemoveMember(member);
                await teamStore.SaveChangesAsync(ct);
            },
            cancellationToken);
    }

    public async Task<TeamMemberResponse> ChangeMemberRoleAsync(
        AuthorizedTeamId teamId,
        AuthorizedTeamMemberUserId memberUserId,
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedMemberUserId = RequireAuthorizedTeamMemberUserId(memberUserId);

        return await teamStore.ExecuteWithTeamMembershipLockAsync(
            validatedTeamId,
            async ct =>
            {
                var member = await teamStore.GetMemberForUpdateAsync(validatedTeamId, validatedMemberUserId, ct)
                    ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team member '{validatedMemberUserId}' was not found.");

                if (member.Role == TeamRole.Lead && command.Role != TeamRole.Lead)
                {
                    await EnsureNotLastLeadAsync(validatedTeamId, ct);
                }

                member.Role = command.Role;
                await teamStore.SaveChangesAsync(ct);

                var user = await teamStore.GetUserAsync(validatedMemberUserId, ct)
                    ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"User '{validatedMemberUserId}' was not found.");

                return new TeamMemberResponse(user.Id, user.Email, user.DisplayName, member.Role);
            },
            cancellationToken);
    }

    private async Task EnsureNotLastLeadAsync(Guid teamId, CancellationToken cancellationToken)
    {
        int leadCount = await teamStore.CountLeadsAsync(teamId, cancellationToken);
        if (leadCount <= 1)
        {
            throw HttpProblemException.Conflict(
                type: ProblemTypes.Conflict,
                detail: "A team must keep at least one Lead.");
        }
    }

    private static string NormalizeName(string value, string fieldName)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(trimmed)
            ? throw HttpProblemException.BadRequest(ProblemTypes.Validation, $"{fieldName} is required.")
            : trimmed;
    }

    private static Guid RequireAuthorizedTeamId(AuthorizedTeamId teamId)
    {
        teamId.ThrowIfDefault();
        return teamId.Value;
    }

    private static Guid RequireAuthorizedTeamMemberUserId(AuthorizedTeamMemberUserId memberUserId)
    {
        memberUserId.ThrowIfDefault();
        return memberUserId.Value;
    }

    private static Guid RequireUserId(Guid userId, string fieldName) => RequireGuid(userId, fieldName);

    private static Guid RequireGuid(Guid value, string fieldName) => value == Guid.Empty ? throw HttpProblemException.BadRequest(ProblemTypes.Validation, $"{fieldName} is required.") : value;

    private static string ComputeTokenHash(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    private TeamPlan GetDefaultCreatedTeamPlan()
        => _createTeamAlphaWorkspaces ? TeamPlan.TeamAlpha : TeamPlan.TeamS;

    private async Task SendInviteEmailAsync(string recipientEmail, string inviteUrl, Team team, User inviter, CancellationToken cancellationToken)
    {
        string inviterName = string.IsNullOrWhiteSpace(inviter.DisplayName) ? inviter.Email : inviter.DisplayName;
        await emailService.SendAsync(
            recipientEmail,
            EmailTemplates.SanitizeSubjectLine($"{inviterName} invited you to {team.Name} on Grundet"),
            EmailTemplates.BuildInviteEmail(inviteUrl, team.Name, inviterName),
            cancellationToken);
    }

    private static string BuildTeamInviteAlphaReason(User inviter, Team team, TeamInvite invite, DateTime utcNow)
        => $"Auto-approved via team invite. InvitedByUserId={inviter.Id}; InvitedByEmail={inviter.Email}; InvitedByName={inviter.DisplayName}; TeamId={team.Id}; TeamName={team.Name}; InviteId={invite.Id}; InvitedAt={utcNow:O}";

    private string BuildInviteUrl(string rawToken) => string.IsNullOrWhiteSpace(_appBaseUrl)
            ? throw HttpProblemException.InternalServerError(
                type: ProblemTypes.ServerMisconfiguration,
                detail: "MagicLink:PublicAppBaseUrl must be configured to build invite URLs.")
            : $"{_appBaseUrl}/invite/accept?token={Uri.EscapeDataString(rawToken)}";
}

public sealed record CreateTeamCommand(string Name);

public sealed record RenameTeamCommand(string Name);

public sealed record InviteMemberCommand(string Email, TeamRole Role);

public sealed record ChangeMemberRoleCommand(TeamRole Role);

public sealed record TeamResponse(Guid Id, string Name, DateTime CreatedAt);

public sealed record TeamListItemResponse(Guid Id, string Name, DateTime CreatedAt, TeamRole Role);

public sealed record TeamMemberResponse(Guid UserId, string Email, string? DisplayName, TeamRole Role);

public sealed record TeamInviteResponse(Guid Id, Guid TeamId, string Email, TeamRole Role, DateTime ExpiresAt, DateTime CreatedAt);

public sealed record AcceptInviteResponse(Guid TeamId, string TeamName, TeamRole Role);
