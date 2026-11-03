using System.ComponentModel;
using Grundet.Api.Features.Projects;
using ModelContextProtocol.Server;

namespace Grundet.Api.Features.Mcp;

/// <summary>
/// Read-only MCP tool for project discovery. Thin transport over <see cref="ProjectService"/>: the
/// team-scope guard binds the read to the API key's team, then the existing paginated, DB-pushed list
/// runs. No business logic lives here.
/// </summary>
[McpServerToolType]
public sealed class ProjectTools(
    McpTeamScope scope,
    ProjectService projectService)
{
    [McpServerTool(Name = "list_projects")]
    [Description(
        "List every project in the API key's team, newest first. Returns each project's id, name, " +
        "description, and createdAt. Use this to resolve a human project name to the GUID the intent " +
        "tools require. Project names are NOT unique within a team: if more than one project shares " +
        "the requested name, present the candidates and let the user pick — never guess.")]
    public async Task<IReadOnlyList<McpProject>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var teamScope = await scope.RequireTeamAsync(cancellationToken);

        // The tool contract is "the whole team", but ProjectService is paginated. Walk every page
        // server-side and flatten so discovery works for teams with more than one page of projects;
        // a single truncated page would make later projects indistinguishable from missing, and the
        // skill's name-resolution flow calls this exactly once.
        const int pageSize = 100;
        var projects = new List<McpProject>();
        for (int page = 1; ; page++)
        {
            var result = await projectService.ListProjectsAsync(
                teamScope.TeamId,
                page,
                pageSize,
                lastSeenId: null,
                cancellationToken);

            projects.AddRange(result.Items.Select(McpProject.From));

            if (!result.HasMore || result.Items.Count == 0)
            {
                break;
            }
        }

        return projects;
    }
}
