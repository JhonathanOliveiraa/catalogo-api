---
type: architecture-review
lens: adversary (pares de unidades conformes porém incompatíveis)
target: ../ARCHITECTURE-SPINE.md
companions-lidos: [SPEC.md, produto.md, golden-path.md]
created: '2026-10-13'
---

# Revisão adversarial — catalogo-api spine

## Veredito

A spine trava bem as camadas e o tratamento de erro "feliz", mas deixa abertos **o formato das respostas de escrita, a semântica de campos ausentes, a configuração JSON, o mapeamento de erros que não nascem no Service e o contrato com o consumidor (front/Kong)**. Além disso, o contrato canônico (SPEC + produto.md) ainda contradiz o AD-5. Duas histórias, ou a API e o `catalogo-front`, podem seguir todos os ADs à risca e ainda assim não encaixar.

Método: para cada buraco, descrevo duas unidades **A** e **B** que obedecem a todos os ADs e convenções, mostro onde elas quebram e proponho o AD ou a convenção mínima que fecha o buraco. A ordem é por probabilidade × impacto.

| # | Buraco | Severidade | Probabilidade |
|---|---|---|---|
| 1 | Contrato canônico contradiz o AD-5 (ativos/DeletadoEm/DELETE) | Crítica | Muito alta |
| 2 | Formato e status das respostas de POST/PUT/PATCH, e PATCH sem efeito | Alta | Muito alta |
| 3 | Campo ausente vira valor default (`{}` reativa produto; PUT zera estoque) | Alta | Alta |
| 4 | Dois conjuntos de opções JSON; caixa e números nos enums | Alta | Alta |
| 5 | Erros fora do Service: id não-int, 404/405/415, JSON malformado, overflow no banco | Alta | Alta |
| 6 | CORS: lista separada por vírgula, PATCH, `Location` e Kong como segundo dono | Alta | Alta |
| 7 | Semântica da listagem: desempate, collation, curingas, limites, página vazia | Média | Alta |
| 8 | Health único usado como liveness; ordem do modo `--healthcheck` | Média | Média |
| 9 | Nulos e datas: `descricao` vazia, `AtualizadoEm` na criação, precisão | Média | Média |
| 10 | Persistência do enum `StatusProduto` | Baixa | Média |

---

## 1. O contrato canônico contradiz o AD-5 — CRÍTICA

**Par:** a história do CAP-2 (agente A) e a do CAP-5 (agente B), ambas geradas a partir do SPEC, mais o `catalogo-front`.

- O banner do `SPEC.md` diz que o SPEC e os companions são *"the complete, preservation-validated contract"*. Mesmo assim:
  - O CAP-2 diz *"lista os produtos **ativos**"*.
  - O CAP-5 ainda descreve a remoção lógica via `DeletadoEm`.
  - As Assumptions listam `DELETE /produtos/{id}`.
  - O `produto.md` ainda tem o campo `DeletadoEm` e a regra "não aparecem na listagem e retornam 404".
- **A** segue o AD-5: a listagem traz todos por padrão. **B**, ou o agente de testes, segue o SPEC (o "contrato canônico"): a listagem filtra `Ativo` por padrão e o teste de aceite do CAP-2 espera que os desativados sumam. O front lê o `produto.md` e espera `deletadoEm` no JSON.
- A spine diz que "substitui o CAP-5 original", mas não declara precedência sobre o CAP-2, as Assumptions nem o `produto.md`, e não define como atualizá-los.

**Correção (AD-5, acrescentar):**
> **Precedência:** onde o SPEC/`produto.md` falarem em `DeletadoEm`, remoção lógica, `DELETE` ou "somente ativos", vale este AD. O campo `DeletadoEm` não existe na entidade, no banco nem no JSON. O texto "lista os produtos ativos" do CAP-2 passa a ser "lista os produtos; o filtro `status` é opcional e, sem ele, vêm todos". O SPEC e o `produto.md` devem ser corrigidos (via correct-course) antes de quebrar em histórias. Até lá, nenhuma história pode citar os trechos antigos como critério de aceite.

---

## 2. Formato das respostas de escrita — ALTA

**Par:** a API (o dev do CAP-4/CAP-5) e o `catalogo-front` (serviço Angular).

- Nenhum AD nem convenção define o que POST, PUT e PATCH devolvem. Algumas combinações conformes:
  - **A:** `PUT` → `204 No Content`; `PATCH /status` → `204`.
  - **B:** `PUT` → `200` + `ProdutoResponse`; `PATCH` → `200` + `ProdutoResponse`.
  - O front faz `http.put<ProdutoResponse>(...)` e atualiza a linha com `null` (contra A), ou faz um GET extra que B dispensaria.
- O `POST` com `201` pode vir com ou sem `Location`, e com ou sem corpo.
- O corpo do PATCH pode ser `{ "status": "Desativado" }`, a string JSON `"Desativado"` ou `?status=` na query. O nome `AlterarStatusProdutoRequest` sugere um objeto, mas não obriga.
- **PATCH sem efeito** (`Ativo` → `Ativo`): A lança `RegraNegocioException` ("produto já está ativo", 400). B devolve 200 idempotente. Um duplo clique no toggle do front dá erro em A. O AD-6 diz que "toda mudança de status" atualiza `AtualizadoEm`, mas não diz se um no-op conta como mudança.
- **Valores pós-persistência:** A monta a resposta com a entidade em memória logo após o `SaveChanges`. Então `preco: 10.999` volta como `10.999` no POST e como `11.00` no GET (`numeric(12,2)` arredonda). `CriadoEm` sai com 7 casas no POST e 6 no GET (o Postgres guarda microssegundos). Um teste de integração que compara POST e GET falha de forma intermitente.

**Correção (nova convenção "Respostas de escrita"):**
> `POST /produtos` → `201` + `Location: /produtos/{id}` + `ProdutoResponse`. `PUT /produtos/{id}` → `200` + `ProdutoResponse`. `PATCH /produtos/{id}/status` → corpo `{ "status": "<StatusProduto>" }`, `200` + `ProdutoResponse`. **PATCH para o status atual é idempotente:** devolve 200 e não altera `AtualizadoEm`. A resposta reflete exatamente o que um GET subsequente devolveria: `Preco` com 2 casas (validado, ver #5) e datas truncadas para microssegundos pelo Service antes de persistir.

---

## 3. Campo ausente vira default — ALTA

**Par:** o dev do DTO `AlterarStatusProdutoRequest`/`AtualizarProdutoRequest` e o `catalogo-front` (ou um teste que manda corpo parcial).

- O AD-4 manda validar "obrigatório" via DataAnnotations. Só que `[Required]` em tipo-valor não anulável (`decimal`, `int`, enum) **nunca falha**: o System.Text.Json preenche o default.
  - `PATCH /status` com `{}` → `Status = Ativo` (valor 0) → **reativa o produto silenciosamente**.
  - `PUT` com `{ "nome": "X", "preco": 5 }` → `Estoque = 0` passa no `≥ 0` → **zera o estoque**.
- A modela `decimal Preco` + `[Required]` (conforme). B modela `decimal? Preco` + `[Required]` (conforme). Só B rejeita o campo ausente.
- **Semântica do PUT** não definida: é substituição total ou merge? A trata `descricao` ausente como "limpar". B trata como "manter". O front que manda só os campos editados perde a descrição em A.
- **Campos desconhecidos:** o front reenvia o objeto inteiro recebido (`id`, `status`, `criadoEm`...) no PUT. A ignora (padrão do STJ). B liga `UnmappedMemberHandling.Disallow` e devolve 400. Em A, `status: "Desativado"` dentro do PUT é ignorado em silêncio e o usuário acha que desativou.

**Correção (AD-4, acrescentar):**
> Nos DTOs de request, todo campo obrigatório de tipo-valor ou enum é declarado **anulável** (`decimal?`, `int?`, `StatusProduto?`) com `[Required]`, para que a ausência resulte em 400. `PUT` é **substituição total**: `nome`, `preco` e `estoque` são obrigatórios, e `descricao` ausente ou `null` limpa o campo. Membros desconhecidos no JSON são **ignorados** (padrão do System.Text.Json; `UnmappedMemberHandling.Disallow` é proibido). O front não deve esperar que `status` no corpo do PUT tenha efeito.

---

## 4. Opções JSON e enums — ALTA

**Par:** o dev dos controllers, o dev do middleware de erros e o dev do OpenAPI. O front e a geração de cliente consomem o resultado.

- O ASP.NET Core tem **dois** conjuntos de opções: `AddControllers().AddJsonOptions(...)` (MVC) e `ConfigureHttpJsonOptions(...)` (`Microsoft.AspNetCore.Http.Json.JsonOptions`, usado por `IProblemDetailsService`/`Results.Problem` e na geração de schema do `Microsoft.AspNetCore.OpenApi`).
  - A registra `JsonStringEnumConverter` só no MVC: as respostas saem com enum string, mas o OpenAPI pode exibir enums inteiros, e o front gerado a partir do OpenAPI tipa `status: number`.
  - B, no middleware, serializa com `JsonSerializer.Serialize(problem)` sem opções, e o corpo sai `"Title"`/`"Status"` em PascalCase.
- **Caixa do valor:** A usa `new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` (copiado de exemplos comuns) e devolve `"ativo"`. B usa o conversor sem política e devolve `"Ativo"`. Os dois respeitam o AD-7 ("pelo nome, sem diferenciar maiúsculas" na **entrada**), mas o front compara `p.status === 'Ativo'`.
- **Números:** o `JsonStringEnumConverter` aceita inteiros por padrão (`allowIntegerValues: true`). No model binding da query, `?ordenarPor=7` é convertido para `(CampoOrdenacao)7` sem erro. O AD-7 diz "valor fora do enum → 400", e A confia no conversor enquanto B põe `[EnumDataType]`. Só B cumpre.

**Correção (AD-7, acrescentar):**
> A **mesma** configuração JSON (camelCase; `JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)`; `Descricao` com `JsonIgnore(WhenWritingNull)`) é aplicada às opções do MVC **e** às do `Http.Json`, por um único método de extensão. Na saída, o enum é o **nome C# exato** (`Ativo`, `Desativado`, `Nome`, `Preco`, `CriadoEm`, `Asc`, `Desc`). Valores numéricos são rejeitados na query e no corpo: toda propriedade enum de request leva `[EnumDataType(typeof(T))]`. O middleware de erro escreve via `IProblemDetailsService`, nunca por `JsonSerializer` cru.

---

## 5. Erros que não passam pelo Service — ALTA

**Par:** o dev do controller (rotas e binding) e o dev do middleware. O front depende de sempre receber ProblemDetails.

O AD-3 cobre as exceções de domínio e o 400 de binding. Ficam de fora:

| Entrada | Dev A (conforme) | Dev B (conforme) |
|---|---|---|
| `GET /produtos/abc` | rota `{id:int}`: **404 com corpo vazio** | `{id}` + `int id`: 400 `ValidationProblemDetails` |
| `DELETE /produtos/1` (não existe por AD-5) | 405 com corpo vazio | idem |
| rota inexistente | 404 com corpo vazio | idem |
| `Content-Type` errado | 415 com corpo vazio | idem |
| JSON malformado / enum inválido no corpo | 400 com chave `$.status` + erro "request field is required" | o mesmo, mas o front mapeia `errors.status` (a chave da query) e não acha nada |
| `preco: 1e12` (> `numeric(12,2)`) | `DbUpdateException` → **500** | idem |
| `preco: 10.999` | arredondado em silêncio (#2) | idem |
| `nome` com 5000 caracteres e EF configurado `HasMaxLength(200)` | **500** | B não limitou: `text`, aceita |
| `precoMin=50&precoMax=10` | A: `IValidatableObject` → 400 com `errors` | B: Service → `RegraNegocioException` → 400 **sem** `errors` |

**Correção (AD-3 e AD-4, acrescentar):**
> Rotas com id usam a restrição `{id:int}`. `AddProblemDetails()` + `UseStatusCodePages()` garantem ProblemDetails também em 404/405/415 vindos do framework. Id não numérico → 404. Limites de entrada definidos na spine e espelhados no DTO e na configuração EF: `Nome` 1–200 caracteres, `Descricao` ≤ 2000, `Preco` 0–9 999 999 999,99 com no máximo 2 casas decimais (`[Range]` + validação de escala), `Estoque` 0–`int.MaxValue`. Regra cruzada `precoMin ≤ precoMax` fica em `IValidatableObject` no `ListarProdutosQuery`: é formato, não estado. Saída: `ValidationProblemDetails`. `RegraNegocioException` também sai como `ValidationProblemDetails` quando carrega o nome do campo. Nenhuma entrada válida segundo os DTOs pode gerar 500 no banco.

---

## 6. CORS: formato da chave e dono duplicado — ALTA

**Par:** o dev do `Program.cs` e o overlay no `tcc-platform`. Também a API contra o Kong.

- `Cors__OrigensPermitidas` é "lista separada por vírgula". A lê com `GetSection("Cors:OrigensPermitidas").Get<string[]>()`: um valor escalar não é dividido, o array vem vazio e **o front é bloqueado**. B lê como string e faz `Split(',')`. Se o `appsettings.json` de A tiver o padrão como array JSON (`["http://localhost:4200"]`), as chaves viram `Cors:OrigensPermitidas:0` e convivem com a escalar do overlay.
- O overlay escreve `https://catalogo.hml.exemplo/, https://outro` (barra final e espaço). A origem nunca bate com o header `Origin`.
- A copia um template com `.WithMethods("GET","POST","PUT","DELETE")`: o preflight do **PATCH** falha. O front lê `Location` do 201, mas o header não está exposto.
- **Dois donos:** o AD-8 põe o CORS na app, e a "rota Kong" foi adiada para o overlay. Se o overlay ligar o plugin `cors` do Kong, a resposta sai com `Access-Control-Allow-Origin` duplicado e o navegador rejeita.

**Correção (AD-8, acrescentar):**
> `Cors__OrigensPermitidas` é lido como **string**, dividido por `,`, com `Trim()` e barra final removida em cada item. `*` e itens vazios são rejeitados no startup, com falha explícita. O padrão em `appsettings.json` também é string. A política libera `GET, POST, PUT, PATCH` e qualquer header, e expõe `Location`. **A app é a única dona do CORS:** o overlay/Kong não habilita o plugin `cors` nessa rota. O Kong encaminha sem prefixo (`strip_path`), e a app serve na raiz.

---

## 7. Semântica da listagem — MÉDIA

**Par:** o dev do Repository e o front (paginação) ou o teste de integração.

- **Desempate:** o padrão `ordenarPor=Nome`, com nomes repetidos, é instável no Postgres entre `OFFSET`s: um item aparece em duas páginas e outro em nenhuma. A ordena só por `Nome`; B acrescenta `ThenBy(Id)`.
- **Collation:** o Testcontainers sobe o Postgres com `en_US.utf8`, enquanto o banco da plataforma pode estar em `C`/`C.UTF-8` (aí "Zebra" vem antes de "abacate"). O teste passa e a produção ordena diferente. O AD-12 não fecha isso.
- **`nome` "contém":** A usa `EF.Functions.ILike($"%{nome}%")` e `"50%"` casa com tudo. B usa `ToLower().Contains()`, que escapa os curingas. Nenhum dos dois ignora acentos.
- **Limites de preço:** inclusivos ou exclusivos?
- **Página além do fim:** 200 com `itens: []`, ou 404?
- **Filtro `status` ausente** = todos (AD-5). Mas o front da vitrine vai querer `status=Ativo`, e ninguém registrou isso como requisito do consumidor.

**Correção (convenção Listagem, acrescentar):**
> Ordenação sempre com `Id` como critério secundário, na mesma direção. Ordenação de texto pela collation do banco, com uma migration que fixa a collation da coluna `Nome` (ex.: `und-x-icu`, determinística), para os testes e a produção coincidirem. `nome` faz *contains* sem diferenciar maiúsculas, com `%`, `_` e `\` escapados. `precoMin`/`precoMax` são inclusivos. Página além do fim → 200 com `itens: []` e `total` real. Sem `status` → todos.

---

## 8. Health, liveness e ordem do startup — MÉDIA

**Par:** a API (`/health` com o check do banco) e o `golden-path/base`/overlay (probes adiadas).

- O AD-11 define um `/health` **único** que dá 503 quando o banco cai. Se a plataforma o usar como **liveness**, uma queda do Postgres reinicia todos os pods em cascata, e cada reinício roda `Migrate()` contra um banco fora, o que vira CrashLoopBackOff. A app está conforme e a plataforma está conforme ("probes definidas fora deste repo"), mas o resultado quebra.
- **Modo `--healthcheck`:** A faz o branch depois de `WebApplication.CreateBuilder` e do registro do OTel. O processo do probe exporta spans do HttpClient a cada 30 s e lê configuração/segredos. B faz o branch na primeira linha. Os dois cumprem "sem iniciar o servidor nem rodar migrations".
- **Forma do HEALTHCHECK:** a imagem chiseled não tem shell, então só a forma exec (`["dotnet","CatalogoApi.dll","--healthcheck"]`) funciona. A forma shell falha sempre.
- Cada probe gera um trace com span de EF no Grafana Cloud (CAP-7 com ruído).

**Correção (AD-11, acrescentar):**
> `/health` é a probe de **readiness**. A liveness da plataforma não usa `/health`: usa `tcpSocket:8080` ou um `/health/live` sem dependências, se ele for criado. O modo `--healthcheck` é tratado na **primeira instrução** de `Program.cs`, antes de qualquer builder, DI ou OTel, com `HttpClient` cru e timeout de 3 s. O `HEALTHCHECK` usa a forma exec. Requisições a `/health` são excluídas da instrumentação ASP.NET Core (`Filter`).

---

## 9. Nulos e datas — MÉDIA

**Par:** o Service do CAP-1 e o Service do CAP-4, com o front tipando `ProdutoResponse`.

- `descricao: ""`: A grava `""` e devolve `""`. B normaliza para `null` e a omite. O front tipa `descricao?: string` e a tela mostra coisas diferentes depois de criar e depois de editar.
- `AtualizadoEm` na criação: o `produto.md` diz "a cada edição". A deixa `null` (`DateTimeOffset?`) e devolve `"atualizadoEm": null`, o que viola a convenção de `null` "apenas em descricao" de forma ambígua: a convenção fala de omissão, não de proibição. B iguala a `CriadoEm`.
- A usa `DateTime.UtcNow`, B usa `TimeProvider`. Os testes de unidade de A não conseguem fixar o horário. Com `DateTimeOffset.Now` (offset −03:00), o Npgsql lança exceção ao gravar `timestamptz`.

**Correção (convenções JSON/Datas):**
> `Descricao` vazia ou só com espaços é normalizada para `null` pelo Service. `Nome` sofre `Trim()`. `AtualizadoEm` é não anulável e, na criação, igual a `CriadoEm`. Nenhum campo além de `descricao` pode ser `null` ou ausente na resposta. Datas vêm de `TimeProvider` injetado (`GetUtcNow()`), truncadas para microssegundos.

---

## 10. Persistência do enum — BAIXA

**Par:** a migration inicial e uma história futura que reordena ou acrescenta valores ao `StatusProduto`.

- Sem conversão, o EF grava o enum como `int`. Reordenar `{ Desativado, Ativo }` inverte o significado dos dados existentes. Um `HasDefaultValue(StatusProduto.Ativo)` com `Ativo = 0` cai no problema de sentinel do EF.

**Correção (AD-5):**
> `Status` é persistido como texto (`HasConversion<string>()`, `varchar(20)`), sem default no banco: o Service é a única fonte do valor inicial (AD-6).

---

## Itens checados que estão fechados

- Direção de camadas, DTO ≠ entidade e exceções → middleware: sem ambiguidade relevante.
- Lock de migrations concorrentes: o EF Core 9+ com Npgsql usa advisory lock, então está coberto.
- Cultura dos decimais na query: o provider de query string do ASP.NET usa `InvariantCulture`. `10,50` vindo do front pt-BR dá 400, o que é correto, mas convém registrar no contrato do front.
