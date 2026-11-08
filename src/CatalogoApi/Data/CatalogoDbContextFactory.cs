using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CatalogoApi.Data;

/// <summary>
/// Usada só pelo <c>dotnet ef</c> para gerar migrations. Não precisa de conexão,
/// então nenhuma connection string fica no repositório (AD-8).
/// </summary>
internal sealed class CatalogoDbContextFactory : IDesignTimeDbContextFactory<CatalogoDbContext>
{
    public CatalogoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogoDbContext>().UseNpgsql().Options);
}
