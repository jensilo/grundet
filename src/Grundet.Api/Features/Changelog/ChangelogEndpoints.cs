using System.Globalization;
using System.Security.Claims;
using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Features.Changelog;

public static class ChangelogEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        api.MapGet("/projects/{projectId:guid}/changelog", async (
                Guid projectId,
                [AsParameters] ProjectChangelogRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ChangelogService changelogService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);

                // Feature access is authorized here; the service enforces feature.ProjectId == projectId.
                // authorId is a filter, not a resource: it is safely DB-scoped to this project (no auth needed).
                if (request.FeatureId.HasValue)
                {
                    _ = await authorizationService.RequireFeatureAccessAsync(
                        userId, request.FeatureId.Value, TeamRole.Contributor, cancellationToken);
                }

                var response = await changelogService.GetProjectChangelogAsync(
                    authorizedProjectId, BuildQuery(request), cancellationToken);

                return Results.Ok(response);
            })
            .WithName("GetProjectChangelog")
            .WithSummary("Get the aggregated changelog for a project")
            .Produces<PagedResponse<ChangelogEntryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/changelog/authors", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ChangelogService changelogService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);

                var authors = await changelogService.GetAuthorsAsync(authorizedProjectId, cancellationToken);

                return Results.Ok(authors);
            })
            .WithName("GetProjectChangelogAuthors")
            .WithSummary("List the distinct authors of changelog activity in a project")
            .Produces<IReadOnlyList<ChangelogAuthorResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static ChangelogQuery BuildQuery(ProjectChangelogRequest request)
    {
        var entityKinds = ParseEntityKinds(request.EntityTypes);
        var changeTypes = ParseChangeTypes(request.ChangeTypes);
        DateTime? from = ParseDateBound(request.From, "from", isExclusiveUpperBound: false);
        DateTime? to = ParseDateBound(request.To, "to", isExclusiveUpperBound: true);
        int page = ParsePaginationParam(request.Page, "page", defaultValue: 1, min: 1, max: int.MaxValue);
        int pageSize = ParsePaginationParam(request.PageSize, "pageSize", defaultValue: 50, min: 1, max: 200);
        ValidateOffsetRange(page, pageSize);

        return new ChangelogQuery(
            entityKinds,
            changeTypes,
            request.FeatureId,
            request.AuthorId,
            from,
            to,
            page,
            pageSize);
    }

    private static List<ChangelogEntityKind> ParseEntityKinds(IReadOnlyList<string>? rawValues)
    {
        var kinds = new List<ChangelogEntityKind>();

        foreach (string rawValue in (rawValues ?? []).SelectMany(SplitCsv))
        {
            if (IsNumericEnumToken(rawValue)
                || !Enum.TryParse<ChangelogEntityKind>(rawValue, ignoreCase: true, out var parsed))
            {
                throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    $"entityTypes contains an invalid value '{rawValue}'.");
            }

            if (!kinds.Contains(parsed))
            {
                kinds.Add(parsed);
            }
        }

        // Empty filter means "all kinds".
        return kinds.Count == 0
            ? [ChangelogEntityKind.Intent, ChangelogEntityKind.Work, ChangelogEntityKind.Link]
            : kinds;
    }

    private static List<string> ParseChangeTypes(IReadOnlyList<string>? rawValues)
    {
        var changeTypes = new List<string>();

        foreach (string rawValue in (rawValues ?? []).SelectMany(SplitCsv))
        {
            string canonical = ResolveChangeType(rawValue)
                ?? throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    $"changeTypes contains an invalid value '{rawValue}'.");

            if (!changeTypes.Contains(canonical))
            {
                changeTypes.Add(canonical);
            }
        }

        return changeTypes;
    }

    // A change type is valid if it names a member of any of the three version-change enums.
    // The canonical (PascalCase) name is returned so it matches the stored enum text in SQL.
    private static string? ResolveChangeType(string rawValue)
        => IsNumericEnumToken(rawValue) ? null
            : Enum.TryParse<RequirementChange>(rawValue, ignoreCase: true, out var requirementChange) ? requirementChange.ToString()
            : Enum.TryParse<WorkItemChange>(rawValue, ignoreCase: true, out var workItemChange) ? workItemChange.ToString()
            : Enum.TryParse<WorkItemLinkChange>(rawValue, ignoreCase: true, out var linkChange) ? linkChange.ToString()
            : null;

    private static DateTime? ParseDateBound(string? value, string key, bool isExclusiveUpperBound)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            // Date-only bounds are interpreted as UTC calendar days, matching the
            // timeline's UTC day grouping (see lib/format-date.ts dayKey). Clients
            // wanting offset-aware bounds can send a full ISO date-time instead.
            var midnight = new DateTime(dateOnly.Year, dateOnly.Month, dateOnly.Day, 0, 0, 0, DateTimeKind.Utc);
            return isExclusiveUpperBound ? midnight.AddDays(1) : midnight;
        }

        return DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var instant)
            ? instant.UtcDateTime
            : throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"{key} must be an ISO date (yyyy-MM-dd) or date-time.");
    }

    private static int ParsePaginationParam(string? value, string key, int defaultValue, int min, int max)
    {
        int parsed = string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : int.TryParse(value, out int parsedValue)
            ? parsedValue
            : throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"{key} must be a valid integer.");

        return parsed < min || parsed > max
            ? throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"{key} must be between {min} and {max}.")
            : parsed;
    }

    private static void ValidateOffsetRange(int page, int pageSize)
    {
        long offset = ((long)page - 1L) * pageSize;
        if (offset > int.MaxValue)
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "page and pageSize produce an offset that is too large.");
        }
    }

    private static bool IsNumericEnumToken(string value)
    {
        string candidate = value.Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        int startIndex = candidate[0] is '+' or '-' ? 1 : 0;
        return startIndex < candidate.Length && candidate[startIndex..].All(char.IsDigit);
    }

    private static IEnumerable<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public sealed class ProjectChangelogRequest
    {
        [FromQuery(Name = "entityTypes")]
        public string[]? EntityTypes { get; init; }

        [FromQuery(Name = "changeTypes")]
        public string[]? ChangeTypes { get; init; }

        [FromQuery(Name = "featureId")]
        public Guid? FeatureId { get; init; }

        [FromQuery(Name = "authorId")]
        public Guid? AuthorId { get; init; }

        [FromQuery(Name = "from")]
        public string? From { get; init; }

        [FromQuery(Name = "to")]
        public string? To { get; init; }

        [FromQuery(Name = "page")]
        public string? Page { get; init; }

        [FromQuery(Name = "pageSize")]
        public string? PageSize { get; init; }
    }
}
