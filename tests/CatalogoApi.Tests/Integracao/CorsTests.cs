using System.Net;
using System.Net.Http.Json;
using CatalogoApi.Tests.Suporte;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 1.4: CORS configurável por ambiente.</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class CorsTests(PostgresFixture fx)
{
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task Preflight_de_origem_permitida_aceita_os_metodos_da_API(string metodo)
    {
        await using var factory = new CatalogoApiFactory(fx.ConnectionString, "https://a.com/, https://b.com");
        using var client = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/produtos/1");
        preflight.Headers.Add("Origin", "https://a.com");
        preflight.Headers.Add("Access-Control-Request-Method", metodo);
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var resposta = await client.SendAsync(preflight);

        Assert.True(resposta.IsSuccessStatusCode);
        Assert.Equal("https://a.com", Assert.Single(resposta.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains(metodo, string.Join(",", resposta.Headers.GetValues("Access-Control-Allow-Methods")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requisicao_de_origem_permitida_expoe_Location()
    {
        await using var factory = new CatalogoApiFactory(fx.ConnectionString, "https://a.com/, https://b.com");
        using var client = factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/produtos")
        {
            Content = JsonContent.Create(new { nome = $"{ApiHttp.NovoPrefixo()} cors", preco = 1m, estoque = 1 }),
        };
        requisicao.Headers.Add("Origin", "https://b.com");

        var resposta = await client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal("https://b.com", Assert.Single(resposta.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Location", string.Join(",", resposta.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Origem_fora_da_lista_nao_recebe_Access_Control_Allow_Origin()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/produtos");
        requisicao.Headers.Add("Origin", "https://intruso.com");

        var resposta = await fx.Client.SendAsync(requisicao);

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
