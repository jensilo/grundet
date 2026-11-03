namespace Grundet.Api.Data.Entities;

public sealed class JiraConnection
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public required string CloudId { get; set; }
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public DateTime TokenExpiresAt { get; set; }
    public string? JiraProjectKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
