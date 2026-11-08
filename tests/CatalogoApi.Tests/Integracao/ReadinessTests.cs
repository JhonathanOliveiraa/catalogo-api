using System.Net;
using CatalogoApi.Data;
using CatalogoApi.Tests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 1.2: PostgreSQL, migrations no startup e readiness.</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class ReadinessTests(PostgresFixture fx)
{
    [Fact]
    public async Task Migrations_foram_aplicadas_no_startup()
    {
        await using var scope = fx.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();

        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_Inicial", StringComparison.Ordinal));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Readiness_200_com_banco_disponivel() =>
        Assert.Equal(HttpStatusCode.OK, (await fx.Client.GetAsync("/health")).StatusCode);

    [Fact]
    public async Task Readiness_503_com_banco_fora_e_liveness_continua_200()
    {
        // container próprio: derrubá-lo não afeta os outros testes
        await using var container = PostgresFixture.NovoContainer();
        await container.StartAsync();
        await using var factory = new CatalogoApiFactory(container.GetConnectionString());
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        await container.StopAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
}
