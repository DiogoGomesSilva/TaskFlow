namespace TaskFlow.Api.Contracts.Common;

public readonly struct Optional<T>
{
    private Optional(T value)
    {
        IsSpecified = true;
        Value = value;
    }

    public bool IsSpecified { get; }

    public T Value { get; } = default!;

    public static Optional<T> Specified(T value) => new(value);
}
