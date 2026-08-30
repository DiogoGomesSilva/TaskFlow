using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Application.Tasks;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Infrastructure.Http;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Controllers;

[ApiController]
public sealed class TasksController(
    CreateTaskUseCase createTask,
    ListProjectTasksUseCase listProjectTasks,
    UpdateTaskUseCase updateTask,
    DeleteTaskUseCase deleteTask,
    TaskFlowProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    [HttpPost("projetos/{id}/tarefas")]
    [Consumes("application/json")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskResponse>> CreateAsync(
        Guid id,
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createTask.ExecuteAsync(id, request, cancellationToken);

        return result.IsSuccess
            ? Created($"/tarefas/{result.Value.Id}", result.Value)
            : problemDetailsFactory.Create(HttpContext, result.Error!);
    }

    [HttpGet("projetos/{id}/tarefas")]
    [ProducesResponseType<IReadOnlyList<TaskResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> ListAsync(
        Guid id,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        CancellationToken cancellationToken)
    {
        TaskStatus? parsedStatus = null;
        TaskPriority? parsedPriority = null;

        if (status is not null)
        {
            if (!TaskFilterContract.TryParseStatus(status, out var value))
            {
                ModelState.AddModelError(
                    "status",
                    "O campo status deve ser pending, in_progress ou done.");
            }
            else
            {
                parsedStatus = value;
            }
        }

        if (priority is not null)
        {
            if (!TaskFilterContract.TryParsePriority(priority, out var value))
            {
                ModelState.AddModelError(
                    "priority",
                    "O campo priority deve ser low, medium ou high.");
            }
            else
            {
                parsedPriority = value;
            }
        }

        if (!ModelState.IsValid)
        {
            return problemDetailsFactory.CreateValidation(ControllerContext);
        }

        var result = await listProjectTasks.ExecuteAsync(
            id,
            parsedStatus,
            parsedPriority,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : problemDetailsFactory.Create(HttpContext, result.Error!);
    }

    [HttpPatch("tarefas/{id}")]
    [Consumes("application/json")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskResponse>> UpdateAsync(
        Guid id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await updateTask.ExecuteAsync(id, request, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : problemDetailsFactory.Create(HttpContext, result.Error!);
    }

    [HttpDelete("tarefas/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await deleteTask.ExecuteAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : problemDetailsFactory.Create(HttpContext, result.Error!);
    }
}
