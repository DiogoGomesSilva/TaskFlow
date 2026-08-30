using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Infrastructure.Persistence;

namespace TaskFlow.Api.Application.Projects;

public sealed class ListProjectsUseCase(TaskFlowDbContext dbContext)
{
    public async Task<IReadOnlyList<ProjectResponse>> ExecuteAsync(
        ProjectStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Projects.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(project => project.Status == status.Value);
        }

        return await query
            .OrderBy(project => project.Id)
            .Select(project => new ProjectResponse(
                project.Id,
                project.Name,
                project.Description,
                project.Status,
                project.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
