using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Domain.Common;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Persistence;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Application.Projects;

public sealed class UpdateProjectUseCase(TaskFlowDbContext dbContext)
{
    public async Task<Result<ProjectResponse>> ExecuteAsync(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

        if (project is null)
        {
            return Result<ProjectResponse>.Failure(TaskFlowErrors.ProjectNotFound);
        }

        var isArchiving = request.Status.IsSpecified &&
            request.Status.Value == ProjectStatus.Archived &&
            project.Status != ProjectStatus.Archived;

        if (isArchiving)
        {
            var hasInProgressTasks = await dbContext.Tasks.AnyAsync(
                task => task.ProjectId == project.Id && task.Status == TaskStatus.InProgress,
                cancellationToken);

            if (hasInProgressTasks)
            {
                return Result<ProjectResponse>.Failure(
                    TaskFlowErrors.ProjectHasInProgressTasks);
            }
        }

        if (request.Name.IsSpecified)
        {
            project.UpdateName(request.Name.Value!);
        }

        if (request.Description.IsSpecified)
        {
            project.UpdateDescription(request.Description.Value);
        }

        if (request.Status.IsSpecified)
        {
            project.ChangeStatus(request.Status.Value!.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProjectResponse>.Success(ProjectResponse.FromEntity(project));
    }
}
