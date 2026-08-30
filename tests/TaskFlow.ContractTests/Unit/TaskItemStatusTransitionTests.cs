using TaskFlow.Api.Domain.Entities;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Domain.Errors;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.ContractTests;

public sealed class TaskItemStatusTransitionTests
{
    [Fact]
    public void TransitionTo_ReturnsStableFailure_WhenTransitionIsNotAllowed()
    {
        var task = TaskItem.Create(
            Guid.NewGuid(),
            "Tarefa",
            null,
            TaskPriority.Medium,
            DateTimeOffset.Parse("2026-08-29T18:00:00Z"));

        var result = task.TransitionTo(
            TaskStatus.Done,
            DateTimeOffset.Parse("2026-08-29T20:30:00Z"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidTaskStatusTransition, result.Error?.Code);
        Assert.Equal(TaskStatus.Pending, task.Status);
        Assert.Null(task.CompletedAt);
    }
}
