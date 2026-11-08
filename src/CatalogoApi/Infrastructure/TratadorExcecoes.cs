using CatalogoApi.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CatalogoApi.Infrastructure;

/// <summary>
/// Caminho único de erro (AD-3): exceções de domínio viram 404/400 e qualquer outra
/// vira 500, sempre em ProblemDetails (RFC 9457) e sem stack trace no corpo.
/// </summary>
public sealed partial class TratadorExcecoes(
    IProblemDetailsService problemDetailsService,
    ILogger<TratadorExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // cliente desistiu; não há para quem responder
            LogRequisicaoCancelada(logger, httpContext.Request.Method, httpContext.Request.Path);
            return true;
        }

        var (status, titulo, detalhe) = exception switch
        {
            ProdutoNaoEncontradoException e => (StatusCodes.Status404NotFound, "Recurso não encontrado", e.Message),
            RegraNegocioException e => (StatusCodes.Status400BadRequest, "Requisição inválida", e.Message),
            BadHttpRequestException e => (e.StatusCode, "Requisição inválida", "A requisição não pôde ser lida."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado ao processar a requisição."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogErroNaoTratado(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = detalhe,
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado em {Metodo} {Caminho}")]
    private static partial void LogErroNaoTratado(ILogger logger, Exception exception, string metodo, PathString caminho);

    [LoggerMessage(Level = LogLevel.Information, Message = "Requisição {Metodo} {Caminho} cancelada pelo cliente")]
    private static partial void LogRequisicaoCancelada(ILogger logger, string metodo, PathString caminho);
}
