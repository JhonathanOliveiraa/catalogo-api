using CatalogoApi.Contracts;
using CatalogoApi.Domain;
using CatalogoApi.Domain.Exceptions;
using CatalogoApi.Services;
using CatalogoApi.Tests.Suporte;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogoApi.Tests.Unidade;

public sealed class ProdutoServiceTests
{
    // 123_456_7 ticks: a parte abaixo do microssegundo (7) precisa ser descartada
    private static readonly DateTimeOffset Inicio = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

    private readonly RepositorioEmMemoria _repositorio = new();
    private readonly RelogioFixo _relogio = new(Inicio);
    private readonly ProdutoService _service;

    public ProdutoServiceTests() =>
        _service = new ProdutoService(_repositorio, _relogio, NullLogger<ProdutoService>.Instance);

    private static CriarProdutoRequest NovoProduto(string? descricao = "Descrição") =>
        new() { Nome = "Caneca", Descricao = descricao, Preco = 19.90m, Estoque = 5 };

    [Fact]
    public async Task Criar_nasce_ativo_com_datas_iguais_em_utc_truncadas_a_microssegundos()
    {
        var produto = await _service.CriarAsync(NovoProduto(), CancellationToken.None);

        Assert.Equal(StatusProduto.Ativo, produto.Status);
        Assert.Equal(produto.CriadoEm, produto.AtualizadoEm);
        Assert.Equal(TimeSpan.Zero, produto.CriadoEm.Offset);
        Assert.Equal(0, produto.CriadoEm.UtcTicks % 10);
        Assert.Equal(Inicio.UtcTicks - 7, produto.CriadoEm.UtcTicks);
        Assert.Equal(1, produto.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Criar_com_descricao_vazia_grava_null(string? descricao)
    {
        var produto = await _service.CriarAsync(NovoProduto(descricao), CancellationToken.None);

        Assert.Null(produto.Descricao);
        Assert.Null(_repositorio.Produtos.Single().Descricao);
    }

    [Fact]
    public async Task Obter_inexistente_lanca_ProdutoNaoEncontrado()
    {
        var erro = await Assert.ThrowsAsync<ProdutoNaoEncontradoException>(
            () => _service.ObterAsync(42, CancellationToken.None));

        Assert.Equal(42, erro.Id);
    }

    [Fact]
    public async Task Atualizar_substitui_tudo_avanca_AtualizadoEm_e_preserva_status_e_CriadoEm()
    {
        var criado = await _service.CriarAsync(NovoProduto(), CancellationToken.None);
        await _service.AlterarStatusAsync(criado.Id, new() { Status = StatusProduto.Desativado }, CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromMinutes(1));

        var atualizado = await _service.AtualizarAsync(
            criado.Id,
            new AtualizarProdutoRequest { Nome = "Caneca grande", Preco = 25m, Estoque = 0 },
            CancellationToken.None);

        Assert.Equal("Caneca grande", atualizado.Nome);
        Assert.Null(atualizado.Descricao);
        Assert.Equal(25m, atualizado.Preco);
        Assert.Equal(0, atualizado.Estoque);
        Assert.Equal(StatusProduto.Desativado, atualizado.Status);
        Assert.Equal(criado.CriadoEm, atualizado.CriadoEm);
        Assert.True(atualizado.AtualizadoEm > criado.AtualizadoEm);
    }

    [Fact]
    public async Task Atualizar_inexistente_lanca_ProdutoNaoEncontrado() =>
        await Assert.ThrowsAsync<ProdutoNaoEncontradoException>(() => _service.AtualizarAsync(
            7, new AtualizarProdutoRequest { Nome = "X", Preco = 1m, Estoque = 1 }, CancellationToken.None));

    [Fact]
    public async Task AlterarStatus_para_o_mesmo_status_nao_altera_nada()
    {
        var criado = await _service.CriarAsync(NovoProduto(), CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromMinutes(1));

        var resultado = await _service.AlterarStatusAsync(
            criado.Id, new() { Status = StatusProduto.Ativo }, CancellationToken.None);

        Assert.Equal(criado, resultado);
        Assert.Equal(0, _repositorio.Salvamentos);
    }

    [Fact]
    public async Task AlterarStatus_desativa_e_reativa_avancando_AtualizadoEm()
    {
        var criado = await _service.CriarAsync(NovoProduto(), CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromSeconds(1));

        var desativado = await _service.AlterarStatusAsync(
            criado.Id, new() { Status = StatusProduto.Desativado }, CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        var reativado = await _service.AlterarStatusAsync(
            criado.Id, new() { Status = StatusProduto.Ativo }, CancellationToken.None);

        Assert.Equal(StatusProduto.Desativado, desativado.Status);
        Assert.True(desativado.AtualizadoEm > criado.AtualizadoEm);
        Assert.Equal(StatusProduto.Ativo, reativado.Status);
        Assert.True(reativado.AtualizadoEm > desativado.AtualizadoEm);
        Assert.Equal(2, _repositorio.Salvamentos);
    }

    [Fact]
    public async Task AlterarStatus_inexistente_lanca_ProdutoNaoEncontrado() =>
        await Assert.ThrowsAsync<ProdutoNaoEncontradoException>(() => _service.AlterarStatusAsync(
            9, new() { Status = StatusProduto.Ativo }, CancellationToken.None));

    [Fact]
    public async Task AlterarStatus_sem_status_lanca_RegraNegocio() =>
        await Assert.ThrowsAsync<RegraNegocioException>(() => _service.AlterarStatusAsync(
            1, new AlterarStatusProdutoRequest(), CancellationToken.None));

    [Fact]
    public async Task Listar_repassa_filtro_e_devolve_a_pagina_pedida()
    {
        await _service.CriarAsync(NovoProduto(), CancellationToken.None);

        var pagina = await _service.ListarAsync(new ListarProdutosQuery
        {
            Page = 2,
            PageSize = 5,
            Nome = "  cam ",
            PrecoMin = 1m,
            PrecoMax = 10m,
            Status = StatusProduto.Desativado,
            OrdenarPor = CampoOrdenacao.Preco,
            Direcao = DirecaoOrdenacao.Desc,
        }, CancellationToken.None);

        Assert.Equal(
            new FiltroProdutos(2, 5, "cam", 1m, 10m, StatusProduto.Desativado, CampoOrdenacao.Preco, DirecaoOrdenacao.Desc),
            _repositorio.UltimoFiltro);
        Assert.Equal(2, pagina.Page);
        Assert.Equal(5, pagina.PageSize);
        Assert.Equal(1, pagina.Total);
        Assert.Single(pagina.Itens);
    }

    [Fact]
    public async Task Listar_sem_parametros_usa_os_padroes_e_ignora_nome_em_branco()
    {
        await _service.ListarAsync(new ListarProdutosQuery { Nome = "   " }, CancellationToken.None);

        Assert.Equal(
            new FiltroProdutos(1, 20, null, null, null, null, CampoOrdenacao.Nome, DirecaoOrdenacao.Asc),
            _repositorio.UltimoFiltro);
    }
}
