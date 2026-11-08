using CatalogoApi.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogoApi.Data.Configuracoes;

/// <summary>Limites espelhados dos DTOs (AD-4); status como texto (AD-5).</summary>
public sealed class ProdutoConfiguracao : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos", t =>
        {
            t.HasCheckConstraint("ck_produtos_preco", "preco >= 0");
            t.HasCheckConstraint("ck_produtos_estoque", "estoque >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        builder.Property(p => p.Nome).HasColumnName("nome")
            .HasMaxLength(LimitesProduto.NomeMaximo).IsRequired();

        builder.Property(p => p.Descricao).HasColumnName("descricao")
            .HasMaxLength(LimitesProduto.DescricaoMaxima);

        builder.Property(p => p.Preco).HasColumnName("preco")
            .HasPrecision(LimitesProduto.PrecoPrecisao, LimitesProduto.PrecoEscala);

        builder.Property(p => p.Estoque).HasColumnName("estoque");

        builder.Property(p => p.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(p => p.CriadoEm).HasColumnName("criado_em");
        builder.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(p => p.Status);
    }
}
