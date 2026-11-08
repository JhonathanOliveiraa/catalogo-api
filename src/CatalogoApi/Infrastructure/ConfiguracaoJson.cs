using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatalogoApi.Infrastructure;

/// <summary>
/// Configuração JSON única (AD-7), aplicada ao MVC (<c>AddJsonOptions</c>) e ao
/// <c>ConfigureHttpJsonOptions</c>, que é a que o OpenAPI lê.
/// </summary>
public static class ConfiguracaoJson
{
    public static void Aplicar(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    }
}
