using CatalogoApi.Data;
using CatalogoApi.Infrastructure;
using CatalogoApi.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

// HEALTHCHECK do contêiner: responde e sai antes de montar qualquer coisa (AD-11).
if (args.Contains(ModoHealthcheck.Argumento)) return await ModoHealthcheck.ExecutarAsync();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options => options.ModelBinderProviders.Insert(0, new EnumPorNomeModelBinderProvider()))
    .AddJsonOptions(options => ConfiguracaoJson.Aplicar(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfiguracaoJson.Aplicar(options.SerializerOptions));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorExcecoes>();
builder.Services.AddOpenApi();
builder.Services.AddCorsCatalogo();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddPersistencia();

builder.AddObservabilidade();

var app = builder.Build();

// Configuração obrigatória ausente ou inválida derruba o startup com mensagem clara (AD-8).
CorsExtensions.LerOrigens(app.Configuration);
PersistenciaExtensions.ObterConnectionString(app.Configuration);

// Schema atualizado antes de aceitar tráfego (AD-9).
await app.Services.AplicarMigrationsAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsExtensions.Politica);

app.MapOpenApi();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(PersistenciaExtensions.TagReadiness),
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapControllers();

await app.RunAsync();
return 0;

/// <summary>Exposto para o <c>WebApplicationFactory</c> dos testes.</summary>
public partial class Program;
