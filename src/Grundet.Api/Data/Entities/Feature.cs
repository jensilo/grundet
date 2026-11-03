using NpgsqlTypes;

namespace Grundet.Api.Data.Entities;

public sealed class Feature
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
