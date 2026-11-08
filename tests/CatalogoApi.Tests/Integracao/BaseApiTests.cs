using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using CatalogoApi.Contracts;
using CatalogoApi.Services;
using CatalogoApi.Tests.Suporte;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 1.1: base da API com erros padronizados.</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class BaseApiTests(PostgresFixture fx)
{
    [Fact]
    public async Task Rota_inexistente_retorna_404_em_ProblemDetails() =>
        await (await fx.Client.GetAsync("/nao-existe")).AssertProblemDetailsAsync(HttpStatusCode.NotFound);

    [Fact]
    public async Task Metodo_nao_permitido_retorna_405_em_ProblemDetails() =>
        await (await fx.Client.DeleteAsync("/produtos/1")).AssertProblemDetailsAsync(HttpStatusCode.MethodNotAllowed);

    [Fact]
    public async Task Content_type_nao_suportado_retorna_415_em_ProblemDetails()
    {
        using var corpo = new StringContent("nome=x", Encoding.UTF8, "text/plain");

        await (await fx.Client.PostAsync("/produtos", corpo)).AssertProblemDetailsAsync(HttpStatusCode.UnsupportedMediaType);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/openapi/v1.json")]
    public async Task Liveness_e_OpenAPI_respondem_200(string rota) =>
        Assert.Equal(HttpStatusCode.OK, (await fx.Client.GetAsync(rota)).StatusCode);

    [Fact]
    public async Task Excecao_nao_tratada_vira_500_sem_stack_trace_e_e_logada()
    {
        var logs = new ColetorLogs();
        await using var factory = new CatalogoApiFactory(fx.ConnectionString, servicos: s =>
        {
            s.AddScoped<IProdutoService, ServiceQueFalha>();
            s.AddSingleton<ILoggerProvider>(logs);
        });
        using var client = factory.CreateClient();

        var resposta = await client.GetAsync("/produtos/1");

        var corpo = await resposta.AssertProblemDetailsAsync(HttpStatusCode.InternalServerError);
        var texto = corpo.GetRawText();
        Assert.DoesNotContain(ServiceQueFalha.Segredo, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", texto, StringComparison.Ordinal);
        Assert.Contains(logs.Entradas, e => e.Nivel == LogLevel.Error && e.Excecao is InvalidOperationException);
    }

    [Fact]
    public void AddJsonOptions_e_ConfigureHttpJsonOptions_usam_a_mesma_configuracao()
    {
        var mvc = fx.Factory.Services.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions;
        var http = fx.Factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        foreach (var opcoes in new[] { mvc, http })
        {
            Assert.Equal(System.Text.Json.JsonNamingPolicy.CamelCase, opcoes.PropertyNamingPolicy);
            Assert.Contains(opcoes.Converters, c => c is JsonStringEnumConverter);
        }

        var exemplo = new AlterarStatusProdutoRequest { Status = Domain.StatusProduto.Desativado };
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(exemplo, mvc),
            System.Text.Json.JsonSerializer.Serialize(exemplo, http));
    }

    private sealed class ServiceQueFalha : IProdutoService
    {
        public const string Segredo = "detalhe-interno-que-nao-pode-vazar";

        private static Task<T> Falhar<T>() => throw new InvalidOperationException(Segredo);

        public Task<ProdutoResponse> CriarAsync(CriarProdutoRequest request, CancellationToken cancellationToken) => Falhar<ProdutoResponse>();
        public Task<ProdutoResponse> ObterAsync(int id, CancellationToken cancellationToken) => Falhar<ProdutoResponse>();
        public Task<PaginaResponse<ProdutoResponse>> ListarAsync(ListarProdutosQuery query, CancellationToken cancellationToken) => Falhar<PaginaResponse<ProdutoResponse>>();
        public Task<ProdutoResponse> AtualizarAsync(int id, AtualizarProdutoRequest request, CancellationToken cancellationToken) => Falhar<ProdutoResponse>();
        public Task<ProdutoResponse> AlterarStatusAsync(int id, AlterarStatusProdutoRequest request, CancellationToken cancellationToken) => Falhar<ProdutoResponse>();
    }
}
