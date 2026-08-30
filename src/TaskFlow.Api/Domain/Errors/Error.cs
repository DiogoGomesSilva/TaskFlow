namespace TaskFlow.Api.Domain.Errors;

public sealed record Error(
    string Code,
    string Slug,
    string Detail,
    ErrorKind Kind);
