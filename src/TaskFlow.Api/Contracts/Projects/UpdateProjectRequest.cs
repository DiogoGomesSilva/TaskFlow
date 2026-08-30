using System.ComponentModel.DataAnnotations;
using TaskFlow.Api.Contracts.Common;
using TaskFlow.Api.Domain.Enums;

namespace TaskFlow.Api.Contracts.Projects;

public sealed class UpdateProjectRequest : IValidatableObject
{
    public Optional<string?> Name { get; init; }

    public Optional<string?> Description { get; init; }

    public Optional<ProjectStatus?> Status { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Name.IsSpecified && !Description.IsSpecified && !Status.IsSpecified)
        {
            yield return new ValidationResult(
                "O body deve conter ao menos uma propriedade para atualização.");
        }

        if (Name.IsSpecified)
        {
            if (Name.Value is null || Name.Value.Length == 0)
            {
                yield return new ValidationResult(
                    "O campo name é obrigatório.",
                    ["name"]);
            }
            else if (!UnicodeLengthAttribute.IsValid(Name.Value, 1, 100))
            {
                yield return new ValidationResult(
                    "O campo name deve possuir entre 1 e 100 caracteres.",
                    ["name"]);
            }
        }

        if (Status.IsSpecified && Status.Value is null)
        {
            yield return new ValidationResult(
                "O campo status não pode ser nulo.",
                ["status"]);
        }
    }
}
