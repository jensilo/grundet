using System.ComponentModel;
using System.Globalization;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Projects;
using Grundet.Api.Features.Requirements;
using ModelContextProtocol.Server;

namespace Grundet.Api.Features.Mcp;

/// <summary>
/// Read-only MCP tools over versioned intent. Thin transport: each tool runs the team-scope guard,
/// resolves references, authorizes, and calls the existing services. No business logic lives here.
/// </summary>
[McpServerToolType]
public sealed class IntentTools(
    McpTeamScope scope,
    IResourceAuthorizationService authorizationService,
    ProjectService projectService,
    RequirementService requirementService)
{
    [McpServerTool(Name = "list_intent")]
    [Description("List current intent for a project in the API key's team. Returns the latest version of each non-deleted requirement (Intent), newest activity first.")]
    public async Task<IReadOnlyList<McpIntent>> ListIntentAsync(
        [Description("The project's GUID.")] Guid projectId,
        [Description("Optional feature GUID to filter to a single feature.")] Guid? featureId = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size (1-100).")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var projectScope = await scope.RequireProjectAsync(projectId, cancellationToken);
        var command = new ListRequirementsCommand(
            Statuses: [],
            Priorities: [],
            Labels: [],
            FeatureId: featureId,
            Query: null,
            Page: Math.Max(1, page),
            PageSize: Math.Clamp(pageSize, 1, 100),
            // Updated = ChangedAt DESC first, so the contract's "newest activity first" holds.
            Sort: RequirementBrowseSort.Updated);

        var result = await requirementService.ListProjectRequirementsAsync(projectScope.ProjectId, command, cancellationToken);
        return result.Items.Select(McpIntent.From).ToList();
    }

    [McpServerTool(Name = "get_intent")]
    [Description("Get a single current intent (requirement) by its IN- reference within a project, e.g. 'IN-7'.")]
    public async Task<McpIntent> GetIntentAsync(
        [Description("The project's GUID.")] Guid projectId,
        [Description("The intent reference, e.g. 'IN-7'.")] string reference,
        CancellationToken cancellationToken = default)
    {
        var (projectScope, requirementId) = await ResolveIntentAsync(projectId, reference, cancellationToken);
        var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
            projectScope.UserId, requirementId, TeamRole.Contributor, cancellationToken);
        // The reference was resolved within the authorized project, so it already belongs to it —
        // no need for the project-scoped overload's extra belongs-to-project round-trip.
        var requirement = await requirementService.GetRequirementAsync(authorizedRequirementId, cancellationToken);
        return McpIntent.From(requirement);
    }

    [McpServerTool(Name = "get_intent_history")]
    [Description("Get the full version history (changelog) of an intent by its IN- reference. Each entry is the complete requirement state at that version, oldest to newest.")]
    public async Task<IReadOnlyList<McpIntent>> GetIntentHistoryAsync(
        [Description("The project's GUID.")] Guid projectId,
        [Description("The intent reference, e.g. 'IN-7'.")] string reference,
        CancellationToken cancellationToken = default)
    {
        var (projectScope, requirementId) = await ResolveIntentAsync(projectId, reference, cancellationToken);
        var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
            projectScope.UserId, requirementId, TeamRole.Contributor, cancellationToken);
        var history = await requirementService.GetHistoryAsync(authorizedRequirementId, cancellationToken);
        // The service returns newest-first; the tool advertises oldest-to-newest (changelog reading order).
        return history.Select(McpIntent.From).Reverse().ToList();
    }

    private async Task<(McpProjectScope Scope, Guid RequirementId)> ResolveIntentAsync(
        Guid projectId, string reference, CancellationToken cancellationToken)
    {
        var projectScope = await scope.RequireProjectAsync(projectId, cancellationToken);
        int sequenceNumber = ParseIntentReference(reference);
        var resolved = await projectService.ResolveReferenceAsync(
            projectScope.ProjectId,
            new ResolveProjectReferenceCommand(ProjectReferenceType.Requirement, sequenceNumber, reference),
            cancellationToken);
        return (projectScope, resolved.Id);
    }

    private static int ParseIntentReference(string reference)
    {
        string trimmed = reference.Trim();
        string digits = trimmed.StartsWith("IN-", StringComparison.OrdinalIgnoreCase)
            ? trimmed[3..]
            : trimmed.TrimStart('#');

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int sequenceNumber) && sequenceNumber > 0
            ? sequenceNumber
            : throw HttpProblemException.BadRequest(
                ProblemTypes.Validation, $"Invalid intent reference '{reference}'. Expected e.g. 'IN-7'.");
    }
}
