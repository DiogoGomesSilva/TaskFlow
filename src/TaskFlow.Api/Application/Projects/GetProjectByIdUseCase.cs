using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Domain.Common;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Persistence;

namespace TaskFlow.Api.Application.Projects;

public sealed class GetProjectByIdUseCase(TaskFlowDbContext dbContext)
{
    public async Task<Result<ProjectResponse>> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == id)
            .Select(project => new ProjectResponse(
                project.Id,
                project.Name,
                project.Description,
                project.Status,
                project.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return project is null
            ? Result<ProjectResponse>.Failure(TaskFlowErrors.ProjectNotFound)
            : Result<ProjectResponse>.Success(project);
    }
}
