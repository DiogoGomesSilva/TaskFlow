using TaskFlow.Api.Domain.Errors;

namespace TaskFlow.Api.Domain.Common;

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
    }

    private Result(Error error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not contain a value.");

    public Error? Error { get; }

    public static Result<T> Success(T value) =>
        new(value ?? throw new ArgumentNullException(nameof(value)));

    public static Result<T> Failure(Error error) =>
        new(error ?? throw new ArgumentNullException(nameof(error)));
}
