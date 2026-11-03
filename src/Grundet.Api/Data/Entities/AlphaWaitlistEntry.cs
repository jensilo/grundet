namespace Grundet.Api.Data.Entities;

public sealed class AlphaWaitlistEntry
{
    public Guid Id { get; set; }
    public required string EmailOriginal { get; set; }
    public required string EmailNormalized { get; set; }
    public required string Name { get; set; }
    public AlphaWaitlistAccountType AccountType { get; set; }
    public AlphaWaitlistTeamSize? TeamSize { get; set; }
    public AlphaWaitlistLineOfWork? LineOfWork { get; set; }
    public string? OrganizationName { get; set; }
    public string? OrganizationCountry { get; set; }
    public string? Reason { get; set; }
    public AlphaWaitlistStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? DeclinedAt { get; set; }
    public string? DeclinedBy { get; set; }
}
