namespace Grundet.Api.Data.Entities;

public sealed class ProjectEntityCounter
{
    public Guid ProjectId { get; set; }
    public int NextId { get; set; } = 1;
}
