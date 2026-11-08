using System.ComponentModel.DataAnnotations;

namespace CatalogoApi.Contracts.Validacao;

/// <summary>
/// O PostgreSQL não armazena U+0000 em texto; sem esta validação a entrada passaria
/// pelo DTO e estouraria no banco como 500 (AD-4).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SemCaractereNuloAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not string s || !s.Contains('\0', StringComparison.Ordinal);

    public override string FormatErrorMessage(string name) =>
        $"O campo {name} contém caractere inválido.";
}
