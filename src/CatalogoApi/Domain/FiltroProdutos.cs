namespace CatalogoApi.Domain;

/// <summary>Critérios de listagem já validados, sem nenhum tipo HTTP (AD-2).</summary>
public sealed record FiltroProdutos(
    int Page,
    int PageSize,
    string? Nome,
    decimal? PrecoMin,
    decimal? PrecoMax,
    StatusProduto? Status,
    CampoOrdenacao OrdenarPor,
    DirecaoOrdenacao Direcao);

public sealed record PaginaProdutos(IReadOnlyList<Produto> Itens, int Total);
