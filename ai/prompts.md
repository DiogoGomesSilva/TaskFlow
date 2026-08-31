# Prompts — TaskFlow

Prompts principais, consolidados a partir da sessão de desenvolvimento (não é
transcrição literal), na ordem em que dirigiram o trabalho. Cada um aponta as
revisões de [`revisoes.md`](revisoes.md) que originou, e — onde houve **decisão
sob ambiguidade** — o vaivém com a IA.

O skill [`taskflow-sdd`](../.agents/skills/taskflow-sdd/SKILL.md) esteve ativo
o tempo todo (com `aspnet-core`, `ef-core`, `csharp-testing`) e carrega as
regras permanentes do projeto, então os prompts ficam no **nível de intenção**.

---

## Especificação

### #01 — Análise de requisitos

Ative o fluxo SDD da TaskFlow. Com base no enunciado, me devolva ambiguidades,
edge cases e os pontos que precisam estar explícitos no OpenAPI. Ainda sem
código: separe validação de entrada, existência de recurso e regra de negócio,
e classifique cada falha esperada como `400`, `404` ou `422`.

**Produziu:** checklist de ambiguidades e a matriz preliminar de respostas HTTP.
→ revisões 5, 7.

### #02 — Decisão: transição `pending -> done`

O enunciado define `pending -> in_progress -> done` e proíbe retroceder, mas não
veda explicitamente pular `in_progress`. Devo permitir `pending -> done` direto?

- **IA propôs:** permitir, por ser transição comum e não estar vedada pela letra.
- **Questionei:** "seguir o fluxo" implica passar por cada etapa; o atalho
  esvazia a máquina de estados.
- **Fechou em:** proibir com `422 INVALID_TASK_STATUS_TRANSITION` — leitura
  estrita e deliberada. → revisão 6.

### #03 — Decisão: `null` no PATCH

Como tratar `description: null` vs. campo omitido, e `null` em campos não
anuláveis?

- **IA propôs:** usar `string?` e tratar `null` como "limpar".
- **Questionei:** `string?` não distingue "omitido" de "enviado como `null`".
- **Fechou em:** presença explícita via `Optional<T>`; `description` aceita
  `null`, os demais rejeitam com `400`, body vazio e propriedade desconhecida
  também `400`. → revisão 3.

### #04 — Redação e revisão crítica do openapi.yaml

Escreva o `openapi.yaml` completo (8 endpoints, todos os responses de erro,
campos do servidor como `readOnly`). Depois faça uma revisão crítica dele:
lacunas, status incorretos, schemas permissivos, exemplos incompatíveis; para
cada achado, diga se **altera** ou apenas **esclarece** o contrato. Apliquei só
os que esclareciam.

**Produziu:** `openapi.yaml` consolidado e congelado para a implementação.
→ revisões 2, 4, 7.

---

## Arquitetura e implementação

### #05 — Arquitetura e decisoes.md

Proponha a arquitetura respeitando o contrato e o tamanho do desafio — camadas
só onde há responsabilidade concreta, nada de Clean Architecture completa,
MediatR, Repository genérico ou AutoMapper. Escreva `docs/decisoes.md` e depois
uma versão mais enxuta dele.

**Produziu:** estrutura de pastas e `docs/decisoes.md`. → revisões 11, 17.

### #06 — Implementação da API

Implemente domínio, persistência, erros, serialização, casos de uso e
controllers seguindo o `openapi.yaml`. Restrições minhas:

- domínio com construtores privados e fábricas; máquina de estados como
  invariante em `TaskItem.TransitionTo`, não nos use cases;
- `TaskFlowDbContext` direto nos use cases (sem Repository que só repassa);
  migration inicial + `MigrateAsync()` no startup;
- `ProblemDetails` / `ValidationProblemDetails` nativos, `code` estável, sem
  detalhe interno no corpo;
- camelCase case-sensitive, enums `snake_case` estrito (rejeitar `InProgress`,
  `IN_PROGRESS`, número), `Optional<T>` no PATCH, propriedade desconhecida →
  `400`;
- ordem de avaliação contrato → existência → regra;
- `TimeProvider` para timestamps, sempre UTC.

**Produziu:** `Domain`, `Infrastructure`, `Application`, `Controllers`.
→ revisões 4, 8, 18.

---

## Testes e validação de contrato

### #07 — Suíte de contrato e validação contra o OpenAPI

Monte os testes com `WebApplicationFactory<Program>` e SQLite isolado
(in-memory nomeado com conexão âncora, **nunca** EF InMemory). Além das
assertions, valide cada resposta real contra o schema do `openapi.yaml`
localizado por `path + método + status + media type`, sem duplicar schema no
teste. Inclua um teste da migration em SQLite vazio.

**Produziu:** `Support/`, `Contract/`, `Endpoints/`, `Unit/`.
→ revisões 10, 15.

---

## Auditoria e ajustes

### #08 — Code review de aderência ao contrato

Faça um code review da implementação contra o `openapi.yaml`. Achados
priorizados por impacto, com evidência reproduzível.

**Produziu:** achados que viraram os ajustes abaixo — migrations em runtime
(a API real caía com `500`), binding de `Guid` amplo, contagem UTF-16 de string
e media type de entrada não estrito. → revisões 12, 13.

### #09 — Media type estrito e `415`

Qualquer coisa diferente de `application/json` no corpo deve retornar `415` com
`ProblemDetails` e `code` `UNSUPPORTED_MEDIA_TYPE`. Declare o `415` no
`openapi.yaml` nas 4 operações com body e cubra com teste que valida o corpo
contra o schema.

**Produziu:** middleware de `415` cobrindo os dois caminhos (roteamento e
seleção de formatter) e `components/responses/UnsupportedMediaType`.
→ revisão 14.

### #10 — Decisão: reverter transações de concorrência

O code review propôs transações SQLite serializáveis nos use cases para tornar
atômicas as regras de arquivamento/criação.

- **IA propôs:** manter, para eliminar a race entre checagem e escrita.
- **Questionei:** é controle explícito de concorrência que o `decisoes.md`
  diz não existir, e coloca `SqliteConnection`/`IsolationLevel` na camada
  Application.
- **Fechou em:** reverter para checagens sequenciais (last-write-wins), remover
  o teste de concorrência e registrar o trade-off. → revisão 16.

### #11 — Decisão: OpenAPI 3.1.1 → 3.0.3

O item 1.1 do enunciado pede OpenAPI 3.0; o contrato foi escrito em 3.1.1.

- **IA propôs (na especificação):** 3.1.1 pela tipagem de `null` precisa.
- **Questionei (na auditoria):** o enunciado pede 3.0 literalmente e o
  `revisoes.md` chegou a afirmar que 3.1.1 "era requisito" — falso.
- **Fechou em:** reverter para `3.0.3` (`nullable: true`), adaptar o validador
  dos testes e reescrever a revisão como desvio deliberado revertido.
  → revisão 1.

### #12 — Formato de saída das datas

Normalize as datas expostas para UTC em RFC 3339 com milissegundos e sufixo
`Z`; mantenha `DateTimeOffset` no domínio. Verifique se o `openapi.yaml`
precisa mudar.

**Produziu:** `Rfc3339DateTimeOffsetConverter`. O contrato não precisou mudar —
`format: date-time` já cobre e os exemplos já usavam `Z`. → revisão 9.

---

## Documentação

### #13 — README e artefatos de auditoria de IA

Escreva o `README.md` com as seções de entrega e diagramas (Mermaid) para
camadas, modelo de dados, máquina de estados e classificação de erros. Mantenha
`ai/skills.md`, `ai/prompts.md` e `ai/revisoes.md` factuais, sem
subdimensionar a contribuição da IA nem inventar revisão humana.

**Produziu:** `README.md` e os artefatos em `ai/`. → revisão 19.
