# Decisões técnicas — TaskFlow

## Objetivo

Este documento registra as decisões técnicas adotadas para a API TaskFlow e os trade-offs aceitos. O objetivo é manter a implementação previsível, testável e aderente ao contrato, sem adicionar abstrações que não resolvam um problema atual do escopo.

## 1. Contrato e processo de desenvolvimento

### OpenAPI como fonte da verdade

O arquivo [`openapi.yaml`](../openapi.yaml) é a fonte da verdade do contrato HTTP. Rotas, métodos, parâmetros, formatos de entrada e saída, códigos de status, enums e exemplos de erro devem permanecer compatíveis com essa especificação.

Uma divergência entre implementação e OpenAPI é considerada um defeito. Mudanças de comportamento devem começar pela revisão e aprovação do contrato; somente depois devem ser refletidas no código e nos testes. A documentação gerada em runtime é uma representação do contrato, não uma segunda fonte independente.

### Specification-Driven Development

O projeto segue Specification-Driven Development (SDD):

1. definir e revisar o comportamento no OpenAPI;
2. transformar os cenários do contrato em testes de integração;
3. implementar o menor conjunto de código necessário para satisfazer o contrato;
4. validar continuamente implementação e especificação contra os mesmos cenários.

Essa abordagem antecipa ambiguidades de integração e reduz decisões implícitas nos controllers. Como custo, alterações aparentemente pequenas exigem disciplina para manter contrato, testes e implementação sincronizados.

## 2. Plataforma e arquitetura

### ASP.NET Core sobre .NET 8

A API será implementada em ASP.NET Core Web API sobre .NET 8, versão LTS compatível com o requisito do desafio. Serão utilizados os recursos nativos da plataforma para injeção de dependência, model binding, validação da camada HTTP, serialização JSON, `ProblemDetails`, logging e `TimeProvider`.

### Organização em camadas

A solução será organizada em **Controllers + Application/UseCases + Domain + Infrastructure**:

- **Controllers:** traduzem HTTP para chamadas de aplicação e transformam resultados em respostas HTTP. Não concentram regras de negócio nem acesso a dados.
- **Application/UseCases:** orquestram cada operação exposta pela API, consultam/persistem dados e coordenam as regras do domínio. Um caso de uso explícito por operação favorece leitura e testes sem exigir um mediator.
- **Domain:** contém entidades, enums e invariantes que independem de HTTP e de EF Core, especialmente as transições de estado de tarefas.
- **Infrastructure:** contém `DbContext`, mapeamentos do EF Core, migrations e detalhes do SQLite.

As dependências apontam para o núcleo da aplicação. A separação existe para tornar responsabilidades visíveis, não para impor uma Clean Architecture completa. Tipos e interfaces só serão extraídos quando houver uma fronteira real a proteger ou um benefício concreto de teste.

## 3. Persistência

### Entity Framework Core

O EF Core será o mecanismo de persistência. O `DbContext` representa a unidade de trabalho da requisição e seus `DbSet`s fornecem as operações de coleção necessárias. Consultas específicas permanecem próximas aos casos de uso ou são extraídas apenas quando ganharem complexidade ou reutilização real.

As migrations versionam o schema. Restrições que também possam ser expressas no banco, como chaves estrangeiras e nulabilidade, complementam — mas não substituem — as validações e invariantes da aplicação.

### SQLite

SQLite foi escolhido porque faz parte da stack definida, é relacional, não requer um serviço externo e torna a aplicação simples de executar e avaliar localmente. Também permite que os testes de integração exercitem SQL, constraints, relacionamentos e transações com comportamento muito mais próximo do ambiente real do desafio.

A escolha não pressupõe que SQLite seja o banco ideal para produção em qualquer escala. Ele privilegia portabilidade e baixo custo operacional neste contexto.

### Por que não usar EF Core InMemory

O provider `Microsoft.EntityFrameworkCore.InMemory` não é um banco relacional e não reproduz fielmente o comportamento do SQLite. Entre as diferenças relevantes estão ausência de SQL real, constraints relacionais, semântica de transações e diferenças na tradução/execução de consultas LINQ. Testes podem passar no InMemory e falhar com o provider real.

Por isso, testes de integração usarão SQLite, preferencialmente com um banco isolado por teste ou suíte. Quando for útil executar em memória, será usado **SQLite in-memory com conexão mantida aberta**, e não o provider EF InMemory. Testes unitários do domínio não precisam de provider de persistência.

## 4. Erros HTTP

### ProblemDetails

Erros serão retornados com `Content-Type: application/problem+json`, seguindo `ProblemDetails` e, para erros de campos, `ValidationProblemDetails`. Além dos campos padronizados (`type`, `title`, `status`, `detail` e `instance`), a resposta inclui `code`, um identificador estável e legível por máquina conforme definido no OpenAPI.

Detalhes internos, stack traces, SQL e nomes de infraestrutura não serão expostos. O tratamento será centralizado pelos mecanismos do ASP.NET Core para manter o mesmo formato entre falhas de binding, validação, recurso inexistente e regra de negócio.

### Semântica de 400, 404 e 422

| Status | Quando usar | Exemplos |
| --- | --- | --- |
| `400 Bad Request` | A requisição não pode ser interpretada ou viola o contrato de entrada. | JSON inválido, UUID malformado, enum desconhecido, campo obrigatório ausente, tipo incorreto, string fora do tamanho permitido, body vazio, PATCH sem propriedades ou propriedade não prevista. |
| `404 Not Found` | A requisição é válida, mas o recurso identificado não existe. | Projeto ou tarefa não encontrados; listagem/criação de tarefa para um `projectId` inexistente. |
| `422 Unprocessable Content` | A requisição é sintaticamente e estruturalmente válida e o recurso existe, porém a operação viola uma regra de negócio no estado atual. | Criar tarefa em projeto arquivado, arquivar projeto com tarefa em andamento, transição inválida de tarefa ou exclusão de tarefa não pendente. |

A ordem de avaliação deve evitar respostas enganosas: primeiro valida-se o contrato; depois, a existência do recurso; por fim, as regras dependentes do estado persistido. Um identificador malformado resulta em `400`; um UUID válido que não identifica recurso resulta em `404`.

## 5. Atualizações parciais e `null`

### Estratégia para PATCH

Os endpoints `PATCH` implementam **partial update com `application/json`**, conforme o schema específico do OpenAPI. Não será adotado JSON Patch (`application/json-patch+json`), pois operações como `op`, `path` e `value` não fazem parte do contrato aprovado.

Para cada propriedade, a aplicação precisa distinguir explicitamente:

- propriedade ausente: preservar o valor atual;
- propriedade presente com valor: validar e substituir;
- propriedade presente com `null`: limpar apenas quando o contrato permitir `null`.

Essa distinção será preservada no DTO de entrada por representação explícita de presença (por exemplo, um tipo `Optional<T>` com conversão JSON), porque propriedades C# apenas anuláveis não diferenciam com segurança “ausente” de “enviado como `null`”. O body deve conter ao menos uma propriedade reconhecida, e propriedades adicionais são rejeitadas, em conformidade com `minProperties: 1` e `additionalProperties: false`.

O PATCH retorna `200 OK` com o recurso atualizado. Reenviar o valor já vigente é aceito como operação idempotente e não constitui transição de estado.

### Comportamento de campos `null`

- `description` de projeto e tarefa é anulável. Quando omitida, não muda; quando enviada como `null`, é apagada.
- `name`, `title`, `status` e `priority` não são anuláveis. Se presentes com `null`, produzem `400`.
- Campos controlados pelo servidor (`id`, `projectId`, `createdAt` e `completedAt`) não pertencem aos schemas de atualização. Se enviados, são propriedades adicionais e produzem `400`; não são silenciosamente ignorados.
- Na criação, uma `description` ausente ou explicitamente `null` resulta em descrição nula. Os demais campos obrigatórios seguem o schema de criação.

## 6. Estados e regras de negócio

### Status de projetos

Projetos possuem os estados `active` e `archived` e são criados como `active`.

- Um projeto `active` pode receber novas tarefas.
- Um projeto só pode mudar para `archived` quando não possuir tarefa `in_progress`; caso contrário, a resposta é `422` com o código previsto no contrato.
- Um projeto `archived` não aceita novas tarefas; a tentativa retorna `422`.
- Como o contrato permite atualizar o status para ambos os valores e não define `archived` como terminal, a reativação `archived -> active` é permitida.
- Enviar o status atual é um no-op válido.

Arquivar um projeto não altera nem exclui suas tarefas. As operações sobre tarefas existentes continuam obedecendo às regras próprias dessas tarefas.

### Máquina de estados das tarefas

Tarefas são criadas como `pending` e seguem o fluxo estrito:

```text
pending ──> in_progress ──> done
```

Decisões decorrentes:

- `pending -> done` é deliberadamente proibida;
- `in_progress -> pending` é proibida;
- `done -> pending` e `done -> in_progress` são proibidas;
- `done` é estado terminal;
- repetir o status atual é um no-op, não uma nova transição;
- somente tarefas `pending` podem ser excluídas.

Uma transição proibida é uma violação de regra de negócio e retorna `422`, não `400`, porque o valor do enum é válido, mas não é aplicável ao estado atual. A proibição de `pending -> done` força a passagem explícita por `in_progress`, preservando o significado do fluxo aprovado mesmo que implique duas requisições para concluir imediatamente uma tarefa.

### `completedAt` controlado pelo servidor

`completedAt` é somente leitura e nunca é aceito em requests. O servidor o define no instante da transição válida `in_progress -> done`. Enquanto a tarefa não estiver em `done`, o campo permanece `null`.

Como `done` é terminal, não há regra de limpeza ou recálculo de `completedAt`. Repetir `status: done` preserva o timestamp original. Essa decisão evita que clientes forjem ou alterem o histórico de conclusão.

## 7. Tempo e serialização

### Timestamps em UTC e `TimeProvider`

`createdAt` e `completedAt` são produzidos pelo servidor em UTC e serializados em ISO 8601/RFC 3339 com offset UTC (`Z`), conforme `format: date-time` do OpenAPI. A implementação deve preferir `DateTimeOffset` para preservar de forma explícita o offset e não deve depender de `DateTime.Now` nem do fuso horário da máquina.

O relógio será acessado por `TimeProvider`, injetado nos casos de uso ou serviços que criam timestamps. Em produção usa-se `TimeProvider.System`; em testes, um provider controlável permite afirmar valores exatos sem sleeps ou tolerâncias frágeis.

### Serialização de enums

Enums são expostos como strings, nunca como números. Em particular, o valor interno `InProgress` deve ser serializado e desserializado exatamente como `in_progress`. A política de enum será `snake_case` em minúsculas, resultando também em `pending`, `done`, `active`, `archived`, `low`, `medium` e `high`.

A política se aplica aos valores de enum; os nomes das propriedades JSON permanecem em `camelCase`, como `createdAt`, `completedAt` e `projectId`. Valores fora do conjunto definido resultam em `400`.

## 8. Segurança e escopo

Autenticação e autorização não serão implementadas porque estão explicitamente fora do escopo. Isso está refletido por `security: []` no OpenAPI. Não serão adicionados JWT, usuários, papéis, ownership ou filtros por identidade apenas para simular segurança.

Essa decisão significa que qualquer cliente com acesso à API pode executar todas as operações. Em um cenário de produção, autenticação, autorização por projeto, gestão de segredos, HTTPS e políticas de abuso seriam requisitos prévios à exposição pública.

## 9. Abstrações deliberadamente não adotadas

Para o tamanho e os requisitos atuais, foram descartados:

- **MediatR/CQRS framework:** os casos de uso já fornecem limites explícitos. Um mediator acrescentaria handlers, indireção e dependência sem necessidade atual de pipeline ou dispatch desacoplado.
- **Repository genérico:** `DbContext`/`DbSet` já oferecem unidade de trabalho e operações de persistência. Uma interface CRUD genérica ocultaria recursos úteis do EF Core, empobreceria consultas e tenderia a vazar `IQueryable` ou a crescer com métodos específicos.
- **AutoMapper:** os DTOs são pequenos e o mapeamento explícito torna campos controlados pelo servidor e mudanças contratuais visíveis no code review.
- **Mensageria, eventos distribuídos e consistência eventual:** as operações são locais, síncronas e cabem em uma transação do banco. Não há consumidor assíncrono ou integração externa no escopo.
- **Event sourcing, DDD tático completo, sagas, buses internos e múltiplos bancos:** não há requisitos que justifiquem o custo operacional e cognitivo.
- **Service/repository por entidade apenas por convenção:** classes serão criadas por caso de uso ou responsabilidade real, evitando camadas que somente repassam chamadas.

Essas não são rejeições permanentes às tecnologias. São decisões proporcionais ao problema atual. Uma abstração poderá ser introduzida quando houver pelo menos um caso concreto que compense sua complexidade e puder ser sustentada por testes.

## 10. Limitações e trade-offs conhecidos

- **Concorrência do SQLite:** é adequado para execução local e baixo volume, mas possui limitações de escrita concorrente e escalabilidade horizontal. Uma carga de produção maior pode exigir outro banco relacional e nova validação de comportamento.
- **Fidelidade de testes:** SQLite oferece maior fidelidade que EF InMemory, porém não garante portabilidade automática para SQL Server ou PostgreSQL; diferenças de tipos, índices, locking e SQL continuam existindo.
- **Sem controle de concorrência otimista:** o contrato não define ETag, `If-Match`, row version ou resposta `409`. Atualizações concorrentes seguem last-write-wins. Isso é simples, mas pode perder alterações em cenários multiusuário reais.
- **Listagens sem paginação:** o contrato retorna arrays completos. É suficiente para o desafio, mas consumo de memória e latência crescem com o volume; paginação exigiria mudança explícita no OpenAPI.
- **Exclusão física de tarefas:** não há soft delete, lixeira ou auditoria de exclusão. A simplicidade reduz armazenamento e código, mas impede recuperação e histórico.
- **Auditoria limitada:** existem timestamps de criação e conclusão, mas não `updatedAt`, ator da mudança ou histórico de transições.
- **Sem reabertura de tarefas concluídas:** a máquina de estados é intencionalmente restrita. Uma necessidade futura de correção/reabertura exigirá uma nova transição contratual e a definição do efeito sobre `completedAt`.
- **Reativação de projetos:** é permitida por não haver proibição no contrato. Se `archived` precisar se tornar terminal, isso deve ser definido primeiro no OpenAPI e coberto por um novo erro de negócio.
- **Sem autenticação:** aceitável apenas no ambiente controlado do desafio; não é uma postura de segurança adequada para exposição pública.

Esses limites são conscientes e mantêm a solução compatível com o escopo. Evoluções devem ser motivadas por requisitos observáveis, começar pelo OpenAPI e preservar a distinção entre validação de entrada, inexistência de recurso e violação de regra de negócio.
