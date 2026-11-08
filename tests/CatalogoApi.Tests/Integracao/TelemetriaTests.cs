using System.Diagnostics;
using System.Net;
using CatalogoApi.Tests.Suporte;
using OpenTelemetry.Trace;

namespace CatalogoApi.Tests.Integracao;

/// <summary>História 1.3: traces OpenTelemetry com spans de ASP.NET Core e EF Core.</summary>
[Trait("Category", "Integracao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed class TelemetriaTests(PostgresFixture fx)
{
    private const string FonteAspNetCore = "Microsoft.AspNetCore";
    private const string FonteEfCore = "OpenTelemetry.Instrumentation.EntityFrameworkCore";

    [Fact]
    public async Task Requisicao_que_consulta_o_banco_gera_spans_de_AspNetCore_e_EfCore()
    {
        var atividades = new AtividadesColetadas();
        await using var factory = new CatalogoApiFactory(fx.ConnectionString, servicos: s =>
            s.ConfigureOpenTelemetryTracerProvider(t => t.AddInMemoryExporter(atividades)));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        // o span do servidor só é exportado quando termina, às vezes depois da resposta
        Activity? servidor = null;
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limite)
        {
            servidor = SpanDoServidor(atividades);
            if (servidor is not null && SpansDoBanco(atividades, servidor).Length > 0)
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(servidor);
        Assert.Equal(ActivityKind.Server, servidor.Kind);
        // o health check do EF pode abrir a conexão e executar o SELECT 1 em spans separados
        Assert.NotEmpty(SpansDoBanco(atividades, servidor));
    }

    private static Activity? SpanDoServidor(AtividadesColetadas atividades) =>
        atividades.Instantaneo().FirstOrDefault(a =>
            a.Source.Name == FonteAspNetCore
            && (a.DisplayName.EndsWith("/health", StringComparison.Ordinal) || a.GetTagItem("url.path") as string == "/health"));

    // só os spans EF da mesma requisição (a migration do startup também usa EF)
    private static Activity[] SpansDoBanco(AtividadesColetadas atividades, Activity servidor) =>
        atividades.Instantaneo().Where(a => a.Source.Name == FonteEfCore && a.TraceId == servidor.TraceId).ToArray();
}
