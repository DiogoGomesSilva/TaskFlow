using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Application.Projects;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Infrastructure.Http;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("projetos")]
public sealed class ProjectsController(
    CreateProjectUseCase createProject,
    ListProjectsUseCase listProjects,
    GetProjectByIdUseCase getProjectById,
    UpdateProjectUseCase updateProject,
    TaskFlowProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    private const string GetProjectByIdRoute = "GetProjectById";

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> CreateAsync(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var response = await createProject.ExecuteAsync(request, cancellationToken);

        return CreatedAtRoute(
            GetProjectByIdRoute,
            new { id = response.Id },
            response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProjectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> ListAsync(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        ProjectStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!ProjectStatusContract.TryParse(status, out var value))
            {
                ModelState.AddModelError(
                    "status",
                    "O campo status deve ser active ou archived.");

                return problemDetailsFactory.CreateValidation(ControllerContext);
            }

            parsedStatus = value;
        }

        var response = await listProjects.ExecuteAsync(parsedStatus, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id}", Name = GetProjectByIdRoute)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await getProjectById.ExecuteAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : problemDetailsFactory.Create(HttpContext, result.Error!);
    }

    [HttpPatch("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectResponse>> UpdateAsync(
        Guid id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await updateProject.ExecuteAsync(id, request, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : problemDetailsFactory.Create(HttpContext, result.Error!);
    }
}
