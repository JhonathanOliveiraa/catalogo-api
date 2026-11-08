using CatalogoApi.Domain;

namespace CatalogoApi.Repositories;

/// <summary>Só persiste e consulta <see cref="Produto"/>; não conhece DTO nem HTTP (AD-1, AD-2).</summary>
public interface IProdutoRepository
{
    Task AdicionarAsync(Produto produto, CancellationToken cancellationToken);

    /// <summary>Devolve o produto rastreado, pronto para ser alterado e salvo, ou null.</summary>
    Task<Produto?> ObterAsync(int id, CancellationToken cancellationToken);

    Task SalvarAsync(Produto produto, CancellationToken cancellationToken);

    Task<PaginaProdutos> ListarAsync(FiltroProdutos filtro, CancellationToken cancellationToken);
}
