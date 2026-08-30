# Decisões Técnicas — TaskFlow

## Objetivo

Este documento registra decisões de implementação e arquitetura
adotadas para atender ao contrato definido em `openapi.yaml`.

O OpenAPI é a fonte da verdade para comportamento HTTP e regras
contratuais. Este documento explica decisões técnicas e trade-offs,
não substitui nem redefine a especificação.

---

# 1. Specification-Driven Development

## OpenAPI como fonte da verdade

O desenvolvimento segue:

OpenAPI
→ revisão
→ implementação
→ testes de contrato

Mudanças comportamentais devem começar pelo contrato.

Uma divergência entre código, testes e OpenAPI é considerada defeito,
salvo quando uma alteração contratual estiver sendo deliberadamente
discutida.

## Versão

O contrato usa `openapi: 3.0.3`, conforme o item 1.1 do desafio.
Campos anuláveis são representados com `nullable: true`. A validação por
JSON Schema nos testes traduz `nullable` para uma união de tipos com
`null`, já que a palavra-chave não existe em JSON Schema 2020-12.

A aplicação expõe uma Swagger UI em `/swagger` apenas para navegação. Ela
renderiza o próprio `openapi.yaml`, servido em `/openapi.yaml`. Não há
geração de contrato a partir do código, evitando uma segunda fonte que
possa divergir.

---

# 2. Arquitetura

A solução utiliza uma arquitetura em camadas simples:

HTTP
 ↓
Controllers
 ↓
Application / UseCases
 ↓
Domain
 ↓
Infrastructure / EF Core
 ↓
SQLite

### Controllers

Responsáveis exclusivamente pela camada HTTP:

- routing;
- binding;
- validação de entrada;
- tradução do resultado para respostas HTTP.

Não contêm regras de negócio.

### Application / UseCases

Um caso de uso representa uma operação relevante da API.

Responsabilidades:

- orquestração;
- consulta/persistência;
- verificação de existência;
- coordenação das regras de domínio.

### Domain

Contém:

- entidades;
- enums;
- invariantes;
- transições de estado.

Não depende de HTTP ou EF Core.

### Infrastructure

Contém:

- DbContext;
- configurações EF Core;
- SQLite;
- migrations.

### Decisão

Não foi adotada uma Clean Architecture completa.

A separação existe apenas onde há responsabilidade concreta,
mantendo a solução proporcional ao tamanho do desafio.

---

# 3. Persistência

## EF Core + SQLite

SQLite foi escolhido por:

- atender ao requisito;
- não exigir infraestrutura externa;
- permitir migrations;
- possuir comportamento relacional real;
- facilitar execução local e testes.

O `DbContext` funciona como unidade de trabalho da requisição.

Não será criado Repository ou Unit of Work adicional sem necessidade.

## Testes

Testes de integração utilizam SQLite.

O provider EF Core InMemory foi descartado porque não reproduz
adequadamente comportamento relacional, constraints, SQL e transações.

Quando executado em memória será utilizado SQLite in-memory mantendo
a conexão aberta durante o teste.

---

# 4. Estratégia de erros

A API utiliza `ProblemDetails` e `ValidationProblemDetails`.

A classificação segue o contrato:

400 → entrada inválida
404 → recurso inexistente
422 → regra de negócio

A avaliação ocorre preferencialmente na seguinte ordem:

Request Contract
      ↓
Resource Existence
      ↓
Business Rules

Isso evita, por exemplo, consultar recursos quando a própria
requisição já viola o contrato.

Os erros possuem `code` estável conforme definido no OpenAPI.

Detalhes internos de implementação não são expostos.

---

# 5. PATCH

PATCH utiliza partial update com `application/json`.

JSON Patch RFC 6902 não será utilizado porque não faz parte
do contrato.

A implementação precisa distinguir:

campo ausente
→ manter valor

campo presente
→ atualizar

campo presente como null
→ aplicar somente quando permitido pelo schema

Será utilizada representação explícita de presença no DTO
(por exemplo `Optional<T>`).

Essa decisão evita a ambiguidade existente em propriedades
C# simplesmente anuláveis.

Campos controlados pelo servidor não fazem parte dos schemas
de atualização.

---

# 6. Regras de domínio

As regras de estado são aquelas definidas em `openapi.yaml`.

A implementação as representa como invariantes do domínio,
principalmente:

- transições de estado;
- conclusão de tarefas;
- arquivamento de projetos;
- exclusão de tarefas.

Uma operação estruturalmente válida que viole o estado atual
do domínio produz o erro `422` definido no contrato.

Reenvio do estado atual é tratado como operação idempotente
quando permitido pelo contrato.

## Transição de status da tarefa (leitura estrita)

O enunciado define o fluxo `pending -> in_progress -> done` e proíbe
retroceder. A implementação trata cada etapa do fluxo como obrigatória:
o atalho `pending -> done` também é recusado, com `422
INVALID_TASK_STATUS_TRANSITION`.

É uma interpretação deliberada e mais estrita do que a leitura literal
(que proíbe apenas o retrocesso). Está refletida no `openapi.yaml` e nos
testes de contrato. Liberar `pending -> done` exigiria alterar primeiro o
contrato.

---

# 7. Datas e relógio

Timestamps gerados pelo servidor utilizam UTC.

Será utilizado:

`TimeProvider`

em vez de chamadas diretas a:

`DateTime.Now`
`DateTime.UtcNow`

Isso permite controlar o relógio durante testes.

`DateTimeOffset` será preferido para timestamps expostos pela API.

No fio, um conversor normaliza os timestamps para UTC no formato RFC 3339 com
milissegundos e sufixo `Z` (ex.: `2026-08-30T21:04:28.445Z`), alinhado a
`format: date-time` e aos exemplos do `openapi.yaml`. O domínio permanece em
`DateTimeOffset`; só a representação de saída é fixada.

---

# 8. Serialização

A API utiliza `System.Text.Json`.

Propriedades seguem:

camelCase

Enums são serializados como strings de acordo com os valores
definidos no OpenAPI.

A configuração garante especificamente a representação contratual
de valores como `in_progress`.

---

# 9. Abstrações não adotadas

Não serão utilizados inicialmente:

- MediatR;
- Repository genérico;
- Unit of Work customizado;
- AutoMapper;
- mensageria;
- event sourcing;
- sagas;
- microservices.

### Motivo

Essas abstrações não resolvem problemas presentes no escopo atual.

O objetivo é evitar complexidade acidental e manter rastreabilidade
entre:

OpenAPI
→ UseCase
→ implementação
→ teste

Novas abstrações só serão introduzidas quando houver requisito
concreto que justifique seu custo.

---

# 10. Segurança

Autenticação e autorização estão fora do escopo atual e não serão
implementadas.

O contrato representa isso explicitamente.

Em ambiente produtivo, autenticação, autorização, HTTPS,
gestão de segredos e políticas de acesso seriam requisitos
necessários antes da exposição pública.

---

# 11. Limitações conhecidas

## SQLite

Adequado para o desafio e execução local, mas não representa uma
decisão de banco para sistemas de alta escala.

## Concorrência

Não existem ETags, row versions ou controle explícito de concorrência.
Atualizações concorrentes seguem last-write-wins.

## Paginação

Listagens não possuem paginação porque o contrato atual não a define.

## Auditoria

Não existe histórico completo de alterações.

## Exclusão

A exclusão de tarefas é física; não existe soft delete.

Essas limitações são aceitas conscientemente para manter a solução
proporcional ao escopo.

---

# 12. Critério para evolução

Uma decisão arquitetural nova deve responder pelo menos uma destas
perguntas:

1. Qual requisito atual ela resolve?
2. Qual problema observável ela elimina?
3. Qual parte do contrato exige essa complexidade?

Se nenhuma delas possuir resposta concreta, a abstração não deve
ser introduzida.

Mudanças comportamentais começam pelo `openapi.yaml`.