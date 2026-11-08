using CatalogoApi.Contracts;
using CatalogoApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogoApi.Controllers;

/// <summary>Recebe, delega ao Service e devolve; sem regra de negócio nem try/catch (AD-1, AD-3).</summary>
[ApiController]
[Route("produtos")]
[Produces("application/json")]
public sealed class ProdutosController(IProdutoService service) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ProdutoResponse>> Criar(
        CriarProdutoRequest request, CancellationToken cancellationToken)
    {
        var produto = await service.CriarAsync(request, cancellationToken);
        return Created($"/produtos/{produto.Id}", produto);
    }

    [HttpGet]
    [ProducesResponseType<PaginaResponse<ProdutoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PaginaResponse<ProdutoResponse>> Listar(
        [FromQuery] ListarProdutosQuery query, CancellationToken cancellationToken) =>
        service.ListarAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ProdutoResponse> Obter(int id, CancellationToken cancellationToken) =>
        service.ObterAsync(id, cancellationToken);

    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ProdutoResponse> Atualizar(
        int id, AtualizarProdutoRequest request, CancellationToken cancellationToken) =>
        service.AtualizarAsync(id, request, cancellationToken);

    [HttpPatch("{id:int}/status")]
    [Consumes("application/json")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ProdutoResponse> AlterarStatus(
        int id, AlterarStatusProdutoRequest request, CancellationToken cancellationToken) =>
        service.AlterarStatusAsync(id, request, cancellationToken);
}
