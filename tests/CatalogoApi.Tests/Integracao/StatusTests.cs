using System.Net;
using System.Text;
using CatalogoApi.Domain;
using CatalogoApi.Tests.Suporte;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 2.4: desativar e reativar produto.</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class StatusTests(PostgresFixture fx)
{
    [Fact]
    public async Task Desativa_e_reativa_e_o_produto_continua_visivel()
    {
        var p = ApiHttp.NovoPrefixo();
        var criado = await fx.Client.CriarAsync($"{p} Garfo");

        var desativado = await fx.Client.AlterarStatusAsync(criado.Id, StatusProduto.Desativado);
        Assert.Equal(StatusProduto.Desativado, desativado.Status);
        Assert.True(desativado.AtualizadoEm > criado.AtualizadoEm);
        Assert.Equal(desativado, await (await fx.Client.GetAsync($"/produtos/{criado.Id}")).LerProdutoAsync());
        Assert.Equal(desativado, Assert.Single((await fx.Client.ListarAsync($"nome={p}")).Itens));

        var reativado = await fx.Client.AlterarStatusAsync(criado.Id, StatusProduto.Ativo);
        Assert.Equal(StatusProduto.Ativo, reativado.Status);
        Assert.True(reativado.AtualizadoEm > desativado.AtualizadoEm);
    }

    [Fact]
    public async Task Repetir_o_status_atual_retorna_200_sem_mudar_AtualizadoEm()
    {
        var criado = await fx.Client.CriarAsync($"{ApiHttp.NovoPrefixo()} Faca");

        var mesmo = await fx.Client.AlterarStatusAsync(criado.Id, StatusProduto.Ativo);

        Assert.Equal(criado, mesmo);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"status":1}""")]
    [InlineData("""{"status":"1"}""")]
    [InlineData("""{"status":"Xpto"}""")]
    [InlineData("""{"status":null}""")]
    public async Task Corpo_invalido_retorna_400(string corpo)
    {
        var criado = await fx.Client.CriarAsync($"{ApiHttp.NovoPrefixo()} Colher");
        using var conteudo = new StringContent(corpo, Encoding.UTF8, "application/json");

        var resposta = await fx.Client.PatchAsync($"/produtos/{criado.Id}/status", conteudo);

        await resposta.AssertProblemDetailsAsync(HttpStatusCode.BadRequest, validacao: true);
    }

    [Fact]
    public async Task Id_inexistente_retorna_404()
    {
        using var conteudo = new StringContent("""{"status":"Ativo"}""", Encoding.UTF8, "application/json");

        await (await fx.Client.PatchAsync("/produtos/2147483647/status", conteudo))
            .AssertProblemDetailsAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_nao_existe_e_retorna_405()
    {
        var criado = await fx.Client.CriarAsync($"{ApiHttp.NovoPrefixo()} Prato");

        await (await fx.Client.DeleteAsync($"/produtos/{criado.Id}")).AssertProblemDetailsAsync(HttpStatusCode.MethodNotAllowed);
        Assert.Equal(HttpStatusCode.OK, (await fx.Client.GetAsync($"/produtos/{criado.Id}")).StatusCode);
    }
}
