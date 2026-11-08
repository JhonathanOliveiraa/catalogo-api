using Microsoft.AspNetCore.Cors.Infrastructure;

namespace CatalogoApi.Infrastructure;

/// <summary>CORS para o catalogo-front, com origens vindas do ambiente (AD-8).</summary>
public static class CorsExtensions
{
    public const string Politica = "catalogo-front";
    public const string ChaveOrigens = "Cors:OrigensPermitidas";

    public static IServiceCollection AddCorsCatalogo(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
            options.AddPolicy(Politica, policy => policy
                .WithOrigins(LerOrigens(configuration))
                .WithMethods("GET", "POST", "PUT", "PATCH")
                .AllowAnyHeader()
                .WithExposedHeaders("Location")
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
        return services;
    }

    /// <summary>
    /// Lê <c>Cors__OrigensPermitidas</c> separada por vírgula, removendo espaços e barra final.
    /// Vazia, com <c>*</c> ou com uma origem malformada, derruba o startup.
    /// </summary>
    public static string[] LerOrigens(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var origens = (configuration[ChaveOrigens] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'))
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origens.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors__OrigensPermitidas está vazia. Informe as origens do catalogo-front separadas por vírgula, " +
                "por exemplo \"https://catalogo.exemplo.com\".");
        }

        if (origens.Any(o => o.Contains('*', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Cors__OrigensPermitidas não aceita '*'. Liste as origens explicitamente.");
        }

        var invalida = origens.FirstOrDefault(o => !OrigemValida(o));
        if (invalida is not null)
        {
            throw new InvalidOperationException(
                $"Cors__OrigensPermitidas contém uma origem inválida: '{invalida}'. " +
                "Use esquema e host, como \"https://catalogo.exemplo.com\".");
        }

        return origens;
    }

    private static bool OrigemValida(string origem) =>
        Uri.TryCreate(origem, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
