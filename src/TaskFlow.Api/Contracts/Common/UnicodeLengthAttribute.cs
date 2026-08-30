using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Contracts.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class UnicodeLengthAttribute(int maximumLength) : ValidationAttribute
{
    public int MaximumLength { get; } = maximumLength;

    public int MinimumLength { get; init; }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text && IsValid(text, MinimumLength, MaximumLength);
    }

    public static bool IsValid(string text, int minimumLength, int maximumLength)
    {
        var length = text.EnumerateRunes().Count();
        return length >= minimumLength && length <= maximumLength;
    }
}
