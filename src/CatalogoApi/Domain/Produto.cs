namespace CatalogoApi.Domain;

/// <summary>Entidade única do catálogo. Só o Service altera estado e datas (AD-6).</summary>
public class Produto
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public int Estoque { get; set; }
    public StatusProduto Status { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
}
