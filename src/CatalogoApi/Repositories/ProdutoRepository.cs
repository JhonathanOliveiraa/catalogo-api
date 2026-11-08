using CatalogoApi.Data;
using CatalogoApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace CatalogoApi.Repositories;

public sealed class ProdutoRepository(CatalogoDbContext db) : IProdutoRepository
{
    public async Task AdicionarAsync(Produto produto, CancellationToken cancellationToken)
    {
        db.Produtos.Add(produto);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Produto?> ObterAsync(int id, CancellationToken cancellationToken) =>
        db.Produtos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task SalvarAsync(Produto produto, CancellationToken cancellationToken)
    {
        if (db.Entry(produto).State == EntityState.Detached)
        {
            db.Produtos.Update(produto);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginaProdutos> ListarAsync(FiltroProdutos filtro, CancellationToken cancellationToken)
    {
        var consulta = db.Produtos.AsNoTracking();

        if (!string.IsNullOrEmpty(filtro.Nome))
        {
            var padrao = $"%{EscaparLike(filtro.Nome)}%";
            consulta = consulta.Where(p => EF.Functions.ILike(p.Nome, padrao, @"\"));
        }

        if (filtro.PrecoMin is { } precoMin)
        {
            consulta = consulta.Where(p => p.Preco >= precoMin);
        }

        if (filtro.PrecoMax is { } precoMax)
        {
            consulta = consulta.Where(p => p.Preco <= precoMax);
        }

        if (filtro.Status is { } status)
        {
            consulta = consulta.Where(p => p.Status == status);
        }

        var total = await consulta.CountAsync(cancellationToken);

        // page vai até int.MaxValue: o deslocamento é calculado em long para não estourar.
        var deslocamento = (long)(filtro.Page - 1) * filtro.PageSize;
        if (total == 0 || deslocamento >= total)
        {
            return new PaginaProdutos([], total);
        }

        var itens = await Ordenar(consulta, filtro.OrdenarPor, filtro.Direcao)
            .Skip((int)deslocamento)
            .Take(filtro.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginaProdutos(itens, total);
    }

    private static IQueryable<Produto> Ordenar(
        IQueryable<Produto> consulta, CampoOrdenacao campo, DirecaoOrdenacao direcao)
    {
        var ordenada = (campo, direcao) switch
        {
            (CampoOrdenacao.Preco, DirecaoOrdenacao.Asc) => consulta.OrderBy(p => p.Preco),
            (CampoOrdenacao.Preco, DirecaoOrdenacao.Desc) => consulta.OrderByDescending(p => p.Preco),
            (CampoOrdenacao.CriadoEm, DirecaoOrdenacao.Asc) => consulta.OrderBy(p => p.CriadoEm),
            (CampoOrdenacao.CriadoEm, DirecaoOrdenacao.Desc) => consulta.OrderByDescending(p => p.CriadoEm),
            (CampoOrdenacao.Nome, DirecaoOrdenacao.Desc) => consulta.OrderByDescending(p => p.Nome),
            _ => consulta.OrderBy(p => p.Nome),
        };

        // desempate estável por Id crescente, qualquer que seja a direção
        return ordenada.ThenBy(p => p.Id);
    }

    /// <summary>Faz '%', '_' e '\' do termo valerem como texto literal no ILIKE.</summary>
    internal static string EscaparLike(string termo) =>
        termo.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
