using System.ComponentModel.DataAnnotations;
using TaskFlow.Api.Contracts.Common;

namespace TaskFlow.Api.Contracts.Projects;

public sealed class CreateProjectRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "O campo name é obrigatório.")]
    [UnicodeLength(
        100,
        MinimumLength = 1,
        ErrorMessage = "O campo name deve possuir entre 1 e 100 caracteres.")]
    public string Name { get; init; } = null!;

    public string? Description { get; init; }
}
