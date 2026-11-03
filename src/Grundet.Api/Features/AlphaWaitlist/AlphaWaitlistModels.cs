using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.AlphaWaitlist;

public sealed record SubmitAlphaWaitlistRequest(
    string AccountType,
    string Name,
    string Email,
    string? TeamSize,
    string? LineOfWork,
    string? OrganizationName,
    string? OrganizationCountry,
    string? Reason);

public sealed record SubmitAlphaWaitlistResponse(string Message);

public sealed record SubmitAlphaWaitlistCommand(
    AlphaWaitlistAccountType AccountType,
    string Name,
    string EmailOriginal,
    string EmailNormalized,
    AlphaWaitlistTeamSize? TeamSize,
    AlphaWaitlistLineOfWork? LineOfWork,
    string? OrganizationName,
    string? OrganizationCountry,
    string? Reason);
