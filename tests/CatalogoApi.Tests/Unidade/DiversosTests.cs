using CatalogoApi.Infrastructure;
using CatalogoApi.Repositories;
using Microsoft.Extensions.Configuration;

namespace CatalogoApi.Tests.Unidade;

public sealed class DiversosTests
{
    [Theory]
    [InlineData("cam", "cam")]
    [InlineData("50%", @"50\%")]
    [InlineData("a_b", @"a\_b")]
    [InlineData(@"c:\x", @"c:\\x")]
    public void Termo_do_ILIKE_e_escapado(string termo, string esperado) =>
        Assert.Equal(esperado, ProdutoRepository.EscaparLike(termo));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("http://alloy:4317", true)]
    public void Exporter_OTLP_so_liga_com_endpoint_no_ambiente(string? endpoint, bool esperado)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint })
            .Build();

        Assert.Equal(esperado, ObservabilidadeExtensions.ExporterConfigurado(config));
    }

    [Fact]
    public void Exporter_OTLP_liga_com_endpoint_especifico_de_sinal()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = "http://x:4318/v1/traces" })
            .Build();

        Assert.True(ObservabilidadeExtensions.ExporterConfigurado(config));
    }
}
