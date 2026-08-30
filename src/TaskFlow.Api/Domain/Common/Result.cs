using TaskFlow.Api.Domain.Errors;

namespace TaskFlow.Api.Domain.Common;

public sealed class Result
{
    private Result(Error? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(Error error) =>
        new(error ?? throw new ArgumentNullException(nameof(error)));
}
