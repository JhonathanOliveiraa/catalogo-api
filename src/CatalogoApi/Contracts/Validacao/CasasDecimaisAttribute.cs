using System.ComponentModel.DataAnnotations;

namespace CatalogoApi.Contracts.Validacao;

/// <summary>Rejeita decimais com mais casas do que a coluna aceita (AD-4).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CasasDecimaisAttribute(int maximo) : ValidationAttribute
{
    public int Maximo { get; } = maximo;

    public override bool IsValid(object? value) =>
        value is not decimal d || decimal.Round(d, Maximo) == d;

    public override string FormatErrorMessage(string name) =>
        $"O campo {name} aceita no máximo {Maximo} casas decimais.";
}
