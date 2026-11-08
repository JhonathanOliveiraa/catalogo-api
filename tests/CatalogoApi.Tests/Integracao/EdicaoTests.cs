using System.Net;
using System.Net.Http.Json;
using System.Text;
using CatalogoApi.Domain;
using CatalogoApi.Tests.Suporte;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 2.3: editar produto (substituição total).</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class EdicaoTests(PostgresFixture fx)
{
    [Fact]
    public async Task Put_valido_atualiza_avanca_AtualizadoEm_e_o_GET_seguinte_mostra_o_mesmo()
    {
        var criado = await fx.Client.CriarAsync($"{ApiHttp.NovoPrefixo()} Copo", 5m, 1, "Vidro");
        var novoNome = $"{ApiHttp.NovoPrefixo()} Copo grande";

        var resposta = await fx.Client.PutAsJsonAsync($"/produtos/{criado.Id}",
            new { nome = novoNome, descricao = "Cristal", preco = 7.5m, estoque = 4 });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var atualizado = await resposta.LerProdutoAsync();
        Assert.Equal(novoNome, atualizado.Nome);
        Assert.Equal("Cristal", atualizado.Descricao);
        Assert.Equal(7.5m, atualizado.Preco);
        Assert.Equal(4, atualizado.Estoque);
        Assert.Equal(criado.CriadoEm, atualizado.CriadoEm);
        Assert.True(atualizado.AtualizadoEm > criado.AtualizadoEm);
        Assert.Equal(atualizado, await (await fx.Client.GetAsync($"/produtos/{criado.Id}")).LerProdutoAsync());
    }

    [Fact]
    public async Task Put_sem_descricao_limpa_a_descricao_e_nao_mexe_no_status_de_produto_desativado()
    {
        var criado = await fx.Client.CriarAsync($"{ApiHttp.NovoPrefixo()} Prato", 5m, 1, "Fundo");
        await fx.Client.AlterarStatusAsync(criado.Id, StatusProduto.Desativado);

        var resposta = await fx.Client.PutAsJsonAsync($"/produtos/{criado.Id}",
            new { nome = criado.Nome, preco = 5m, estoque = 1, status = "Ativo" });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var atualizado = await resposta.LerProdutoAsync();
        Assert.Null(atualizado.Descricao);
        Assert.Equal(StatusProduto.Desativado, atualizado.Status);
    }

    [Fact]
    public async Task Put_em_id_inexistente_retorna_404() =>
        await (await fx.Client.PutAsJsonAsync("/produtos/2147483647", new { nome = "a", preco = 1, estoque = 1 }))
            .AssertProblemDetailsAsync(HttpStatusCode.NotFound);

    [Theory]
    [InlineData("""{"nome":"","preco":1,"estoque":1}""")]
    [InlineData("""{"nome":"a","preco":-1,"estoque":1}""")]
    [InlineData("""{"nome":"a","preco":1.001,"estoque":1}""")]
    [InlineData("""{"nome":"a","preco":1}""")]
    public async Task Put_invalido_retorna_400(string corpo)
    {
        var criado = await fx.Client.CriarAsync($"{ApiHttp.NovoPrefixo()} inválido");
        using var conteudo = new StringContent(corpo, Encoding.UTF8, "application/json");

        var resposta = await fx.Client.PutAsync($"/produtos/{criado.Id}", conteudo);

        await resposta.AssertProblemDetailsAsync(HttpStatusCode.BadRequest, validacao: true);
    }
}
