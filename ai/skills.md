# Skills do Codex utilizadas

As seguintes skills foram efetivamente usadas durante o desenvolvimento e a
revisão do projeto:

| Skill | Finalidade no projeto |
|---|---|
| [`taskflow-sdd`](../.agents/skills/taskflow-sdd/SKILL.md) | Aplicar o fluxo SDD específico da TaskFlow, manter o OpenAPI como fonte da verdade e preservar as regras de PATCH, erros e máquina de estados. |
| `aspnet-core` | Apoiar decisões e implementação de Controllers, pipeline HTTP, dependency injection, serialização, model binding e ProblemDetails. |
| `ef-core` | Orientar `DbContext`, configurações de entidades, relacionamento, índices, SQLite, migrations e consultas assíncronas. |
| `csharp-testing` | Orientar a suíte xUnit, `WebApplicationFactory<Program>`, SQLite isolado e testes de integração e contrato. |
| `skill-creator` | Criar e estruturar a skill personalizada `taskflow-sdd` com instruções específicas do projeto. |
| `skill-installer` | Instalar no ambiente Codex as skills técnicas usadas no desenvolvimento. |

As skills forneceram instruções especializadas para a IA, mas não substituíram
o `openapi.yaml`, os requisitos fornecidos pelo desenvolvedor nem a revisão
humana. Quando havia conflito, o contrato aprovado e a decisão explícita do
desenvolvedor prevaleciam.
