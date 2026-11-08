using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace CatalogoApi.Infrastructure;

/// <summary>
/// Traces, métricas e logs pelo SDK OpenTelemetry (AD-10). Endpoint, protocolo,
/// service.name e atributos de recurso vêm só das variáveis OTEL_* padrão.
/// </summary>
public static class ObservabilidadeExtensions
{
    public static WebApplicationBuilder AddObservabilidade(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var otel = builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithLogging(configureBuilder: null, configureOptions: options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            });

        // Exporta os três sinais por OTLP quando a plataforma injeta o endpoint.
        // Sem endpoint (dev local, testes), a telemetria continua gerada mas não sai do processo,
        // e o console continua escrevendo os logs no stdout.
        if (ExporterConfigurado(builder.Configuration))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }

    private static readonly string[] ChavesEndpoint =
    [
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
        "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
        "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
    ];

    internal static bool ExporterConfigurado(IConfiguration configuration) =>
        ChavesEndpoint.Any(chave => !string.IsNullOrWhiteSpace(configuration[chave]));
}
