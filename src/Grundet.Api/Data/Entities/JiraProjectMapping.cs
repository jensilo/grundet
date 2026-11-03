namespace Grundet.Api.Data.Entities;

public sealed class JiraProjectMapping
{
    public Guid TeamId { get; set; }
    public Guid ProjectId { get; set; }
    public required string JiraProjectKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
