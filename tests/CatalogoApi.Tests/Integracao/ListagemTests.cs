using System.Net;
using System.Text.Json;
using CatalogoApi.Domain;
using CatalogoApi.Tests.Suporte;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 2.2: listagem com paginação, filtros e ordenação.</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class ListagemTests(PostgresFixture fx)
{
    [Fact]
    public async Task Sem_parametros_usa_os_padroes_e_traz_ativos_e_desativados_ordenados_por_nome()
    {
        var p = ApiHttp.NovoPrefixo();
        var c = await fx.Client.CriarAsync($"{p} C");
        var a = await fx.Client.CriarAsync($"{p} A");
        var b = await fx.Client.CriarAsync($"{p} B");
        await fx.Client.AlterarStatusAsync(b.Id, StatusProduto.Desativado);

        var tudo = await fx.Client.ListarAsync("");
        Assert.Equal(1, tudo.Page);
        Assert.Equal(20, tudo.PageSize);
        Assert.True(tudo.Total >= 3);

        var meus = await fx.Client.ListarAsync($"nome={p}");
        Assert.Equal([a.Id, b.Id, c.Id], meus.Itens.Select(i => i.Id));
        Assert.Equal(3, meus.Total);
        Assert.Contains(meus.Itens, i => i.Status == StatusProduto.Desativado);
    }

    [Fact]
    public async Task Nome_contem_sem_diferenciar_maiusculas()
    {
        var p = ApiHttp.NovoPrefixo();
        var camiseta = await fx.Client.CriarAsync($"{p}-Camiseta");
        await fx.Client.CriarAsync($"{p}-Caneca");

        var resultado = await fx.Client.ListarAsync($"nome={p.ToUpperInvariant()}-CAMI");

        Assert.Equal(camiseta.Id, Assert.Single(resultado.Itens).Id);
    }

    [Fact]
    public async Task Curingas_do_LIKE_no_termo_sao_texto_literal()
    {
        var p = ApiHttp.NovoPrefixo();
        var percentual = await fx.Client.CriarAsync($"{p}-50% off");
        await fx.Client.CriarAsync($"{p}-500 off");
        var sublinhado = await fx.Client.CriarAsync($"{p}-a_b");
        await fx.Client.CriarAsync($"{p}-axb");

        Assert.Equal(percentual.Id, Assert.Single((await fx.Client.ListarAsync($"nome={Uri.EscapeDataString($"{p}-50%")}")).Itens).Id);
        Assert.Equal(sublinhado.Id, Assert.Single((await fx.Client.ListarAsync($"nome={p}-a_b")).Itens).Id);
    }

    [Fact]
    public async Task Limites_de_preco_sao_inclusivos()
    {
        var p = ApiHttp.NovoPrefixo();
        var dez = await fx.Client.CriarAsync($"{p} 1", 10m);
        var vinte = await fx.Client.CriarAsync($"{p} 2", 20m);
        await fx.Client.CriarAsync($"{p} 3", 20.01m);
        await fx.Client.CriarAsync($"{p} 0", 9.99m);

        var resultado = await fx.Client.ListarAsync($"nome={p}&precoMin=10&precoMax=20");

        Assert.Equal([dez.Id, vinte.Id], resultado.Itens.Select(i => i.Id));
        Assert.Equal(2, resultado.Total);
    }

    [Theory]
    [InlineData("Desativado")]
    [InlineData("desativado")]
    public async Task Filtro_de_status_traz_so_o_status_pedido(string status)
    {
        var p = ApiHttp.NovoPrefixo();
        await fx.Client.CriarAsync($"{p} A");
        var b = await fx.Client.CriarAsync($"{p} B");
        await fx.Client.AlterarStatusAsync(b.Id, StatusProduto.Desativado);

        var resultado = await fx.Client.ListarAsync($"nome={p}&status={status}");

        Assert.Equal(b.Id, Assert.Single(resultado.Itens).Id);
    }

    [Fact]
    public async Task Ordenacao_e_aplicada_e_desempata_por_id_crescente()
    {
        var p = ApiHttp.NovoPrefixo();
        var x = await fx.Client.CriarAsync($"{p} X", 5m);
        var y = await fx.Client.CriarAsync($"{p} Y", 5m);
        var z = await fx.Client.CriarAsync($"{p} Z", 1m);

        var porPrecoDesc = await fx.Client.ListarAsync($"nome={p}&ordenarPor=Preco&direcao=Desc");
        Assert.Equal([x.Id, y.Id, z.Id], porPrecoDesc.Itens.Select(i => i.Id));

        var porPrecoAsc = await fx.Client.ListarAsync($"nome={p}&ordenarPor=preco&direcao=asc");
        Assert.Equal([z.Id, x.Id, y.Id], porPrecoAsc.Itens.Select(i => i.Id));

        var porCriacaoDesc = await fx.Client.ListarAsync($"nome={p}&ordenarPor=CriadoEm&direcao=Desc");
        Assert.Equal([z.Id, y.Id, x.Id], porCriacaoDesc.Itens.Select(i => i.Id));

        var porNomeDesc = await fx.Client.ListarAsync($"nome={p}&direcao=Desc");
        Assert.Equal([z.Id, y.Id, x.Id], porNomeDesc.Itens.Select(i => i.Id));
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("page=abc")]
    [InlineData("ordenarPor=Xpto")]
    [InlineData("ordenarPor=1")]
    [InlineData("direcao=1")]
    [InlineData("status=1")]
    [InlineData("status=Xpto")]
    [InlineData("precoMin=10&precoMax=5")]
    [InlineData("precoMin=-1")]
    public async Task Parametro_invalido_retorna_400_em_ProblemDetails(string query) =>
        await (await fx.Client.GetAsync($"/produtos?{query}")).AssertProblemDetailsAsync(HttpStatusCode.BadRequest, validacao: true);

    [Fact]
    public async Task Total_conta_depois_dos_filtros_e_pagina_alem_do_fim_vem_vazia()
    {
        var p = ApiHttp.NovoPrefixo();
        for (var i = 0; i < 3; i++)
        {
            await fx.Client.CriarAsync($"{p} {i}");
        }

        var segunda = await fx.Client.ListarAsync($"nome={p}&pageSize=2&page=2");
        Assert.Single(segunda.Itens);
        Assert.Equal(3, segunda.Total);
        Assert.Equal(2, segunda.Page);
        Assert.Equal(2, segunda.PageSize);

        var alem = await fx.Client.ListarAsync($"nome={p}&pageSize=2&page=5");
        Assert.Empty(alem.Itens);
        Assert.Equal(3, alem.Total);

        var extremo = await fx.Client.ListarAsync($"nome={p}&pageSize=100&page={int.MaxValue}");
        Assert.Empty(extremo.Itens);
    }

    [Fact]
    public async Task OpenAPI_descreve_os_enums_como_string()
    {
        var resposta = await fx.Client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        var enums = new List<JsonElement>();
        ColetarEnums(doc.RootElement, enums);

        foreach (var esperado in new[]
                 {
                     Enum.GetNames<CampoOrdenacao>(),
                     Enum.GetNames<DirecaoOrdenacao>(),
                     Enum.GetNames<StatusProduto>(),
                 })
        {
            Assert.Contains(enums, e => esperado.All(nome =>
                e.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.String && v.GetString() == nome)));
        }

        // nenhum enum do documento é numérico
        Assert.All(enums, e => Assert.All(e.EnumerateArray(),
            v => Assert.True(v.ValueKind is JsonValueKind.String or JsonValueKind.Null)));
    }

    private static void ColetarEnums(JsonElement elemento, List<JsonElement> enums)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propriedade in elemento.EnumerateObject())
                {
                    if (propriedade.Name == "enum" && propriedade.Value.ValueKind == JsonValueKind.Array)
                    {
                        enums.Add(propriedade.Value);
                    }
                    else
                    {
                        ColetarEnums(propriedade.Value, enums);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in elemento.EnumerateArray())
                {
                    ColetarEnums(item, enums);
                }

                break;
        }
    }
}
