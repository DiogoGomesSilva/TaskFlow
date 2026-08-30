using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Common;
using TaskFlow.Api.Domain.Entities;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Persistence;

namespace TaskFlow.Api.Application.Tasks;

public sealed class CreateTaskUseCase(
    TaskFlowDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<Result<TaskResponse>> ExecuteAsync(
        Guid projectId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new { item.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return Result<TaskResponse>.Failure(TaskFlowErrors.ProjectNotFound);
        }

        if (project.Status == ProjectStatus.Archived)
        {
            return Result<TaskResponse>.Failure(TaskFlowErrors.ProjectArchived);
        }

        var task = TaskItem.Create(
            projectId,
            request.Title,
            request.Description,
            request.Priority!.Value,
            timeProvider.GetUtcNow());

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TaskResponse>.Success(TaskResponse.FromEntity(task));
    }
}
