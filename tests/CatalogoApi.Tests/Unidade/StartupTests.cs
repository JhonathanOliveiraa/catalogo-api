using CatalogoApi.Tests.Suporte;

namespace CatalogoApi.Tests.Unidade;

/// <summary>
/// Configuração obrigatória é validada antes de tocar no banco, então estes casos
/// não precisam de PostgreSQL (AD-8).
/// </summary>
public sealed class StartupTests
{
    private static Exception SubirFalhando(string? connectionString, string? origens)
    {
        using var factory = new CatalogoApiFactory(connectionString, origens);
        var erro = Record.Exception(() => factory.CreateClient());
        Assert.NotNull(erro);
        return erro;
    }

    private static string Mensagens(Exception erro)
    {
        var partes = new List<string>();
        for (var e = erro; e is not null; e = e.InnerException)
        {
            partes.Add(e.Message);
        }

        return string.Join(" | ", partes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" , ")]
    [InlineData("*")]
    public void Startup_falha_com_CORS_vazio_ou_curinga(string origens)
    {
        var erro = SubirFalhando("Host=nao-usado", origens);

        Assert.Contains("Cors__OrigensPermitidas", Mensagens(erro), StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_falha_sem_connection_string()
    {
        var erro = SubirFalhando(connectionString: null, CatalogoApiFactory.OrigemFront);

        Assert.Contains("ConnectionStrings__Catalogo", Mensagens(erro), StringComparison.Ordinal);
    }
}
