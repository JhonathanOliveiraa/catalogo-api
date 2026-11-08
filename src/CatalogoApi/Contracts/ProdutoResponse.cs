using CatalogoApi.Domain;

namespace CatalogoApi.Contracts;

public sealed record ProdutoResponse(
    int Id,
    string Nome,
    string? Descricao,
    decimal Preco,
    int Estoque,
    StatusProduto Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm);
