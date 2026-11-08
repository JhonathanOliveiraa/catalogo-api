using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatalogoApi.Contracts;
using CatalogoApi.Domain;
using CatalogoApi.Infrastructure;

namespace CatalogoApi.Tests.Suporte;

public static class ApiHttp
{
    public static readonly JsonSerializerOptions Json = CriarJson();

    private static JsonSerializerOptions CriarJson()
    {
        var opcoes = new JsonSerializerOptions();
        ConfiguracaoJson.Aplicar(opcoes);
        return opcoes;
    }

    /// <summary>Prefixo único por teste: isola os dados sem limpar o banco compartilhado.</summary>
    public static string NovoPrefixo() => $"t{Guid.NewGuid():N}"[..10];

    public static async Task<ProdutoResponse> CriarAsync(
        this HttpClient client, string nome, decimal preco = 10m, int estoque = 1, string? descricao = null)
    {
        var resposta = await client.PostAsJsonAsync("/produtos", new { nome, descricao, preco, estoque }, Json);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoResponse>(Json))!;
    }

    public static async Task<ProdutoResponse> AlterarStatusAsync(this HttpClient client, int id, StatusProduto status)
    {
        var resposta = await client.PatchAsJsonAsync($"/produtos/{id}/status", new { status }, Json);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoResponse>(Json))!;
    }

    public static async Task<PaginaResponse<ProdutoResponse>> ListarAsync(this HttpClient client, string query)
    {
        var resposta = await client.GetAsync($"/produtos?{query}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PaginaResponse<ProdutoResponse>>(Json))!;
    }

    public static async Task<ProdutoResponse> LerProdutoAsync(this HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<ProdutoResponse>(Json))!;

    /// <summary>Confere que o corpo é ProblemDetails (RFC 9457) com o status esperado.</summary>
    public static async Task<JsonElement> AssertProblemDetailsAsync(
        this HttpResponseMessage resposta, HttpStatusCode status, bool validacao = false)
    {
        Assert.Equal(status, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, corpo.GetProperty("status").GetInt32());
        Assert.True(corpo.TryGetProperty("title", out _));
        Assert.True(corpo.TryGetProperty("type", out _));
        if (validacao)
        {
            Assert.Equal(JsonValueKind.Object, corpo.GetProperty("errors").ValueKind);
        }

        return corpo;
    }
}
