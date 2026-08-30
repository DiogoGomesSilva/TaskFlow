using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Http;

namespace TaskFlow.ContractTests;

public sealed class ErrorHandlingTests
{
    private readonly TaskFlowProblemDetailsFactory _factory = new();

    public static TheoryData<Error, int, string, string> ExpectedErrors => new()
    {
        {
            TaskFlowErrors.ProjectNotFound,
            StatusCodes.Status404NotFound,
            "Project not found",
            "https://taskflow/errors/project-not-found"
        },
        {
            TaskFlowErrors.TaskNotFound,
            StatusCodes.Status404NotFound,
            "Task not found",
            "https://taskflow/errors/task-not-found"
        },
        {
            TaskFlowErrors.ProjectHasInProgressTasks,
            StatusCodes.Status422UnprocessableEntity,
            "Business rule violation",
            "https://taskflow/errors/project-has-in-progress-tasks"
        },
        {
            TaskFlowErrors.ProjectArchived,
            StatusCodes.Status422UnprocessableEntity,
            "Business rule violation",
            "https://taskflow/errors/project-archived"
        },
        {
            TaskFlowErrors.TaskCannotBeDeleted,
            StatusCodes.Status422UnprocessableEntity,
            "Business rule violation",
            "https://taskflow/errors/task-cannot-be-deleted"
        },
        {
            TaskFlowErrors.InvalidTaskStatusTransition,
            StatusCodes.Status422UnprocessableEntity,
            "Business rule violation",
            "https://taskflow/errors/invalid-task-status-transition"
        }
    };

    [Theory]
    [MemberData(nameof(ExpectedErrors))]
    public void Create_ProducesContractProblemDetails(
        Error error,
        int expectedStatus,
        string expectedTitle,
        string expectedType)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/resource/123";

        var result = _factory.Create(httpContext, error);

        var problemDetails = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Contains("application/problem+json", result.ContentTypes);
        Assert.Equal(expectedType, problemDetails.Type);
        Assert.Equal(expectedTitle, problemDetails.Title);
        Assert.Equal(expectedStatus, problemDetails.Status);
        Assert.Equal(error.Detail, problemDetails.Detail);
        Assert.Equal("/resource/123", problemDetails.Instance);
        Assert.Equal(error.Code, problemDetails.Extensions["code"]);
    }

    [Fact]
    public void CreateValidation_ProducesContractValidationProblemDetails()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/projetos";
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "O campo name é obrigatório.");
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        var result = Assert.IsType<ObjectResult>(_factory.CreateValidation(actionContext));

        var problemDetails = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("application/problem+json", result.ContentTypes);
        Assert.Equal(
            "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            problemDetails.Type);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("Um ou mais campos são inválidos.", problemDetails.Detail);
        Assert.Equal("/projetos", problemDetails.Instance);
        Assert.Equal(ErrorCodes.ValidationError, problemDetails.Extensions["code"]);
        Assert.Equal("O campo name é obrigatório.", Assert.Single(problemDetails.Errors["name"]));
    }
}
