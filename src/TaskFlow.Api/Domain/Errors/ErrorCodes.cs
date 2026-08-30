namespace TaskFlow.Api.Domain.Errors;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string TaskNotFound = "TASK_NOT_FOUND";
    public const string ProjectHasInProgressTasks = "PROJECT_HAS_IN_PROGRESS_TASKS";
    public const string ProjectArchived = "PROJECT_ARCHIVED";
    public const string TaskCannotBeDeleted = "TASK_CANNOT_BE_DELETED";
    public const string InvalidTaskStatusTransition = "INVALID_TASK_STATUS_TRANSITION";
}
