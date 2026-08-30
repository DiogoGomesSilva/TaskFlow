using System.ComponentModel.DataAnnotations;
using TaskFlow.Api.Contracts.Common;
using TaskFlow.Api.Domain.Enums;

namespace TaskFlow.Api.Contracts.Tasks;

public sealed class CreateTaskRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "O campo title é obrigatório.")]
    [UnicodeLength(
        200,
        MinimumLength = 1,
        ErrorMessage = "O campo title deve possuir entre 1 e 200 caracteres.")]
    public string Title { get; init; } = null!;

    public string? Description { get; init; }

    [Required(ErrorMessage = "O campo priority é obrigatório.")]
    public TaskPriority? Priority { get; init; }
}
