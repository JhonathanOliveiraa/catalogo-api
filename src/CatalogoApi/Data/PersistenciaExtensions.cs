using CatalogoApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CatalogoApi.Data;

public static class PersistenciaExtensions
{
    public const string NomeConnectionString = "Catalogo";
    public const string TagReadiness = "ready";
    private static readonly TimeSpan TimeoutCheckBanco = TimeSpan.FromSeconds(3);

    /// <summary>DbContext, repositório e o check de readiness do PostgreSQL.</summary>
    public static IServiceCollection AddPersistencia(this IServiceCollection services)
    {
        // a connection string é lida só na resolução: vem do ambiente (ExternalSecret), AD-8
        services.AddDbContext<CatalogoDbContext>((sp, options) =>
            options.UseNpgsql(ObterConnectionString(sp.GetRequiredService<IConfiguration>())));

        services.AddScoped<IProdutoRepository, ProdutoRepository>();

        services.AddHealthChecks().AddDbContextCheck<CatalogoDbContext>(
            name: "postgresql",
            tags: [TagReadiness],
            customTestQuery: async (db, cancellationToken) =>
            {
                // consulta real, não só abertura de conexão; limitada para não pendurar a probe
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeoutCheckBanco);
                await db.Database.ExecuteSqlRawAsync("SELECT 1", cts.Token);
                return true;
            });

        return services;
    }

    /// <summary>
    /// Aplica as migrations antes de aceitar tráfego (AD-9). O EF Core 9+ trava
    /// "__EFMigrationsHistory" dentro da transação, o que serializa réplicas concorrentes.
    /// </summary>
    public static async Task AplicarMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public static string ObterConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(NomeConnectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string ausente: defina a variável de ambiente ConnectionStrings__{NomeConnectionString}.");
        }

        return connectionString;
    }
}
