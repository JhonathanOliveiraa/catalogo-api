# catalogo-api

API REST de Produtos em ASP.NET Core (.NET 10) com EF Core sobre PostgreSQL 17. O cliente cadastra,
lista com filtros e ordenação, consulta, edita, desativa e reativa produtos. Não existe remoção.

É a aplicação de validação do TCC: um CRUD simples o bastante para que o foco fique no **contrato do
Golden Path** da plataforma (build, segurança, deploy GitOps e observabilidade).

| Artefato do Golden Path | Onde |
|---|---|
| Dockerfile (`linux/arm64`, chiseled, não-root, `HEALTHCHECK`) | `Dockerfile` |
| Esteira de CI (chama o template compartilhado) | `.github/workflows/ci.yml` |
| OpenTelemetry (traces, métricas e logs por OTLP) | `src/CatalogoApi/Infrastructure/ObservabilidadeExtensions.cs` |
| Health checks: `/health` (readiness) e `/health/live` (liveness) | `src/CatalogoApi/Program.cs` |
| Manifestos | **não ficam aqui**: overlay em `tcc-platform/apps/catalogo-api/hml` |
| Segredos | **não ficam aqui**: OCI Vault, declarados por `ExternalSecret` no `tcc-platform` |

O contrato completo está em `_bmad-output/specs/spec-catalogo-api/` e as decisões de arquitetura (AD-1..AD-13)
em `_bmad-output/planning-artifacts/architecture/architecture-catalogo-api-2026-10-13/ARCHITECTURE-SPINE.md`.

## Estrutura

```text
CatalogoApi.slnx
Directory.Build.props           # net10.0, nullable, analisadores, globalização invariante
Dockerfile / .dockerignore
src/CatalogoApi/
  Controllers/                  # só DTOs; recebem, delegam e devolvem
  Services/                     # regras, DTO <-> entidade, datas (TimeProvider)
  Repositories/                 # só entidade; consultas EF
  Data/                         # CatalogoDbContext, configuração EF, Migrations/, readiness
  Domain/                       # Produto, enums, limites, exceções
  Contracts/                    # DTOs de request/response e validações
  Infrastructure/               # erros (ProblemDetails), JSON, CORS, OTel, modo --healthcheck
  Program.cs
tests/CatalogoApi.Tests/
  Unidade/                      # Service com repositório simulado, validações, JSON, CORS, binder
  Integracao/                   # WebApplicationFactory + Testcontainers postgres:17-alpine
scripts/carga.k6.js             # carga do experimento de observabilidade
```

Camadas: `Controller → Service → Repository → EF Core`. Só `Repositories/` e `Data/` usam EF Core.

## Endpoints

| Método e rota | Resposta |
|---|---|
| `POST /produtos` | 201 + `Location: /produtos/{id}` + produto (nasce `Ativo`); 400 se inválido |
| `GET /produtos` | 200 + `{ itens, total, page, pageSize }`; 400 se algum parâmetro for inválido |
| `GET /produtos/{id}` | 200 + produto (ativo ou desativado); 404 se não existir |
| `PUT /produtos/{id}` | 200 + produto; substituição total (`descricao` ausente vira `null`); não muda o status |
| `PATCH /produtos/{id}/status` | corpo `{ "status": "Ativo" \| "Desativado" }`; 200 + produto; repetir o status atual não altera `atualizadoEm` |
| `GET /health` | readiness: 200 com o PostgreSQL respondendo, 503 sem ele |
| `GET /health/live` | liveness: 200 enquanto o processo atende, sem testar dependências |
| `GET /openapi/v1.json` | documento OpenAPI (enums descritos como string) |

Produto: `id`, `nome` (1–200), `descricao` (≤ 2000, opcional), `preco` (0–9 999 999 999,99, até 2 casas),
`estoque` (≥ 0), `status`, `criadoEm`, `atualizadoEm` (UTC, precisão de microssegundos).

Parâmetros da listagem: `page` (≥ 1, padrão 1), `pageSize` (1–100, padrão 20), `nome` (contém, sem diferenciar
maiúsculas; `%` e `_` valem como texto), `precoMin`/`precoMax` (inclusivos), `status` (`Ativo`/`Desativado`; sem
ele vêm todos), `ordenarPor` (`Nome`, `Preco`, `CriadoEm`; padrão `Nome`), `direcao` (`Asc`, `Desc`; padrão `Asc`).
A ordenação sempre desempata por `id` crescente.

Enums trafegam pelo nome, sem diferenciar maiúsculas na entrada. Número (`?status=1`, `{"status":1}`) retorna 400.
Todo erro, inclusive 404/405/415 do framework, sai em ProblemDetails (`application/problem+json`); erros de
validação trazem `errors`. Um 500 não expõe stack trace e é logado.

```bash
curl -i -X POST localhost:8080/produtos -H 'Content-Type: application/json' \
  -d '{"nome":"Caneca","descricao":"Azul","preco":19.90,"estoque":10}'
curl 'localhost:8080/produtos?nome=can&ordenarPor=Preco&direcao=Desc'
curl -X PATCH localhost:8080/produtos/1/status -H 'Content-Type: application/json' -d '{"status":"Desativado"}'
```

## Configuração

Tudo vem do ambiente; nenhum segredo ou endpoint fica no repositório.

| Variável | Origem no cluster | Uso |
|---|---|---|
| `ConnectionStrings__Catalogo` | `ExternalSecret` (OCI Vault) | connection string Npgsql. O usuário precisa de permissão DDL (migrations no startup). |
| `Cors__OrigensPermitidas` | overlay | origens do `catalogo-front`, separadas por vírgula (espaços e barra final são ignorados). Vazia ou com `*` derruba o startup. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `golden-path/base` | ex.: `http://<alloy>:4317`. Liga o exporter OTLP dos três sinais. |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `golden-path/base` | `grpc` (padrão do SDK) ou `http/protobuf` |
| `OTEL_SERVICE_NAME` | `golden-path/base` | `catalogo-api` |
| `OTEL_RESOURCE_ATTRIBUTES` | `golden-path/base` | ex.: `deployment.environment=hml` |
| `ASPNETCORE_*` / `DOTNET_*` | padrão | a imagem já fixa `ASPNETCORE_HTTP_PORTS=8080` |

Sem `OTEL_EXPORTER_OTLP_ENDPOINT` (ou um endpoint específico de sinal), a telemetria é gerada mas não sai do
processo. Os logs vão sempre para o stdout (console, uma linha por evento) e, com o exporter ligado, também por OTLP.

## Como rodar localmente

Pré-requisitos: SDK .NET 10 e um PostgreSQL 17 acessível.

```bash
docker run -d --name catalogo-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine

export ConnectionStrings__Catalogo='Host=localhost;Database=catalogo;Username=postgres;Password=postgres'
dotnet run --project src/CatalogoApi        # http://localhost:8080, ambiente Development
```

Em `Development`, `Cors__OrigensPermitidas` já vem como `http://localhost:5173` (`appsettings.Development.json`).
Em qualquer outro ambiente ela é obrigatória.

No startup a app valida CORS e connection string, aplica as migrations (`Database.Migrate()`) e só então
aceita tráfego. Com o banco inacessível o processo encerra com erro e o Kubernetes o reinicia até o banco voltar.

### Migrations

```bash
dotnet tool install --global dotnet-ef --version 10.0.14
cd src/CatalogoApi
dotnet ef migrations add <Nome> -o Data/Migrations   # não precisa de banco
```

O schema só muda por migration commitada em `Data/Migrations`. Não há `EnsureCreated()`.

## Testes

```bash
dotnet test --configuration Release                                  # tudo (precisa de Docker)
dotnet test --configuration Release --filter "Category!=Integracao"  # só os que não precisam de Docker
```

- **Unidade**: `ProdutoService` com repositório simulado e relógio fixo, validações dos DTOs, configuração JSON,
  binder de enums, leitura do CORS e falhas de startup por configuração.
- **Integração** (`[Trait("Category","Integracao")]`): `WebApplicationFactory` contra PostgreSQL real em
  Testcontainers (`postgres:17-alpine`), com as migrations reais. Cobrem todas as histórias: ProblemDetails
  (404/405/415/500), readiness 200/503 derrubando o contêiner do banco, spans de ASP.NET Core e EF Core com
  exporter em memória, CORS, CRUD completo, `?status=1` → 400 e enums como string no OpenAPI.

A CI roda `dotnet test --configuration Release` na raiz, no runner `ubuntu-latest`, que tem Docker.

## Contêiner

```bash
docker buildx build --platform linux/arm64 -t catalogo-api:local .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Catalogo='Host=host.docker.internal;Database=catalogo;Username=postgres;Password=postgres' \
  -e Cors__OrigensPermitidas='http://localhost:5173' \
  catalogo-api:local
```

- Multi-stage. O estágio SDK roda em `--platform=$BUILDPLATFORM` (x64 na CI) e publica com `-a $TARGETARCH`:
  a compilação para arm64 é cruzada, sem emulação QEMU no estágio pesado.
- Imagem final `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`: sem shell, sem gerenciador de pacotes,
  sem `RUN`, usuário não-root da imagem (`$APP_UID`, 1654), porta 8080. Globalização invariante (a imagem
  chiseled não tem ICU).
- `HEALTHCHECK` em exec form: `dotnet CatalogoApi.dll --healthcheck`. Esse modo é tratado na primeira instrução
  do `Program.cs`: faz `GET http://localhost:8080/health/live` e sai com 0 (saudável) ou 1, sem subir o servidor
  nem rodar migrations. O Kubernetes ignora o `HEALTHCHECK` do Docker e usa as probes do Deployment.

## Health checks: liveness × readiness

| Endpoint | Probe | Testa o banco? | Para que serve |
|---|---|---|---|
| `/health/live` | liveness | não | o processo está vivo e atendendo. Falhar aqui faz o kubelet **reiniciar** o contêiner. |
| `/health` | readiness | sim (`SELECT 1` via `CatalogoDbContext`, timeout de 3 s) | o pod pode receber tráfego. Falhar aqui só **tira o pod do Service**, sem reiniciá-lo. |

A separação evita que uma queda do PostgreSQL reinicie todos os pods em cascata: com o banco fora, `/health`
responde 503 (os pods saem do balanceamento) e `/health/live` continua 200 (ninguém é reiniciado à toa).
A plataforma (`golden-path/base`) deve apontar a liveness para `/health/live` e a readiness para `/health`.

## Observabilidade

SDK OpenTelemetry .NET, configurado só pelas variáveis `OTEL_*` (`UseOtlpExporter()`):

- **Traces**: ASP.NET Core, HttpClient e EF Core (spans das consultas ao PostgreSQL).
- **Métricas**: ASP.NET Core (`http.server.request.duration` etc.), HttpClient e runtime .NET.
- **Logs**: provider OpenTelemetry do `ILogger` (com mensagem formatada e escopos), além do console no stdout.

Destino no cluster: Grafana Alloy (receptor OTLP) → Grafana Cloud.

## CI/CD

`ci.yml` chama `backend-service.yml` do `tcc-pipeline-templates` com `dotnet-version: "10.0.x"`:
testes → Trivy no código → imagem `linux/arm64` no GHCR → Trivy na imagem → commit da nova tag em
`tcc-platform/apps/catalogo-api/hml` → Argo CD sincroniza.

Gatilhos: `push` na `main`, `pull_request` (sem deploy: `enable-deploy` é falso) e `workflow_dispatch`.
Secret necessário: `PLATFORM_REPO_TOKEN` (fine-grained, só o `tcc-platform`, Contents: Read and write).

## Carga (k6)

`scripts/carga.k6.js` percorre o ciclo CRUD em cada iteração: `POST`, `GET` da lista com filtros
(`nome`, `precoMin`/`precoMax`, `status`, ordenação e paginação), `GET` por id, `PUT` e `PATCH` de status
(desativa e reativa). O `setup` aborta se `/health` não responder 200.

```bash
k6 run -e BASE_URL=https://<host-hml> scripts/carga.k6.js                  # 10 VUs por 5 min (+ rampas)
k6 run -e BASE_URL=http://localhost:8080 -e VUS=5 -e DURACAO=1m scripts/carga.k6.js
```

Thresholds: `http_req_failed` < 1%, `p(95)` < 500 ms, `p(99)` < 1 s, `p(95)` da listagem < 400 ms e
`checks` > 99%. Como não há `DELETE`, os produtos criados pela carga ficam no banco (nome começa com `k6 produto`).

## Experimento de falha

Duas falhas para o experimento de observabilidade do TCC. **Nenhuma está no código nem atrás de flag**: cada uma
entra num commit próprio, passa pela esteira normal e sai com `git revert <commit>`. Nos dois patches o
compilador avisa CS0162 (código inalcançável depois do `throw`); é esperado e não quebra o build.

**A suíte de testes barra os dois patches**: (a) derruba todo teste que sobe a API e (b) quebra os testes de
listagem, e o job `build` do template só roda com `test` aprovado ou pulado. Para a falha chegar ao cluster, o
mesmo commit precisa desligar os testes no `ci.yml` (`test-command: ""`, que faz o template pular o job), e o
`git revert` religa. Esse bloqueio também é um resultado do experimento: mostra o Golden Path impedindo a falha
de chegar ao cluster quando ninguém contorna a esteira.

### (a) Falha de inicialização

O processo encerra no startup, depois das migrations e antes de aceitar tráfego.

```diff
--- a/src/CatalogoApi/Program.cs
+++ b/src/CatalogoApi/Program.cs
@@
 app.MapControllers();

+throw new InvalidOperationException("Falha injetada (experimento TCC): startup interrompido.");
+
 await app.RunAsync();
 return 0;
```

Esperado: o contêiner sai com código 134 (exceção não tratada), o pod entra em `CrashLoopBackOff`, a readiness
nunca fica verde e o Argo CD mostra a aplicação degradada. Com rolling update, os pods da versão anterior
continuam atendendo. A evidência principal está nos logs do contêiner (stdout/stderr) e nos eventos do
Kubernetes: o processo aborta antes de o exporter OTLP enviar o lote, então não há trace dessa falha.

### (b) Falha funcional

`GET /produtos` responde 500, mas o processo e o banco estão saudáveis: `/health` e `/health/live` continuam 200,
então o Kubernetes não reage e só a observabilidade revela o problema.

```diff
--- a/src/CatalogoApi/Services/ProdutoService.cs
+++ b/src/CatalogoApi/Services/ProdutoService.cs
@@
     public async Task<PaginaResponse<ProdutoResponse>> ListarAsync(
         ListarProdutosQuery query, CancellationToken cancellationToken)
     {
+        throw new InvalidOperationException("Falha injetada (experimento TCC): listagem indisponível.");
+
         var filtro = new FiltroProdutos(
```

Esperado: 500 em ProblemDetails, sem stack trace, para toda listagem; os demais endpoints seguem normais.
No Grafana Cloud: spans de `GET /produtos` com status de erro, `http.server.request.duration` com
`http.response.status_code=500` e log `Erro não tratado em GET /produtos` com a exceção. No k6, `http_req_failed`
e `checks` estouram os thresholds.

## Desenvolvimento

Planejado com o **BMAD Method**: spec, arquitetura, épicos e `sprint-status.yaml` em `_bmad-output/`.
