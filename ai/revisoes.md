# Revisões de sugestões de IA — TaskFlow

## Objetivo

Este documento registra sugestões produzidas com apoio de IA que foram
submetidas a revisão durante o desenvolvimento da TaskFlow. O registro inclui
sugestões aceitas, ajustadas e rejeitadas.

A presença de uma sugestão neste documento não significa aceitação automática.
O desenvolvedor permaneceu responsável por confrontá-la com os requisitos, o
`openapi.yaml`, o comportamento real do ASP.NET Core e do EF Core, os testes e
o nível de complexidade adequado ao projeto.

Um resumo por veredito está na [Conclusão](#conclusão).

---

## 1. Versão do OpenAPI: 3.1.1 e reversão para 3.0.3

### Sugestão da IA

Usar OpenAPI 3.0 como base inicial da especificação.

### Problema ou risco identificado

Durante a especificação, o contrato foi movido para `openapi: 3.1.1` para
representar nulabilidade com o modelo de tipos do JSON Schema 2020-12
(`type: [string, 'null']`) em vez de `nullable: true`.

Numa auditoria posterior contra o enunciado, o item 1.1 pede explicitamente
**OpenAPI 3.0**. O documento em 3.1.1 era, portanto, um desvio da instrução
literal, além de poder não validar em ferramentas presas ao 3.0
(as sugeridas na Etapa 3, como NJsonSchema e Schemathesis).

### Análise

A versão do documento não é só metadado: muda a sintaxe de nulabilidade e a
semântica dos Schema Objects. A vantagem do 3.1.1 (tipagem de `null` precisa)
não superava o custo de divergir do que o desafio pede e do ferramental
esperado pelo avaliador.

### Decisão tomada

O contrato foi revertido para `openapi: 3.0.3`. Campos anuláveis voltaram a
usar `nullable: true`. O validador de contrato dos testes ganhou um shim que
traduz `nullable: true` para uma união de tipos com `null` ao tratar os
fragmentos de schema como JSON Schema 2020-12.

### Resultado

O `openapi.yaml` está em 3.0.3, aderente ao item 1.1 do desafio, e continua
sendo a fonte da verdade para código e testes. Os 120 testes de contrato
seguem passando. Trade-off aceito: a tipagem de `null` é menos explícita do
que no 3.1.

---

## 2. Declaração explícita de ausência de segurança

### Sugestão da IA

Deixar a propriedade `security` ausente porque autenticação não fazia parte do
desafio.

### Problema ou risco identificado

A ausência poderia ser interpretada como omissão acidental ou deixar o
comportamento dependente de uma futura definição global. O contrato precisava
mostrar que a falta de autenticação era deliberada.

### Análise

Não implementar autenticação era coerente com o escopo. Entretanto, essa
decisão deveria estar visível para revisores e ferramentas, sem criar JWT,
usuários ou autorização fictícia.

### Decisão tomada

A sugestão foi ajustada: foi incluído `security: []` no nível raiz.

### Resultado

O contrato comunica explicitamente que as oito operações não exigem esquema de
segurança. A limitação para uso em produção ficou registrada em
`docs/decisoes.md`.

---

## 3. Nulabilidade e distinção "ausente vs. null" no PATCH

### Sugestão da IA

Modelar `description` e `completedAt` como simples strings anuláveis e confiar
em propriedades C# `string?` para o PATCH.

### Problema ou risco identificado

Propriedade C# apenas anulável não distingue "campo omitido" de "campo enviado
como `null`". No PATCH isso muda o comportamento: omitir mantém o valor; enviar
`null` limpa (quando o schema permite).

### Análise

A distinção precisa existir no DTO de entrada, independentemente da versão do
contrato. `description` aceita `null`; `name`, `title`, `status` e `priority`
não.

### Decisão tomada

A sugestão simples foi rejeitada.

- No contrato (3.0.3): campos anuláveis usam `nullable: true`.
- Na implementação: presença explícita via `Optional<T>` com conversor JSON,
  distinguindo ausência de `null`.

### Resultado

O PATCH aplica `null` apenas onde o schema permite e trata campo ausente como
"manter". Campos não anuláveis enviados como `null` retornam `400`.

---

## 4. Serialização de `InProgress`

### Sugestão da IA

Usar a conversão camelCase padrão dos enums C#, que produziria `inProgress` para
o membro `InProgress`.

### Problema ou risco identificado

O contrato define literalmente `in_progress`. Aceitar ou emitir `inProgress`
criaria incompatibilidade nos requests, responses e filtros.

### Análise

Uma política genérica de nomes não era suficiente, pois o contrato usa
snake_case para esse valor e exige enums estritos em letras minúsculas. Também
era necessário rejeitar números e variações como `IN_PROGRESS` (ver também a
revisão 13).

### Decisão tomada

A sugestão genérica foi rejeitada. Foi implementado um conversor explícito de
enums e parsers contratuais para query strings.

### Resultado

A API aceita e serializa exatamente `pending`, `in_progress` e `done`, além de
rejeitar representações fora do OpenAPI com `400`.

---

## 5. Separação entre 400, 404 e 422

### Sugestão da IA

Usar `400 Bad Request` para qualquer operação que não pudesse ser concluída.

### Problema ou risco identificado

Essa abordagem misturaria falhas estruturais, inexistência de recurso e regras
dependentes do estado atual, empobrecendo o contrato e dificultando clientes e
testes.

### Análise

Foi adotada a ordem de avaliação:

```text
contrato de entrada -> existência do recurso -> regra de negócio
```

JSON inválido, enum inválido, UUID inválido e PATCH vazio pertencem a `400`.
Um identificador válido que não encontra recurso pertence a `404`. Uma operação
válida sobre recurso existente, impedida por seu estado, pertence a `422`.

### Decisão tomada

A sugestão foi rejeitada. O contrato e a implementação passaram a distinguir
explicitamente `400`, `404` e `422`.

### Resultado

Erros de entrada usam `ValidationProblemDetails`; inexistência e regras de
negócio usam `ProblemDetails` com códigos estáveis e
`application/problem+json`.

---

## 6. Máquina de estados da tarefa: `pending -> done` e atribuição direta

### Sugestão da IA

Duas sugestões relacionadas:

1. Permitir concluir uma tarefa diretamente a partir de `pending`, por ser uma
   transição comum em sistemas simples.
2. Expor um setter de `TaskItem.Status` e atribuí-lo direto a partir do DTO de
   PATCH, validando apenas se o valor do enum existe.

### Problema ou risco identificado

O enunciado exige o fluxo `pending -> in_progress -> done` e proíbe retroceder,
mas não veda explicitamente o atalho. Validar apenas a existência do valor do
enum permitiria `pending -> done` e todos os retrocessos, reduzindo a máquina de
estados a uma convenção de interface, sem proteção no domínio.

### Análise

As alternativas foram explicitadas antes da implementação. A escolha afeta
contrato, domínio, resposta `422`, preenchimento de `completedAt` e testes.
"Seguir o fluxo" foi interpretado como passar por cada etapa — leitura estrita e
deliberada.

### Decisão tomada

Ambas as sugestões foram rejeitadas. `Status` tem setter privado e toda mudança
passa por `TaskItem.TransitionTo`. O único fluxo progressivo aceito é:

```text
pending -> in_progress -> done
```

### Resultado

`pending -> done` e todo retrocesso retornam `422 INVALID_TASK_STATUS_TRANSITION`.
`done` é terminal, reenviar o status atual é idempotente, e a entidade controla
o preenchimento de `completedAt` sem usar exception como fluxo normal.

---

## 7. `completedAt` nos requests

### Sugestão da IA

Incluir `completedAt` no DTO de atualização para permitir que o cliente
informasse a data de conclusão.

### Problema ou risco identificado

Isso permitiria datas arbitrárias, conclusão sem transição válida e
inconsistência entre `status` e `completedAt`.

### Análise

O timestamp é consequência da transição de domínio, não dado de entrada. A
presença de `completedAt` nos schemas de request também contrariaria sua
definição como `readOnly` na resposta.

### Decisão tomada

A sugestão foi rejeitada. `completedAt` foi removido de todos os requests e
permaneceu apenas em `TaskResponse`.

### Resultado

O servidor preenche `completedAt` somente em `in_progress -> done`. Tentativas
do cliente de enviar a propriedade retornam `400`, e `done -> done` preserva o
valor original.

---

## 8. Uso direto de `DateTime.UtcNow`

### Sugestão da IA

Gerar `createdAt` e `completedAt` diretamente com `DateTime.UtcNow` dentro das
entidades ou dos casos de uso.

### Problema ou risco identificado

O relógio global tornaria os testes dependentes do tempo real e acoplaria a
regra de negócio à infraestrutura temporal do processo.

### Análise

O requisito já previa horários UTC e testes determinísticos. O .NET 8 fornece
`TimeProvider`, eliminando a necessidade de uma abstração de relógio própria.

### Decisão tomada

A sugestão foi rejeitada. `TimeProvider` é injetado nos casos de uso, e o
horário é fornecido às entidades nas operações relevantes.

### Resultado

Os timestamps continuam controlados pelo servidor e em UTC, enquanto os testes
usam um `TimeProvider` fixo sem acessar `DateTime.UtcNow` na regra de negócio.

---

## 9. Formato de saída das datas

### Sugestão da IA

Serializar `DateTimeOffset` no formato padrão do `System.Text.Json`
(`2026-08-30T21:04:28.4471234+00:00`).

### Problema ou risco identificado

O formato default é verboso, tem sete casas de fração e usa `+00:00`, enquanto
os exemplos do `openapi.yaml` usam `Z` (`2026-08-29T18:00:00Z`). Não é
incompatível com `format: date-time`, mas fica inconsistente com o contrato e
com o que clientes JavaScript emitem.

### Análise

O domínio deveria continuar em `DateTimeOffset` (offset explícito); apenas a
representação de saída precisava ser fixada. Segundos puros descartariam
precisão real por estética.

### Decisão tomada

A sugestão foi ajustada. Um `JsonConverter<DateTimeOffset>` normaliza a saída
para UTC em RFC 3339 com milissegundos e sufixo `Z`
(`2026-08-30T21:04:28.447Z`). O `openapi.yaml` não precisou mudar.

### Resultado

Todas as datas expostas seguem um formato único, alinhado aos exemplos do
contrato e aos testes, sem alteração no domínio.

---

## 10. EF Core InMemory nos testes

### Sugestão da IA

Usar o provider EF Core InMemory para simplificar os testes de integração.

### Problema ou risco identificado

O provider não reproduz o comportamento relacional do SQLite, incluindo SQL,
foreign keys, índices, transações e várias diferenças de consulta e
persistência.

### Análise

Os testes pretendiam validar a aplicação ASP.NET Core real e sua integração com
a tecnologia de persistência escolhida. Substituir SQLite por outro modelo de
banco reduziria a fidelidade justamente na fronteira testada.

### Decisão tomada

A sugestão foi rejeitada. Foi adotado SQLite in-memory isolado, com conexão
âncora durante a fixture e conexões reais por `DbContext`.

### Resultado

Os testes continuam rápidos, mas exercitam EF Core sobre SQLite e passam pelo
relacionamento, pelas constraints e pelas migrations reais.

---

## 11. Repository genérico e Unit of Work customizado

### Sugestão da IA

Adicionar `IRepository<T>` e uma Unit of Work própria para abstrair o EF Core.

### Problema ou risco identificado

As abstrações apenas repetiriam `DbSet` e `DbContext`, esconderiam recursos
úteis como `AsNoTracking`, `AnyAsync` e projeções, e acrescentariam indireção
sem um segundo mecanismo de persistência ou necessidade de substituição.

### Análise

O projeto possui oito operações e consultas específicas pequenas. O próprio
`DbContext` já representa a unidade de trabalho da requisição. Uma interface
CRUD genérica tenderia a vazar `IQueryable` ou acumular métodos específicos.

### Decisão tomada

A sugestão foi rejeitada. Os casos de uso acessam diretamente o
`TaskFlowDbContext`.

### Resultado

As consultas permanecem explícitas, rastreáveis ao OpenAPI e sem uma camada que
apenas repassa chamadas.

---

## 12. Ausência de migrations na aplicação real

### Sugestão da IA

O código inicial, gerado com apoio de IA, provisionava o schema apenas pela
factory de testes (`EnsureCreated()`) e deixava a aplicação real depender de um
banco já existente.

### Problema ou risco identificado

No code review, a API real foi iniciada com SQLite vazio e retornou `500` por
`no such table: Projects`. A suíte passava porque seu setup mascarava a ausência
do schema.

### Análise

Esse foi um desvio crítico: todos os endpoints dependiam de uma preparação que
não existia fora dos testes. `EnsureCreated()` também não fornece histórico de
evolução equivalente a migrations.

### Decisão tomada

O comportamento inicial foi rejeitado após a evidência. Foi criada a migration
`InitialCreate`, o startup passou a executar `MigrateAsync()` e a factory
deixou de chamar `EnsureCreated()`.

### Resultado

Uma execução real sobre SQLite vazio aplica a migration antes de receber
tráfego. Um teste confirma a migration aplicada e a ausência de alterações de
modelo pendentes.

---

## 13. Rigor das fronteiras de entrada

### Sugestão da IA

O código gerado herdava os defaults do ASP.NET Core: nomes JSON
case-insensitive, `Guid.TryParse` amplo, contagem de strings por UTF-16 e
formatters que aceitam media types JSON alternativos.

### Problema ou risco identificado

O code review demonstrou que esses defaults eram mais permissivos ou mais
restritivos que o JSON Schema aprovado:

- `Name` poderia ser aceito no lugar de `name`;
- `Guid.TryParse` aceitava representação sem hífens;
- `string.Length` contava um emoji como duas unidades UTF-16;
- `text/json` e `application/*+json` não estavam declarados no request body.

### Análise

O framework não deveria redefinir silenciosamente a fronteira contratual. Cada
caso foi tratado de forma localizada, sem alterar o OpenAPI para acomodar o
código existente.

### Decisão tomada

Manter os defaults foi rejeitado. Foram adotados JSON case-sensitive, model
binder de UUID canônico, contagem por Unicode runes e restrição a
`application/json` (esta última evoluída na revisão 14).

### Resultado

Testes de fronteira comprovam casing, UUID e Unicode. A regra de `minLength: 1`
foi seguida literalmente: whitespace não foi proibido porque o contrato não
define trim nem padrão de conteúdo.

---

## 14. Media type estrito e resposta `415`

### Sugestão da IA

Restringir os request bodies a `application/json` retornando `415` para
qualquer outro media type, sem declarar esse status no contrato ("é só uma
consequência HTTP").

### Problema ou risco identificado

O `415` podia surgir sem corpo por dois caminhos diferentes — o roteamento
(`ConsumesMatcherPolicy`, para media types incompatíveis como `text/plain`) e a
seleção de input formatter (o MVC ainda considera o sufixo `+json` compatível) —
e um `415` não declarado é reprovado por validadores estritos de contrato como
o Schemathesis, citado na Etapa 3.

### Análise

Se o `415` é um resultado observável dos oito endpoints com body, ele deveria
estar no `openapi.yaml` como qualquer outro, com o mesmo envelope
`ProblemDetails` e `code` estável dos demais erros.

### Decisão tomada

A sugestão foi ajustada. Foi adicionado `components/responses/UnsupportedMediaType`
e referenciado nas quatro operações com body. Um middleware normaliza os dois
caminhos de `415` para `ProblemDetails` com `code` `UNSUPPORTED_MEDIA_TYPE`.

### Resultado

`415` passou a ser resposta declarada, com corpo consistente. Um teste envia
`text/json` e `application/vnd.taskflow+json` para as quatro operações e valida
o corpo contra o schema do contrato.

---

## 15. Validação de responses contra o contrato

### Sugestão da IA

Validar responses apenas por assertions C# escritas manualmente nos testes.

### Problema ou risco identificado

Schemas duplicados nos testes poderiam divergir do `openapi.yaml` e continuar
verdes. A cobertura inicial por schema também validava apenas exemplos de
status, não todos os pares operação/resposta.

### Análise

Para manter SDD, o teste precisava carregar o contrato, localizar
`path + método + status + media type` e usar o schema ali definido. Assertions
de negócio continuariam úteis, mas não substituiriam a validação contratual.

### Decisão tomada

A sugestão foi ajustada. `Microsoft.OpenApi.YamlReader` valida a estrutura do
documento e `JsonSchema.Net` valida os responses reais, sem duplicar schemas.

### Resultado

As oito operações possuem cobertura para todos os responses declarados:
`200`, `201`, `400`, `404`, `415`, `422` e `204` sem body.

---

## 16. Transações `IMMEDIATE`, ETag e `409` para concorrência

### Sugestão da IA

Após o primeiro code review, duas sugestões encadeadas:

1. Adicionar transações SQLite `IMMEDIATE` com isolamento serializável ao
   arquivamento de projeto e à criação de tarefa, para serializar as
   verificações de estado.
2. Complementar com ETag, `If-Match`, row version e `409 Conflict`.

### Problema ou risco identificado

A transação resolvia uma janela concorrente real, mas introduzia
`SqliteConnection`, `SqliteTransaction` e `IsolationLevel` diretamente na
camada Application e passava a oferecer controle explícito de concorrência que
o OpenAPI não define e que contradizia a decisão documentada de
last-write-wins. ETag, `If-Match`, row version e `409` também não existiam no
contrato e mudariam requests, responses e semântica pública.

### Análise

Controle otimista seria razoável numa evolução multiusuário, mas precisa
começar pela especificação e por uma decisão sobre precondições HTTP — não é
uma correção interna transparente. O benefício não compensava o desvio
arquitetural e contratual neste escopo.

### Decisão tomada

A transação foi implementada para avaliação e depois revertida; o teste que
exigia serialização atômica foi retirado. ETag/`409` foram rejeitados sem
implementar.

### Resultado

Os casos de uso voltaram à orquestração simples com `DbContext`. Nenhum status
ou header não aprovado foi acrescentado. A limitação de concorrência
(last-write-wins) e a possível evolução ficaram explícitas em
`docs/decisoes.md` e neste histórico, como trade-off e não como abstração
antecipada.

---

## 17. Abstrações e funcionalidades adicionais

### Sugestão da IA

Considerar MediatR, AutoMapper, mensageria, autenticação, paginação e uma Clean
Architecture mais completa como melhorias de maturidade.

### Problema ou risco identificado

Nenhuma resolvia requisito presente e algumas exigiriam mudança do contrato.
Adotá-las apenas para demonstrar conhecimento aumentaria o volume de código e
reduziria a rastreabilidade.

### Decisão tomada

Rejeitadas pelo mesmo critério da revisão 11: cada proposta foi confrontada com
"qual requisito atual resolve", "qual problema observável elimina" e "qual
parte do OpenAPI exige a complexidade". Nenhuma teve resposta concreta.

### Resultado

Permaneceram apenas Controllers, casos de uso, domínio, EF Core e a
infraestrutura necessária ao contrato. Autenticação e paginação continuam fora
do escopo e precisam começar por revisão da especificação.

---

## 18. Uso dos tipos nativos de Problem Details

### Sugestão da IA

Usar `ProblemDetails` e `ValidationProblemDetails` nativos do ASP.NET Core, com
uma factory central para acrescentar os códigos estáveis definidos no contrato.

### Problema ou risco identificado

A sugestão precisava ser verificada para garantir que não criaria um envelope
proprietário incompatível com o OpenAPI nem respostas com content type
`application/json` em vez de `application/problem+json`.

### Análise

Os tipos nativos já representam a estrutura RFC esperada e permitem extensions
como `code`. A centralização também evita que cada controller monte erros com
títulos, detalhes ou status diferentes.

### Decisão tomada

A sugestão foi revisada e **aceita**. Foi criada `TaskFlowProblemDetailsFactory`,
mantendo os tipos nativos e acrescentando somente os campos previstos pelo
contrato.

### Resultado

Erros `400`, `404`, `415` e `422` possuem formato consistente, content type
correto, `instance` da requisição e código legível por máquina. Testes
verificam tanto o envelope quanto sua compatibilidade com o schema OpenAPI.

---

## 19. Organização do projeto de testes em pastas

### Sugestão da IA

Manter todos os arquivos de teste na raiz de `TaskFlow.ContractTests`, como
foram criados.

### Problema ou risco identificado

A pasta plana misturava infraestrutura de teste, testes de contrato HTTP,
suítes por recurso e testes de domínio sem HTTP, dificultando a navegação.

### Análise

Uma separação por responsabilidade ajuda a leitura sem exigir mudança de
código. Sub-namespaces por pasta acrescentariam `using` em quase todos os
arquivos, já que `TaskFlowApiFactory`, `OpenApiResponseValidator` e o relógio
fixo são usados em toda a suíte.

### Decisão tomada

A sugestão foi ajustada. Os arquivos foram movidos com `git mv` para quatro
pastas, mantendo o namespace único `TaskFlow.ContractTests`:

- `Support/` — `TaskFlowApiFactory`, `OpenApiResponseValidator` (não são testes);
- `Contract/` — testes HTTP transversais: happy path, categorias de erro,
  fronteiras (casing, Unicode, UUID, media type, `415`) e cobertura por
  operação do OpenAPI;
- `Endpoints/` — suítes por recurso (`ProjectsEndpointsTests`,
  `TasksEndpointsTests`);
- `Unit/` — testes sem HTTP: máquina de estados do domínio e a fábrica de
  `ProblemDetails`.

### Resultado

Build limpo e 120 testes passando sem qualquer alteração de código. A mudança é
apenas de organização de arquivos.

---
