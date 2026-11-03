using Grundet.Api.Features.Projects;

namespace Grundet.Api.Features.Mcp;

/// <summary>
/// Stable, agent-facing shape of a project in the API key's team. Carries <see cref="CreatedAt"/> and
/// <see cref="Description"/> as disambiguators because project names are not unique within a team —
/// the skill resolves a human name to the <see cref="Id"/> the intent tools require.
/// </summary>
public sealed record McpProject(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt)
{
    public static McpProject From(ProjectResponse project)
        => new(project.Id, project.Name, project.Description, project.CreatedAt);
}
