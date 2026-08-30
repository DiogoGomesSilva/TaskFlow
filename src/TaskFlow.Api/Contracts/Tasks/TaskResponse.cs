using TaskFlow.Api.Domain.Entities;
using TaskFlow.Api.Domain.Enums;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Contracts.Tasks;

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Guid ProjectId)
{
    public static TaskResponse FromEntity(TaskItem task) => new(
        task.Id,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.CreatedAt,
        task.CompletedAt,
        task.ProjectId);
}
