using System.ComponentModel.DataAnnotations;
using CatalogoApi.Contracts;

namespace CatalogoApi.Tests.Unidade;

public sealed class ValidacaoContratosTests
{
    private static List<ValidationResult> Validar(object instancia)
    {
        var erros = new List<ValidationResult>();
        Validator.TryValidateObject(instancia, new ValidationContext(instancia), erros, validateAllProperties: true);
        return erros;
    }

    private static CriarProdutoRequest Valido(
        string? nome = "Caneca", string? descricao = null, decimal? preco = 10m, int? estoque = 1) =>
        new() { Nome = nome, Descricao = descricao, Preco = preco, Estoque = estoque };

    [Fact]
    public void Produto_valido_nos_limites_passa()
    {
        Assert.Empty(Validar(Valido(nome: new string('a', 200), descricao: new string('d', 2000), preco: 9_999_999_999.99m, estoque: int.MaxValue)));
        Assert.Empty(Validar(Valido(preco: 0m, estoque: 0)));
    }

    public static TheoryData<CriarProdutoRequest, string> Invalidos => new()
    {
        { Valido(nome: null), nameof(CriarProdutoRequest.Nome) },
        { Valido(nome: ""), nameof(CriarProdutoRequest.Nome) },
        { Valido(nome: "   "), nameof(CriarProdutoRequest.Nome) },
        { Valido(nome: new string('a', 201)), nameof(CriarProdutoRequest.Nome) },
        { Valido(nome: "a\0b"), nameof(CriarProdutoRequest.Nome) },
        { Valido(descricao: new string('d', 2001)), nameof(CriarProdutoRequest.Descricao) },
        { Valido(preco: null), nameof(CriarProdutoRequest.Preco) },
        { Valido(preco: -0.01m), nameof(CriarProdutoRequest.Preco) },
        { Valido(preco: 1.999m), nameof(CriarProdutoRequest.Preco) },
        { Valido(preco: 10_000_000_000m), nameof(CriarProdutoRequest.Preco) },
        { Valido(estoque: null), nameof(CriarProdutoRequest.Estoque) },
        { Valido(estoque: -1), nameof(CriarProdutoRequest.Estoque) },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public void Produto_invalido_aponta_o_campo(CriarProdutoRequest request, string campo)
    {
        var erros = Validar(request);

        Assert.Contains(erros, e => e.MemberNames.Contains(campo));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Listagem_com_paginacao_fora_da_faixa_e_invalida(int page, int pageSize) =>
        Assert.NotEmpty(Validar(new ListarProdutosQuery { Page = page, PageSize = pageSize }));

    [Fact]
    public void Listagem_com_precoMin_maior_que_precoMax_e_invalida() =>
        Assert.NotEmpty(Validar(new ListarProdutosQuery { PrecoMin = 10m, PrecoMax = 5m }));

    [Fact]
    public void Listagem_com_preco_negativo_e_invalida() =>
        Assert.NotEmpty(Validar(new ListarProdutosQuery { PrecoMin = -1m }));

    [Fact]
    public void Listagem_com_precos_iguais_e_valida() =>
        Assert.Empty(Validar(new ListarProdutosQuery { PrecoMin = 5m, PrecoMax = 5m, Page = int.MaxValue, PageSize = 100 }));

    [Fact]
    public void AlterarStatus_sem_status_e_invalido() =>
        Assert.NotEmpty(Validar(new AlterarStatusProdutoRequest()));
}
