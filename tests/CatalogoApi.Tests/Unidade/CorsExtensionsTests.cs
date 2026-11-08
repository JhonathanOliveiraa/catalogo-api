using CatalogoApi.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace CatalogoApi.Tests.Unidade;

public sealed class CorsExtensionsTests
{
    private static IConfiguration Config(string? origens) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CorsExtensions.ChaveOrigens] = origens })
            .Build();

    [Fact]
    public void Separa_por_virgula_removendo_espacos_e_barra_final() =>
        Assert.Equal(
            ["https://a.com", "https://b.com", "http://localhost:5173"],
            CorsExtensions.LerOrigens(Config(" https://a.com/, https://b.com ,http://localhost:5173/,https://A.com")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" , ,")]
    public void Vazia_derruba_com_mensagem_clara(string? origens)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => CorsExtensions.LerOrigens(Config(origens)));

        Assert.Contains("Cors__OrigensPermitidas", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://a.com,*")]
    [InlineData("https://*.a.com")]
    public void Curinga_derruba_com_mensagem_clara(string origens)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => CorsExtensions.LerOrigens(Config(origens)));

        Assert.Contains("'*'", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a.com")]
    [InlineData("ftp://a.com")]
    [InlineData("https://a.com/caminho")]
    public void Origem_malformada_derruba(string origens) =>
        Assert.Throws<InvalidOperationException>(() => CorsExtensions.LerOrigens(Config(origens)));
}
