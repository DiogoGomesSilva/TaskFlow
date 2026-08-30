using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Domain.Common;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Persistence;

namespace TaskFlow.Api.Application.Tasks;

public sealed class DeleteTaskUseCase(TaskFlowDbContext dbContext)
{
    public async Task<Result> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (task is null)
        {
            return Result.Failure(TaskFlowErrors.TaskNotFound);
        }

        var canBeDeleted = task.CanBeDeleted();

        if (canBeDeleted.IsFailure)
        {
            return canBeDeleted;
        }

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
