---
title: 'Correções M1/M4 da auditoria de legado (logging silencioso + SQL interpolado)'
type: 'chore'
created: '2026-08-22'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
---

# Correções M1/M4 da auditoria de legado (logging silencioso + SQL interpolado)

## Intent

**Problem:** A auditoria de legado (`docs/specs/auditoria-legado.md`) encontrou blocos `catch` silenciosos em caminhos de coleta/limpeza do `HardwareOptimizer.Agent` (dificultando diagnóstico de falhas em campo) e uma interpolação de string em `CommandText` SQL em `RepositorioSqlite` (sem risco de injeção hoje, mas um padrão arriscado de copiar para o futuro).

**Approach:** Adicionar `ILogger` real (não `Trace`, que não tem listener configurado na solução) nos catches silenciosos identificados, roteado pelo mesmo padrão de injeção já usado no restante do código (`GerenciadorLimpeza`/helpers estáticos de `LeitorWindows` passam a receber `ILogger` como parâmetro, espelhando `EstadoSistemaWindows.Selecionar(_log)`); e trocar a interpolação em `RepositorioSqlite.ContarAsync` por um `switch` com `CommandText` literal por tabela. Nenhuma regra de negócio ou fluxo de execução foi alterada — apenas passou a existir rastro de log onde antes não havia.

## Suggested Review Order

**Logging em coletores/limpeza do Agent (M1)**

- Ponto de entrada: assinatura ganhou `ILogger`, seguindo o padrão já usado por `EstadoSistemaWindows.Selecionar(_log)` em `RoteadorIpc`.
  [`GerenciadorLimpeza.cs:12`](../../src/HardwareOptimizer.Agent/Cleanup/GerenciadorLimpeza.cs#L12)

- `Limpar` também passou a receber `ILogger` e propagá-lo para cada operação de limpeza (`LimparPasta`, `EsvaziarLixeira`, `LimparEventLogs`).
  [`GerenciadorLimpeza.cs:28`](../../src/HardwareOptimizer.Agent/Cleanup/GerenciadorLimpeza.cs#L28)

- `LerPlaca`/`LerChipsetEBus` — helpers estáticos que antes não tinham como logar (chamados de `LerAsync`, método de instância) agora recebem `log` explicitamente.
  [`LeitorWindows.cs:105`](../../src/HardwareOptimizer.Agent/Collector/LeitorWindows.cs#L105) · [`LeitorWindows.cs:125`](../../src/HardwareOptimizer.Agent/Collector/LeitorWindows.cs#L125)

- `LeitorSensoresWindows.ExecutarPowerShell` já tinha `_log` de instância — os três catches específicos (`Win32Exception`/`InvalidOperationException`/`IOException`) passaram a logar o objeto de exceção completo, não só a mensagem.
  [`LeitorSensoresWindows.cs:191`](../../src/HardwareOptimizer.Agent/Sensors/LeitorSensoresWindows.cs#L191)

- Únicos dois call sites de `GerenciadorLimpeza.Escanear`/`Limpar` (via IPC) atualizados para passar `_log` — mudança mecânica de assinatura, sem tocar no protocolo/roteamento de métodos.
  [`RoteadorIpc.cs:1114`](../../src/HardwareOptimizer.Ipc/RoteadorIpc.cs#L1114)

**SQL literal por tabela (M4)**

- `ContarAsync` trocou `$"SELECT COUNT(*) FROM {tabela};"` por um `switch` com texto literal por tabela conhecida.
  [`RepositorioSqlite.cs:181`](../../src/HardwareOptimizer.Agent/Persistence/RepositorioSqlite.cs#L181)

**Auditoria (documentação)**

- Relatório completo da varredura (Deep Recon) que originou estas correções, incluindo os itens explicitamente não corrigidos nesta rodada e por quê.
  [`auditoria-legado.md`](../specs/auditoria-legado.md)
