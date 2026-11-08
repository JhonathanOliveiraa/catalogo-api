using CatalogoApi.Contracts;

namespace CatalogoApi.Services;

public interface IProdutoService
{
    Task<ProdutoResponse> CriarAsync(CriarProdutoRequest request, CancellationToken cancellationToken);
    Task<ProdutoResponse> ObterAsync(int id, CancellationToken cancellationToken);
    Task<PaginaResponse<ProdutoResponse>> ListarAsync(ListarProdutosQuery query, CancellationToken cancellationToken);
    Task<ProdutoResponse> AtualizarAsync(int id, AtualizarProdutoRequest request, CancellationToken cancellationToken);
    Task<ProdutoResponse> AlterarStatusAsync(int id, AlterarStatusProdutoRequest request, CancellationToken cancellationToken);
}
