using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Teams;

public interface ITeamStore
{
    Task AddTeamWithLeadAsync(Team team, TeamMember leadMember, CancellationToken cancellationToken = default);

    Task<bool> HasSoloWorkspaceAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<TeamSummary>> ListTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Team?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<Team?> GetTeamForUpdateAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<List<TeamMemberSummary>> ListMembersAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamMember?> GetMemberForUpdateAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountLeadsAsync(Guid teamId, CancellationToken cancellationToken = default);

    void RemoveMember(TeamMember member);

    Task AddInviteAsync(TeamInvite invite, CancellationToken cancellationToken = default);

    Task<TeamInvite?> GetPendingInviteForEmailAsync(Guid teamId, string email, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<List<TeamInviteSummary>> ListPendingInvitesAsync(Guid teamId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<TeamInvite?> GetPendingInviteForUpdateAsync(Guid teamId, Guid inviteId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<TeamInvite?> GetPendingInviteByTokenHashForUpdateAsync(string tokenHash, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TeamMember?> GetMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    Task AddMemberAsync(TeamMember member, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    Task ExecuteWithTeamMembershipLockAsync(
        Guid teamId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteWithTeamMembershipLockAsync<T>(
        Guid teamId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task SoftDeleteTeamAsync(Guid teamId, Guid changedByUserId, DateTime utcNow, CancellationToken cancellationToken = default);
}

public sealed record TeamSummary(Guid Id, string Name, DateTime CreatedAt, TeamRole Role);

public sealed record TeamMemberSummary(Guid UserId, string Email, string? DisplayName, TeamRole Role);

public sealed record TeamInviteSummary(Guid Id, Guid TeamId, string Email, TeamRole Role, DateTime ExpiresAt, DateTime CreatedAt);
