---
name: taskflow-sdd
description: Desenvolver ou revisar a API TaskFlow em ASP.NET Core usando seu fluxo SDD orientado pelo OpenAPI. Use somente no repositório TaskFlow ou quando o usuário mencionar explicitamente a TaskFlow; não aplique estas regras específicas do projeto a APIs não relacionadas.
---

# TaskFlow SDD

Mantenha a implementação, os testes e a documentação subordinados ao contrato
aprovado em `openapi.yaml`.

## Estabeleça o contrato atual

Antes de alterar qualquer comportamento:

1. Leia completamente o `openapi.yaml`.
2. Leia `docs/decisoes.md`, quando existir.
3. Inspecione a implementação e os testes relevantes.
4. Trate qualquer diferença entre o OpenAPI e o código como defeito ou como
   alteração contratual ainda não resolvida.

Não modifique o `openapi.yaml` apenas para fazer o código existente ou proposto
passar. Se uma solicitação conflitar com o contrato aprovado, identifique o
path, método, schema, response ou regra afetada e interrompa essa linha de
implementação até que a especificação seja revisada deliberadamente.

## Respeite a fase do SDD

Durante o trabalho de especificação, não crie código de implementação. Em vez
disso:

- identifique ambiguidades e edge cases;
- separe validação de entrada, existência de recurso e regras de negócio;
- classifique as falhas esperadas como `400`, `404` ou `422`;
- garanta que todo comportamento obrigatório esteja explícito no OpenAPI;
- altere o contrato somente quando o usuário solicitar uma mudança na
  especificação.

Após a aprovação, implemente em incrementos pequenos e rastreáveis às operações
e responses definidos no `openapi.yaml`.

## Preserve a base técnica

- Use .NET 8 e ASP.NET Core Web API com Controllers.
- Organize o único projeto da API em Controllers, Application/UseCases,
  Domain, Contracts e Infrastructure/Persistence.
- Use EF Core com SQLite e migrations.
- Use a injeção de dependência nativa, `System.Text.Json`, `ProblemDetails` e
  `ValidationProblemDetails`.
- Use xUnit e `WebApplicationFactory<Program>` nos testes de integração através
  das fronteiras HTTP reais.
- Use bancos SQLite isolados ou bancos SQLite em memória nomeados com uma
  conexão âncora. Não substitua pelo provider EF Core InMemory nos testes de
  integração ou de contrato.
- Use `TimeProvider` para timestamps gerados pelo servidor e represente-os em
  UTC.
- Mantenha explícitos os mapeamentos entre DTOs e entidades.

Não introduza MediatR, Repository genérico, Unit of Work customizado,
AutoMapper, mensageria, event sourcing, microsserviços ou autenticação, a menos
que o escopo aprovado seja alterado e um requisito concreto justifique o custo.

## Preserve os limites entre dependências

- Controllers cuidam de roteamento, binding, validação de entrada e tradução
  para HTTP.
- Use cases da camada Application orquestram queries, verificações de
  existência e operações de domínio.
- O Domain é responsável por entidades, enums, invariantes e transições de
  estado; ele não deve depender de HTTP nem de EF Core.
- Infrastructure é responsável por `DbContext`, configurações das entidades,
  SQLite e migrations.

Use `TaskFlowDbContext` diretamente nos use cases. Não adicione uma abstração de
Repository que apenas repasse chamadas.

## Preserve a semântica HTTP

Avalie as falhas nesta ordem:

```text
contrato da requisição -> existência do recurso -> estado do negócio
```

- `400`: JSON malformado, campos inválidos ou ausentes, enum inválido, UUID
  inválido, propriedade desconhecida, `null` proibido, PATCH vazio ou outra
  falha estrutural.
- `404`: um identificador de projeto ou tarefa sintaticamente válido não
  existe.
- `422`: uma operação válida tem como alvo um recurso existente, mas viola seu
  estado de negócio atual.

Use `ValidationProblemDetails` para `400` e `ProblemDetails` para `404` e `422`.
Preserve `application/problem+json`, o `code` estável definido no contrato e o
path da requisição como `instance`. Nunca exponha stack traces, SQL ou detalhes
internos de exceptions.

Mantenha os nomes das propriedades JSON case-sensitive e em camelCase. Aceite
media types no body somente conforme declarado pelo OpenAPI. Faça o parse dos
identificadores de path usando o formato UUID canônico exigido pelo contrato.

## Preserve a semântica do PATCH

O PATCH usa um objeto JSON parcial, não JSON Patch RFC 6902.

- Uma propriedade ausente preserva o valor armazenado.
- Uma `description` explicitamente nula limpa seu valor.
- `name`, `title`, `status` e `priority` rejeitam `null` explícito.
- Rejeite um objeto vazio e propriedades desconhecidas.
- Rejeite tentativas do cliente de definir `id`, `projectId`, `createdAt` ou
  `completedAt`.
- Preserve explicitamente a presença de cada campo; propriedades nullable
  simples em C# são insuficientes quando omitido e nulo possuem significados
  diferentes.

## Preserve as regras de projeto

- Um projeto começa como `active`.
- Os valores de status de projeto são `active` e `archived`.
- O arquivamento falha com `422 PROJECT_HAS_IN_PROGRESS_TASKS` enquanto existir
  alguma tarefa `in_progress`; use `AnyAsync`, sem carregar a coleção.
- Um projeto arquivado rejeita a criação de tarefas com
  `422 PROJECT_ARCHIVED`.
- A reativação é permitida porque o contrato aprovado não a proíbe.

## Preserve as regras de tarefa

Uma tarefa começa como `pending` e segue somente esta sequência:

```text
pending -> in_progress -> done
```

- Rejeite `pending -> done` com `422`.
- Rejeite toda transição de retrocesso com `422`.
- Trate `done` como estado terminal.
- Trate uma solicitação para o status atual como idempotente.
- Somente tarefas `pending` podem ser excluídas.
- Defina `completedAt` a partir do `TimeProvider` apenas na transição válida
  para `done`; nunca aceite esse campo do cliente.
- Preserve o `completedAt` existente em `done -> done`.

Serialize os valores de enums como strings minúsculas definidas pelo contrato.
Em especial, serialize e aceite `InProgress` exatamente como `in_progress`.

## Preserve as decisões de persistência

- Use `AsNoTracking` e projections nas queries somente leitura.
- Use operações assíncronas do EF Core com `CancellationToken`.
- Preserve a chave estrangeira `Project 1:N TaskItem` e os índices configurados.
- Aplique e teste migrations do EF Core; nunca permita que `EnsureCreated`
  esconda a ausência de um schema de produção.
- Não adicione controle explícito de concorrência, ETags, row versions ou `409`
  sem antes alterar o contrato. A limitação atualmente documentada é
  last-write-wins.

## Verifique as alterações

Derive os cenários da operação OpenAPI, do schema da requisição e de cada
response declarado. No mínimo, verifique:

- status code e content type do response;
- body do response contra o schema selecionado por path, método, status e
  media type;
- `Location` para criações com `201`;
- body vazio para exclusões bem-sucedidas com `204`;
- todas as transições permitidas e proibidas de tarefas;
- todos os casos definidos de `400`, `404` e `422`;
- comportamento das migrations em um banco SQLite vazio.

Execute os comandos relevantes de build e testes antes de declarar a conclusão.
Informe os comandos que não puderam ser executados e não declare uma
verificação que não ocorreu.

## Mantenha os artefatos de auditoria de IA

Mantenha `ai/prompts.md`, `ai/skills.md` e `ai/revisoes.md` factuais. Registre as
contribuições reais da IA, as decisões humanas, as sugestões aceitas, as
sugestões ajustadas e as sugestões rejeitadas. Não minimize a contribuição da
IA nem invente atividades de revisão humana que não sejam sustentadas pelo
histórico de desenvolvimento.
