using TaskFlow.Api.Domain.Common;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Domain.Enums;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Domain.Entities;

public sealed class TaskItem
{
    private const int MaxTitleLength = 200;

    private TaskItem()
    {
    }

    private TaskItem(
        Guid id,
        string title,
        string? description,
        TaskStatus status,
        TaskPriority priority,
        DateTimeOffset createdAt,
        Guid projectId)
    {
        Id = id;
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        CreatedAt = createdAt;
        ProjectId = projectId;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public TaskStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid ProjectId { get; private set; }

    public static TaskItem Create(
        Guid projectId,
        string title,
        string? description,
        TaskPriority priority,
        DateTimeOffset createdAt)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id cannot be empty.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrEmpty(title);

        if (title.EnumerateRunes().Count() > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Task title cannot exceed {MaxTitleLength} characters.",
                nameof(title));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown task priority.");
        }

        return new TaskItem(
            Guid.NewGuid(),
            title,
            description,
            TaskStatus.Pending,
            priority,
            createdAt.ToUniversalTime(),
            projectId);
    }

    public Result TransitionTo(TaskStatus targetStatus, DateTimeOffset occurredAt)
    {
        if (!Enum.IsDefined(targetStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetStatus),
                targetStatus,
                "Unknown task status.");
        }

        if (targetStatus == Status)
        {
            return Result.Success();
        }

        switch (Status, targetStatus)
        {
            case (TaskStatus.Pending, TaskStatus.InProgress):
                Status = TaskStatus.InProgress;
                return Result.Success();

            case (TaskStatus.InProgress, TaskStatus.Done):
                Status = TaskStatus.Done;
                CompletedAt = occurredAt.ToUniversalTime();
                return Result.Success();

            default:
                return Result.Failure(TaskFlowErrors.InvalidTaskStatusTransition);
        }
    }

    public void UpdateTitle(string title)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);

        if (title.EnumerateRunes().Count() > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Task title cannot exceed {MaxTitleLength} characters.",
                nameof(title));
        }

        Title = title;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void ChangePriority(TaskPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown task priority.");
        }

        Priority = priority;
    }

    public Result CanBeDeleted() => Status == TaskStatus.Pending
        ? Result.Success()
        : Result.Failure(TaskFlowErrors.TaskCannotBeDeleted);
}
