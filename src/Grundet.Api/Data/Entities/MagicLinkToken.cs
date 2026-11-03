namespace Grundet.Api.Data.Entities;

public sealed class MagicLinkToken
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string Email { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
