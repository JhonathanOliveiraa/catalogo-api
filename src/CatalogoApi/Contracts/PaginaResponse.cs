namespace CatalogoApi.Contracts;

public sealed record PaginaResponse<T>(IReadOnlyList<T> Itens, int Total, int Page, int PageSize);
