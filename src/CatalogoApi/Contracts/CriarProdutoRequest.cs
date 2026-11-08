using System.ComponentModel.DataAnnotations;
using CatalogoApi.Contracts.Validacao;
using CatalogoApi.Domain;

namespace CatalogoApi.Contracts;

public sealed class CriarProdutoRequest : IDadosProduto
{
    [Required]
    [StringLength(LimitesProduto.NomeMaximo, MinimumLength = 1)]
    [SemCaractereNulo]
    public string? Nome { get; init; }

    [StringLength(LimitesProduto.DescricaoMaxima)]
    [SemCaractereNulo]
    public string? Descricao { get; init; }

    [Required]
    [Range(typeof(decimal), "0", LimitesProduto.PrecoMaximo,
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    [CasasDecimais(LimitesProduto.PrecoEscala)]
    public decimal? Preco { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? Estoque { get; init; }
}
