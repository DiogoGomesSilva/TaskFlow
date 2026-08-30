using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskFlow.Api.Infrastructure.ModelBinding;

internal sealed class CanonicalGuidModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(
            bindingContext.ModelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(
            bindingContext.ModelName,
            valueProviderResult);

        var value = valueProviderResult.FirstValue;
        if (value is not null && Guid.TryParseExact(value, "D", out var identifier))
        {
            bindingContext.Result = ModelBindingResult.Success(identifier);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"O campo {bindingContext.ModelName} deve ser um UUID válido no formato canônico.");

        return Task.CompletedTask;
    }
}
