using System.Globalization;
using System.Net.Mail;
using Grundet.Api.Common.Email;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Middleware;
using Microsoft.AspNetCore.RateLimiting;

namespace Grundet.Api.Features.AlphaWaitlist;

public static class AlphaWaitlistEndpoints
{
    public const string RateLimitPolicy = "alpha-waitlist";

    public static void Map(RouteGroupBuilder group)
        => group.MapPost("/alpha-waitlist", async (
                SubmitAlphaWaitlistRequest request,
                AlphaWaitlistService service,
                CancellationToken cancellationToken) =>
            {
                var command = ToCommand(request);
                await service.SubmitAsync(command, cancellationToken);

                return Results.Ok(new SubmitAlphaWaitlistResponse(AlphaWaitlistService.GenericSuccessMessage));
            })
            .RequireRateLimiting(RateLimitPolicy)
            .AllowAnonymous()
            .SkipOnboardingGuard()
            .WithName("SubmitAlphaWaitlist")
            .WithSummary("Submit an alpha waitlist request")
            .Produces<SubmitAlphaWaitlistResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static SubmitAlphaWaitlistCommand ToCommand(SubmitAlphaWaitlistRequest request)
    {
        string emailOriginal = RequireTrimmed(request.Email, "Email", 320);

        return !MailAddress.TryCreate(emailOriginal, out _)
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Email format is invalid.")
            : new SubmitAlphaWaitlistCommand(
                ParseEnum<AlphaWaitlistAccountType>(request.AccountType, "Account type"),
                RequireTrimmed(request.Name, "Name", 120),
                emailOriginal,
                EmailNormalizer.Normalize(emailOriginal),
                ParseOptionalEnum<AlphaWaitlistTeamSize>(request.TeamSize, "Team size"),
                ParseOptionalEnum<AlphaWaitlistLineOfWork>(request.LineOfWork, "Line of work"),
                OptionalTrimmed(request.OrganizationName, "Organization name", 160),
                OptionalTrimmed(request.OrganizationCountry, "Organization country", 120),
                OptionalTrimmed(request.Reason, "Reason", 1000));
    }

    private static string RequireTrimmed(string value, string fieldName, int maxLength)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(trimmed)
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"{fieldName} is required.")
            : trimmed.Length > maxLength
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"{fieldName} cannot exceed {maxLength} characters.")
            : trimmed;
    }

    private static string? OptionalTrimmed(string? value, string fieldName, int maxLength)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : trimmed.Length > maxLength
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"{fieldName} cannot exceed {maxLength} characters.")
            : trimmed;
    }

    private static T ParseEnum<T>(string value, string fieldName)
        where T : struct, Enum
    {
        string trimmed = RequireTrimmed(value, fieldName, 80);
        return !IsNumericEnumToken(trimmed) && Enum.TryParse(trimmed, ignoreCase: true, out T parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"{fieldName} is invalid.");
    }

    private static T? ParseOptionalEnum<T>(string? value, string fieldName)
        where T : struct, Enum
    {
        string trimmed = value?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : !IsNumericEnumToken(trimmed) && Enum.TryParse(trimmed, ignoreCase: true, out T parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: $"{fieldName} is invalid.");
    }

    private static bool IsNumericEnumToken(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
}
