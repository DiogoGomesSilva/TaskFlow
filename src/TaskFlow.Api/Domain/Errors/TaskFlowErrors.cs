namespace TaskFlow.Api.Domain.Errors;

public static class TaskFlowErrors
{
    public static Error ProjectNotFound { get; } = new(
        ErrorCodes.ProjectNotFound,
        "project-not-found",
        "O projeto informado não foi encontrado.",
        ErrorKind.NotFound);

    public static Error TaskNotFound { get; } = new(
        ErrorCodes.TaskNotFound,
        "task-not-found",
        "A tarefa informada não foi encontrada.",
        ErrorKind.NotFound);

    public static Error ProjectHasInProgressTasks { get; } = new(
        ErrorCodes.ProjectHasInProgressTasks,
        "project-has-in-progress-tasks",
        "O projeto não pode ser arquivado enquanto possuir tarefas em andamento.",
        ErrorKind.BusinessRule);

    public static Error ProjectArchived { get; } = new(
        ErrorCodes.ProjectArchived,
        "project-archived",
        "Não é permitido criar novas tarefas em um projeto arquivado.",
        ErrorKind.BusinessRule);

    public static Error TaskCannotBeDeleted { get; } = new(
        ErrorCodes.TaskCannotBeDeleted,
        "task-cannot-be-deleted",
        "Somente tarefas com status pending podem ser excluídas.",
        ErrorKind.BusinessRule);

    public static Error InvalidTaskStatusTransition { get; } = new(
        ErrorCodes.InvalidTaskStatusTransition,
        "invalid-task-status-transition",
        "A transição de status solicitada não é permitida.",
        ErrorKind.BusinessRule);
}
