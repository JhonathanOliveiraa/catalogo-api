using System.ComponentModel.DataAnnotations;
using CatalogoApi.Contracts.Validacao;
using CatalogoApi.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CatalogoApi.Contracts;

public sealed class ListarProdutosQuery : IValidatableObject
{
    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [FromQuery(Name = "pageSize")]
    [Range(1, LimitesProduto.PageSizeMaximo)]
    public int PageSize { get; init; } = LimitesProduto.PageSizePadrao;

    [FromQuery(Name = "nome")]
    [SemCaractereNulo]
    public string? Nome { get; init; }

    [FromQuery(Name = "precoMin")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? PrecoMin { get; init; }

    [FromQuery(Name = "precoMax")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? PrecoMax { get; init; }

    [FromQuery(Name = "status")]
    public StatusProduto? Status { get; init; }

    [FromQuery(Name = "ordenarPor")]
    public CampoOrdenacao OrdenarPor { get; init; } = CampoOrdenacao.Nome;

    [FromQuery(Name = "direcao")]
    public DirecaoOrdenacao Direcao { get; init; } = DirecaoOrdenacao.Asc;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PrecoMin is { } min && PrecoMax is { } max && min > max)
        {
            yield return new ValidationResult(
                "precoMin não pode ser maior que precoMax.",
                ["precoMin", "precoMax"]);
        }
    }
}
