namespace Grundet.Api.Data.Entities;

public sealed class TeamInvite
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public required string Email { get; set; }
    public TeamRole Role { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
