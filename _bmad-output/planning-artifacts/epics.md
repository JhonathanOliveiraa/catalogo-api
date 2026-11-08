---
status: final
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/specs/spec-catalogo-api/SPEC.md
  - _bmad-output/specs/spec-catalogo-api/produto.md
  - _bmad-output/specs/spec-catalogo-api/golden-path.md
  - _bmad-output/planning-artifacts/architecture/architecture-catalogo-api-2026-10-13/ARCHITECTURE-SPINE.md
---

# catalogo-api - Epic Breakdown

## Overview

Este documento quebra em épicos e histórias implementáveis os requisitos do spec (`SPEC.md`, que substitui o PRD) e as decisões da arquitetura (`ARCHITECTURE-SPINE.md`, AD-1..AD-13). Não há UX: a interface fica no `catalogo-front`.

## Requirements Inventory

### Functional Requirements

Os requisitos funcionais usam os IDs `CAP-N` do spec.

- CAP-1: Cadastrar produto — `POST /produtos`; 201 + `Location` + `ProdutoResponse`, produto nasce `Ativo`; inválido → 400.
- CAP-2: Listar produtos (ativos e desativados) — paginação `page`/`pageSize`; filtros `nome` (contém, sem diferenciar maiúsculas), `precoMin`/`precoMax` (inclusivos), `status` (opcional; sem ele vêm todos); ordenação `ordenarPor`/`direcao` (enums); resposta `{itens,total,page,pageSize}`; parâmetro inválido → 400.
- CAP-3: Consultar produto por id — 200 também para desativado; inexistente → 404.
- CAP-4: Editar produto — `PUT /produtos/{id}`, substituição total, permitido para desativado; inexistente → 404; inválido → 400.
- CAP-5: Desativar/reativar — `PATCH /produtos/{id}/status` com `{status}`; 200 + produto; repetir o status atual não altera `AtualizadoEm`; inexistente → 404; status inválido → 400.
- CAP-6: Health — `GET /health` (readiness, com banco: 200 / 503) e `GET /health/live` (liveness, sem dependências: 200).
- CAP-7: Telemetria — requisição HTTP gera trace OTLP com spans de ASP.NET Core e EF Core, visível no Grafana Cloud.

### NonFunctional Requirements

- NFR1: Imagem `linux/arm64`, usuário não-root, `aspnet:10.0-noble-chiseled`, porta 8080, `HEALTHCHECK` em exec form (AD-11).
- NFR2: A esteira roda testes + Trivy (CRITICAL com correção disponível) antes de publicar, e os dois precisam passar; gatilhos `push`/`pull_request` ligados no `ci.yml`.
- NFR3: Configuração só pelo ambiente (`ConnectionStrings__Catalogo`, `Cors__OrigensPermitidas`, `OTEL_*`); nenhum segredo ou endpoint no repositório (AD-8).
- NFR4: Migrations EF Core aplicadas no startup, tolerando réplicas concorrentes; sem `EnsureCreated()` (AD-9).
- NFR5: Enums tipados que trafegam pelo nome, rejeitam inteiros e aparecem como string no OpenAPI (AD-7).
- NFR6: Todo erro sai como ProblemDetails, inclusive 404/405/415 do framework (AD-3).
- NFR7: CORS com origens configuráveis por ambiente para o `catalogo-front` (AD-8).
- NFR8: .NET 10 em toda a cadeia: projeto `net10.0`, imagem `10.0`, `ci.yml` com `dotnet-version: 10.0.x` (AD-13).

### Additional Requirements

- Não há template de partida definido; a primeira história cria a base com `dotnet new webapi --use-controllers`: solução na raiz, projeto único `src/CatalogoApi` com pastas por camada, `tests/CatalogoApi.Tests`.
- Camadas Controller → Service → Repository → EF, com DTOs só nos Controllers e entidade só no Repository (AD-1, AD-2).
- Exceções de domínio traduzidas por um único `IExceptionHandler`; rotas `{id:int}` (AD-3).
- Validação em dois níveis, com limites espelhados entre DTO e EF (AD-4).
- Ciclo de vida por `Status`, sem `DELETE` (AD-5); datas pelo Service via `TimeProvider`, truncadas a microssegundos (AD-6).
- Testes de integração com `WebApplicationFactory` + Testcontainers `postgres:17-alpine`; casos obrigatórios `?status=1` → 400 e OpenAPI com enums como string (AD-12).
- Dockerfile multi-stage, com o estágio SDK em `$BUILDPLATFORM` publicando com `-a $TARGETARCH` (AD-11).
- Dependências externas (fora deste repositório): overlay `tcc-platform/apps/catalogo-api` com ExternalSecret e `Cors__OrigensPermitidas`; probes do `golden-path/base` em `/health/live` (liveness) e `/health` (readiness); merge de `fix/trivy-action-sha` no `tcc-pipeline-templates`.

### UX Design Requirements

Não se aplica (API sem interface).

### FR Coverage Map

- CAP-1: Épico 2 — cadastrar produto
- CAP-2: Épico 2 — listar com paginação, filtros e ordenação
- CAP-3: Épico 2 — consultar por id
- CAP-4: Épico 2 — editar (substituição total)
- CAP-5: Épico 2 — desativar/reativar
- CAP-6: Épico 1 — `/health` e `/health/live`
- CAP-7: Épico 1 — traces OTLP no Grafana Cloud
- NFR1–NFR4, NFR6–NFR8: Épico 1
- NFR5: Épico 2

## Epic List

### Épico 1: Serviço no ar pelo Golden Path

O operador vê o `catalogo-api` rodando no cluster, publicado pela esteira, com health checks respondendo e traces chegando ao Grafana Cloud. Esqueleto ponta a ponta: base da solução em camadas, tratamento de erro, configuração JSON e CORS, conexão com PostgreSQL e migrations no startup, health, OpenTelemetry, Dockerfile chiseled arm64, `ci.yml` com gatilhos e testes de integração na esteira.

**FRs covered:** CAP-6, CAP-7 · NFR1, NFR2, NFR3, NFR4, NFR6, NFR7, NFR8

### Épico 2: Gestão do catálogo de produtos

O cliente (`catalogo-front`) cadastra, lista com filtros e ordenação, consulta, edita, desativa e reativa produtos. Cada história é publicada pela esteira do Épico 1.

**FRs covered:** CAP-1, CAP-2, CAP-3, CAP-4, CAP-5 · NFR5

## Epic 1: Serviço no ar pelo Golden Path

O operador vê o `catalogo-api` rodando no cluster, publicado pela esteira, com health checks respondendo e traces chegando ao Grafana Cloud.

### Story 1.1: Base da API com erros padronizados

As a desenvolvedor,
I want a solução criada com as camadas e o tratamento de erro único,
So that toda história seguinte já nasça no padrão (AD-1, AD-3, AD-7, AD-13; NFR6, NFR8).

**Acceptance Criteria:**

**Given** o repositório sem código
**When** rodo `dotnet test --configuration Release` na raiz
**Then** a solução `net10.0` compila e os testes passam
**And** `src/CatalogoApi` tem as pastas Controllers, Services, Repositories, Data, Domain, Contracts e Infrastructure, e `tests/CatalogoApi.Tests` usa `WebApplicationFactory`

**Given** a API rodando
**When** chamo uma rota inexistente, um método não permitido ou um content type não suportado
**Then** recebo 404, 405 ou 415 em ProblemDetails

**Given** um Service que lança uma exceção não tratada
**When** a requisição é processada
**Then** a resposta é 500 em ProblemDetails, sem stack trace no corpo
**And** o erro é logado

**Given** a configuração JSON da app
**When** inspeciono `AddJsonOptions` e `ConfigureHttpJsonOptions`
**Then** as duas usam a mesma configuração: camelCase e `JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)`

**Given** a API rodando
**When** chamo `GET /health/live` e `GET /openapi/v1.json`
**Then** recebo 200 nas duas

### Story 1.2: Conexão com PostgreSQL, migrations no startup e readiness

As a plataforma,
I want saber se o serviço consegue falar com o banco,
So that ele só receba tráfego quando estiver pronto (CAP-6; AD-8, AD-9, AD-12; NFR3, NFR4).

**Acceptance Criteria:**

**Given** `ConnectionStrings__Catalogo` definida no ambiente
**When** a app sobe
**Then** `Database.Migrate()` roda antes de aceitar tráfego
**And** o código não usa `EnsureCreated()`

**Given** o banco disponível
**When** chamo `GET /health`
**Then** recebo 200
**And** o check do banco executa uma consulta real via `CatalogoDbContext` (por exemplo, `SELECT 1`), não só a abertura da conexão

**Given** o banco indisponível
**When** chamo `GET /health`
**Then** recebo 503
**And** `GET /health/live` continua 200

**Given** a suíte de testes
**When** os testes de integração rodam
**Then** usam uma fixture Testcontainers `postgres:17-alpine`, compartilhada pelas histórias seguintes
**And** não existe connection string no repositório; o `appsettings.json` só tem padrões não sensíveis

### Story 1.3: Traces OpenTelemetry

As a operador,
I want traces de cada requisição,
So that eu observe o serviço no Grafana Cloud (CAP-7; AD-10).

**Acceptance Criteria:**

**Given** a app com o OpenTelemetry configurado e um exporter em memória no teste
**When** faço uma requisição que consulta o banco (por exemplo, `GET /health`)
**Then** são gerados spans de ASP.NET Core e EF Core

**Given** o código da app
**When** procuro endpoint, protocolo ou nome de serviço OTel
**Then** nenhum valor está fixo no código; tudo vem de `OTEL_*`
**And** as instrumentações de ASP.NET Core, HttpClient e EF Core estão registradas

### Story 1.4: CORS configurável por ambiente

As a `catalogo-front`,
I want chamar a API a partir do meu host,
So that o navegador não bloqueie as requisições (AD-8; NFR7).

**Acceptance Criteria:**

**Given** `Cors__OrigensPermitidas="https://a.com/, https://b.com"`
**When** chega uma requisição preflight de `https://a.com`
**Then** ela é permitida para `GET`, `POST`, `PUT` e `PATCH`, com `Location` nos headers expostos
**And** a barra final e os espaços da configuração são ignorados

**Given** uma origem fora da lista
**When** chega uma requisição dela
**Then** a resposta não inclui `Access-Control-Allow-Origin`

**Given** `Cors__OrigensPermitidas` vazia ou com `*`
**When** a app sobe
**Then** o startup falha com uma mensagem clara

### Story 1.5: Imagem de contêiner arm64 chiseled

As a plataforma,
I want uma imagem segura e padronizada,
So that ela passe no Trivy e cumpra o Golden Path (AD-11; NFR1).

**Acceptance Criteria:**

**Given** o `Dockerfile` na raiz
**When** rodo `docker buildx build --platform linux/arm64`
**Then** o estágio SDK roda em `$BUILDPLATFORM` e publica com `-a $TARGETARCH`
**And** a imagem final é `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`, sem `RUN`, com usuário não-root e porta 8080

**Given** o contêiner rodando
**When** o `HEALTHCHECK` em exec form executa `dotnet CatalogoApi.dll --healthcheck`
**Then** o processo faz GET em `http://localhost:8080/health/live` e sai com 0 se saudável ou 1 se não
**And** esse modo é tratado na primeira linha do `Program.cs`, sem subir o servidor nem rodar migrations

### Story 1.6: Esteira e deploy no cluster

As a operador,
I want que um push na `main` publique o serviço,
So that o Golden Path seja validado de ponta a ponta (AD-13; NFR2).

Pré-requisitos externos (fora deste repositório): merge de `fix/trivy-action-sha` no `tcc-pipeline-templates`; overlay `tcc-platform/apps/catalogo-api` com ExternalSecret e `Cors__OrigensPermitidas`; probes do `golden-path/base` em `/health/live` (liveness) e `/health` (readiness); secret `PLATFORM_REPO_TOKEN`.

**Acceptance Criteria:**

**Given** o `ci.yml` com `dotnet-version: "10.0.x"` e os gatilhos `push`/`pull_request` ligados
**When** abro um pull request
**Then** rodam testes e Trivy, sem deploy

**Given** um push na `main`
**When** a esteira termina
**Then** a imagem está no GHCR, a tag foi gravada no overlay e o Argo CD sincronizou
**And** `GET /health` responde 200 pelo Kong
**And** o trace dessa requisição aparece no Grafana Cloud

## Epic 2: Gestão do catálogo de produtos

O cliente (`catalogo-front`) cadastra, lista com filtros e ordenação, consulta, edita, desativa e reativa produtos. Cada história é publicada pela esteira do Épico 1.

### Story 2.1: Cadastrar e consultar produto

As a cliente,
I want cadastrar um produto e consultá-lo pelo id,
So that o catálogo comece a existir (CAP-1, CAP-3; AD-2, AD-4, AD-5, AD-6).

**Acceptance Criteria:**

**Given** um `POST /produtos` válido
**When** a requisição é processada
**Then** recebo 201 com `Location: /produtos/{id}` e um `ProdutoResponse` (`id`, `nome`, `descricao`, `preco`, `estoque`, `status`, `criadoEm`, `atualizadoEm`)
**And** `status` é `Ativo` e `criadoEm = atualizadoEm`, em UTC truncado a microssegundos

**Given** um campo ausente ou fora do limite (nome vazio ou com mais de 200 caracteres, `preco` negativo ou com mais de 2 casas, `estoque` ausente ou negativo, `descricao` com mais de 2000 caracteres)
**When** envio o `POST`
**Then** recebo 400 em `ValidationProblemDetails`
**And** `estoque` ausente não vira `0`

**Given** `descricao` vazia
**When** cadastro o produto
**Then** ela é gravada e devolvida como `null`

**Given** um id existente
**When** faço `GET /produtos/{id}`
**Then** recebo 200 com o mesmo conteúdo devolvido pelo `POST`

**Given** um id inexistente ou não numérico (`/produtos/abc`)
**When** faço `GET`
**Then** recebo 404 em ProblemDetails

**Given** a implementação
**When** reviso o código
**Then** esta história criou a entidade `Produto` e a migration da tabela, com `Status` como texto, `Preco numeric(12,2)` e os limites espelhados no EF
**And** o controller só fala com o Service e o Service só fala com o Repository

### Story 2.2: Listar produtos com paginação, filtros e ordenação

As a `catalogo-front`,
I want buscar e ordenar produtos,
So that o usuário encontre o que procura (CAP-2; AD-7; NFR5).

**Acceptance Criteria:**

**Given** produtos ativos e desativados cadastrados
**When** faço `GET /produtos` sem parâmetros
**Then** recebo 200 com `{itens, total, page: 1, pageSize: 20}`, incluindo ativos e desativados, ordenados por `Nome Asc`

**Given** `nome=cam`
**When** listo
**Then** voltam os produtos cujo nome contém "cam", sem diferenciar maiúsculas
**And** `%` e `_` no termo são tratados como texto literal

**Given** `precoMin` e `precoMax`
**When** listo
**Then** os limites são inclusivos

**Given** `status=Desativado`
**When** listo
**Then** só voltam produtos desativados

**Given** `ordenarPor` e `direcao`
**When** listo
**Then** a ordenação é aplicada, sempre desempatando por `Id` crescente

**Given** `pageSize=0`, `pageSize=101`, `page=0`, `ordenarPor=Xpto`, `status=1` ou `precoMin > precoMax`
**When** listo
**Then** recebo 400 em ProblemDetails

**Given** filtros aplicados
**When** listo
**Then** `total` conta os registros depois dos filtros
**And** uma página além do fim retorna 200 com `itens: []`

**Given** o documento `/openapi/v1.json`
**When** o inspeciono em teste
**Then** `CampoOrdenacao`, `DirecaoOrdenacao` e `StatusProduto` aparecem como enums de string

### Story 2.3: Editar produto

As a cliente,
I want corrigir os dados de um produto,
So that o catálogo fique atualizado (CAP-4; AD-4, AD-6).

**Acceptance Criteria:**

**Given** um produto existente, ativo ou desativado
**When** faço um `PUT /produtos/{id}` válido
**Then** recebo 200 com o produto atualizado e `atualizadoEm` avançado
**And** um `GET` em seguida mostra o mesmo conteúdo

**Given** um `PUT` sem `descricao`
**When** a requisição é processada
**Then** `descricao` passa a `null`, porque o `PUT` é substituição total
**And** `status` não muda pelo `PUT`

**Given** um id inexistente
**When** faço o `PUT`
**Then** recebo 404 em ProblemDetails

**Given** dados inválidos (mesmos limites da 2.1)
**When** faço o `PUT`
**Then** recebo 400 em `ValidationProblemDetails`

### Story 2.4: Desativar e reativar produto

As a cliente,
I want desativar e reativar produtos,
So that o front os mostre de forma diferente sem perder o registro (CAP-5; AD-5).

**Acceptance Criteria:**

**Given** um produto `Ativo`
**When** envio `PATCH /produtos/{id}/status` com `{"status":"Desativado"}`
**Then** recebo 200 com o produto desativado e `atualizadoEm` avançado
**And** o produto continua na listagem e na consulta

**Given** um produto `Desativado`
**When** envio `{"status":"Ativo"}`
**Then** ele é reativado

**Given** o mesmo status que o produto já tem
**When** envio o `PATCH`
**Then** recebo 200 e `atualizadoEm` não muda

**Given** um corpo `{}`, `{"status":1}` ou `{"status":"Xpto"}`
**When** envio o `PATCH`
**Then** recebo 400 em ProblemDetails

**Given** um id inexistente
**When** envio o `PATCH`
**Then** recebo 404 em ProblemDetails

**Given** a API
**When** envio `DELETE /produtos/{id}`
**Then** recebo 405 em ProblemDetails
