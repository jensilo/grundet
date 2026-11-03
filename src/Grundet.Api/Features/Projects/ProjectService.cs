using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Validation;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Billing;

namespace Grundet.Api.Features.Projects;

public sealed class ProjectService(IProjectStore projectStore, ITeamQuotaService teamQuotaService)
{
    public async Task<ProjectResponse> CreateProjectAsync(
        AuthorizedTeamId teamId,
        CreateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();

        await teamQuotaService.EnsureCanCreateProjectAsync(teamId, cancellationToken);

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TeamId = teamId.Value,
            Name = TextInput.Required(command.Name, "Project name", TextInput.NameMaxLength),
            Description = TextInput.Optional(command.Description, "Project description", TextInput.DescriptionMaxLength),
            CreatedAt = now
        };

        await projectStore.AddProjectAsync(project, cancellationToken);
        return Map(project);
    }

    public async Task<PagedResponse<ProjectResponse>> ListProjectsAsync(
        AuthorizedTeamId teamId,
        int page,
        int pageSize,
        Guid? lastSeenId,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        var (projects, total) = await projectStore.ListPageByTeamAsync(
            teamId.Value,
            page,
            pageSize,
            lastSeenId,
            cancellationToken);
        var items = projects.Select(Map).ToList();
        Guid? responseLastSeenId = items.Count == 0 ? null : items[^1].Id;
        return PagedResponse.Create(items, page, pageSize, total, responseLastSeenId);
    }

    public async Task<ProjectResponse> GetProjectByIdAsync(
        AuthorizedProjectId projectId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        var project = await projectStore.GetAsync(projectId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Project '{projectId.Value}' was not found.");
        return Map(project);
    }

    public async Task<ProjectResponse> GetProjectAsync(
        AuthorizedTeamId teamId,
        AuthorizedProjectId projectId,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        projectId.ThrowIfDefault();

        var project = await GetProjectForTeamAsync(teamId, projectId, cancellationToken);
        return Map(project);
    }

    public async Task<ProjectResponse> UpdateProjectAsync(
        AuthorizedTeamId teamId,
        AuthorizedProjectId projectId,
        UpdateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        projectId.ThrowIfDefault();

        var project = await projectStore.GetByTeamForUpdateAsync(teamId.Value, projectId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Project '{projectId.Value}' was not found in team '{teamId.Value}'.");

        if (command.Name is not null)
        {
            project.Name = TextInput.Required(command.Name, "Project name", TextInput.NameMaxLength);
        }

        if (command.DescriptionWasProvided)
        {
            project.Description = TextInput.Optional(
                command.Description, "Project description", TextInput.DescriptionMaxLength);
        }

        await projectStore.SaveChangesAsync(cancellationToken);
        return Map(project);
    }

    public async Task DeleteProjectAsync(
        AuthorizedTeamId teamId,
        AuthorizedProjectId projectId,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        projectId.ThrowIfDefault();

        _ = await GetProjectForTeamAsync(teamId, projectId, cancellationToken);
        await projectStore.SoftDeleteProjectAsync(teamId.Value, projectId.Value, changedByUserId, DateTime.UtcNow, cancellationToken);
    }

    public async Task<ResolveProjectReferenceResponse> ResolveReferenceAsync(
        AuthorizedProjectId projectId,
        ResolveProjectReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();

        if (command.SequenceNumber <= 0)
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "Reference sequence number must be a positive integer.");
        }

        // Requirements and work items share one per-project sequence counter, so a
        // bare reference (Any) can match at most one of them. The explicit IN-/WO-
        // forms additionally pin the expected kind.
        if (command.Type is ProjectReferenceType.Requirement or ProjectReferenceType.Any)
        {
            var requirement = await projectStore.ResolveRequirementReferenceAsync(
                projectId.Value, command.SequenceNumber, cancellationToken);
            if (requirement is not null)
            {
                return new ResolveProjectReferenceResponse(
                    $"IN-{requirement.SequenceNumber}",
                    "requirement",
                    requirement.RequirementId,
                    requirement.SequenceNumber,
                    requirement.Title,
                    requirement.Status.ToString());
            }
        }

        if (command.Type is ProjectReferenceType.WorkItem or ProjectReferenceType.Any)
        {
            var workItem = await projectStore.ResolveWorkItemReferenceAsync(
                projectId.Value, command.SequenceNumber, cancellationToken);
            if (workItem is not null)
            {
                return new ResolveProjectReferenceResponse(
                    $"WO-{workItem.SequenceNumber}",
                    "workItem",
                    workItem.WorkItemId,
                    workItem.SequenceNumber,
                    workItem.Title,
                    workItem.Status.ToString());
            }
        }

        throw HttpProblemException.NotFound(
            ProblemTypes.NotFound,
            $"Reference '{command.Ref}' was not found.");
    }

    private async Task<Project> GetProjectForTeamAsync(
        AuthorizedTeamId teamId,
        AuthorizedProjectId projectId,
        CancellationToken cancellationToken)
        => await projectStore.GetByTeamAsync(teamId.Value, projectId.Value, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Project '{projectId.Value}' was not found in team '{teamId.Value}'.");

    private static ProjectResponse Map(Project project)
        => new(project.Id, project.TeamId, project.Name, project.Description, project.CreatedAt);

}

public sealed record CreateProjectCommand(string Name, string? Description);

public sealed record UpdateProjectCommand(string? Name, string? Description, bool DescriptionWasProvided);

public enum ProjectReferenceType
{
    Requirement,
    WorkItem,

    // A bare reference (#N or N) with no IN-/WO- prefix; resolves to whichever
    // entity owns that sequence number in the project.
    Any
}

public sealed record ResolveProjectReferenceCommand(ProjectReferenceType Type, int SequenceNumber, string Ref);

public sealed record ResolveProjectReferenceResponse(
    string Ref,
    string Type,
    Guid Id,
    int SequenceNumber,
    string Title,
    string Status);

public sealed record ProjectResponse(Guid Id, Guid TeamId, string Name, string? Description, DateTime CreatedAt);
