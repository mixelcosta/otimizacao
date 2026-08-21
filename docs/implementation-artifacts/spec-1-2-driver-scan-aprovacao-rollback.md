---
title: 'Usuário varre e aprova atualização de driver, com rollback'
type: 'feature'
created: '2026-08-21'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '40c1d7b2ebdb7a47b216b1af3825de07f69f574d'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** O fluxo real de instalação de driver (`RoteadorIpc.InstalarDriverAsync`) hoje baixa e instala via `pnputil` sem verificação real de fonte oficial, sem confirmação explícita de risco, e sem backup prévio — `AtualizadorDrivers`/`IRepositorioDriversWhql`/`IServicoBackup` existem no código mas estão desconectados desse caminho. Não existe também nenhum mecanismo de rollback: `IServicoBackup` só cria snapshot de inventário, nunca restaura nada.

**Approach:** Ligar as peças já existentes (`AtualizadorDrivers`, `IRepositorioDriversWhql`, `DriversViewModel`) num fluxo único: varredura → `Confirmation Panel` (componente de UI novo, parametrizado por severidade) → backup dos drivers atuais via `pnputil /export-driver` (reaproveitando `AtualizadorDrivers.ExportarBackupAsync`, já existente) → instalação → rollback disponível na mesma tela, executado pelo usuário importando o backup exportado. Cria os contratos `GanhoEstimado` e `Custo` em `Core`, e o componente novo `IProvedorFonteOficial` (interface real; implementação driver nesta história envolve o catálogo estático existente atrás da nova fronteira — consulta a fontes oficiais reais por HTTP fica para depois, já registrado como não-bloqueante na espinha de arquitetura).

## Boundaries & Constraints

**Always:**
- Nenhuma atualização de driver é aplicada sem o usuário confirmar explicitamente no `Confirmation Panel` (botão de aplicar desabilitado até a confirmação).
- Backup dos drivers atuais é criado (via `pnputil /export-driver`) **antes** de qualquer instalação — sem backup bem-sucedido, a instalação não prossegue.
- O rollback é acionado pelo usuário (importar o backup exportado), nunca automático/silencioso.
- `Confirmation Panel` é um `UserControl` Avalonia genérico, parametrizado por severidade (`driver` nesta história) — nunca um modal/popup, sempre inline no fluxo de scroll (convenção já estabelecida no app).
- `GanhoEstimado` e `Custo` seguem o estilo de record já usado em `Core/Contracts/` (`sealed record`, `required`/`init`, `DateTimeOffset` para timestamp).
- Toda consulta de versão passa por `IProvedorFonteOficial` — `DriversViewModel`/`RoteadorIpc` não consultam `IRepositorioDriversWhql` diretamente.

**Ask First:**
- Se durante a implementação a exportação de backup (`pnputil /export-driver`) falhar de um jeito não previsto (ex. driver sem `.inf` exportável), decidir com o humano se a instalação deve ser bloqueada por completo ou seguir com aviso — não decidir sozinho.

**Never:**
- Nunca chamar `ExecutorControlado.AplicarPerfilAsync`/`IComandoInterno` para este fluxo — o pipeline genérico de perfis está desconectado da realidade do app (nenhuma `AcaoOtimizacao` de categoria `Drivers` existe) e construir suporte de rollback ali só para esta feature seria reaproveitamento superficial, não real (equivalente a inventar uma abstração para um único uso).
- Nunca implementar rollback automático "um clique desfaz tudo" — o usuário decide explicitamente quando importar o backup.
- Nunca consultar fonte de driver fora da fronteira `IProvedorFonteOficial` (nem busca genérica na web, nem chamada direta a `IRepositorioDriversWhql` de fora dessa fronteira).
- Nunca implementar aqui a consulta HTTP real a fabricantes — fica como interface pronta, implementação concreta é trabalho futuro (já registrado como não-bloqueante na espinha, PRD §10 item 3).
- **`[NOTE FOR PM]` Decisão pós-implementação (achado da revisão):** o fluxo de aprovação passou a aceitar só `.inf`/`.cab` via `pnputil` — o antigo caminho ad-hoc que também aceitava instaladores `.exe` (via `Process.Start` com `UseShellExecute=true`) foi removido, já que não se encaixa no par backup+rollback via Driver Store que esta história exige. Drivers distribuídos só como `.exe` ficam sem caminho de atualização até uma história futura decidir como tratá-los.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Driver desatualizado encontrado | Varredura via `IProvedorFonteOficial` retorna versão diferente da instalada | Item listado com versão atual vs. oficial | N/A |
| Sem driver desatualizado | Todos os drivers com versão igual à oficial | Lista vazia / mensagem "tudo atualizado" | N/A |
| Consulta à fonte oficial falha (offline, timeout) | `IProvedorFonteOficial` lança/retorna falha | Driver aparece como `Desconhecido`, sem bloquear os demais | Log de warning, nunca propaga exceção pra UI |
| Usuário clica em atualizar | Driver com `Confirmation Panel` fechado | Painel abre inline, botão de aplicar desabilitado | N/A |
| Usuário confirma sem antes o backup ter sucesso | Backup falhou | Botão de aplicar permanece desabilitado, mensagem de erro exibida no painel | Instalação não prossegue |
| Backup e instalação com sucesso | Confirmação + backup OK | Driver instalado, backup acessível (caminho exibido) na mesma tela | N/A |
| Instalação falha (`pnputil` erro) | pnputil retorna código de erro | Painel mostra erro, driver permanece na lista como desatualizado | Backup já criado continua disponível para referência |
| Usuário aciona rollback | Backup existente para aquele driver | `AtualizadorDrivers` reinstala a partir do backup (pnputil /add-driver no caminho exportado) | Se o backup não existir mais (removido do disco), mensagem clara — não falha silenciosa |

</frozen-after-approval>

## Code Map

- `src/HardwareOptimizer.Core/Contracts/GanhoEstimado.cs` — NOVO. `sealed record GanhoEstimado { Percentual (double), MargemConfianca (double), AtualizadoEm (DateTimeOffset) }`. Estilo: ver `src/HardwareOptimizer.Core/Contracts/SaudeDisco.cs` como referência de forma (record, `required`/`init`, `DateTimeOffset ... = DateTimeOffset.UtcNow`).
- `src/HardwareOptimizer.Core/Contracts/Custo.cs` — NOVO. `sealed record Custo { ValorEstimado (decimal), Moeda (string, default "BRL") }`. `decimal` é o tipo já usado no projeto pra dinheiro (`ResultadoCompatibilidade.PecaSubstituta.PrecoEstimado`).
- `src/HardwareOptimizer.Features.Atualizacao/` — NOVO projeto (csproj + referência a `Core`, `Features.Drivers`, `Agent`), paralelo aos demais `Features.*`.
  - `IProvedorFonteOficial.cs` — NOVO, vive em `Core/Contracts/` (não em `Features.Atualizacao` — ver Tasks). Interface: `Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct)`, onde `InfoFonteOficial` carrega versão + URL de download + certificação. Implementação driver nesta história (`ProvedorFonteOficialDriver`, em `Features.Atualizacao`) delega para `IRepositorioDriversWhql` existente (`src/HardwareOptimizer.Features.Drivers/IRepositorioDriversWhql.cs`) — consulta HTTP real a fabricante é trabalho futuro, não desta história.
  - `OrquestradorAtualizacao.cs` — NOVO. Orquestra varredura (via `AtualizadorDrivers.VarrerAsync`, `src/HardwareOptimizer.Features.Drivers/AtualizadorDrivers.cs`) + backup (via `AtualizadorDrivers.ExportarBackupAsync`) + instalação (via `AtualizadorDrivers.InstalarAsync`) + rollback (via novo método `AtualizadorDrivers.RestaurarBackupAsync`, ver abaixo).
- `src/HardwareOptimizer.Features.Drivers/AtualizadorDrivers.cs` — MODIFICADO. Adicionar `RestaurarBackupAsync(string caminhoBackup, CancellationToken ct) → Task<Resultado>`, reaproveitando o mesmo padrão de `InstalarAsync` (chama `pnputil /add-driver {caminhoBackup}\*.inf /install`).
- `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` — MODIFICADO. Métodos `"varrerdrivers"` (substitui a chamada direta a `ColetorHwid` em `ObterDriversWindows`, linha ~385, por `OrquestradorAtualizacao`), `"aprovaratualizacaodriver"` (substitui `InstalarDriverAsync` ad-hoc, linha ~461, pelo fluxo backup→instalar), `"reverteratualizacaodriver"` (NOVO — rollback).
- `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` — MODIFICADO. Adicionar estado do `Confirmation Panel` (driver selecionado, flag de confirmação, `GanhoEstimado`/`Custo` do item, caminho do backup após sucesso) e `[RelayCommand]` para confirmar/aplicar/reverter, substituindo o `InstalarDriverCommand` atual que dispara direto.
- `src/HardwareOptimizer.App/Views/DriversView.axaml` — MODIFICADO. Usa o novo `ConfirmationPanel` (ver abaixo) no lugar do botão "⬆ instalar" direto (linha ~133).
- `src/HardwareOptimizer.App/Controls/ConfirmationPanel.axaml` (+ `.axaml.cs`) — NOVO `UserControl`. Propriedades: `Severidade` (enum `driver`/`bios`/`manutencao`, só `driver` usado aqui), `Mensagem`, `PodeConfirmar` (bool, gate do botão), `ConfirmarCommand`. Estilo herdado do banner de `DriversView.axaml` (linhas ~190-211: `Border` gradiente `#0C0C1E`→`#09091A`, `BorderBrush="#1E1E3C"`, botão `IsEnabled="{Binding !Flag}"`, sem converter) e do vocabulário de cor de `DESIGN.md` (`accent`/`warning` para driver).
- `tests/HardwareOptimizer.Features.Atualizacao.Tests/` — NOVO projeto de teste, xUnit, fakes manuais (sem Moq), seguindo o padrão 1:1 de `tests/HardwareOptimizer.Features.Drivers.Tests/`.

## Tasks & Acceptance

**Execution:**
- [x] `src/HardwareOptimizer.Core/Contracts/GanhoEstimado.cs` -- criado -- contrato compartilhado, primeiro consumo real na Story 3.4/3.5
- [x] `src/HardwareOptimizer.Core/Contracts/Custo.cs` -- criado -- contrato compartilhado, primeiro consumo real na Story 3.8
- [x] `src/HardwareOptimizer.Features.Atualizacao/HardwareOptimizer.Features.Atualizacao.csproj` -- novo projeto -- home de FR-1 a FR-7 (AD do épico)
- [x] `src/HardwareOptimizer.Core/Contracts/IProvedorFonteOficial.cs` (**divergência do Code Map**: movida para `Core`, não `Features.Atualizacao` -- `ProvedorFonteOficialDriver.cs` referencia `Features.Drivers`, então a interface em `Features.Atualizacao` criaria referência circular; `Core` não referencia nenhum dos dois projetos) + `ProvedorFonteOficialDriver.cs` -- fronteira única de consulta de versão (AD-4)
- [x] `src/HardwareOptimizer.Features.Drivers/AtualizadorDrivers.cs` -- `RestaurarBackupAsync` adicionado; construtor migrado de `IRepositorioDriversWhql` para `IProvedorFonteOficial` (fecha a fronteira única de verdade -- ver nota abaixo) -- mecanismo de rollback real, ausente antes desta história
- [x] `src/HardwareOptimizer.Features.Atualizacao/OrquestradorAtualizacao.cs` -- orquestra varredura/backup/instalação/rollback -- ponto único que liga as peças existentes
- [x] `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` -- `"varrerdrivers"`, `"aprovaratualizacaodriver"`, `"reverteratualizacaodriver"` adicionados; wiring ad-hoc de `"instalardriver"`/`"obterdrivers"` removido -- conecta o fluxo real
- [x] `src/HardwareOptimizer.App/Controls/ConfirmationPanel.axaml(.cs)` -- criado -- reaproveitado por Story 1.4, Story 2.3
- [x] `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` + `DriversView.axaml` -- integrado -- fecha o fluxo ponta a ponta
- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests/` -- testes do orquestrador e do provedor de fonte oficial -- cobre a I/O Matrix acima

**Nota de correção pós-implementação:** a primeira versão desta história (subagente de implementação) manteve `AtualizadorDrivers.VarrerAsync` consultando `IRepositorioDriversWhql` diretamente, com `IProvedorFonteOficial` construído mas nunca chamado pelo caminho real de varredura — violava a AC ("consultada via IProvedorFonteOficial") e o Prevents da AD-4 (dois caminhos concorrentes decidindo "qual é a versão mais recente"). Corrigido: `IProvedorFonteOficial.ConsultarAsync` agora retorna `InfoFonteOficial` (versão + URL + certificação, não só a versão), e `AtualizadorDrivers` depende exclusivamente dela. `IRepositorioDriversWhql` só é referenciado por `ProvedorFonteOficialDriver`, escondido atrás da fronteira.

**Acceptance Criteria:**
- Given que o usuário abriu a tela de Núcleo de Atualização com o app aberto, when o sistema varre os drivers instalados, then cada driver desatualizado aparece com versão atual vs. versão oficial mais recente, consultada via `IProvedorFonteOficial`
- Given um driver desatualizado listado, when o usuário clica em atualizar, then o `ConfirmationPanel` (severidade `driver`) aparece inline com o botão de aplicar desabilitado até confirmação
- Given a confirmação do usuário, when a instalação é acionada, then um backup dos drivers atuais é criado via `pnputil /export-driver` antes de qualquer instalação, e o caminho do backup fica visível na mesma tela
- Given um backup existente para um driver já instalado, when o usuário aciona reverter, then `AtualizadorDrivers.RestaurarBackupAsync` reinstala a versão anterior a partir do backup exportado
- Given a implementação desta história, then `GanhoEstimado` e `Custo` existem em `Core/Contracts/` e `ConfirmationPanel` existe como `UserControl` reutilizável, parametrizado por severidade

## Design Notes

`IProvedorFonteOficial` é deliberadamente uma fronteira fina nesta história — a implementação `ProvedorFonteOficialDriver` delega para o catálogo estático já existente (`RepositorioWhqlEstatico`), não faz nenhuma chamada HTTP real a fabricante. Isso é uma decisão consciente para não inflar esta história com o trabalho de integrar múltiplas APIs/sites de fabricantes distintos (NVIDIA, AMD, Intel, Realtek — cada um com formato próprio), que não tem solução unificada conhecida e já está registrado como item não-bloqueante em aberto na espinha de arquitetura (PRD §10 item 3). A interface fica pronta para receber essa implementação real depois, sem quebrar quem já a consome.

O rollback usa o Driver Store do Windows via `pnputil`, não um "ponto de restauração" do sistema operacional — é o mecanismo mais simples que já tem precedente real no código (`AtualizadorDrivers.ExportarBackupAsync`/`InstalarAsync` já chamam `pnputil` para export/instalação; `RestaurarBackupAsync` só fecha o ciclo reinstalando a partir do backup exportado).

## Verification

**Commands:**
- `dotnet build HardwareOptimizer.sln` -- expected: 0 erros, incluindo o novo projeto `Features.Atualizacao` (bloqueado por bug pré-existente em `Features.LifeCounter`, ver `deferred-work.md` — verificado com placeholder local, removido antes do commit)
- `dotnet test tests/HardwareOptimizer.Features.Atualizacao.Tests` -- 8/8 verdes
- `dotnet test tests/HardwareOptimizer.Features.Drivers.Tests` -- 18/18 verdes (nenhuma regressão em `AtualizadorDrivers`)
- `dotnet test tests/HardwareOptimizer.Ipc.Tests` -- 57/57 verdes
- `dotnet test tests/HardwareOptimizer.App.Tests` -- 91/91 verdes

## Suggested Review Order

**Fronteira única de fonte oficial (AD-4)**

- Ponto de entrada — contrato `IProvedorFonteOficial`/`InfoFonteOficial`, vive em `Core` pra evitar ciclo entre `Features.Drivers` e `Features.Atualizacao`.
  [`IProvedorFonteOficial.cs:17`](../../src/HardwareOptimizer.Core/Contracts/IProvedorFonteOficial.cs#L17)

- `AtualizadorDrivers` migrado de `IRepositorioDriversWhql` direto para a fronteira única — fecha o gap que a revisão encontrou na primeira versão.
  [`AtualizadorDrivers.cs:14`](../../src/HardwareOptimizer.Features.Drivers/AtualizadorDrivers.cs#L14)

**Fluxo de aprovação: backup obrigatório antes de instalar**

- Backup roda e tem que ter sucesso antes de qualquer instalação; caminho do backup sobrevive mesmo se a instalação falhar depois.
  [`RoteadorIpc.cs:421`](../../src/HardwareOptimizer.Ipc/RoteadorIpc.cs#L421)

- Orquestrador único que liga varredura/backup/instalação/rollback — nenhum consumidor toca `AtualizadorDrivers` diretamente.
  [`OrquestradorAtualizacao.cs:23`](../../src/HardwareOptimizer.Features.Atualizacao/OrquestradorAtualizacao.cs#L23)

**Correção de revisão: elevação de privilégio (`pnputil /install`)**

- `Verb="runas"` só funciona com `UseShellExecute=true` — a versão original combinava com `RedirectStandardOutput/Error`, o que silenciosamente desativa a elevação e faria o rollback falhar sem UAC.
  [`AtualizadorDrivers.cs:114`](../../src/HardwareOptimizer.Features.Drivers/AtualizadorDrivers.cs#L114)

**ConfirmationPanel — componente de UI novo, reutilizável**

- Painel inline (nunca modal), botão de aplicar desabilitado até o usuário confirmar explicitamente.
  [`ConfirmationPanel.axaml.cs:22`](../../src/HardwareOptimizer.App/Controls/ConfirmationPanel.axaml.cs#L22)

- Cada tentativa (sucesso ou falha) reseta a confirmação — correção de revisão, sem isso o botão ficava reutilizável sem reconfirmar o risco.
  [`DriversViewModel.cs:144`](../../src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs#L144)

**Peripherals**

- `tests/HardwareOptimizer.Features.Atualizacao.Tests/OrquestradorAtualizacaoTests.cs` — cobertura do orquestrador
- `tests/HardwareOptimizer.App.Tests/DriversViewModelConfirmacaoTests.cs` — cobertura do gate de confirmação, incluindo os dois casos de reset pós-tentativa
