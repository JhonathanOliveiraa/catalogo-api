using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CatalogoApi.Infrastructure;

/// <summary>
/// O binder padrão do MVC aceita enum na query pelo número (<c>?status=1</c>) e por
/// combinações separadas por vírgula. Este só aceita o nome exato do membro, sem
/// diferenciar maiúsculas (AD-7).
/// </summary>
public sealed class EnumPorNomeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Metadata.UnderlyingOrModelType.IsEnum ? new EnumPorNomeModelBinder() : null;
    }
}

public sealed class EnumPorNomeModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var resultado = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (resultado == ValueProviderResult.None)
        {
            // parâmetro ausente: mantém o padrão da propriedade
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, resultado);

        var metadata = bindingContext.ModelMetadata;
        var tipo = metadata.UnderlyingOrModelType;
        var valor = resultado.FirstValue?.Trim();

        if (string.IsNullOrEmpty(valor) && metadata.IsReferenceOrNullableType)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var nome = Array.Find(Enum.GetNames(tipo), n => string.Equals(n, valor, StringComparison.OrdinalIgnoreCase));
        if (nome is null)
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"O valor '{resultado.FirstValue}' não é válido para {bindingContext.ModelName}. " +
                $"Valores aceitos: {string.Join(", ", Enum.GetNames(tipo))}.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(Enum.Parse(tipo, nome));
        return Task.CompletedTask;
    }
}
