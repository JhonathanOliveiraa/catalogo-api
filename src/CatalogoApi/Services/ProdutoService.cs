using CatalogoApi.Contracts;
using CatalogoApi.Domain;
using CatalogoApi.Domain.Exceptions;
using CatalogoApi.Repositories;

namespace CatalogoApi.Services;

/// <summary>Única camada que muda estado, preenche datas e converte DTO ↔ entidade (AD-2, AD-6).</summary>
public sealed partial class ProdutoService(
    IProdutoRepository repository,
    TimeProvider timeProvider,
    ILogger<ProdutoService> logger) : IProdutoService
{
    public async Task<ProdutoResponse> CriarAsync(CriarProdutoRequest request, CancellationToken cancellationToken)
    {
        var agora = Agora();
        var produto = new Produto
        {
            Nome = ExigirNome(request),
            Status = StatusProduto.Ativo,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
        AplicarDados(produto, request);

        await repository.AdicionarAsync(produto, cancellationToken);
        LogCadastrado(logger, produto.Id);
        return ParaResponse(produto);
    }

    public async Task<ProdutoResponse> ObterAsync(int id, CancellationToken cancellationToken) =>
        ParaResponse(await ObterExistenteAsync(id, cancellationToken));

    public async Task<PaginaResponse<ProdutoResponse>> ListarAsync(
        ListarProdutosQuery query, CancellationToken cancellationToken)
    {
        var filtro = new FiltroProdutos(
            query.Page,
            query.PageSize,
            string.IsNullOrWhiteSpace(query.Nome) ? null : query.Nome.Trim(),
            query.PrecoMin,
            query.PrecoMax,
            query.Status,
            query.OrdenarPor,
            query.Direcao);

        var pagina = await repository.ListarAsync(filtro, cancellationToken);
        return new PaginaResponse<ProdutoResponse>(
            pagina.Itens.Select(ParaResponse).ToList(), pagina.Total, query.Page, query.PageSize);
    }

    public async Task<ProdutoResponse> AtualizarAsync(
        int id, AtualizarProdutoRequest request, CancellationToken cancellationToken)
    {
        var produto = await ObterExistenteAsync(id, cancellationToken);

        produto.Nome = ExigirNome(request);
        AplicarDados(produto, request);
        produto.AtualizadoEm = Agora();

        await repository.SalvarAsync(produto, cancellationToken);
        LogAtualizado(logger, produto.Id);
        return ParaResponse(produto);
    }

    public async Task<ProdutoResponse> AlterarStatusAsync(
        int id, AlterarStatusProdutoRequest request, CancellationToken cancellationToken)
    {
        var novoStatus = request.Status
            ?? throw new RegraNegocioException("O status é obrigatório.");
        var produto = await ObterExistenteAsync(id, cancellationToken);

        // repetir o status atual é idempotente: nada muda, nem AtualizadoEm (AD-5)
        if (produto.Status == novoStatus)
        {
            return ParaResponse(produto);
        }

        produto.Status = novoStatus;
        produto.AtualizadoEm = Agora();

        await repository.SalvarAsync(produto, cancellationToken);
        LogStatusAlterado(logger, produto.Id, novoStatus);
        return ParaResponse(produto);
    }

    private async Task<Produto> ObterExistenteAsync(int id, CancellationToken cancellationToken) =>
        await repository.ObterAsync(id, cancellationToken) ?? throw new ProdutoNaoEncontradoException(id);

    /// <summary>UTC truncado a microssegundos, a precisão do timestamptz do PostgreSQL.</summary>
    private DateTimeOffset Agora()
    {
        var agora = timeProvider.GetUtcNow();
        return new DateTimeOffset(agora.UtcTicks - (agora.UtcTicks % 10), TimeSpan.Zero);
    }

    // Os DTOs já garantem presença e faixa (AD-4); estas checagens só protegem
    // chamadas diretas ao Service que pulem a validação do MVC.
    private static string ExigirNome(IDadosProduto dados) =>
        string.IsNullOrWhiteSpace(dados.Nome)
            ? throw new RegraNegocioException("O nome é obrigatório.")
            : dados.Nome;

    private static void AplicarDados(Produto produto, IDadosProduto dados)
    {
        produto.Descricao = string.IsNullOrWhiteSpace(dados.Descricao) ? null : dados.Descricao;
        produto.Preco = dados.Preco ?? throw new RegraNegocioException("O preço é obrigatório.");
        produto.Estoque = dados.Estoque ?? throw new RegraNegocioException("O estoque é obrigatório.");
    }

    internal static ProdutoResponse ParaResponse(Produto p) =>
        new(p.Id, p.Nome, p.Descricao, p.Preco, p.Estoque, p.Status, p.CriadoEm, p.AtualizadoEm);

    [LoggerMessage(Level = LogLevel.Information, Message = "Produto {ProdutoId} cadastrado")]
    private static partial void LogCadastrado(ILogger logger, int produtoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Produto {ProdutoId} atualizado")]
    private static partial void LogAtualizado(ILogger logger, int produtoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Produto {ProdutoId} passou para {Status}")]
    private static partial void LogStatusAlterado(ILogger logger, int produtoId, StatusProduto status);
}
