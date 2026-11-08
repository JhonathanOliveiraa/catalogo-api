namespace CatalogoApi.Domain.Exceptions;

/// <summary>Traduzida para 404 pelo tratador único de erros (AD-3).</summary>
public sealed class ProdutoNaoEncontradoException(int id)
    : Exception($"Produto {id} não encontrado.")
{
    public int Id { get; } = id;
}
