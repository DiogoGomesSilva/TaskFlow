using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Common;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Persistence;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Application.Tasks;

public sealed class ListProjectTasksUseCase(TaskFlowDbContext dbContext)
{
    public async Task<Result<IReadOnlyList<TaskResponse>>> ExecuteAsync(
        Guid projectId,
        TaskStatus? status,
        TaskPriority? priority,
        CancellationToken cancellationToken)
    {
        var projectExists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            return Result<IReadOnlyList<TaskResponse>>.Failure(
                TaskFlowErrors.ProjectNotFound);
        }

        var query = dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId);

        if (status is not null)
        {
            query = query.Where(task => task.Status == status.Value);
        }

        if (priority is not null)
        {
            query = query.Where(task => task.Priority == priority.Value);
        }

        var tasks = await query
            .OrderBy(task => task.Id)
            .Select(task => new TaskResponse(
                task.Id,
                task.Title,
                task.Description,
                task.Status,
                task.Priority,
                task.CreatedAt,
                task.CompletedAt,
                task.ProjectId))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<TaskResponse>>.Success(tasks);
    }
}
