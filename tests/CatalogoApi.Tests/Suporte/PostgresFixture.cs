using Testcontainers.PostgreSql;

namespace CatalogoApi.Tests.Suporte;

/// <summary>
/// PostgreSQL real via Testcontainers, na mesma major do cluster (17), compartilhado pela
/// coleção de integração. A API aplica as migrations reais no startup (AD-9, AD-12).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Imagem = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = NovoContainer();

    public string ConnectionString => _container.GetConnectionString();
    public CatalogoApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public static PostgreSqlContainer NovoContainer() => new PostgreSqlBuilder(Imagem).Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new CatalogoApiFactory(ConnectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class ColecaoIntegracao : ICollectionFixture<PostgresFixture>
{
    public const string Nome = "Integracao";
}
