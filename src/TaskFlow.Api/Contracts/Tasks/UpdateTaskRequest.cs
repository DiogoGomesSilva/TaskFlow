using System.ComponentModel.DataAnnotations;
using TaskFlow.Api.Contracts.Common;
using TaskFlow.Api.Domain.Enums;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Contracts.Tasks;

public sealed class UpdateTaskRequest : IValidatableObject
{
    public Optional<string?> Title { get; init; }

    public Optional<string?> Description { get; init; }

    public Optional<TaskStatus?> Status { get; init; }

    public Optional<TaskPriority?> Priority { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Title.IsSpecified &&
            !Description.IsSpecified &&
            !Status.IsSpecified &&
            !Priority.IsSpecified)
        {
            yield return new ValidationResult(
                "O body deve conter ao menos uma propriedade para atualização.");
        }

        if (Title.IsSpecified)
        {
            if (Title.Value is null || Title.Value.Length == 0)
            {
                yield return new ValidationResult(
                    "O campo title é obrigatório.",
                    ["title"]);
            }
            else if (!UnicodeLengthAttribute.IsValid(Title.Value, 1, 200))
            {
                yield return new ValidationResult(
                    "O campo title deve possuir entre 1 e 200 caracteres.",
                    ["title"]);
            }
        }

        if (Status.IsSpecified && Status.Value is null)
        {
            yield return new ValidationResult(
                "O campo status não pode ser nulo.",
                ["status"]);
        }

        if (Priority.IsSpecified && Priority.Value is null)
        {
            yield return new ValidationResult(
                "O campo priority não pode ser nulo.",
                ["priority"]);
        }
    }
}
