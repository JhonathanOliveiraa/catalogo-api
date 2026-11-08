using System.Net;
using System.Net.Http.Json;
using System.Text;
using CatalogoApi.Domain;
using CatalogoApi.Tests.Suporte;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 2.1: cadastrar e consultar produto.</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class CadastroConsultaTests(PostgresFixture fx)
{
    [Fact]
    public async Task Post_valido_retorna_201_com_Location_e_produto_ativo()
    {
        var nome = $"{ApiHttp.NovoPrefixo()} Caneca";

        var resposta = await fx.Client.PostAsJsonAsync("/produtos",
            new { nome, descricao = "Azul", preco = 19.9m, estoque = 3 }, ApiHttp.Json);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var produto = await resposta.LerProdutoAsync();
        Assert.Equal($"/produtos/{produto.Id}", resposta.Headers.Location?.OriginalString);
        Assert.Equal(nome, produto.Nome);
        Assert.Equal("Azul", produto.Descricao);
        Assert.Equal(19.90m, produto.Preco);
        Assert.Equal(3, produto.Estoque);
        Assert.Equal(StatusProduto.Ativo, produto.Status);
        Assert.Equal(produto.CriadoEm, produto.AtualizadoEm);
        Assert.Equal(TimeSpan.Zero, produto.CriadoEm.Offset);
        Assert.Equal(0, produto.CriadoEm.UtcTicks % 10);
    }

    [Fact]
    public async Task Resposta_tem_todos_os_campos_com_descricao_null_presente()
    {
        var resposta = await fx.Client.PostAsJsonAsync("/produtos",
            new { nome = $"{ApiHttp.NovoPrefixo()} x", descricao = "", preco = 1, estoque = 0 });

        var json = await resposta.Content.ReadAsStringAsync();
        foreach (var campo in new[] { "\"id\":", "\"nome\":", "\"descricao\":null", "\"preco\":", "\"estoque\":", "\"status\":\"Ativo\"", "\"criadoEm\":", "\"atualizadoEm\":" })
        {
            Assert.Contains(campo, json, StringComparison.Ordinal);
        }
    }

    public static TheoryData<string> CorposInvalidos => new()
    {
        """{"preco":1,"estoque":1}""",
        """{"nome":"","preco":1,"estoque":1}""",
        $$"""{"nome":"{{new string('a', 201)}}","preco":1,"estoque":1}""",
        """{"nome":"a","preco":-1,"estoque":1}""",
        """{"nome":"a","preco":1.999,"estoque":1}""",
        """{"nome":"a","preco":10000000000,"estoque":1}""",
        """{"nome":"a","preco":1}""",
        """{"nome":"a","preco":1,"estoque":-1}""",
        """{"nome":"a","preco":1,"estoque":1.5}""",
        $$"""{"nome":"a","descricao":"{{new string('d', 2001)}}","preco":1,"estoque":1}""",
        """{"nome":"a\u0000b","preco":1,"estoque":1}""",
        """{"nome":"a","preco":"abc","estoque":1}""",
        """{"nome":"a","preco":1,"estoque":99999999999}""",
        """{nao e json""",
    };

    [Theory]
    [MemberData(nameof(CorposInvalidos))]
    public async Task Post_invalido_retorna_400_em_ValidationProblemDetails(string corpo)
    {
        using var conteudo = new StringContent(corpo, Encoding.UTF8, "application/json");

        var resposta = await fx.Client.PostAsync("/produtos", conteudo);

        await resposta.AssertProblemDetailsAsync(HttpStatusCode.BadRequest, validacao: true);
    }

    [Fact]
    public async Task Estoque_ausente_nao_vira_zero()
    {
        var resposta = await fx.Client.PostAsJsonAsync("/produtos", new { nome = "a", preco = 1 });

        var corpo = await resposta.AssertProblemDetailsAsync(HttpStatusCode.BadRequest, validacao: true);
        Assert.True(corpo.GetProperty("errors").TryGetProperty("Estoque", out _) ||
                    corpo.GetProperty("errors").TryGetProperty("estoque", out _));
    }

    [Fact]
    public async Task Get_por_id_devolve_o_mesmo_conteudo_do_post()
    {
        var criado = await fx.Client.CriarAsync($"{ApiHttp.NovoPrefixo()} Prato", 12.34m, 7, "Raso");

        var resposta = await fx.Client.GetAsync($"/produtos/{criado.Id}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(criado, await resposta.LerProdutoAsync());
    }

    [Theory]
    [InlineData("/produtos/2147483647")]
    [InlineData("/produtos/abc")]
    [InlineData("/produtos/99999999999")]
    public async Task Get_inexistente_ou_nao_numerico_retorna_404_em_ProblemDetails(string rota) =>
        await (await fx.Client.GetAsync(rota)).AssertProblemDetailsAsync(HttpStatusCode.NotFound);
}
