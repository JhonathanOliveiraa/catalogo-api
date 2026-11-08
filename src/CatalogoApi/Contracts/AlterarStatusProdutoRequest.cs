using System.ComponentModel.DataAnnotations;
using CatalogoApi.Domain;

namespace CatalogoApi.Contracts;

public sealed class AlterarStatusProdutoRequest
{
    [Required]
    public StatusProduto? Status { get; init; }
}
