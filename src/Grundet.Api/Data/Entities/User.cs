namespace Grundet.Api.Data.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }
}
