namespace CatalogoApi.Contracts;

/// <summary>Campos editáveis comuns ao cadastro e à edição.</summary>
public interface IDadosProduto
{
    string? Nome { get; }
    string? Descricao { get; }
    decimal? Preco { get; }
    int? Estoque { get; }
}
