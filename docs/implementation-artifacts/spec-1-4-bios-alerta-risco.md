---
title: 'Usuário é orientado a atualizar a BIOS com alerta de risco obrigatório'
type: 'feature'
created: '2026-08-21'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 'ee880ae'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** o app já coleta fabricante/modelo/versão de BIOS no inventário (`Inventario.Placa`) e já tem um pipeline completo de decisão/guia (`AnalisadorBios`/`GeradorGuiaBios`), mas hoje só o `HardwareOptimizer.Cli` usa esse pipeline — nada avisa o usuário do app real que a BIOS está desatualizada, nem o orienta a atualizar.

**Approach:** nova classe `VerificadorBios` (em `Features.Atualizacao`, mesmo padrão de `VerificadorSoftware`) consulta `IProvedorFonteOficial` via uma implementação nova `ProvedorFonteOficialBios` — que delega ao catálogo estático já existente (`BancoCuradoBios`), fechando a fronteira única (AD-4) também pra BIOS. A comparação de versão reaproveita `VersaoBios.Comparar` (já existe); o guia passo a passo reaproveita `GeradorGuiaBios.Gerar` (já existe) — nenhuma lógica de comparação/geração de guia é recriada. A UI mostra o alerta na mesma tela de Núcleo de Atualização (`DriversView`, mesma tela de driver/software), com um `ConfirmationPanel` (severidade `bios`) que aparece toda vez que o usuário opta por ver a orientação — nunca "não mostrar de novo" — e só então revela o guia (nenhuma chamada de sistema, é conteúdo já carregado).

## Boundaries & Constraints

**Always:**
- Toda consulta de versão oficial de BIOS passa por `IProvedorFonteOficial` — nunca acesso direto a `BancoCuradoBios`/`IProvedorInfoBios` fora dessa fronteira.
- O `ConfirmationPanel` (severidade `bios`) aparece **toda vez** que o usuário clica pra ver a orientação — mesmo que já tenha visto antes nesta sessão — informando que a interrupção durante a gravação pode comprometer a placa-mãe e recomendando um profissional qualificado caso o usuário não tenha experiência.
- Quando não há dado de versão oficial pra placa (sem cobertura no catálogo) ou a versão instalada já é a mais recente, nenhum alerta aparece (guard anti-alucinação, mesma regra de FR1/NFR2).

**Ask First:**
- Nenhuma decisão desta história precisa de aprovação humana durante a implementação — escopo é leitura/comparação/exibição de guia textual, sem ação mutante no sistema do usuário.

**Never:**
- O app nunca executa a gravação/flash da BIOS — o botão de confirmação só revela o guia textual já carregado (via `GeradorGuiaBios`), nenhuma chamada de sistema é feita.
- Nunca criar um segundo coletor de dados de placa-mãe — reaproveitar `Inventario.Placa`, já coletado no scan inicial (mesmo padrão de `PopularProgramas` da Story 1.3).
- Nunca tocar em `AnalisadorBios`, `ModuloBios`, `ProvedorBiosComCache`, `RelatorioBios` nem no `HardwareOptimizer.Cli` — esse pipeline continua servindo o CLI como está; esta história cria um caminho novo e mais simples (`VerificadorBios`) através da fronteira `IProvedorFonteOficial`, não estende o pipeline existente.
- Nunca confundir com `BiosGuideViewModel`/`BiosGuideView` (feature de ativação de XMP/EXPO + chat IA, já plugada na Shell) — são telas e fluxos diferentes; esta história não modifica essa página.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| BIOS desatualizada, no catálogo | `VersaoAtual` mais antiga que `VersaoDisponivel` | Alerta aparece com fabricante/modelo/versão atual vs. oficial e link | N/A |
| BIOS com mesma versão (ou mais nova) | `VersaoBios.Comparar` <= 0 | Nenhum alerta aparece | N/A |
| Placa sem cobertura no catálogo | `ProvedorFonteOficialBios` retorna `null` | Nenhum alerta aparece — nunca "sem informação" genérico | N/A |
| Consulta lança exceção | Falha inesperada no provedor | Tratado como sem cobertura (nenhum alerta), log de warning, nunca propaga | Log + retorna `null` |
| Usuário reabre a orientação de uma BIOS já vista | Segundo clique em "ver orientação" na mesma sessão | `ConfirmationPanel` aparece de novo, com a confirmação resetada (`Confirmado=false`) | N/A |

</frozen-after-approval>

## Code Map

- `src/HardwareOptimizer.Core/Bios/ModelosBios.cs` — MODIFICADO. Adicionar overload `IdentificacaoBios.DeInventario(PlacaMae placa)`; o overload existente `DeInventario(Inventario inventario)` passa a delegar pra ele (`=> DeInventario(inventario.Placa)`). Necessário porque o novo handler IPC recebe só `PlacaMae` do cliente (já coletado), não o `Inventario` inteiro.
- `src/HardwareOptimizer.Core/Contracts/InfoBios.cs` — NOVO. `sealed record InfoBios { Fabricante, Modelo, VersaoAtual, VersaoDisponivel, UrlDownload?, TeclaSetup, Utilitario, Passos (IReadOnlyList<string>), Avisos (IReadOnlyList<string>) }` — mesmo estilo de `InfoSoftware.cs`.
- `src/HardwareOptimizer.Features.Atualizacao/ProvedorFonteOficialBios.cs` — NOVO. Implementa `IProvedorFonteOficial`, delega pra `IProvedorInfoBios` (`Core/Bios/IProvedorInfoBios.cs`, injetado — default `BancoCuradoBios`), mesmo padrão exato de `ProvedorFonteOficialSoftware.cs`.
- `src/HardwareOptimizer.Features.Atualizacao/VerificadorBios.cs` — NOVO. `VerificarAsync(PlacaMae placa, CancellationToken) -> Task<InfoBios?>`: monta `IdentificacaoBios.DeInventario(placa)`, consulta `IProvedorFonteOficial.ConsultarAsync(identificacao.ChaveBusca)`, compara com `VersaoBios.Comparar` (`Core/Bios/VersaoBios.cs`, já existe — reaproveitar, não recriar), se desatualizada monta `InfoBios` com o guia de `GeradorGuiaBios.Gerar(identificacao)` (`Core/Bios/GeradorGuiaBios.cs`, já existe). Mesmo padrão de guard anti-alucinação e tratamento de exceção de `VerificadorSoftware.cs` (Story 1.3, incluindo `catch (OperationCanceledException) { throw; }` antes do catch genérico).
- `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` — MODIFICADO. Novo método `"verificarbios"` (sem gate `OperatingSystem.IsWindows()` — só compara dado já coletado contra o catálogo, mesmo raciocínio de `"verificarsoftware"`), recebe `placa` (`PlacaMae` serializada) como parâmetro, monta `VerificadorBios` com `ProvedorFonteOficialBios(new BancoCuradoBios())`, devolve `InfoBios?`.
- `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` — MODIFICADO. `PopularBios(PlacaMae placa)` (guarda `_placaMae`, chamado pelo `ShellViewModel`, nenhum coletor novo). `VerificarBiosCommand` (chama `"verificarbios"`, popula `InfoBiosAtual`). Estado de confirmação espelhado e independente do de driver (`PainelConfirmacaoBiosAberto`, `ConfirmadoBios`, `MensagemConfirmacaoBios`, `GuiaBiosVisivel`) — não generaliza o estado existente do driver, evita risco em fluxo já testado. `AbrirConfirmacaoBiosCommand` (abre o painel, sempre reseta `ConfirmadoBios=false` e `GuiaBiosVisivel=false`). `VerGuiaBiosCommand` (ligado ao `ConfirmarCommand` do painel — só revela `GuiaBiosVisivel=true`, nenhuma chamada IPC).
- `src/HardwareOptimizer.App/ViewModels/ShellViewModel.cs` — MODIFICADO. Uma linha nova no callback `Home.Popular`: `Drivers.PopularBios(inv.Placa);`.
- `src/HardwareOptimizer.App/Views/DriversView.axaml` — MODIFICADO. Nova seção "BIOS desatualizada" (3ª seção, mesmo padrão visual de cabeçalho da seção de Software), card com fabricante/modelo/versão atual vs. oficial + botão "ver orientação", `ConfirmationPanel Severidade="Bios"` inline (mensagem de risco: interrupção pode comprometer a placa-mãe, recomenda profissional qualificado), e a lista de passos do guia (`GuiaBiosVisivel`) revelada só após confirmação.
- `tests/HardwareOptimizer.Features.Atualizacao.Tests/VerificadorBiosTests.cs`, `ProvedorFonteOficialBiosTests.cs` — NOVOS, mesmo padrão de fakes manuais dos testes de software.
- `tests/HardwareOptimizer.Core.Tests/ModelosBiosTests.cs` — NOVO (ou extensão de teste existente), cobre o novo overload `DeInventario(PlacaMae)`.

## Tasks & Acceptance

**Execution:**
- [x] `src/HardwareOptimizer.Core/Bios/ModelosBios.cs` -- adicionar overload `DeInventario(PlacaMae placa)`, `DeInventario(Inventario)` delega pra ele -- desacopla a identificação de BIOS de precisar do `Inventario` inteiro
- [x] `src/HardwareOptimizer.Core/Contracts/InfoBios.cs` -- criar o record -- modelo de exibição de BIOS desatualizada + guia
- [x] `src/HardwareOptimizer.Features.Atualizacao/ProvedorFonteOficialBios.cs` -- implementação de `IProvedorFonteOficial` pra BIOS -- fecha a fronteira única (AD-4) também pra BIOS
- [x] `src/HardwareOptimizer.Features.Atualizacao/VerificadorBios.cs` -- verifica uma placa-mãe -- ponto único de comparação de versão + montagem do guia
- [x] `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` -- método `"verificarbios"` -- conecta o fluxo real
- [x] `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` + `DriversView.axaml` + `ShellViewModel.cs` -- seção "BIOS desatualizada" com `ConfirmationPanel` severidade `bios` -- fecha o fluxo ponta a ponta
- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests/` -- `VerificadorBiosTests.cs`, `ProvedorFonteOficialBiosTests.cs` -- cobre a I/O Matrix acima; testes adicionais em `tests/HardwareOptimizer.App.Tests/` (`DriversViewModelBiosTests.cs`), `tests/HardwareOptimizer.Core.Tests/` (`ModelosBiosTests.cs`) e `tests/HardwareOptimizer.Ipc.Tests/IpcTests.cs` (fluxo real via `RoteadorIpc`)

**Nota de correção pós-implementação:** a revisão independente (Blind Hunter + Edge Case Hunter, `bmad-build` step-04) encontrou três itens reais no commit do subagente de implementação, todos corrigidos antes do fechamento da história: (1) `DriversViewModel.TemAlertaBios` tinha o mesmo nome de `ShellViewModel.TemAlertaBios` (propriedade já existente, sobre alerta de XMP/EXPO — semântica completamente diferente) — achado por dois revisores independentes; renomeado para `TemBiosDesatualizada` para eliminar a armadilha de manutenção. (2) `VerificadorBios.VerificarAsync` chamava `IdentificacaoBios.DeInventario(placa)` fora do bloco `try`, então uma exceção dessa chamada não seria tratada pelo guard anti-alucinação da I/O Matrix — movido pra dentro do `try` (e a mensagem de log do catch genérico trocada de `identificacao.ChaveBusca`, que ficaria indefinida nesse cenário, para `placa.Fabricante`/`placa.Modelo`, sempre disponíveis). (3) os testes de integração real só exercitavam a entrada ASUS (versão numérica pura) do `BancoCuradoBios` — as entradas MSI (`"7C91vH9"`, alfanumérica mista) e Gigabyte (`"F16"`, prefixada) nunca passavam pelo caminho novo; adicionados 3 testes em `VerificadorBiosTests.cs` cobrindo os dois fabricantes com o catálogo real, não um fake.

O subagente de implementação também produziu, além do escopo desta spec, uma verificação integrada de todo o Épico 1 (build+testes da solução inteira a partir de clone limpo + smoke test funcional do app real com screenshot) e um documento `bugs-corrigidos.md` — ambos revertidos/removidos do commit desta história por estarem fora do Code Map (a verificação por história já é responsabilidade da fase de QA, não do Dev; o changelog de bugs duplica informação já rastreável via `git log`/`deferred-work.md`). O trabalho em si era genuíno (clone e screenshot confirmados em disco), só fora de escopo.

**Acceptance Criteria:**
- Given que o sistema identificou o fabricante e modelo da placa-mãe, when consulta a versão mais recente de BIOS via `IProvedorFonteOficial`, then compara com a versão instalada e sinaliza se está desatualizada
- Given uma BIOS desatualizada sinalizada, when o usuário opta por ver a orientação de atualização, then o `ConfirmationPanel` (severidade `bios`) aparece — sempre, mesmo que já tenha visto antes — informando o risco à placa-mãe e recomendando um profissional qualificado, e o app nunca executa a gravação da BIOS

## Design Notes

Deliberadamente **não** se reaproveita `AnalisadorBios`/`ModuloBios`/`DecisaoBios`/`RelatorioBios` (o pipeline mais rico já usado pelo CLI, com `Ganho`/`Risco`/`Justificativa`). Esses tipos comparam contra `InfoBiosFabricante` (rico, com changelog/motivo), incompatível com a fronteira fina `IProvedorFonteOficial` (`InfoFonteOficial` só tem `VersaoDisponivel`/`UrlDownload`/`CertificadoWhql`) — a mesma decisão de fronteira fina já tomada em `ProvedorFonteOficialDriver`/`ProvedorFonteOficialSoftware`. Forçar o pipeline rico através da fronteira estreita inflaria esta história e arriscaria quebrar o CLI. `VerificadorBios` é um caminho novo e mais simples, só pra o alerta + guia desta história; o CLI continua usando o pipeline antigo, intocado.

`GeradorGuiaBios.Gerar` não depende de versão nem de `IProvedorFonteOficial` — só do fabricante (pra saber tecla de setup/utilitário) — por isso pode ser chamado diretamente por `VerificadorBios` sem violar a fronteira única (a fronteira é sobre "qual é a versão oficial", não sobre "como orientar o procedimento").

## Verification

**Commands:**
- `dotnet build HardwareOptimizer.sln` -- 0 erros (via placeholder local pro bug conhecido de `Features.LifeCounter`, nunca commitado)
- `dotnet test tests/HardwareOptimizer.Features.Atualizacao.Tests` -- todos verdes
- `dotnet test tests/HardwareOptimizer.Core.Tests` -- todos verdes, sem regressão em `AnalisadorBiosTests`/`GeradorGuiaBiosTests`/`VersaoBiosTests`
- `dotnet test tests/HardwareOptimizer.App.Tests` -- todos verdes, sem regressão
