---
review-of: ARCHITECTURE-SPINE.md (catalogo-api, 2026-10-13)
lens: versões / reality-check (tudo foi pesquisado ou só afirmado de memória?)
date: 2026-10-13
method: consultas à API do NuGet (api.nuget.org/v3-flatcontainer), manifesto e config da imagem no registry MCR, código-fonte de aspnetcore (release/10.0) e efcore.pg (main), Microsoft Learn, avisos de segurança
---

# Revisão de versões e reality-check: catalogo-api

## Veredito

O spine se apoia em bases verificadas. O memlog registra checagens de verdade para .NET 10 LTS, Npgsql 10, o lock de migração do EF 9+ e o status beta do EF Core OTel, e a imagem, a porta e o usuário estão corretos. Mesmo assim, quatro pontos contradizem o comportamento real atual. **AD-7 do jeito que está escrito não gera enums como string no OpenAPI quando se usa controllers.** Por padrão, tanto o JSON quanto a query aceitam enums numéricos. A versão de OTel na tabela Stack já está defasada. E o build arm64 via QEMU do template, junto com a action do Trivy fixada por tag, merecem uma decisão explícita.

## Tabela de verificação

| Afirmação do spine | Valor real (verificado em 2026-10-13) | Status |
|---|---|---|
| .NET 10 LTS, suporte até 2028-11 | LTS, fim em **2028-11-14**; .NET 8/9 encerram em 2026-11-10 | OK |
| EF Core / ASP.NET Core 10.0 | Último patch estável 10.0.13 (EF Core, Microsoft.AspNetCore.OpenApi); 11.0 em rc.2 | OK |
| Npgsql.EntityFrameworkCore.PostgreSQL 10.0 | Último estável 10.0.3 (11.0.0-rc.1.1 em preview) | OK |
| OpenTelemetry SDK + instrumentações **1.15** | SDK/Hosting/OTLP **1.19.1**; Instrumentation.AspNetCore/Http **1.19.0** (estáveis); EF Core **1.19.1-beta.1** (ainda beta, publicado 2026-09-24, alvo net10.0) | **DEFASADO** (o status beta está correto) |
| Testcontainers.PostgreSql 4.13 | **4.16.0** | Defasado (baixo) |
| Microsoft.AspNetCore.OpenApi 10.0, enums como string | O pacote existe, mas o gerador de schema lê `Microsoft.AspNetCore.Http.Json.JsonOptions`, não as `JsonOptions` do MVC | **RISCO**: veja F1 |
| `aspnet:10.0-noble-chiseled`, arm64 | A tag existe para amd64, arm/v7 e **arm64** (índice de manifestos do MCR) | OK |
| Porta 8080, usuário não-root | Config arm64: `ASPNETCORE_HTTP_PORTS=8080`, `User=1654` (`APP_UID`), Entrypoint `/usr/bin/dotnet`, **`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`**, runtime 10.0.13 | OK (note o modo invariante) |
| Lock nativo do `Migrate()` no EF 9+ para réplicas | Npgsql: `LOCK TABLE "__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE`, `LockReleaseBehavior.Transaction` (o lock é liberado se a conexão ou o pod morrer) | OK, com ressalvas (F5) |
| 400 automático em ProblemDetails | `[ApiController]` → `ValidationProblemDetails` por padrão; ProblemDetails automático para resultados de erro | OK, com lacunas (F4) |
| Testcontainers no runner da CI | `ubuntu-latest` (x64) tem Docker, então o job `test` funciona sem alteração | OK |

## Achados

### F1: AD-7 e OpenAPI: o converter só no MVC deixa enums como inteiro no documento [ALTO]

- **Fato:** no `release/10.0`, `OpenApiSchemaService` injeta `IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>` (`using Microsoft.AspNetCore.Http.Json;`). Para controllers, o caminho comum é `AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`. Isso configura `Microsoft.AspNetCore.Mvc.JsonOptions`. O runtime passa a serializar strings, mas **o OpenAPI continua descrevendo os enums como integer**. O front gerado a partir do OpenAPI recebe o contrato errado.
- **Correção:** acrescentar ao AD-7: "Registrar o mesmo `JsonStringEnumConverter` em **`AddControllers().AddJsonOptions(...)` e em `ConfigureHttpJsonOptions(...)`** (ou anotar cada enum com `[JsonConverter(typeof(JsonStringEnumConverter<T>))]`). Um teste de integração verifica que `/openapi/v1.json` traz `type: string` + `enum` para `StatusProduto`, `CampoOrdenacao` e `DirecaoOrdenacao`."
- **Fontes:** https://github.com/dotnet/aspnetcore/blob/release/10.0/src/OpenApi/src/Services/Schemas/OpenApiSchemaService.cs ; https://www.cratis.io/docs/Arc/backend/open-api/enums.html

### F2: AD-7 "pelo nome" não vale por padrão: valores numéricos são aceitos [MÉDIO]

- **Fato:** `JsonStringEnumConverter` tem `allowIntegerValues = true` por padrão, então `{"status": 1}` é aceito. Na query, o `EnumTypeModelBinder` do MVC (release/10.0) só rejeita valores com `!Enum.IsDefined`. Assim, `?status=1` vira `Desativado` em vez de 400. A leitura é case-insensitive nos dois caminhos, o que confere com o AD-7.
- **Correção:** no AD-7, usar `new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)`. Para a query, adicionar um binder ou validação que rejeite valores só numéricos (por exemplo, um `IModelBinder` para enums, ou `[RegularExpression("^[A-Za-z]+$")]` num campo string mapeado). Incluir um teste de integração para `?status=1` → 400.
- **Fontes:** https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonstringenumconverter.-ctor ; https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Mvc/Mvc.Core/src/ModelBinding/Binders/EnumTypeModelBinder.cs

### F3: Stack de OTel defasada (1.15 → 1.19) e existe alternativa estável para spans de banco [MÉDIO]

- **Fato:** no NuGet hoje, OpenTelemetry/Extensions.Hosting/Exporter.OpenTelemetryProtocol estão em 1.19.1, Instrumentation.AspNetCore/Http em 1.19.0 e Instrumentation.EntityFrameworkCore em 1.19.1-beta.1. O próprio memlog registra "1.15.x-beta", que já ficou para trás. `Npgsql.OpenTelemetry` 10.0.3 é **estável** e gera spans de comando SQL no nível do driver.
- **Correção:** atualizar a tabela Stack para "OpenTelemetry 1.19.x (EF Core instrumentation 1.19.x-beta)", ou trocar por "linha 1.x atual, versões exatas no csproj". Considerar `.AddNpgsql()` (Npgsql.OpenTelemetry, estável) no lugar da instrumentação beta de EF Core, ou junto com ela. Isso cumpre "spans em todas as camadas" sem depender de pacote pré-release. Atualizar também Testcontainers.PostgreSql para 4.16.
- **Fontes:** https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EntityFrameworkCore ; https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore ; https://www.nuget.org/packages/Npgsql.OpenTelemetry ; https://www.nuget.org/packages/Testcontainers.PostgreSql

### F4: Template de CI: Trivy fixado por tag de faixa comprometida e build arm64 por QEMU [ALTO (segurança, no template) / MÉDIO (build)]

- **Fato (segurança):** `backend-service.yml` usa `aquasecurity/trivy-action@0.28.0`. Em 2026-03-19, 76 de 77 tags da trivy-action (0.0.1–0.34.2) foram reescritas com código que rouba credenciais (CVE-2026-33634). A versão segura indicada é a 0.35.0. O job `build` tem `GITHUB_TOKEN` com push no GHCR. O AD-13 diz "o template compartilhado não é alterado", e com isso o catalogo-api herda esse risco.
- **Fato (build):** o template constrói `linux/arm64` em runner x64 via QEMU. `dotnet restore/publish` emulado é lento e propenso a falhas. A Microsoft recomenda cross-build: estágio SDK em `--platform=$BUILDPLATFORM` e `dotnet publish -a $TARGETARCH`. Só o estágio final usa a imagem arm64.
- **Fato (testes x64 × imagem arm64):** os testes rodam framework-dependent em x64, e a imagem arm64 nunca é executada na CI. Para código gerenciado puro, o risco é baixo. Os pontos sensíveis são o modo de globalização invariante da imagem chiseled e as libs nativas, que só aparecem em runtime.
- **Correção:** (a) levar ao dono do `tcc-pipeline-templates` a troca para `trivy-action@0.35.0` **fixada por SHA de commit**, e verificar os logs de 19–20/03/2026. Isso não exige mudar o AD-13, só registrar a dependência. (b) No AD-11, fixar: "Dockerfile multi-stage com `FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0` e `dotnet publish -a $TARGETARCH`; nenhum `RUN` no estágio final" (a imagem chiseled não tem shell). (c) Declarar `permissions: { contents: read, packages: write }` no `ci.yml`, porque o workflow reutilizável herda as permissões do chamador.
- **Fontes:** https://csirt.egi.eu/2026/03/31/trivy-supply-chain-vulnerability/ ; https://www.rapid7.com/db/vulnerabilities/cve-2026-33634/ ; https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/

### F5: AD-9 e AD-3/AD-4: comportamentos padrão que o spine não nomeia [BAIXO–MÉDIO]

- **Lock de migração (confirmado):** no Npgsql, o lock é `LOCK TABLE ... ACCESS EXCLUSIVE` dentro da transação da migração. Ele é liberado se a conexão cair, o que é bom para pods mortos. Ressalvas: (1) uma migration que suprime a transação (por exemplo, `CREATE INDEX CONCURRENTLY`) não fica protegida; (2) **desde o EF Core 9, `Migrate()` lança exceção se o modelo tiver mudanças sem migration** (`PendingModelChangesWarning`). Um pod com modelo divergente entra em crash-loop, o que serve como sinal útil na CI. Correção: no AD-9, proibir migrations sem transação e manter o warning como erro. Para isso, um teste de integração que suba a app e rode `Migrate()` contra o Testcontainers.
- **ProblemDetails:** `[ApiController]` cobre o 400 de validação (`ValidationProblemDetails`). Já 404 de rota inexistente, 405 e 415 só saem como ProblemDetails com `AddProblemDetails()` + `UseStatusCodePages()`. Para o middleware próprio, o caminho atual é `AddProblemDetails()` + `IExceptionHandler` + `UseExceptionHandler()`. Correção: no AD-3, citar esse trio no lugar de um "middleware" genérico.
- **DataAnnotations (AD-4):** `[Required]` num `decimal` não-anulável não detecta ausência, porque o valor vira 0 e passa em `≥ 0`. Correção: usar `decimal?` com `[Required]` ou o modificador `required` do C# (o System.Text.Json o respeita, gerando 400).
- **Imagem:** `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` vem ligado na chiseled. Isso não afeta o Npgsql. O Dockerfile `HEALTHCHECK` precisa estar em exec form (`["dotnet","CatalogoApi.dll","--healthcheck"]`), porque não há shell. O Kubernetes ignora o `HEALTHCHECK` e usa as probes.
- **Testcontainers:** a imagem padrão do módulo Postgres é antiga. Fixar `WithImage("postgres:<major da plataforma>")` assim que o `tcc-platform` definir a versão, que está em Deferred.
- **Fontes:** https://github.com/npgsql/efcore.pg/blob/main/src/EFCore.PG/Migrations/Internal/NpgsqlHistoryRepository.cs ; https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes ; https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0

## O que foi confirmado e não precisa mudar

- .NET 10 LTS (fim em 2028-11-14) e a razão do AD-13 (o padrão do template é 9.0.x, que perde suporte em 2026-11-10): https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- Npgsql EF Core 10.0.x estável: https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL
- Tag chiseled arm64, porta 8080 e usuário 1654: manifesto e config em `mcr.microsoft.com/v2/dotnet/aspnet/manifests/10.0-noble-chiseled`
- O job `test` em `ubuntu-latest` tem Docker para o Testcontainers. O `test-command` padrão casa com o AD-12.
