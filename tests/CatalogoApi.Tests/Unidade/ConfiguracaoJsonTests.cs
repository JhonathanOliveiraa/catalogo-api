using System.Text.Json;
using CatalogoApi.Contracts;
using CatalogoApi.Domain;
using CatalogoApi.Infrastructure;

namespace CatalogoApi.Tests.Unidade;

public sealed class ConfiguracaoJsonTests
{
    private static readonly JsonSerializerOptions Opcoes = Criar();

    private static JsonSerializerOptions Criar()
    {
        var opcoes = new JsonSerializerOptions();
        ConfiguracaoJson.Aplicar(opcoes);
        return opcoes;
    }

    [Fact]
    public void Serializa_camelCase_com_enum_pelo_nome_exato_e_descricao_null_presente()
    {
        var json = JsonSerializer.Serialize(
            new ProdutoResponse(1, "Caneca", null, 10.5m, 3, StatusProduto.Desativado,
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
            Opcoes);

        Assert.Contains("\"status\":\"Desativado\"", json, StringComparison.Ordinal);
        Assert.Contains("\"descricao\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"atualizadoEm\":", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"Desativado\"")]
    [InlineData("\"desativado\"")]
    [InlineData("\"DESATIVADO\"")]
    public void Le_enum_pelo_nome_sem_diferenciar_maiusculas(string valor)
    {
        var request = JsonSerializer.Deserialize<AlterarStatusProdutoRequest>($"{{\"status\":{valor}}}", Opcoes);

        Assert.Equal(StatusProduto.Desativado, request!.Status);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"1\"")]
    [InlineData("\"Xpto\"")]
    public void Rejeita_enum_numerico_ou_fora_do_dominio(string valor) =>
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AlterarStatusProdutoRequest>($"{{\"status\":{valor}}}", Opcoes));

    [Fact]
    public void Ignora_membros_desconhecidos()
    {
        var request = JsonSerializer.Deserialize<CriarProdutoRequest>(
            """{"nome":"A","preco":1,"estoque":2,"id":99,"criadoEm":"2020-01-01T00:00:00Z","status":"Desativado"}""",
            Opcoes);

        Assert.Equal("A", request!.Nome);
    }
}
