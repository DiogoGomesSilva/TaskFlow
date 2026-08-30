using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Common;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Persistence;

namespace TaskFlow.Api.Application.Tasks;

public sealed class UpdateTaskUseCase(
    TaskFlowDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<Result<TaskResponse>> ExecuteAsync(
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (task is null)
        {
            return Result<TaskResponse>.Failure(TaskFlowErrors.TaskNotFound);
        }

        if (request.Status.IsSpecified)
        {
            var transition = task.TransitionTo(
                request.Status.Value!.Value,
                timeProvider.GetUtcNow());

            if (transition.IsFailure)
            {
                return Result<TaskResponse>.Failure(transition.Error!);
            }
        }

        if (request.Title.IsSpecified)
        {
            task.UpdateTitle(request.Title.Value!);
        }

        if (request.Description.IsSpecified)
        {
            task.UpdateDescription(request.Description.Value);
        }

        if (request.Priority.IsSpecified)
        {
            task.ChangePriority(request.Priority.Value!.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TaskResponse>.Success(TaskResponse.FromEntity(task));
    }
}
