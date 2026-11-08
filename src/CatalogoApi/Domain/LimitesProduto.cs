namespace CatalogoApi.Domain;

/// <summary>Limites de AD-4, compartilhados pelos DTOs e pela configuração EF.</summary>
public static class LimitesProduto
{
    public const int NomeMaximo = 200;
    public const int DescricaoMaxima = 2000;
    public const string PrecoMaximo = "9999999999.99";
    public const int PrecoPrecisao = 12;
    public const int PrecoEscala = 2;
    public const int PageSizePadrao = 20;
    public const int PageSizeMaximo = 100;
}
