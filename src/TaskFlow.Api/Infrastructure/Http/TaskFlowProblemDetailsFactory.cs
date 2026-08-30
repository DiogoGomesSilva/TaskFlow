using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Domain.Errors;

namespace TaskFlow.Api.Infrastructure.Http;

public sealed class TaskFlowProblemDetailsFactory
{
    private const string ValidationProblemType =
        "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private const string ErrorTypeBaseUri = "https://taskflow/errors/";
    private const string ProblemContentType = "application/problem+json";

    public ObjectResult Create(HttpContext httpContext, Error error)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(error);

        var (status, title) = error.Kind switch
        {
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorKind.BusinessRule => (
                StatusCodes.Status422UnprocessableEntity,
                "Business rule violation"),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Kind, "Unknown error kind.")
        };

        var problemDetails = new ProblemDetails
        {
            Type = $"{ErrorTypeBaseUri}{error.Slug}",
            Title = error.Kind == ErrorKind.NotFound
                ? GetNotFoundTitle(error)
                : title,
            Status = status,
            Detail = error.Detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["code"] = error.Code;

        return CreateObjectResult(problemDetails, status);
    }

    public ObjectResult CreateValidation(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Type = ValidationProblemType,
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Detail = "Um ou mais campos são inválidos.",
            Instance = context.HttpContext.Request.Path
        };

        problemDetails.Extensions["code"] = ErrorCodes.ValidationError;

        return CreateObjectResult(problemDetails, StatusCodes.Status400BadRequest);
    }

    private static string GetNotFoundTitle(Error error) => error.Code switch
    {
        ErrorCodes.ProjectNotFound => "Project not found",
        ErrorCodes.TaskNotFound => "Task not found",
        _ => "Resource not found"
    };

    private static ObjectResult CreateObjectResult(ProblemDetails problemDetails, int status)
    {
        var result = new ObjectResult(problemDetails)
        {
            StatusCode = status
        };

        result.ContentTypes.Add(ProblemContentType);

        return result;
    }
}
