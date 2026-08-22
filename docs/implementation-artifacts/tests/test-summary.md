# Test Automation Summary

## Story validada

Story 1.1 (Épico 1) — Corrigir bug de build que impede compilação de Features.Upgrade e Features.Drivers.

## Generated Tests

### Regressão de build (infraestrutura, não API/UI)

- [x] `scripts/verificar-build-clone-limpo.ps1` — clona o repositório num diretório temporário isolado e valida, de ponta a ponta:
  1. `hardware_catalog.json` e `whql_catalog.json` estão versionados no git.
  2. `git check-ignore` não captura mais `src/HardwareOptimizer.Features.{Upgrade,Drivers}/Data/`.
  3. `git check-ignore` ainda captura `data/backups/` na raiz (pasta de runtime do `ServicoBackup`) — garante que a regra não ficou permissiva demais.
  4. `HardwareOptimizer.Features.Upgrade` compila sem erro a partir do clone limpo.
  5. `HardwareOptimizer.Features.Drivers` compila sem erro a partir do clone limpo.

### Suítes de unidade (já existentes, re-verificadas)

- [x] `tests/HardwareOptimizer.Features.Upgrade.Tests` — 20/20 aprovados (antes desta correção, não compilava).
- [x] `tests/HardwareOptimizer.Features.Drivers.Tests` — 17/17 aprovados (antes desta correção, não compilava).

## Execução

```
PS> .\scripts\verificar-build-clone-limpo.ps1
>> Clonando ... para ...
>> 1. Verificando se os catálogos estão versionados
>> 2. Verificando que Data/ dos projetos NÃO é ignorado
>> 3. Verificando que data/backups/ (runtime) AINDA e ignorado
>> 4. Compilando Features.Upgrade
Compilação com êxito.  0 Aviso(s)  0 Erro(s)
>> 5. Compilando Features.Drivers
Compilação com êxito.  0 Aviso(s)  0 Erro(s)
PASSOU: build limpo e regra do .gitignore corretos.
```

## Coverage

- Critério de aceite da Story 1.1 (Given/When/Then): 4/4 cláusulas validadas de ponta a ponta a partir de um clone genuinamente limpo — não apenas do working directory que já tinha os arquivos soltos em disco.
- Regressão específica coberta: a regra do `.gitignore` não pode voltar a ficar permissiva (testado que `/data/` ainda ignora `data/backups/` na raiz).

## Next Steps

- Incorporar os scripts de regressão num pipeline de CI, quando um existir (nenhum foi encontrado no repositório nesta rodada).

---

## Story validada — Story 1.2 (Épico 1)

Usuário varre e aprova atualização de driver, com rollback. Commits `4ade3ed` (implementação) + `02e601a` (fix pós-revisão independente).

### Regressão de ponta a ponta

- [x] `scripts/verificar-story-1-2-clone-limpo.ps1` — clona o repositório num diretório temporário isolado (com placeholder local só do clone pro bug conhecido de `Features.LifeCounter`) e valida:
  1. **Fix crítico do rollback presente**: `RestaurarBackupAsync` usa a flag `/subdirs` no comando `pnputil` — sem ela, o rollback varre só a raiz do backup e não encontra nada (drivers ficam em subpastas numeradas por `pnputil /export-driver *`), retornando sucesso sem restaurar nenhum driver.
  2. `HardwareOptimizer.Ipc` e `HardwareOptimizer.App` compilam sem erro a partir do clone limpo (arrastam `Features.Atualizacao`/`Features.Drivers`).
  3. As 4 suítes de teste afetadas passam com as contagens exatas esperadas.

### Suítes de unidade (já existentes, re-verificadas a partir de clone limpo)

- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests` — 8/8 aprovados.
- [x] `tests/HardwareOptimizer.Features.Drivers.Tests` — 18/18 aprovados.
- [x] `tests/HardwareOptimizer.Ipc.Tests` — 57/57 aprovados.
- [x] `tests/HardwareOptimizer.App.Tests` — 91/91 aprovados.

### Execução

```
PS> .\scripts\verificar-story-1-2-clone-limpo.ps1
>> Clonando ... para ...
>> Placeholder local (só neste clone) pro bug conhecido de Features.LifeCounter
>> 1. Confirmando o fix crítico do rollback (flag /subdirs)
>> 2. Compilando Ipc (arrasta Atualizacao, Drivers, LifeCounter)
Compilação com êxito.  0 Aviso(s)  0 Erro(s)
>> 3. Compilando App
Compilação com êxito.  1 Aviso(s) (pré-existente, não relacionado)  0 Erro(s)
>> Testando tests/HardwareOptimizer.Features.Atualizacao.Tests (esperado: 8)
>> Testando tests/HardwareOptimizer.Features.Drivers.Tests (esperado: 18)
>> Testando tests/HardwareOptimizer.Ipc.Tests (esperado: 57)
>> Testando tests/HardwareOptimizer.App.Tests (esperado: 91)
PASSOU: Story 1.2 validada a partir de clone limpo -- fix do rollback presente, build e 174 testes verdes.
```

### Coverage

- Bug crítico corrigido pela revisão independente (rollback silenciosamente vazio) tem checagem própria no script — regressão futura desse bug específico é pega antes de virar bug de produção de novo.
- Critérios de aceite da Story 1.2 cobertos pelas suítes unitárias já auditadas na Matrix Test Audit do `bmad-build` (ver `spec-1-2-driver-scan-aprovacao-rollback.md`), aqui re-confirmadas a partir de clone limpo (não só do working directory do Dev).

### Next Steps

- Story 1.3 (Épico 1) segue no ciclo Dev → Revisão → QA.
- Item de deferred-work.md a acompanhar: `whql_catalog.json` tem URLs de landing page, não arquivo direto — o caminho de sucesso de "aprovar atualização" é inalcançável com o catálogo atual em teste manual/exploratório.

---

## Story validada — Story 1.3 (Épico 1)

Usuário é alertado sobre software desatualizado via fonte oficial. Commits `08aa8fe` (implementação) + `8fee95a` (fixes pós-revisão independente `bmad-code-review`: cancelamento propagado corretamente, guard de versão espaços-em-branco).

### Regressão de ponta a ponta

- [x] `scripts/verificar-story-1-3-clone-limpo.ps1` — clona o repositório num diretório temporário isolado (com placeholder local só do clone pro bug conhecido de `Features.LifeCounter`) e valida:
  1. **Fixes críticos da revisão independente presentes**: `VerificadorSoftware.VerificarAsync` tem um `catch (OperationCanceledException) { throw; }` dedicado antes do catch genérico (sem ele, cancelamento do `CancellationToken` seria tratado como "falha do provedor" e engolido silenciosamente) e usa `IsNullOrWhiteSpace` — não `IsNullOrEmpty` — nos dois guards de versão (`programa.Versao` e `oficial.VersaoDisponivel`; sem isso, uma versão só-com-espaços passaria como válida, reabrindo o bug de dado inventado que a primeira rodada de revisão já havia corrigido para nulo/vazio).
  2. `HardwareOptimizer.Ipc` e `HardwareOptimizer.App` compilam sem erro a partir do clone limpo (arrastam `Features.Atualizacao`).
  3. As 3 suítes de teste afetadas passam com as contagens exatas esperadas.

### Suítes de unidade (já existentes, re-verificadas a partir de clone limpo)

- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests` — 33/33 aprovados.
- [x] `tests/HardwareOptimizer.Ipc.Tests` — 62/62 aprovados.
- [x] `tests/HardwareOptimizer.App.Tests` — 97/97 aprovados.

### Execução

```
PS> .\scripts\verificar-story-1-3-clone-limpo.ps1
>> Clonando ... para ...
>> Placeholder local (só neste clone) pro bug conhecido de Features.LifeCounter
>> 1. Confirmando os fixes críticos da revisão independente
>> 2. Compilando Ipc (arrasta Atualizacao, Drivers, LifeCounter)
Compilação com êxito.  0 Aviso(s)  0 Erro(s)
>> 3. Compilando App
Compilação com êxito.  1 Aviso(s) (pré-existente, não relacionado)  0 Erro(s)
>> Testando tests/HardwareOptimizer.Features.Atualizacao.Tests (esperado: 33)
>> Testando tests/HardwareOptimizer.Ipc.Tests (esperado: 62)
>> Testando tests/HardwareOptimizer.App.Tests (esperado: 97)
PASSOU: Story 1.3 validada a partir de clone limpo -- fixes de cancelamento/versao presentes, build e 192 testes verdes.
```

### Coverage

- Matrix Test Audit da spec (`spec-1-3-software-desatualizado.md`, 5 linhas: software desatualizado no catálogo, mesma versão, sem cobertura, lista vazia, exceção no provedor) — todas cobertas por `VerificadorSoftwareTests.cs`, re-confirmadas a partir de clone limpo.
- Os dois bugs reais encontrados pela revisão independente (`bmad-code-review`, 4 lenses: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) — cancelamento engolido e guard de versão com espaços em branco — têm checagem própria no script, além dos testes de regressão dedicados (`VerificarAsync_CancelamentoDoProvedor_Propaga`, `VerificarAsync_VersaoInstaladaSomenteEspacos_NaoAparece`, `VerificarAsync_ProvedorRetornaVersaoSomenteEspacos_TratadoComoSemCobertura`).
- `IpcTests.VerificarSoftware_PayloadSerializadoComProtocoloIpcJson_RoundTripCorreto` fecha a lacuna de regressão do bug de casing self-caught pelo Dev — serializa um `ProgramaInstalado` real com `ProtocoloIpc.Json` (o mesmo codec e shape usados em produção por `DriversViewModel`), não fixtures escritas manualmente em camelCase.

### Next Steps

- Story 1.4 (Épico 1) segue no ciclo Dev → Revisão → QA.
- Item de deferred-work.md a acompanhar: `Drivers.PopularProgramas(...)` em `ShellViewModel.cs` não tem nenhum teste automatizado — infraestrutura de teste headless do Avalonia ainda não existe no projeto `App.Tests`; se essa wiring quebrar silenciosamente, nenhum teste hoje pegaria.

---

## Story validada — Story 1.4 (Épico 1)

Usuário é orientado a atualizar a BIOS com alerta de risco obrigatório. Commits `1ce76b2` (implementação) + `3a58f93` (fixes pós-revisão independente `bmad-code-review`: guard de confirmação ausente em `VerGuiaBios`, estado de painel/guia não resetado numa nova verificação de BIOS).

### Regressão de ponta a ponta

- [x] `scripts/verificar-story-1-4-clone-limpo.ps1` — clona o repositório num diretório temporário isolado (com placeholder local só do clone pro bug conhecido de `Features.LifeCounter`) e valida:
  1. **Fixes críticos da revisão independente presentes**: `VerGuiaBios()` tem o guard `if (!ConfirmadoBios) return;` (sem ele, o guia de atualização seria revelado sem o usuário ter confirmado o risco — defesa em profundidade, já que o `ConfirmationPanel` também desabilita o botão na View) e `VerificarBiosAsync` reseta `PainelConfirmacaoBiosAberto`/`ConfirmadoBios`/`GuiaBiosVisivel` ao fim de cada execução (sem isso, uma confirmação/guia já revelados numa verificação anterior sobreviveriam a um novo alerta de BIOS, violando o "aparece toda vez" da spec).
  2. `HardwareOptimizer.Ipc` e `HardwareOptimizer.App` compilam sem erro a partir do clone limpo (arrastam `Features.Atualizacao`/`Core`).
  3. As 4 suítes de teste afetadas passam com as contagens exatas esperadas.

### Suítes de unidade (já existentes, re-verificadas a partir de clone limpo)

- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests` — 48/48 aprovados.
- [x] `tests/HardwareOptimizer.Core.Tests` — 91/91 aprovados (sem regressão em `AnalisadorBiosTests`/`GeradorGuiaBiosTests`/`VersaoBiosTests`).
- [x] `tests/HardwareOptimizer.App.Tests` — 109/109 aprovados.
- [x] `tests/HardwareOptimizer.Ipc.Tests` — 68/68 aprovados.

### Execução

```
PS> .\scripts\verificar-story-1-4-clone-limpo.ps1
>> Clonando ... para ...
>> Placeholder local (só neste clone) pro bug conhecido de Features.LifeCounter
>> 1. Confirmando os fixes críticos da revisão independente
>> 2. Compilando Ipc (arrasta Atualizacao, Core, Drivers, LifeCounter)
Compilação com êxito.  0 Aviso(s)  0 Erro(s)
>> 3. Compilando App
Compilação com êxito.  1 Aviso(s) (pré-existente, não relacionado)  0 Erro(s)
>> Testando tests/HardwareOptimizer.Features.Atualizacao.Tests (esperado: 48)
>> Testando tests/HardwareOptimizer.Core.Tests (esperado: 91)
>> Testando tests/HardwareOptimizer.App.Tests (esperado: 109)
>> Testando tests/HardwareOptimizer.Ipc.Tests (esperado: 68)
PASSOU: Story 1.4 validada a partir de clone limpo -- fixes de confirmacao/estado presentes, build e 316 testes verdes.
```

### Coverage

- Matrix Test Audit da spec (`spec-1-4-bios-alerta-risco.md`, 5 linhas: BIOS desatualizada no catálogo, mesma versão, sem cobertura, exceção no provedor, reabre orientação já vista) — todas cobertas por `VerificadorBiosTests.cs`/`DriversViewModelBiosTests.cs`, re-confirmadas a partir de clone limpo.
- Os dois bugs reais encontrados pela revisão independente (`bmad-code-review`, 4 lenses: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor — os dois primeiros chegaram à mesma conclusão de forma independente) têm checagem própria no script, além de testes de regressão dedicados (`VerGuiaBios_SemConfirmacao_NaoRevelaGuia`, `VerificarBiosAsync_NovaVerificacao_ResetaConfirmacaoEGuiaJaAbertos`).
- Cobertura de integração real com os 3 formatos de versão do `BancoCuradoBios` (ASUS numérico, MSI alfanumérico misto, Gigabyte prefixado) — gap encontrado pelo Edge Case Hunter e fechado na mesma rodada da revisão (`VerificadorBiosTests.cs`).
- Fronteira única (AD-4) fechada também para BIOS: `IProvedorFonteOficial`/`ProvedorFonteOficialBios` é o único caminho pra dado de versão — `AnalisadorBios`/`ModuloBios`/`ProvedorBiosComCache`/`HardwareOptimizer.Cli` (pipeline mais rico, pré-existente) permanecem intocados e continuam a servir o CLI sem regressão (`AnalisadorBiosTests`/`GeradorGuiaBiosTests`/`VersaoBiosTests` seguem verdes).

### Next Steps

- Story 1.5 (Épico 1, última do épico) segue no ciclo Dev → Revisão → QA.
- Item de deferred-work.md a acompanhar: wiring `Drivers.PopularBios(inv.Placa)` em `ShellViewModel.cs` (mesmo gap de infraestrutura já registrado pra `PopularProgramas` na Story 1.3 — sem `Avalonia.Headless` no projeto de testes, todo o callback `Home.Popular` do `ShellViewModel` fica destestado).

---

## Verificação integrada da sprint — Épico 1 até a Story 1.4 (1.1 + 1.2 + 1.3 + 1.4 em conjunto)

Diferente das validações acima (cada história isolada, só os projetos que ela toca), esta rodada valida as **quatro histórias juntas, na solução inteira**, a partir de um clone git genuinamente limpo — não o working directory do Dev.

- [x] `scripts/verificar-sprint-epico1-clone-limpo.ps1` — clona o repositório para `F:\Testes Software Otimização\otimizacao-sprint-epico1-verificacao` e valida, em sequência:
  1. Os fixes críticos das 4 histórias presentes **ao mesmo tempo** no mesmo clone (catálogos versionados + regra do `.gitignore` da Story 1.1; flag `/subdirs` do rollback da Story 1.2; catch de cancelamento + guards `IsNullOrWhiteSpace` da Story 1.3; guard `if (!ConfirmadoBios) return;` em `VerGuiaBios` + reset de `PainelConfirmacaoBiosAberto`/`ConfirmadoBios`/`GuiaBiosVisivel` em `VerificarBiosAsync` da Story 1.4).
  2. `HardwareOptimizer.sln` — a **solução inteira**, todos os 12 projetos de `src/`, não só os tocados pelo sprint — compila em Release sem erro.
  3. Todas as 9 suítes de teste relevantes rodam **em conjunto** e passam.
  4. O app real (`HardwareOptimizer.App`) sobe a partir dos binários do clone e renderiza a Dashboard corretamente (smoke test funcional, com screenshot como evidência).

### Build da solução inteira

```
Compilação com êxito.  1 Aviso(s) (pré-existente, "xmpDesativado" não usada — não relacionado ao sprint)  0 Erro(s)
```

### Suítes de teste — todas juntas, clone limpo

| Suíte | Aprovados | Falhas |
|---|---|---|
| HardwareOptimizer.Agent.Tests | 173 | 0 |
| HardwareOptimizer.App.Tests | 109 | 0 |
| HardwareOptimizer.Cerebro.Tests | 26 | 0 |
| HardwareOptimizer.Core.Tests | 91 | 0 |
| HardwareOptimizer.Features.Atualizacao.Tests | 48 | 0 |
| HardwareOptimizer.Features.Drivers.Tests | 18 | 0 |
| HardwareOptimizer.Features.Licensing.Tests | 20 | 0 |
| HardwareOptimizer.Features.Upgrade.Tests | 20 | 0 |
| HardwareOptimizer.Ipc.Tests | 68 | 0 |
| HardwareOptimizer.WindowsService.Tests | 10 | 0 |
| **TOTAL** | **583** | **0** |

**Excluída desta rodada:** `HardwareOptimizer.Features.LifeCounter.Tests` — depende de um `tbw_database.json` real/curado que nunca existiu no repositório (gap pré-existente, já registrado em `deferred-work.md`, sem relação com o sprint do Épico 1). Um placeholder mínimo (`[]`) foi aplicado *só neste clone* para permitir que a solução inteira compilasse.

### Smoke test funcional (app real, não só testes unitários)

```
>> 8. Smoke test funcional: subindo o app de verdade a partir do clone
   App subiu, janela encontrada apos 4s.
   Screenshot da Dashboard salvo -- prova visual de que o app renderiza corretamente a partir do clone limpo.
PASSOU: sprint Epico 1 (Stories 1.1+1.2+1.3+1.4) validado em conjunto a partir de clone limpo -- solucao inteira compilando, todas as suites de teste verdes, app funcional.
```

Screenshot salvo em `F:\Testes Software Otimização\otimizacao-sprint-epico1-verificacao\_evidencias-qa\01-dashboard.png`.

**Limitação conhecida:** navegação interativa (clique sintético via `PostMessage`) não é confiável nesta máquina — a tela está em DPI 120 (125% de escala), diferente do DPI 96/100% em que o skill `run-otimize-builder` foi calibrado. A prova funcional aqui é a renderização real do app a partir do clone (build + boot + UI correta), não a navegação entre telas.

### Coverage

- Cobre a lacuna que as validações por história (acima) não cobrem: certeza de que as quatro histórias não quebram uma às outras nem o resto da solução quando tudo é compilado e testado junto, a partir de um clone genuinamente limpo (não do working directory do Dev, que já acumula estado).
- Não substitui as validações por história — é complementar a elas.

### Next Steps

- Story 1.5 (última do Épico 1) ainda não foi desenvolvida — quando estiver, esta rodada integrada deve ser reexecutada com o quinto conjunto de fixes.
- Resolver o gap de `Features.LifeCounter` (`tbw_database.json` ausente) como história própria, já mapeado em `deferred-work.md` — hoje bloqueia rodar a suíte de testes completa da solução sem exclusão manual.
- Script fica disponível em `scripts/verificar-sprint-epico1-clone-limpo.ps1` para reexecução a qualquer momento (`-DestinoBase` configurável, default `F:\Testes Software Otimização`).

---

## Story validada — Story 1.5 (Épico 1, última história do épico)

Usuário vê a causa-raiz de travamentos, correlacionada com o Event Log. Commits `9decc42` (implementação) + `8a46615` (fixes pós-revisão independente `bmad-code-review`: `ReverseDirection` na leitura do Event Log, filtro de severidade WHEA, elemento nulo no array de drivers causando `NullReferenceException` não capturada, tradução PT-BR do tipo de evento).

### Regressão de ponta a ponta

- [x] `scripts/verificar-story-1-5-clone-limpo.ps1` — clona o repositório num diretório temporário isolado (com placeholder local só do clone pro bug conhecido de `Features.LifeCounter`) e valida:
  1. **Fixes críticos da revisão independente presentes**: `EventLogQuery` usa `ReverseDirection = true` (sem isso, o corte de 200 eventos por categoria manteria os mais **antigos** da janela de 30 dias e descartaria os mais recentes — o oposto do que a história promete numa máquina com muitos eventos) e a consulta WHEA filtra por `Level=1 or Level=2` (sem isso, eventos informativos/corrigidos poluiriam a heurística de correlação com BIOS).
  2. `HardwareOptimizer.Ipc` e `HardwareOptimizer.App` compilam sem erro a partir do clone limpo (arrastam `Features.Atualizacao`/`Agent`/`Core`).
  3. As 4 suítes de teste afetadas passam com as contagens exatas esperadas.

### Suítes de unidade (já existentes, re-verificadas a partir de clone limpo)

- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests` — 63/63 aprovados.
- [x] `tests/HardwareOptimizer.Agent.Tests` — 187/187 aprovados (leitura real do Event Log do Windows exercitada, já que a máquina de CI/dev é Windows).
- [x] `tests/HardwareOptimizer.App.Tests` — 121/121 aprovados.
- [x] `tests/HardwareOptimizer.Ipc.Tests` — 75/75 aprovados.

### Execução

```
PS> .\scripts\verificar-story-1-5-clone-limpo.ps1
>> Clonando ... para ...
>> Placeholder local (só neste clone) pro bug conhecido de Features.LifeCounter
>> 1. Confirmando os fixes críticos da revisão independente
>> 2. Compilando Ipc (arrasta Atualizacao, Agent, Core, Drivers, LifeCounter)
Compilação com êxito.  0 Aviso(s)  0 Erro(s)
>> 3. Compilando App
Compilação com êxito.  1 Aviso(s) (pré-existente, não relacionado)  0 Erro(s)
>> Testando tests/HardwareOptimizer.Features.Atualizacao.Tests (esperado: 63)
>> Testando tests/HardwareOptimizer.Agent.Tests (esperado: 187)
>> Testando tests/HardwareOptimizer.App.Tests (esperado: 121)
>> Testando tests/HardwareOptimizer.Ipc.Tests (esperado: 75)
PASSOU: Story 1.5 validada a partir de clone limpo -- fixes de leitura/correlacao presentes, build e 446 testes verdes.
```

### Coverage

- Matrix Test Audit da spec (`spec-1-5-causa-raiz-event-log.md`, 5 linhas: evento correlacionável com driver, evento WHEA com BIOS desatualizada, sem correlação plausível, nenhum evento no período, leitura falha) — as 4 primeiras cobertas por testes reais (`CorrelacionadorCausaRaizTests.cs`); a 5ª (leitura falha) coberta só por inspeção de código — sem teste que force falha real dentro do `try/catch` de `LeitorEventLog`, limitação estrutural já registrada em `deferred-work.md` (mesma aceita pro `LeitorWindows`).
- Os defeitos reais encontrados pela revisão independente (`bmad-code-review`, 4 lenses: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) — leitura invertida (mais antigos em vez de mais recentes), correlação sem filtro de severidade, `NullReferenceException` não capturada, tratamento inconsistente de `null` — têm checagem própria no script (os dois primeiros) e testes de regressão dedicados (`DiagnosticarCausaRaiz_Windows_ArrayComElementoNulo_NaoLancaEIgnoraONulo`, `DiagnosticarCausaRaiz_Windows_DriversDesatualizadosNuloExplicito_TratadoComoListaVazia`).
- Fronteira única (AD-4, indireta) e Boundaries §Never respeitados: `AnalisadorRegressao`/`ModuloBios`/`ProvedorBiosComCache`/`HardwareOptimizer.Cli` intocados; nenhum segundo leitor de driver/BIOS criado (`CorrelacionadorCausaRaiz` reaproveita `InfoDriver`/`InfoBios` já coletados); leitura do Event Log comprovadamente nunca automática (`Popular_PopularProgramas_PopularBios_NuncaDisparamDiagnosticoAutomaticamente`).
- `DiagnosticarCausaRaiz_PayloadSerializadoComProtocoloIpcJson_RoundTripCorreto` fecha a mesma classe de lacuna de regressão de casing já vista nas Stories 1.3/1.4 — serializa `InfoDriver`/`InfoBios` reais com `ProtocoloIpc.Json`, não fixtures manuais.

### Next Steps

- Épico 1 (Núcleo de Atualização) está completo — 5/5 histórias em ciclo Dev → Revisão → QA. Próximo: Épico 2 (Diagnóstico de Manutenção).
- Itens de `deferred-work.md` a acompanhar (Story 1.5): sem sinalização de UI quando o corte de 200 eventos por categoria é atingido; `Eventos`/`StatusTextDiagnostico` não são limpos num novo SCAN (mesmo padrão já aceito pra Software/BIOS); `CancellationToken` não propaga de fato até `EventLogReader`; extração de driver em BSOD provavelmente `null` na maioria dos casos reais (mensagem padrão do WER 1001 não cita módulo `.sys`).
