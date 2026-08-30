using TaskFlow.Api.Domain.Entities;
using TaskFlow.Api.Domain.Enums;

namespace TaskFlow.Api.Contracts.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    ProjectStatus Status,
    DateTimeOffset CreatedAt)
{
    public static ProjectResponse FromEntity(Project project) => new(
        project.Id,
        project.Name,
        project.Description,
        project.Status,
        project.CreatedAt);
}
