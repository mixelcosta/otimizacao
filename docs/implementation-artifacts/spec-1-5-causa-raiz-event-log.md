---
title: 'Usuário vê a causa-raiz de travamentos, correlacionada com o Event Log'
type: 'feature'
created: '2026-08-22'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 'baa39d0'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** o app já sinaliza driver (Story 1.2) e BIOS (Story 1.4) desatualizados, mas não lê o Event Log do Windows nem correlaciona esses achados com travamentos reais (BSOD/WHEA/crash) — o usuário não sabe se o driver/BIOS desatualizado é a causa do seu problema ou uma coincidência.

**Approach:** novo leitor `LeitorEventLog` (Agent, sob demanda, sem daemon — mesmo padrão de `ColetorInventario`/`ServicoSensores`) lê BSOD/WHEA/crash de aplicação via `System.Diagnostics.Eventing.Reader` (nativo do .NET, sem PowerShell). Nova classe pura `CorrelacionadorCausaRaiz` (em `Features.Atualizacao`, testável sem Windows) recebe os eventos lidos mais os drivers/BIOS já sinalizados como desatualizados (Stories 1.2/1.4) e atribui uma causa provável só quando há correlação plausível — nunca força uma causa. UI nova (4ª seção em `DriversView`, mesmo padrão visual de driver/software/BIOS) lista os eventos sob demanda (botão "Diagnosticar causa-raiz").

## Boundaries & Constraints

**Always:**
- Leitura do Event Log é sempre sob demanda (usuário clica) — nunca em timer/daemon/background.
- Cada evento lido carrega timestamp, tipo (Bsod/Whea/CrashAplicacao) e driver/processo associado quando o próprio evento traz essa informação (`EventoInstabilidade.ProcessoOuDriver`).
- Uma causa só é atribuída a um evento (`CausaProvavel`) quando a correlação é plausível (ver Design Notes pra critério exato); sem correlação, o evento aparece sem causa — nunca inventar (guard anti-alucinação, mesma regra de FR1/NFR2 já usada em `InfoSoftware`/`InfoBios`).

**Ask First:**
- Nenhuma decisão desta história precisa de aprovação humana durante a implementação — escopo é leitura histórica + correlação + exibição, sem nenhuma ação mutante no sistema do usuário.

**Never:**
- Nunca reaproveitar `AnalisadorRegressao`/`MedicaoEstresse`/`IFerramentaEstresse` (`Agent/Validation/`) — são sobre teste de estresse *provocado ativamente* pelo próprio app (fluxo de validação pós-aplicação de mudança), não leitura passiva de histórico real do Event Log. Fonte de dado e timing são inteiramente diferentes; não misturar.
- Nunca criar um segundo leitor de driver/BIOS desatualizado — a correlação reaproveita `InfoDriver`/`InfoBios` já produzidos pelas Stories 1.2/1.4 (recebidos do cliente, já coletados na tela).
- Nunca ler o Event Log em qualquer fluxo automático (scan inicial, timer de sensores, etc.) — só a ação explícita desta história.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Evento correlacionável com driver desatualizado | Evento WHEA/crash cujo texto/origem cita o fabricante de um driver já sinalizado como desatualizado | Evento aparece com `CausaProvavel` nomeando o driver | N/A |
| Evento WHEA com BIOS desatualizada sinalizada | Evento tipo Whea + `InfoBios` não-nulo | Evento aparece com `CausaProvavel` = "BIOS desatualizada" | N/A |
| Evento sem correlação plausível | Nenhum driver/BIOS desatualizado bate com o evento | Evento aparece sem `CausaProvavel` (nunca "causa desconhecida" inventada) | N/A |
| Nenhum evento de instabilidade no período | Event Log sem BSOD/WHEA/crash recentes | Lista vazia, mensagem "nenhum evento encontrado" | N/A |
| Leitura do Event Log falha (canal indisponível, permissão) | `EventLogReader` lança exceção | Tratado como lista vazia, log de warning, nunca propaga pro usuário | Log + retorna lista vazia |

</frozen-after-approval>

## Code Map

- `src/HardwareOptimizer.Core/Contracts/EventoInstabilidade.cs` — NOVO. `enum TipoEventoInstabilidade { Bsod, Whea, CrashAplicacao }`; `sealed record EventoInstabilidade { Timestamp (DateTimeOffset), Tipo, Origem (string, ex. Provider.Name), ProcessoOuDriver (string?), Mensagem (string?), CausaProvavel (string?, preenchido só pela correlação) }` — mesmo estilo de `InfoSoftware.cs`/`InfoBios.cs`.
- `src/HardwareOptimizer.Agent/EventLog/ILeitorEventLog.cs` + `LeitorEventLog.cs` — NOVOS. Interface fina (mesmo padrão de `ILeitorSensores`/`ILeitorPlataforma`, testável via fake) + implementação `[SupportedOSPlatform("windows")]` usando `System.Diagnostics.Eventing.Reader` (`EventLogReader`/`EventLogQuery`), sem invocar PowerShell. Consultas: canal `System`, Provider `Microsoft-Windows-WHEA-Logger` (→ Whea); canal `System`, Provider `Microsoft-Windows-WER-SystemErrorReporting`, Event ID 1001 (→ Bsod, extrai nome do bugcheck/driver do `EventData` quando presente); canal `Application`, Provider `Application Error`, Event ID 1000 (→ CrashAplicacao, extrai nome do módulo/processo da mensagem). Cada consulta em `try/catch` própria retornando lista vazia (mesmo padrão defensivo de `LeitorWindows.Coletar*`), nunca propaga. Sem timer/daemon — só `Task<IReadOnlyList<EventoInstabilidade>> LerAsync(int diasRecentes, CancellationToken ct)`.
- `src/HardwareOptimizer.Features.Atualizacao/CorrelacionadorCausaRaiz.cs` — NOVO. Classe pura e testável (sem I/O), `Correlacionar(IReadOnlyList<EventoInstabilidade> eventos, IReadOnlyList<InfoDriver> driversDesatualizados, InfoBios? bios) -> IReadOnlyList<EventoInstabilidade>`. Regra (documentada em Design Notes): evento cujo `Origem`/`ProcessoOuDriver`/`Mensagem` contém (case-insensitive) o `Fabricante` de algum driver em `driversDesatualizados` → `CausaProvavel` = descrição desse driver; evento tipo `Whea` quando `bios != null` → `CausaProvavel` = "BIOS desatualizada"; senão `CausaProvavel` permanece `null`.
- `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` — MODIFICADO. Novo método `"diagnosticarcausaraiz"` (gated por `OperatingSystem.IsWindows()`, mesmo padrão de `varrerdrivers`), recebe `driversDesatualizados`/`bios` (já coletados pelo cliente) como parâmetros, lê o Event Log via `LeitorEventLog` fresco, correlaciona via `CorrelacionadorCausaRaiz`, devolve `IReadOnlyList<EventoInstabilidade>`.
- `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` — MODIFICADO. Nova seção "Diagnóstico de travamentos": `ObservableCollection<EventoInstabilidade> Eventos`, `DiagnosticandoCausaRaiz` (bool), `StatusTextDiagnostico` (string), comando `DiagnosticarCausaRaizCommand` — sob demanda (botão, nunca automático), envia `Drivers`/`InfoBiosAtual` já populados pra `"diagnosticarcausaraiz"`.
- `src/HardwareOptimizer.App/Views/DriversView.axaml` — MODIFICADO. Nova 4ª seção "Diagnóstico de travamentos", mesmo padrão visual das seções anteriores (cabeçalho + botão "Diagnosticar causa-raiz"), lista de eventos com timestamp/tipo, e badge de causa provável quando `CausaProvavel` não é nulo.
- `tests/HardwareOptimizer.Features.Atualizacao.Tests/CorrelacionadorCausaRaizTests.cs` — NOVO, cobre a I/O Matrix acima com eventos construídos à mão (sem Windows real).
- `tests/HardwareOptimizer.Agent.Tests/LeitorEventLogTests.cs` — NOVO, mesmo padrão de `LeitorLinux_le_dados_reais_quando_em_linux`: teste real gated por `if (!OperatingSystem.IsWindows()) return;`, mais um fake `ILeitorEventLog` pra testar a delegação/composição sem depender do Windows real.

## Tasks & Acceptance

**Execution:**
- [x] `src/HardwareOptimizer.Core/Contracts/EventoInstabilidade.cs` -- criar o enum + record -- modelo de exibição de evento de instabilidade com causa opcional
- [x] `src/HardwareOptimizer.Agent/EventLog/ILeitorEventLog.cs` + `LeitorEventLog.cs` -- leitor sob demanda via `EventLogReader` nativo, sem PowerShell -- captura BSOD/WHEA/crash com timestamp/tipo/processo-driver
- [x] `src/HardwareOptimizer.Features.Atualizacao/CorrelacionadorCausaRaiz.cs` -- lógica pura de correlação -- só nomeia causa quando plausível, guard anti-alucinação
- [x] `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` -- método `"diagnosticarcausaraiz"` -- conecta o fluxo real
- [x] `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` + `DriversView.axaml` -- seção "Diagnóstico de travamentos" sob demanda -- fecha o fluxo ponta a ponta
- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests/CorrelacionadorCausaRaizTests.cs`, `tests/HardwareOptimizer.Agent.Tests/LeitorEventLogTests.cs` -- cobre a I/O Matrix acima; testes adicionais em `tests/HardwareOptimizer.App.Tests/` e `tests/HardwareOptimizer.Ipc.Tests/IpcTests.cs` (fluxo real via `RoteadorIpc`)

**Nota de correção pós-implementação:** a revisão independente (Blind Hunter + Edge Case Hunter + Verification Gap, `bmad-build` step-04) encontrou dois defeitos reais, corrigidos: (1) `CorrelacionadorCausaRaiz` comparava fabricante por substring simples — um driver com `Fabricante = "Microsoft"` (valor real pra drivers built-in) batia como substring em `"Microsoft-Windows-WHEA-Logger"` (a `Origem` de todo evento WHEA), gerando falso-positivo de causa em quase qualquer evento e violando diretamente o guard anti-alucinação; corrigido com uma lista de exclusão de fabricantes genéricos demais pra servir de sinal ("Microsoft", "Standard", "Generic", etc.), com teste de regressão reproduzindo o cenário exato. (2) `IpcTests.cs` não tinha um teste round-trip real serializando `InfoDriver`/`InfoBios` via `ProtocoloIpc.Json` (mesma classe de bug de casing self-caught nas Stories 1.3/1.4) — adicionado `DiagnosticarCausaRaiz_PayloadSerializadoComProtocoloIpcJson_RoundTripCorreto`. Também corrigidos, por convergência de dois revisores independentes: a extração de processo/driver via regex era hardcoded só em inglês, mas o público-alvo do produto é PT-BR — Windows em português nunca bateria com os padrões; adicionados os rótulos em português ("Nome do módulo/aplicativo com falha") e extraída a lógica pura pra uma classe nova (`ExtratorEventoTexto`, sem o atributo de plataforma Windows, pra ficar diretamente testável) com 8 testes novos cobrindo os dois idiomas. E adicionado um limite de 200 eventos por categoria na leitura do Event Log — sem cap, uma máquina com problema crônico de hardware (o cenário que a história mais precisa atender) geraria milhares de eventos WHEA em 30 dias, degradando leitura e renderização.

A revisão independente `bmad-code-review` (4 lenses) sobre o commit resultante encontrou mais defeitos reais, corrigidos: (3) o cap de 200 eventos por categoria, combinado com `EventLogQuery` sem `ReverseDirection`, mantinha os eventos **mais antigos** da janela de 30 dias e descartava os mais recentes — o oposto do que a história promete numa máquina com muitos eventos; corrigido com `ReverseDirection = true`. (4) a consulta WHEA não filtrava por severidade (diferente de BSOD/crash, que filtram por `EventID`), capturando eventos informativos/corrigidos que nunca causaram instabilidade real na heurística de correlação; corrigido com filtro `Level=1 or Level=2` (Crítico/Erro). (5) um elemento `null` dentro do array `driversDesatualizados` (JSON malformado ou `[null]`) desserializava pra um `InfoDriver?` nulo na lista, e o acesso a `d.Fabricante` em `CorrelacionadorCausaRaiz` lançaria `NullReferenceException` não capturada pelo filtro de exceções do `TratarAsync`; corrigido filtrando elementos nulos após a desserialização, com testes de regressão. (6) `driversDesatualizados: null` explícito era rejeitado como erro, inconsistente com o parâmetro `bios` (que já tratava `null` explícito como "não informado"); corrigido pro mesmo tratamento. Também adicionado, por achado do Blind Hunter: os valores brutos do enum `TipoEventoInstabilidade` apareciam sem tradução na UI (ex. "Bsod"/"Whea"), inconsistente com a própria justificativa desta história de que o público-alvo é PT-BR — adicionado `TipoEventoInstabilidadeConverter` com rótulos em português.

**Acceptance Criteria:**
- Given que o app está aberto e o usuário solicita a leitura, when o sistema lê o Event Log do Windows (BSOD, WHEA, crash de aplicação), then cada evento é registrado com timestamp, tipo, e driver/processo associado quando disponível — consulta sob demanda, nunca em background/daemon
- Given um driver ou BIOS desatualizado (Stories 1.2/1.4) e eventos do Event Log no mesmo período, when existe correlação plausível entre os dois (mesmo subsistema), then o Diagnóstico nomeia a causa específica, não uma mensagem genérica, and quando não há correlação, o sistema mostra o achado sem inventar uma causa

## Design Notes

**Critério de "correlação plausível" (não definido nas fontes upstream — decisão desta implementação):** como `InfoDriver` não tem campo de subsistema/classe, a correlação usa dois sinais textuais concretos em vez de inventar uma taxonomia de subsistemas: (1) correspondência de fabricante — o texto do evento (`Origem`/`ProcessoOuDriver`/`Mensagem`) contém o nome do fabricante de um driver já desatualizado (ex. "NVIDIA", "Realtek"); (2) heurística WHEA↔BIOS — eventos WHEA (Windows Hardware Error Architecture, erros de hardware em nível de barramento/memória/PCIe) são frequentemente corrigidos por atualizações de BIOS/AGESA (já refletido no `Motivo`/`Changelog` curados de `BancoCuradoBios`), então WHEA + BIOS desatualizada sinalizada é tratado como correlação plausível. Fora desses dois casos, nenhuma causa é atribuída — é uma heurística deliberadamente limitada, não uma inferência genérica por IA/LLM (mantém o guard anti-alucinação determinístico e testável).

`LeitorEventLog` usa a API gerenciada nativa (`System.Diagnostics.Eventing.Reader`, parte do runtime .NET no Windows) em vez do padrão PowerShell/CIM dos outros leitores — não há necessidade de invocar processo externo pra ler o Event Log, e a API estruturada evita parsing de texto frágil.

## Verification

**Commands:**
- `dotnet build HardwareOptimizer.sln` -- 0 erros (via placeholder local pro bug conhecido de `Features.LifeCounter`, nunca commitado)
- `dotnet test tests/HardwareOptimizer.Features.Atualizacao.Tests` -- todos verdes
- `dotnet test tests/HardwareOptimizer.Agent.Tests` -- todos verdes
- `dotnet test tests/HardwareOptimizer.App.Tests` -- todos verdes, sem regressão
- `dotnet test tests/HardwareOptimizer.Ipc.Tests` -- todos verdes, sem regressão
