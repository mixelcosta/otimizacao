---
title: 'Usuário é alertado sobre software desatualizado via fonte oficial'
type: 'feature'
created: '2026-08-21'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '1e3e733a3c9cc3cb53cb70423ea548cf2557504b'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `ProgramaInstalado`/`ColetorInventario` já coletam a lista de software instalado (nome, versão, fabricante), mas nada hoje compara essa versão contra uma fonte oficial nem alerta o usuário — o app não sabe dizer "esse programa está desatualizado".

**Approach:** Nova classe `VerificadorSoftware` (em `Features.Atualizacao`) consulta `IProvedorFonteOficial` (fronteira já existente, Story 1.2) para cada programa instalado, via uma implementação nova `ProvedorFonteOficialSoftware` — que, seguindo o mesmo precedente de `RepositorioWhqlEstatico` (catálogo estático curado para driver), usa um catálogo estático próprio (`software_catalog.json`, ~8 programas comuns) em vez de sempre retornar "sem informação" (que tornaria o caminho de sucesso inalcançável, repetindo o problema já registrado em `deferred-work.md` para o catálogo de driver). A UI mostra a lista na mesma tela de Núcleo de Atualização (`DriversView`), com botão "abrir" que só leva o usuário à URL oficial — reaproveita o padrão já existente de `AbrirDownloadCommand`, sem nenhum download/instalação pelo app.

## Boundaries & Constraints

**Always:**
- Nenhum download nem instalação de software é feito pelo app — o botão só abre a URL oficial no navegador padrão (`ProcessStartInfo { UseShellExecute = true }`, mesmo padrão de `AbrirDownloadCommand` em `DriversViewModel.cs`).
- Toda consulta de versão passa por `IProvedorFonteOficial` (fronteira única, AD-4) — nunca acesso direto a um catálogo de software fora dela.
- Quando não há dado de versão oficial pro programa, o item some da lista de "desatualizados" (nunca aparece com dado inventado) — mesma regra de FR1/NFR2 (guard anti-alucinação).

**Ask First:**
- Nenhuma decisão desta história precisa de aprovação humana durante a implementação — escopo é só leitura/exibição, sem ação mutante no sistema do usuário.

**Never:**
- Nunca estender `OrquestradorAtualizacao` para software — essa classe é acoplada a driver (backup/instalação/rollback via pnputil), que não se aplica aqui; usar uma classe nova e mais simples (`VerificadorSoftware`).
- Nunca criar um segundo coletor de software instalado — reaproveitar `Inventario.ProgramasInstalados`, já coletado no scan inicial.
- Nunca oferecer "aplicar atualização" nem confirmação de risco pra software — só alerta + link, sem `ConfirmationPanel` (essa história não muda nada no sistema do usuário).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Software desatualizado, no catálogo | Programa com versão diferente da oficial | Item aparece com versão atual vs. oficial + link | N/A |
| Software com mesma versão | Versão instalada == oficial | Não aparece na lista de desatualizados | N/A |
| Software sem cobertura no catálogo | `ProvedorFonteOficialSoftware` retorna `null` | Item não aparece — nunca "Desconhecido" genérico poluindo a lista | N/A |
| Lista de programas vazia | `ProgramasInstalados` vazia | Lista de software desatualizado vazia | N/A |
| Consulta lança exceção | Falha inesperada no provedor | Programa tratado como sem cobertura (não aparece), log de warning, nunca propaga | Log + segue para o próximo item |

</frozen-after-approval>

## Code Map

- `src/HardwareOptimizer.Core/Contracts/InfoSoftware.cs` — NOVO. `sealed record InfoSoftware { Nome, VersaoAtual, VersaoDisponivel, UrlDownload, Status (enum StatusSoftware: Atualizado/AtualizacaoDisponivel) }` — mesmo estilo de `InfoDriver.cs`/`SaudeDisco.cs`.
- `src/HardwareOptimizer.Features.Atualizacao/IRepositorioVersoesSoftware.cs` + `RepositorioVersoesSoftwareEstatico.cs` — NOVO, mesmo padrão de `IRepositorioDriversWhql`/`RepositorioWhqlEstatico` (`src/HardwareOptimizer.Features.Drivers/`). Catálogo embarcado `Data/software_catalog.json`, ~8 programas comuns (ex. navegadores, 7-Zip, VLC), busca por nome (case-insensitive, substring — nomes de programa variam mais que HWID).
- `src/HardwareOptimizer.Features.Atualizacao/ProvedorFonteOficialSoftware.cs` — NOVO. Implementa `IProvedorFonteOficial` (`Core/Contracts/`), delega pra `IRepositorioVersoesSoftware`, mesmo padrão exato de `ProvedorFonteOficialDriver.cs`.
- `src/HardwareOptimizer.Features.Atualizacao/VerificadorSoftware.cs` — NOVO. `VerificarAsync(IReadOnlyList<ProgramaInstalado>, CancellationToken) -> Task<IReadOnlyList<InfoSoftware>>` — para cada programa, consulta `IProvedorFonteOficial`, mesma lógica de comparação de versão de `AtualizadorDrivers.VarrerAsync` (`src/HardwareOptimizer.Features.Drivers/AtualizadorDrivers.cs:24-60`), mas sem cobertura = item omitido (não "Desconhecido").
- `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` — MODIFICADO. Novo método `"verificarsoftware"`, recebe `programas` (array serializado de `ProgramaInstalado`) como parâmetro, monta `VerificadorSoftware` com `ProvedorFonteOficialSoftware`/`RepositorioVersoesSoftwareEstatico`, devolve `IReadOnlyList<InfoSoftware>`.
- `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` — MODIFICADO. Nova `ObservableCollection<InfoSoftwareViewModel> Software`, comando `VerificarSoftwareAsync` (chama `"verificarsoftware"` com `inv.ProgramasInstalados` — a ViewModel já recebe o `Inventario` via `Popular`, estender a assinatura ou adicionar overload), `AbrirDownloadSoftwareCommand` (mesmo padrão de `AbrirDownload`, linha 209-215).
- `src/HardwareOptimizer.App/Views/DriversView.axaml` — MODIFICADO. Nova seção "Software desatualizado" dentro do `StackPanel` já existente no `ScrollViewer` (linha ~56), abaixo do bloco de drivers — mesmo padrão visual (`ItemsControl` + `DataTemplate` + card com gradiente), sem `ConfirmationPanel` (não se aplica).
- `tests/HardwareOptimizer.Features.Atualizacao.Tests/VerificadorSoftwareTests.cs`, `ProvedorFonteOficialSoftwareTests.cs` — NOVOS, mesmo padrão de fakes manuais dos testes de driver.

## Tasks & Acceptance

**Execution:**
- [x] `src/HardwareOptimizer.Core/Contracts/InfoSoftware.cs` -- criado o record + `enum StatusSoftware` (só `Atualizado`/`AtualizacaoDisponivel` -- sem "Desconhecido", ao contrário de `StatusDriver`) -- modelo de exibição de software desatualizado
- [x] `src/HardwareOptimizer.Features.Atualizacao/IRepositorioVersoesSoftware.cs` + `RepositorioVersoesSoftwareEstatico.cs` + `Data/software_catalog.json` (8 programas: Chrome, Firefox, 7-Zip, VLC, Notepad++, WinRAR, Adobe Acrobat Reader DC, Zoom) -- catálogo estático curado -- torna o caminho de sucesso testável (evita repetir o gap do catálogo de driver)
- [x] `src/HardwareOptimizer.Features.Atualizacao/ProvedorFonteOficialSoftware.cs` -- implementação de `IProvedorFonteOficial` para software -- fecha a fronteira única (AD-4) também pro software
- [x] `src/HardwareOptimizer.Features.Atualizacao/VerificadorSoftware.cs` -- verifica lista de programas -- ponto único de comparação de versão de software
- [x] `src/HardwareOptimizer.Ipc/RoteadorIpc.cs` -- método `"verificarsoftware"` adicionado (não gated por `OperatingSystem.IsWindows()` -- só compara dados já coletados contra o catálogo, sem dependência de Windows) -- conecta o fluxo real
- [x] `src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs` + `DriversView.axaml` -- seção "Software desatualizado" na mesma tela (`Software`, `VerificarSoftwareCommand`, `AbrirDownloadSoftwareCommand`, `PopularProgramas` chamado por `ShellViewModel` com `inv.ProgramasInstalados`) -- fecha o fluxo ponta a ponta
- [x] `tests/HardwareOptimizer.Features.Atualizacao.Tests/` -- `VerificadorSoftwareTests.cs`, `ProvedorFonteOficialSoftwareTests.cs`, `RepositorioVersoesSoftwareEstaticoTests.cs` (extra, mesmo padrão de `RepositorioWhqlEstaticoTests`) -- cobre a I/O Matrix acima; testes adicionais em `tests/HardwareOptimizer.App.Tests/DriversViewModelSoftwareTests.cs` e `tests/HardwareOptimizer.Ipc.Tests/IpcTests.cs` (fluxo real via `RoteadorIpc`)

**Nota de correção pós-implementação:** a revisão independente (Blind Hunter + Verification Gap Reviewer, `bmad-build` step-04) encontrou dois defeitos reais na primeira versão do subagente de implementação, corrigidos antes do fechamento da história: (1) `VerificadorSoftware.VerificarAsync` comparava `oficial.VersaoDisponivel != programa.Versao` sem checar se `programa.Versao` era nulo/vazio — um programa cuja versão instalada não foi lida corretamente aparecia como "desatualizado" com um dado inventado (`VersaoAtual = null`), violando o guard anti-alucinação; corrigido pulando o item quando `programa.Versao` é nulo/vazio, antes mesmo de consultar o provedor. (2) `DriversViewModel.VerificarSoftwareAsync` só limpava `Software`/`TemResultadosSoftware` no caminho de sucesso — uma falha após uma verificação bem-sucedida anterior deixava a lista desatualizada visível na tela junto com a mensagem de erro, sem indicar que os dados eram stale; corrigido limpando a lista também no caminho de falha. Também foi fortalecido o teste de regressão do bug de casing self-caught pelo subagente (`IpcTests.VerificarSoftware_PayloadSerializadoComProtocoloIpcJson_RoundTripCorreto`): o teste original só usava fixtures escritas manualmente em camelCase, o que não teria pego uma regressão real de casing em nenhuma das duas pontas (cliente ou servidor).

**Acceptance Criteria:**
- Given que o usuário rodou a varredura do Núcleo de Atualização, when o sistema encontra software desatualizado via `IProvedorFonteOficial`, then o item aparece na lista com versão atual vs. oficial e um link direto pra fonte oficial
- Given qualquer item da lista de software desatualizado, when o usuário clica em abrir, then só a URL oficial abre no navegador — nenhum download/instalação pelo app

## Design Notes

`ProvedorFonteOficialSoftware` usa catálogo estático curado (não HTTP real), mesma decisão já tomada e documentada em `ProvedorFonteOficialDriver` — mantém a promessa da fronteira `IProvedorFonteOficial` (nunca inventar dado) sem inflar esta história com integração real a lojas/sites de cada fabricante de software, que não tem solução unificada (PRD §10 item 3, já não-bloqueante).

## Verification

**Commands:**
- `dotnet build HardwareOptimizer.sln` -- 0 erros (via placeholder local pro bug conhecido de `Features.LifeCounter`, nunca commitado)
- `dotnet test tests/HardwareOptimizer.Features.Atualizacao.Tests` -- todos verdes
- `dotnet test tests/HardwareOptimizer.App.Tests` -- todos verdes, sem regressão
