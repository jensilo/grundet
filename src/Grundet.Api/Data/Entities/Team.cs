namespace Grundet.Api.Data.Entities;

public sealed class Team
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public TeamPlan Plan { get; set; } = TeamPlan.Solo;
    public string Region { get; set; } = "eu";
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
