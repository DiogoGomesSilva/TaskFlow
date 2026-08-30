using TaskFlow.Api.Domain.Enums;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Contracts.Tasks;

public static class TaskFilterContract
{
    public static bool TryParseStatus(string? value, out TaskStatus status)
    {
        switch (value)
        {
            case "pending":
                status = TaskStatus.Pending;
                return true;
            case "in_progress":
                status = TaskStatus.InProgress;
                return true;
            case "done":
                status = TaskStatus.Done;
                return true;
            default:
                status = default;
                return false;
        }
    }

    public static bool TryParsePriority(string? value, out TaskPriority priority)
    {
        switch (value)
        {
            case "low":
                priority = TaskPriority.Low;
                return true;
            case "medium":
                priority = TaskPriority.Medium;
                return true;
            case "high":
                priority = TaskPriority.High;
                return true;
            default:
                priority = default;
                return false;
        }
    }
}
