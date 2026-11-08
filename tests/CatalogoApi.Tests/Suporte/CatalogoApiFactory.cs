using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogoApi.Tests.Suporte;

/// <summary>
/// Sobe a API real em memória. A configuração entra pelas mesmas chaves que o ambiente
/// usa em produção (AD-8); nada de provider em memória (AD-12).
/// </summary>
public sealed class CatalogoApiFactory(
    string? connectionString,
    string? origensCors = CatalogoApiFactory.OrigemFront,
    Action<IServiceCollection>? servicos = null) : WebApplicationFactory<Program>
{
    public const string OrigemFront = "https://front.exemplo.com";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        builder.UseSetting("ConnectionStrings:Catalogo", connectionString ?? string.Empty);
        builder.UseSetting("Cors:OrigensPermitidas", origensCors ?? string.Empty);

        if (servicos is not null)
        {
            builder.ConfigureTestServices(servicos);
        }
    }
}
