namespace CatalogoApi.Domain.Exceptions;

/// <summary>Violação de regra que depende de estado; traduzida para 400 (AD-3, AD-4).</summary>
public sealed class RegraNegocioException(string mensagem) : Exception(mensagem);
