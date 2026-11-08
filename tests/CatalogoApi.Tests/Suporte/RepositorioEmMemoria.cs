using CatalogoApi.Domain;
using CatalogoApi.Repositories;

namespace CatalogoApi.Tests.Suporte;

/// <summary>Repository simulado para os testes de unidade do Service (AD-12).</summary>
public sealed class RepositorioEmMemoria : IProdutoRepository
{
    private int _proximoId = 1;

    public List<Produto> Produtos { get; } = [];
    public int Salvamentos { get; private set; }
    public FiltroProdutos? UltimoFiltro { get; private set; }

    public Task AdicionarAsync(Produto produto, CancellationToken cancellationToken)
    {
        produto.Id = _proximoId++;
        Produtos.Add(produto);
        return Task.CompletedTask;
    }

    public Task<Produto?> ObterAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(Produtos.FirstOrDefault(p => p.Id == id));

    public Task SalvarAsync(Produto produto, CancellationToken cancellationToken)
    {
        Salvamentos++;
        return Task.CompletedTask;
    }

    public Task<PaginaProdutos> ListarAsync(FiltroProdutos filtro, CancellationToken cancellationToken)
    {
        UltimoFiltro = filtro;
        return Task.FromResult(new PaginaProdutos(Produtos.ToList(), Produtos.Count));
    }
}
