using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskFlow.Api.Infrastructure.ModelBinding;

internal sealed class CanonicalGuidModelBinderProvider : IModelBinderProvider
{
    private static readonly IModelBinder Binder = new CanonicalGuidModelBinder();

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.ModelType == typeof(Guid) ? Binder : null;
    }
}
