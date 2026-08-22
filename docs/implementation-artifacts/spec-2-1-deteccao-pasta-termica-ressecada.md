---
title: 'Usuário vê detecção de pasta térmica ressecada'
type: 'feature'
created: '2026-08-22'
status: 'done'
baseline_revision: 'db00c3e9b9755d7654b59b0ffb52c771ea280dbc'
review_loop_iteration: 0
followup_review_recommended: true
context: []
warnings: []
deferred:
  - summary: >-
      Nenhum CancellationToken é propagado da UI até o diagnóstico em
      andamento, então o usuário não consegue abortar a janela de carga
      simulada (~8s) uma vez iniciada.
    evidence: |-
      Achado do Blind Hunter (revisão independente). `DiagnosticoManutencaoViewModel.DiagnosticarAsync`
      chama `_agente.TratarAsync(new RequisicaoIpc {...})` sem token, embora
      `GeradorCargaSimulada`/`RoteadorIpc.DiagnosticarManutencaoAsync` já
      suportem cancelamento. Baixo impacto (janela curta), mas real.
    location: >-
      src/HardwareOptimizer.App/ViewModels/DiagnosticoManutencaoViewModel.cs
    severity: medium
  - summary: >-
      Nenhum teste dedicado cobre GeradorCargaSimulada isoladamente (duração
      honrada, cancelamento mid-run, ocupação real dos núcleos).
    evidence: |-
      Achado do Blind Hunter. Só exercitado indiretamente via IpcTests com
      duração de 15ms. Comportamentos de timing/cancelamento não são
      verificados isoladamente.
    location: >-
      src/HardwareOptimizer.Agent/Manutencao/GeradorCargaSimulada.cs
    severity: low
  - summary: >-
      A propriedade JaDiagnosticou do ViewModel nunca é usada no XAML — a
      view não distingue visualmente "nunca diagnosticado" de "diagnosticado,
      nada encontrado".
    evidence: |-
      Achado do Blind Hunter. Estado morto, sem impacto funcional, só
      oportunidade de UX não aproveitada.
    location: >-
      src/HardwareOptimizer.App/ViewModels/DiagnosticoManutencaoViewModel.cs
    severity: low
  - summary: >-
      A detecção usa a temperatura máxima de QUALQUER sensor tipo Temperatura
      (LeituraSensores.TemperaturaMaxC), não um sensor especificamente de CPU
      — GPU/NVMe/VRM/chipset quentes em idle podem gerar o mesmo achado.
    evidence: |-
      Achado do Blind Hunter. Mitigado parcialmente pela Justificativa não
      mencionar "CPU" explicitamente ("acúmulo de poeira no dissipador" é
      genérico), mas o risco de falso positivo por outro componente é real.
      Corrigir exigiria filtrar por nome/tipo de sensor, frágil hoje (sem
      contrato tipado de CPU vs. GPU nos leitores concretos).
    location: >-
      src/HardwareOptimizer.Features.Manutencao/DetectorPastaTermica.cs
    severity: medium
  - summary: >-
      GeradorCargaSimulada é instanciado inline (`new GeradorCargaSimulada()`)
      em RoteadorIpc em vez de injetado via construtor, quebrando o padrão de
      DI usado pelos demais colaboradores (_sensores, _cerebro, etc.).
    evidence: |-
      Achado do Blind Hunter. O parâmetro duracaoCargaManutencao já resolve a
      necessidade prática de teste (duração quase instantânea); a falta de
      injeção do gerador em si é uma questão de pureza de DI, não um
      bloqueador de teste.
    location: >-
      src/HardwareOptimizer.Ipc/RoteadorIpc.cs
    severity: low
  - summary: >-
      O texto da spec ("carga simulada, limitada em duração — poucos
      segundos") e o valor padrão de produção (8s) estão numa fronteira
      semântica discutível — "poucos segundos" normalmente sugere algo mais
      curto.
    evidence: |-
      Achado do Intent Alignment Auditor. Não é uma violação clara, é uma
      leitura de fronteira; 8s foi escolhido pra garantir uma janela de carga
      real o bastante pro sensor responder termicamente.
    location: >-
      src/HardwareOptimizer.Ipc/RoteadorIpc.cs
    severity: low
baseline_commit: 'db00c3e'
---

<intent-contract>

## Intent

**Problem:** o app já lê sensores de temperatura/carga sob demanda (`ServicoSensores`), mas não interpreta essa leitura pra sinalizar um sintoma real e acionável — hoje nada avisa o usuário que a pasta térmica pode estar ressecada, o achado factual mais barato de resolver do produto.

**Approach:** nova fatia `Features.Manutencao` compara duas leituras de `ServicoSensores.LerAsync()` — uma em momento de carga baixa (idle) e outra sob carga simulada por um gerador de carga interno, curto e limitado (não uma ferramenta externa tipo OCCT/Prime95 — essa é `IFerramentaEstresse`, deliberadamente não reaproveitada, ver Design Notes). Quando a temperatura idle já está anormalmente alta (sintoma clássico de pasta ressecada — a peça não dissipa calor com eficiência mesmo em repouso), o achado é sinalizado com um valor de `Custo` comparável (contrato já existente). Sem esse sinal, nada é exibido — nunca um achado inventado.

## Boundaries & Constraints

**Always:**
- A detecção usa só leitura de sensor (`ServicoSensores`, já existente) — nenhuma pergunta de diagnóstico/sintoma ao usuário.
- Um achado só é retornado quando a leitura sinaliza suspeita real (guard anti-alucinação) — sem sinal, `null`/lista vazia, nunca "possível problema" genérico.
- A geração de carga é interna, limitada em duração (poucos segundos), e nunca depende de ferramenta externa instalada pelo usuário.
- Leitura de sensor continua sob demanda (usuário solicita) — nunca em timer/daemon/background.

**Block If:** nenhuma decisão desta história exige aprovação humana durante a execução — escopo é leitura de sensor + interpretação + exibição, sem ação mutante no sistema do usuário.

**Never:**
- Nunca reaproveitar `IFerramentaEstresse`/`AnalisadorRegressao`/`MedicaoEstresse` (`Agent/Validation/`) — pressupõem ferramenta de estresse externa (OCCT/Prime95) disparada por minutos após o usuário já ter aprovado uma mudança; incompatível com "nenhuma pergunta ao usuário" e com o tempo de resposta esperado de uma tela de diagnóstico.
- Nunca introduzir um novo mecanismo de leitura de sensor — reaproveitar `ServicoSensores`/`LeituraSensores` como estão.
- Nunca oferecer confirmação/aplicação de manutenção nesta história — isso é a Story 2.3, que reusa o `ConfirmationPanel` (severidade `manutencao`) depois que este achado já existe.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Temperatura idle anormalmente alta | Leitura idle acima do limiar de suspeita | Achado retornado com temperatura idle/carga e `Custo` | N/A |
| Temperatura idle normal | Leitura idle abaixo do limiar | Nenhum achado retornado (`null`) | N/A |
| Sensor de temperatura indisponível | `LeituraSensores.TemperaturaMaxC` é `null` nas duas leituras | Nenhum achado retornado — nunca inventar dado | N/A |
| Leitura de sensor lança exceção | Falha inesperada em `ServicoSensores.LerAsync()` | Tratado como sem sinal, log de warning, nunca propaga | Log + retorna `null` |

</intent-contract>

## Code Map

- `src/HardwareOptimizer.Core/Contracts/AchadoManutencao.cs` — NOVO. `sealed record AchadoManutencao { TemperaturaIdleC, TemperaturaCargaC, Custo, Justificativa }` — mesmo estilo de `InfoSoftware.cs`/`InfoBios.cs`: só existe quando o achado é real.
- `src/HardwareOptimizer.Agent/Manutencao/GeradorCargaSimulada.cs` — NOVO. `Task GerarAsync(TimeSpan duracao, CancellationToken ct)`: laço ocupado limitado (`Parallel.For` por `Environment.ProcessorCount`, ex. ~8s) só pra produzir uma janela de carga real e comparável — nunca ferramenta externa.
- `src/HardwareOptimizer.Features.Manutencao/HardwareOptimizer.Features.Manutencao.csproj` — NOVO projeto (fatia vertical), referencia só `Core` (mesmo padrão de `Features.Atualizacao`).
- `src/HardwareOptimizer.Features.Manutencao/DetectorPastaTermica.cs` — NOVO. Lógica pura, sem I/O: `Detectar(LeituraSensores idle, LeituraSensores carga) -> AchadoManutencao?`. Limiar de temperatura idle suspeita documentado em Design Notes.
- `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` — MODIFICADO. Novo método `"diagnosticarmanutencao"` (sem gate Windows — `ServicoSensores` já resolve a plataforma internamente, mesmo padrão do case `"sensores"` existente): lê sensor idle, roda `GeradorCargaSimulada`, lê sensor sob carga, chama `DetectorPastaTermica.Detectar`, devolve `AchadoManutencao?`.
- `src/HardwareOptimizer.App/ViewModels/DiagnosticoManutencaoViewModel.cs` + `src/HardwareOptimizer.App/Views/DiagnosticoManutencaoView.axaml(.cs)` — NOVOS. Nova aba (mesmo padrão de `DriversViewModel`/`BiosGuideViewModel`) com comando `DiagnosticarCommand` (sob demanda), `Achado` (`AchadoManutencao?`), `StatusText`.
- `src/HardwareOptimizer.App/ViewModels/ShellViewModel.cs` — MODIFICADO. Nova propriedade `DiagnosticoManutencao`, comando `IrParaDiagnosticoManutencao`, propriedade `PaginaEhDiagnosticoManutencao` (mesmo padrão das abas existentes) — sem gate de `FuncionalidadePremium` (ver Design Notes).
- `src/HardwareOptimizer.App/Views/ShellWindow.axaml` — MODIFICADO. Novo item de sidebar pra a aba.
- `tests/HardwareOptimizer.Features.Manutencao.Tests/DetectorPastaTermicaTests.cs` — NOVO projeto de testes, cobre a I/O Matrix acima.
- `tests/HardwareOptimizer.Ipc.Tests/IpcTests.cs` — testes do fluxo real via `RoteadorIpc`.

## Tasks & Acceptance

**Execution:**
- `src/HardwareOptimizer.Core/Contracts/AchadoManutencao.cs` -- criar o record -- modelo de exibição do achado de manutenção
- `src/HardwareOptimizer.Agent/Manutencao/GeradorCargaSimulada.cs` -- gerador de carga interno limitado -- produz leitura "sob carga" real sem ferramenta externa
- `src/HardwareOptimizer.Features.Manutencao/DetectorPastaTermica.cs` -- lógica pura de detecção -- guard anti-alucinação, só sinaliza com lastro real
- `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` -- método `"diagnosticarmanutencao"` -- conecta o fluxo real
- `src/HardwareOptimizer.App/ViewModels/DiagnosticoManutencaoViewModel.cs` + `DiagnosticoManutencaoView.axaml` + `ShellViewModel.cs` + `ShellWindow.axaml` -- nova aba sob demanda -- fecha o fluxo ponta a ponta
- `tests/HardwareOptimizer.Features.Manutencao.Tests/` -- `DetectorPastaTermicaTests.cs` -- cobre a I/O Matrix acima; testes adicionais em `tests/HardwareOptimizer.Ipc.Tests/IpcTests.cs`

**Acceptance Criteria:**
- Given que o usuário solicitou o diagnóstico de manutenção, when o sistema lê os sensores em pelo menos dois momentos (idle e sob carga simulada), then temperatura idle anormalmente alta é sinalizada como possível pasta térmica ressecada ou necessidade de limpeza
- Given qualquer solicitação de diagnóstico, when a leitura de sensor não sinaliza suspeita real, then nenhum achado é exibido e nenhuma pergunta de diagnóstico é feita ao usuário em nenhum momento do fluxo

## Spec Change Log

## Review Triage Log

### 2026-08-22 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 6 (high 3, medium 1, low 2)
- defer: 6
- reject: 4
- addressed_findings:
  - `[high]` `[patch]` Blind Hunter + Intent Alignment Auditor: a justificativa da spec pra não usar `FuncionalidadePremium` na nova aba estava factualmente errada — `IrParaDrivers`/`IrParaBiosGuide` de fato checam `_licenca.TemAcesso(...)` (verificado em `ShellViewModel.cs:164,171`), diferente do que as Design Notes afirmavam. Corrigido adicionando `FuncionalidadePremium.DiagnosticoManutencao` e o gate correspondente, alinhado ao padrão dos 4 módulos premium já existentes.
  - `[high]` `[patch]` Edge Case Hunter: `DetectorPastaTermica.Detectar` não tratava `NaN`/`Infinity` — `NaN < 55` é `false` em IEEE754, então uma leitura idle `NaN` (sensor com falha) *não* retornava `null` e produzia um achado fabricado (viola o guard anti-alucinação). Corrigido com `double.IsFinite` nos dois lados da comparação.
  - `[high]` `[patch]` Blind Hunter + Intent Alignment Auditor (achado convergente): `TemperaturaCargaC` era `required double` com fallback silencioso pro valor idle quando a leitura sob carga falhava — um valor fabricado exibido como se fosse uma medição real, inclusive na própria UI desta história (não só um risco futuro pra Story 2.3). Corrigido tornando o campo `double?`, removendo o fallback fabricado.
  - `[medium]` `[patch]` Blind Hunter: nenhuma divulgação ao usuário de que o diagnóstico ocupa todos os núcleos de CPU por alguns segundos. Corrigido com texto explícito no status/botão.
  - `[low]` `[patch]` Edge Case Hunter: chamada a `DetectorPastaTermica.Detectar` ficava fora do `try/catch` em `RoteadorIpc.DiagnosticarManutencaoAsync`. Movida pra dentro, defesa em profundidade barata.
  - `[low]` `[patch]` Blind Hunter: `GeradorCargaSimulada` rodava no ThreadPool compartilhado sem isolamento. Adicionado `TaskCreationOptions.LongRunning`.

Itens `defer` (reais, registrados em `deferred-work.md`, não bloqueiam esta história): sem cancelamento de UI pro diagnóstico em andamento; sem testes dedicados de `GeradorCargaSimulada`; `JaDiagnosticou` sem uso na view; detecção usa temperatura máxima de qualquer sensor (não filtra por CPU), risco de falso positivo por GPU/NVMe quente; `GeradorCargaSimulada` não é injetável em `RoteadorIpc`; duração de 8s vs. "poucos segundos" do texto da spec é uma leitura de fronteira.

Itens `reject`: tratamento amplo de exceção "mascarando bug como saudável" (é exatamente o que a I/O Matrix exige — nunca propagar); teste `NaoExigeWindows` não varia plataforma de verdade (mesmo padrão já aceito em histórias anteriores); observações duplicadas do Intent Alignment Auditor já cobertas pelos itens acima.

## Design Notes

**Por que não reaproveitar `IFerramentaEstresse`:** essa infraestrutura pressupõe uma ferramenta de estresse de terceiros (OCCT/Prime95/MemTest86) que o usuário precisaria ter instalado, rodando por minutos, disparada hoje só depois de uma aprovação de mudança já feita (`RoteadorIpc.AprovarAsync`). Isso contradiz "nenhuma pergunta de diagnóstico ao usuário" e o tempo de resposta esperado de uma tela de diagnóstico. `GeradorCargaSimulada` é deliberadamente mais simples: um laço ocupado interno, curto, sem dependência externa — literalmente a "carga simulada" que o critério de aceite permite (em oposição a "carga observada", que exigiria esperar o usuário usar a máquina organicamente).

**Critério de "temperatura idle anormalmente alta" (não definido nas fontes upstream — decisão desta implementação):** o sintoma clássico de pasta térmica ressecada é a CPU não dissipar calor eficientemente mesmo em repouso — por isso o sinal é a temperatura **idle** estar acima de um limiar absoluto (não uma comparação idle-vs-carga), consistente com o texto do critério de aceite ("temperatura alta sob carga baixa"). `DetectorPastaTermica` usa um limiar configurável (constante `TemperaturaIdleSuspeitaC`, valor inicial 55°C — heurística conservadora, documentada no código, revisável). A leitura sob carga é capturada e devolvida no achado (contexto pra Story 2.3 comparar antes/depois), mas não participa da regra de decisão desta história.

**Superfície:** o `epic-2-context.md` deixa a navegação em aberto ("aba nova ou sub-seção de Info Sistema"). Decidido por nova aba dedicada, seguindo o padrão já estabelecido no Épico 1 (Drivers, BiosGuide, Upgrade, VidaUtil são todas abas próprias) — evita inflar `InfoSistemaView`/`InfoSistemaViewModel`, já grandes (563/340 linhas).

**Gate de `FuncionalidadePremium.DiagnosticoManutencao` (corrigido na revisão):** a primeira versão desta spec afirmava que Drivers/BiosGuide não tinham gate Premium no V1 — checado direto no código (`ShellViewModel.cs`), isso é factualmente falso: `IrParaDrivers`/`IrParaBiosGuide` chamam `_licenca.TemAcesso(...)` e `TemAcesso` só retorna `true` para licença `Premium`, independente da funcionalidade pedida. Os 4 valores existentes do enum (`ModuloUpgrade`, `ContadorVidaUtil`, `GerenciadorDrivers`, `GuiaBiosIa`) são todos, sem exceção, telas-módulo gated — o nome do enum já reflete o padrão de monetização do produto (módulos premium nomeados). Corrigido: nova entrada `DiagnosticoManutencao` no enum, mesmo gate aplicado em `IrParaDiagnosticoManutencao`.

**Correções da revisão independente (NaN e fabricação de dado):** `DetectorPastaTermica.Detectar` comparava `temperaturaIdle.Value < TemperaturaIdleSuspeitaC` sem checar `double.IsFinite` — uma leitura `NaN` (sensor com falha, mas não `null`) fazia a comparação avaliar `false` e produzia um achado fabricado a partir de dado inválido, violando o guard anti-alucinação. Corrigido exigindo `double.IsFinite` nos dois lados. Separadamente, `TemperaturaCargaC` era `required double` com fallback pro valor idle quando a leitura sob carga não tinha sensor — um valor fabricado (não uma medição real) exibido como se fosse, inclusive na tela desta própria história (não só um risco futuro pra Story 2.3, como o achado original sugeria). Corrigido tornando o campo `double?`: `null` quando não há leitura real sob carga, nunca um substituto silencioso.

## Verification

**Commands:**
- `dotnet build HardwareOptimizer.sln` -- 0 erros (via placeholder local pro bug conhecido de `Features.LifeCounter`, nunca commitado)
- `dotnet test tests/HardwareOptimizer.Features.Manutencao.Tests` -- todos verdes
- `dotnet test tests/HardwareOptimizer.App.Tests` -- todos verdes, sem regressão
- `dotnet test tests/HardwareOptimizer.Ipc.Tests` -- todos verdes, sem regressão

## Auto Run Result

**Resumo da mudança implementada:** nova fatia `Features.Manutencao` detecta o sintoma clássico de pasta térmica ressecada (temperatura idle anormalmente alta) reaproveitando `ServicoSensores` já existente, comparado contra uma leitura sob carga gerada por um laço interno curto e limitado (`GeradorCargaSimulada` — nunca ferramenta externa tipo OCCT/Prime95). Nova aba "Manutenção" na sidebar, gated por `FuncionalidadePremium.DiagnosticoManutencao` (mesmo padrão dos 4 módulos premium já existentes), sob demanda, sem nenhuma pergunta de diagnóstico ao usuário.

**Arquivos alterados:**
- `src/HardwareOptimizer.Core/Contracts/AchadoManutencao.cs` (novo) -- contrato do achado, `TemperaturaCargaC` nullable (guard anti-alucinação, corrigido na revisão)
- `src/HardwareOptimizer.Agent/Manutencao/GeradorCargaSimulada.cs` (novo) -- gerador de carga interno via `Task.Factory.StartNew(..., TaskCreationOptions.LongRunning)`
- `src/HardwareOptimizer.Features.Manutencao/` (novo projeto) -- `DetectorPastaTermica.cs`, lógica pura com guard `double.IsFinite`
- `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` -- novo método `"diagnosticarmanutencao"`, sem gate Windows
- `src/HardwareOptimizer.App/ViewModels/DiagnosticoManutencaoViewModel.cs` + `Views/DiagnosticoManutencaoView.axaml(.cs)` (novos) -- nova aba sob demanda
- `src/HardwareOptimizer.App/ViewModels/ShellViewModel.cs` + `Views/ShellWindow.axaml` -- wiring da nova aba, gate Premium + padrão visual `nav-premium`/`nav-locked`
- `src/HardwareOptimizer.Features.Licensing/FuncionalidadePremium.cs` -- novo valor `DiagnosticoManutencao`
- `tests/HardwareOptimizer.Features.Manutencao.Tests/`, `tests/HardwareOptimizer.App.Tests/DiagnosticoManutencaoViewModelTests.cs`, `tests/HardwareOptimizer.Ipc.Tests/IpcTests.cs` (novos/modificados) -- cobertura da I/O Matrix + patches

**Achados da revisão (Blind Hunter + Edge Case Hunter + Verification Gap + Intent Alignment Auditor):** 6 patches aplicados (3 high, 1 medium, 2 low — ver Review Triage Log), 6 itens deferidos (registrados no frontmatter `deferred`), 4 rejeitados (ruído/já coberto). Nenhum intent_gap, nenhum bad_spec (o achado mais sério — gate Premium ausente com justificativa factualmente errada na spec — foi tratado como patch mecanicamente trivial e bem precedenciado, não exigiu reversão/re-derivação completa).

**Recomendação de revisão de acompanhamento:** `true` -- 3 patches de severidade `high` foram corrigidos nesta passada (critério: qualquer patch `high` já dispara `true`).

**Verificação realizada:** `dotnet build HardwareOptimizer.sln` (0 erros) e as 3 suítes afetadas rodadas de forma independente pelo orquestrador (não só pelo subagente de implementação) após os patches: `Features.Manutencao.Tests` 12/12, `Ipc.Tests` 82/82, `App.Tests` 129/129 -- todas verdes, sem regressão.

**Riscos residuais:** ver os 6 itens em `deferred` no frontmatter -- nenhum crítico; o mais relevante é a ausência de cancelamento de UI pro diagnóstico em andamento (janela de ~8s) e o risco de falso positivo por sensor não-CPU (GPU/NVMe/VRM quentes em idle).
