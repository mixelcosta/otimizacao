# Guia de Testes

Como rodar, o que é coberto e a rastreabilidade das regras.

## Executar

```bash
dotnet test HardwareOptimizer.sln -c Release            # toda a suíte
dotnet test tests/HardwareOptimizer.Core.Tests          # um projeto
dotnet test --filter "FullyQualifiedName~Validacao"     # por nome
```

A suíte roda no CI (`.github/workflows/ci.yml`) a cada push/PR.

## Projetos de teste

| Projeto | Cobre |
| --- | --- |
| `HardwareOptimizer.Core.Tests` | Catálogo, validador, perfis, consentimento, sanitização, BIOS, versão, score, `Resultado`, `FaixaNumerica`. |
| `HardwareOptimizer.Agent.Tests` | Executor + rollback, comandos, pré-condições, coletor, backup, persistência, **sensores**, **validação/regressão**, BIOS+cache. |
| `HardwareOptimizer.Cerebro.Tests` | Guard da resposta, matriz, cérebro local/LLM, privacidade, **visão** (parser, módulo, conferência). |
| `HardwareOptimizer.Ipc.Tests` | Roteador (todos os métodos) + **loopback real de named pipe**. |
| `HardwareOptimizer.App.Tests` | ViewModels da UI (com roteador falso). |

## Rastreabilidade — regras invariantes ↔ testes

| Regra | Teste |
| --- | --- |
| Limite absoluto bloqueia (rígido) | `ValidadorAcaoTests`, `ConstrutorPerfilTests` |
| Perfil seguro usa só a faixa segura | `ConstrutorPerfilTests` |
| Customizado exige consentimento (2 checkboxes) | `ConsentimentoTests` |
| Sem backup, nada prossegue | `ExecutorControladoTests` |
| Uma categoria por vez + rollback | `ExecutorControladoTests` |
| Regressão reverte automaticamente | `ValidacaoTests` |
| LLM só escolhe do catálogo (guard) | `GuardRespostaTests`, `CerebroTests` |
| Sanitização não vaza segredo | `SanitizadorTests`, `FluxoCompletoTests` |
| Catálogo ↔ comandos consistentes | `RegistroComandosTests` |
| Confiança baixa pede nova foto (visão) | `VisaoTests` |

## Tipos de teste

- **Unitário determinístico** — lógica pura (parsers, validador, score, matriz).
- **Com injeção** — E/S testada com fakes/caminhos-base (hwmon fabricado, leitor
  de plataforma falso, cliente LLM falso, coletor falso).
- **Integração** — executor + runner (regressão→rollback), fluxo ponta a ponta
  (`FluxoCompletoTests`), IPC loopback de named pipe.

> Total atual: **160 testes**. Ao adicionar funcionalidade, adicione o teste
> correspondente e mantenha a build limpa (warnings = erros).
