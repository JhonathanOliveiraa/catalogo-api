using System.Globalization;
using CatalogoApi.Domain;
using CatalogoApi.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace CatalogoApi.Tests.Unidade;

public sealed class EnumPorNomeModelBinderTests
{
    private static async Task<DefaultModelBindingContext> BindAsync<T>(string nome, string? valor)
    {
        var query = valor is null
            ? new QueryCollection()
            : new QueryCollection(new Dictionary<string, StringValues> { [nome] = valor });

        var contexto = new DefaultModelBindingContext
        {
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(T)),
            ModelName = nome,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, query, CultureInfo.InvariantCulture),
        };

        await new EnumPorNomeModelBinder().BindModelAsync(contexto);
        return contexto;
    }

    [Theory]
    [InlineData("Desativado")]
    [InlineData("desativado")]
    [InlineData(" DESATIVADO ")]
    public async Task Aceita_o_nome_sem_diferenciar_maiusculas(string valor)
    {
        var contexto = await BindAsync<StatusProduto?>("status", valor);

        Assert.True(contexto.Result.IsModelSet);
        Assert.Equal(StatusProduto.Desativado, contexto.Result.Model);
        Assert.Equal(0, contexto.ModelState.ErrorCount);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("Xpto")]
    [InlineData("Ativo,Desativado")]
    public async Task Rejeita_numero_combinacao_ou_valor_fora_do_enum(string valor)
    {
        var contexto = await BindAsync<StatusProduto?>("status", valor);

        Assert.False(contexto.Result.IsModelSet);
        Assert.Equal(1, contexto.ModelState.ErrorCount);
        Assert.Contains("status", contexto.ModelState.Keys);
    }

    [Fact]
    public async Task Parametro_ausente_mantem_o_padrao()
    {
        var contexto = await BindAsync<CampoOrdenacao>("ordenarPor", null);

        Assert.False(contexto.Result.IsModelSet);
        Assert.Equal(0, contexto.ModelState.ErrorCount);
    }

    [Fact]
    public async Task Valor_vazio_em_enum_anulavel_vira_null()
    {
        var contexto = await BindAsync<StatusProduto?>("status", "");

        Assert.True(contexto.Result.IsModelSet);
        Assert.Null(contexto.Result.Model);
    }

    [Fact]
    public async Task Valor_vazio_em_enum_obrigatorio_e_erro()
    {
        var contexto = await BindAsync<DirecaoOrdenacao>("direcao", "");

        Assert.Equal(1, contexto.ModelState.ErrorCount);
    }
}
