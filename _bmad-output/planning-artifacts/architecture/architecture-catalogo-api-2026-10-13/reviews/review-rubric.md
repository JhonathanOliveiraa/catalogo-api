# Review (rubric): ARCHITECTURE-SPINE — catalogo-api

- **Alvo:** `../ARCHITECTURE-SPINE.md` (draft, 2026-10-13)
- **Base:** `spec-catalogo-api` (SPEC.md, produto.md, golden-path.md) e `.memlog.md`
- **Fora do escopo:** a redefinição do CAP-5 (Status Ativo/Desativado, sem DELETE nem `DeletadoEm`) é intencional e não conta como divergência.

## Veredito

**Pronto com ajustes.** O spine está enxuto e coerente, e a maioria das regras dá para verificar. Camadas, fronteiras de DTO, erros, validação, enums, configuração, migrations, OTel, contêiner, testes e runtime estão decididos. O que falta é o **contrato HTTP de resposta por rota** (status e corpo de sucesso do PUT/PATCH, `Location`, PATCH sem mudança de estado). Esse é o ponto em que stories paralelas mais vão divergir. Há também lacunas menores: ProblemDetails fora do caminho de exceções, enforcement de enum por nome, probe vs. `/health` com banco, CORS de métodos e a falta de uma seção de Open Questions.

## Checklist

| Critério | Resultado | Nota |
|---|---|---|
| Fixa os pontos reais de divergência da camada de baixo | Parcial | Falta o contrato de sucesso por rota (F1). Também faltam validação cruzada `precoMin`/`precoMax` e ordenação estável (F6). |
| Cada Rule é aplicável e previne a divergência declarada | Quase | AD-7 não diz como barrar valores numéricos (F3). AD-3 não cobre erros fora de exceção (F2). AD-1 só vale "por convenção" e não tem verificação (F8). |
| Nada em Deferred permite divergência | Parcial | "Probes" e "Hospedagem/versão do PostgreSQL" podem divergir da app e dos testes (F4, F7). "Rota Kong" pode afetar o path base (F5). |
| Cobre capabilities e constraints do spec | Sim, com ressalvas | CAP-1..7 mapeados. Requisitos silenciosos: Location/201 do CAP-1, gatilhos de CI e `PLATFORM_REPO_TOKEN`, confirmação das variáveis `OTEL_*` (F1, F5, F9). |
| Dimensões da altitude decididas, adiadas ou em aberto | Parcial | Deploy e ambientes estão esboçados (hml/prod como overlays). Ficam sem decisão: `ASPNETCORE_ENVIRONMENT`, exposição do OpenAPI por ambiente, saída de logs (stdout/formato) e compatibilidade de migration com rollback. Não existe seção de Open Questions (F5, F9). |
| Contradições internas | Pequenas | AD-8 "a app lê só estas chaves" vs. chaves `ASPNETCORE_*` e `Logging`, que a app necessariamente lê (F9). |
| Sobre-especificação | Baixa | Ver F10. |

## Achados

### F1 — Contrato de sucesso por rota não está fixado — **high**

**Onde:** Conventions › Rotas, AD-2 e AD-5.
**Problema:** Só as rotas estão fixadas, não as respostas. Ficam em aberto:
- se o `PUT` devolve 200 com `ProdutoResponse` ou 204;
- se o `PATCH /status` devolve 200 com corpo ou 204;
- se o `POST` 201 traz `Location` (o CAP-1 exige "201 com o recurso");
- o que acontece num `PATCH` que não muda o estado (Ativo→Ativo): 200 idempotente ou 400 via `RegraNegocioException`;
- a semântica do `PUT`: substituição total, e se omitir `descricao` a apaga.

Cada story de endpoint vai escolher sozinha, e o `catalogo-front` vai depender da escolha.
**Correção:** acrescentar uma linha "Respostas" às Conventions, por exemplo: `POST` → 201 + `Location: /produtos/{id}` + `ProdutoResponse`; `GET {id}`/`PUT`/`PATCH` → 200 + `ProdutoResponse`; `PATCH` para o status atual → 200 idempotente, sem tocar em `AtualizadoEm` (ou 400, mas decidido); `PUT` é substituição completa, e `descricao` ausente vira `null`.

### F2 — "Todo erro é ProblemDetails" só vale para exceções e model binding — **medium**

**Onde:** AD-3.
**Problema:** Algumas respostas de erro não passam pelo middleware nem pelo model binding e saem com corpo vazio:
- 404 de rota inexistente;
- `/produtos/abc` com a restrição `{id:int}`;
- 405 de método não suportado;
- 415 de content-type errado.

O memlog decidiu que "toda resposta de erro é ProblemDetails", mas a Rule não garante isso.
**Correção:** acrescentar à Rule: "`AddProblemDetails()` + `UseStatusCodePages()`; toda resposta 4xx/5xx sem corpo também sai em ProblemDetails". Fixar `{id:int}` como restrição de rota.

### F3 — AD-7 não impede valores numéricos de enum — **medium**

**Onde:** AD-7 e AD-4.
**Problema:** No ASP.NET Core, o binding padrão aceita `?ordenarPor=1` e até `?ordenarPor=99` (`Enum.TryParse` aceita números, inclusive os não definidos). O `JsonStringEnumConverter` aceita inteiros por padrão. A Rule diz "pelo nome; fora do enum → 400", mas cada implementador vai aplicar isso de um jeito diferente, ou não vai aplicar.
**Correção:** declarar o mecanismo na Rule: `JsonStringEnumConverter(allowIntegerValues: false)` e `[EnumDataType(typeof(T))]` (ou um validador equivalente) em cada propriedade enum dos DTOs e da query. Incluir um teste de integração para `?direcao=1` → 400.

### F4 — `/health` com o banco + probes adiados = risco de restart em cascata — **medium**

**Onde:** AD-11 e Deferred › Probes.
**Problema:** A app expõe um único `/health`, que devolve 503 com o banco fora. As probes ficam no `golden-path/base`, fora do repo. Se a liveness apontar para `/health`, uma queda do Postgres reinicia todos os pods. App e plataforma podem divergir aqui sem que nenhuma das duas veja.
**Correção:** uma de duas:
- (a) adicionar `GET /health/live` sem o check do banco, para liveness, mantendo `/health` (com o banco) para readiness e para o CAP-6;
- (b) registrar como premissa/Open Question que o `golden-path/base` usa `/health` só como readiness.

### F5 — Falta uma seção de Open Questions; dependências de plataforma assumidas em silêncio — **medium**

**Onde:** o documento inteiro, Deferred e AD-8/AD-10.
**Problema:** Pontos que o spec mandou confirmar ou que dependem do `tcc-platform` aparecem como fato ou como "fora do repo":
- **Variáveis OTel:** o spec diz "confirmar no `golden-path/base`" sobre `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_SERVICE_NAME`.
- **Kong:** a rota pode usar prefixo com ou sem strip-path. Se não remover o prefixo, a app precisaria de `UsePathBase`, o que afeta rotas, `Location` e CORS.
- **CORS:** o nome real da chave no overlay.
- **Ambiente:** o valor de `ASPNETCORE_ENVIRONMENT` em hml/prod. Ele decide se o OpenAPI é mapeado (o template padrão só mapeia em Development).

**Correção:** criar "Open Questions" com esses itens, cada um com um dono (`tcc-platform`), e decidir agora a exposição do OpenAPI: sempre, ou só fora de prod.

### F6 — Listagem: validação cruzada e ordenação estável sem dono — **medium**

**Onde:** AD-4 e Conventions › Listagem.
**Problema:**
- `precoMin > precoMax` não é "formato" (DataAnnotations por propriedade) nem "estado/banco" (Service). A Rule de dois níveis não diz onde isso fica, nem se vira 400 ou lista vazia.
- Ordenar por `Nome`/`Preco` sem desempate deixa a paginação instável: itens repetem ou somem entre páginas.
- O filtro `nome` (contém, sem diferenciar maiúsculas) pode sair como `ILIKE`, `ToLower()` ou collation, cada um com comportamento diferente.

**Correção:**
- `precoMin > precoMax` → 400 via `IValidatableObject` no `ListarProdutosQuery` (ainda no nível DTO);
- ordenação sempre com `Id` como critério secundário;
- `nome` via `EF.Functions.ILike` com o curinga escapado.

### F7 — Versão do Postgres dos testes vs. produção — **low**

**Onde:** Deferred › Hospedagem e versão do PostgreSQL, e AD-12.
**Problema:** A versão foi adiada para a plataforma, mas o Testcontainers precisa de uma tag de imagem. Se não estiver fixada, o teste roda em `latest` ou numa major diferente da de produção.
**Correção:** fixar a major nos testes (por exemplo `postgres:17`) e incluir "alinhar com a major do `tcc-platform`" nas Open Questions.

### F8 — AD-1 sem mecanismo de verificação — **low**

**Onde:** AD-1.
**Problema:** "Por convenção, não pelo compilador" deixa a Rule dependente de review. O mesmo vale para "só `Repositories/` e `Data/` referenciam EF Core".
**Correção:** acrescentar ao AD-12 um teste de arquitetura (NetArchTest ou ArchUnitNET) que falhe se `Controllers` referenciar `Microsoft.EntityFrameworkCore`/`Repositories`, ou se `Repositories` referenciar `Contracts`. É barato e torna a regra aplicável.

### F9 — Operações: lacunas e contradições pequenas — **low**

- **AD-8:** "a app lê só estas chaves" é falso na prática (`ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_HTTP_PORTS`, `Logging__*`). Trocar para "chaves **da aplicação**".
- **CORS:** a Rule fixa só as origens. Se os métodos não forem liberados, o preflight de `PUT`/`PATCH` do front falha. Fixar: métodos `GET, POST, PUT, PATCH`, header `Content-Type`, `Location` exposto.
- **Logs:** os logs OTLP foram adiados, mas a saída atual não está decidida (stdout, texto ou JSON). Fixar "console JSON em stdout" ou declarar como adiado.
- **Migrations vs. rollback:** o Argo pode voltar a imagem, e o schema não volta. Basta uma linha: "migrations só aditivas/compatíveis com a versão anterior", ou declarar como adiado.
- **Golden Path:** o `PLATFORM_REPO_TOKEN` e a ativação dos gatilhos aparecem só num comentário do seed. Tudo bem para o spine, mas vale citar no Capability Map (Golden Path → `ci.yml`).
- **Fonte de tempo:** AD-6 não diz se é `DateTimeOffset.UtcNow` ou `TimeProvider`. Fixar `TimeProvider` injetado evita testes com relógio divergente (opcional).

### F10 — Sobre-especificação — **low**

O spine está enxuto. Itens que podem sair ou encolher:
- **Stack:** "(LTS, suporte até 2028-11)" e "(EF Core ainda em beta)" são justificativa. O memlog já guarda isso.
- **AD-12:** "Testes de unidade cobrem o Service com Repository simulado" é estratégia de teste, não previne divergência entre unidades. Pode sair, a menos que se queira proibir outro estilo.
- **AD-9:** "O usuário do banco precisa de permissão DDL" é uma exigência à plataforma, não uma regra da app. Mover para Open Questions/premissas.
- **Conventions › Logs:** "sem dados sensíveis" não é verificável. Manter só "templates estruturados".

## O que está bom

- O AD-3 e o AD-4 juntos eliminam o principal ponto de divergência de um CRUD em camadas (onde validar e como errar).
- O AD-5 fecha a redefinição do CAP-5 sem ambiguidade: sem `DELETE`, sem filtro global, reativação permitida e `AtualizadoEm` também na mudança de status.
- O AD-8 nomeia as chaves de configuração, o que impede o descasamento app × overlay.
- O AD-9 resolve réplicas concorrentes sem um mecanismo próprio e proíbe `EnsureCreated`.
- O AD-11 evita shell/curl na imagem chiseled com o modo `--healthcheck`.
- O AD-13 pega a armadilha do default `9.0.x` do template.
- Deferred lista apenas itens que de fato não criam divergência, com as exceções de F4/F7.
