# Deferred Work

Itens encontrados durante o desenvolvimento que não pertencem à história atual — pré-existentes, não causados pela mudança em curso.

- source_spec: `docs/planning-artifacts/epics.md` (Épico 1, Story 1.1)
  summary: `HardwareOptimizer.Features.LifeCounter.csproj` referencia `EmbeddedResource Include="Data\tbw_database.json"`, mas esse arquivo nunca existiu no repositório (não só nesta cópia — não há histórico dele no git). O build de `HardwareOptimizer.sln` falha com `CS1566` no mesmo padrão que a Story 1.1 corrigiu para Features.Upgrade/Features.Drivers.
  evidence: achado do Blind Hunter (revisão de bmad-build) rodando `dotnet build HardwareOptimizer.sln` completo — fora do escopo da Story 1.1, que só cobria Features.Upgrade e Features.Drivers. Precisa de história própria: localizar/recriar `tbw_database.json` (usado por `Features.LifeCounter`, provavelmente para cálculo de vida útil de SSD — TBW).

- source_spec: `docs/planning-artifacts/epics.md` (Épico 1, Story 1.1)
  summary: os dois catálogos JSON recém-versionados têm shapes inconsistentes entre si — `whql_catalog.json` é um array no nível raiz; `hardware_catalog.json` é um objeto com três arrays nomeados (`processadores`, `gpus`, `placasMae`). Não há schema/convenção compartilhada entre catálogos do produto.
  evidence: achado do Blind Hunter. Corrigir exigiria alterar o parsing em `RepositorioWhqlEstatico` e/ou `ValidadorCompatibilidade`, fora do escopo de um fix de build. Vale revisitar quando o catálogo estático crescer (ver ARCHITECTURE-SPINE.md, Deferred — "Curadoria/crescimento do catálogo estático de compatibilidade").

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: `whql_catalog.json` (versionado na Story 1.1) tem `urlDownload` apontando pra páginas de landing do fabricante (ex. `nvidia.com/Download/index.aspx`), nunca um arquivo `.inf`/`.cab` direto — com a validação de extensão desta história, toda tentativa real de "confirmar e aplicar" falha antes de instalar, tornando o caminho de sucesso (`instalação com sucesso`) inalcançável com o catálogo atual.
  evidence: achado do Blind Hunter (revisão da Story 1.2), confirmado inspecionando o JSON. Não é regressão desta história — o catálogo já existia assim; a nova validação de extensão só expôs o problema. Precisa de dado de catálogo real (URLs diretas) ou de um mecanismo que resolva a URL de download real a partir da landing page.

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: backup e rollback não são escopados por driver — `pnputil /export-driver *` exporta TODOS os drivers de terceiros, e o rollback reinstala tudo daquele snapshot. A UI comunica "reverter este driver", mas o efeito real é maior (reverte todo o driver store ao estado do snapshot).
  evidence: achado do Blind Hunter. Escopar por driver exigiria correlacionar published name/INF por dispositivo, não suportado diretamente pelo `pnputil /export-driver`. Corrigir a comunicação na UI é barato; corrigir o mecanismo é trabalho maior, avaliar separadamente.

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: `CaminhoBackupAtual` é de sessão única — abrir o painel de confirmação para um driver diferente descarta a referência ao backup do driver anterior (o arquivo continua no disco, só a UI perde a referência).
  evidence: achado do Blind Hunter. Corrigir exigiria histórico de backups por driver na ViewModel/tela, não só o último caminho.

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: pastas de backup (`%LocalAppData%\OtimizeBuilder\DriverBackups\`) e de download temporário (`%TEMP%\OtimizeBuilder\Drivers\`) nunca são limpas — crescem indefinidamente.
  evidence: achado do Blind Hunter. Precisa de política de retenção/limpeza, fora do escopo desta história.

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: lista de drivers não é re-escaneada após uma atualização (sucesso ou falha) — o item continua marcado como desatualizado até o usuário re-escanear manualmente.
  evidence: achado do Blind Hunter. Polish de UX, não bloqueia o critério de aceite.

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: `MensagemConfirmacao` do painel não avisa que a tentativa pode falhar por formato de arquivo não suportado nem que a instalação/rollback pode disparar um prompt de elevação (UAC) — o usuário só descobre ao tentar.
  evidence: achado do Blind Hunter. Ajuste de copy, baixo custo, mas não feito nesta rodada.

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: nenhuma operação longa desta história (download de até 5 min, chamadas de `pnputil` elevadas) tem afordance de cancelamento na UI.
  evidence: achado do Edge Case Hunter. Cancelamento de UI é trabalho maior, fora do escopo desta história.

- source_spec: `docs/implementation-artifacts/spec-1-2-driver-scan-aprovacao-rollback.md`
  summary: `RoteadorIpc.CriarOrquestradorAtualizacao()` é reconstruído do zero em toda chamada IPC (`varrerdrivers`, `aprovaratualizacaodriver`, `reverteratualizacaodriver`), incluindo `ColetorHwid` e `RepositorioWhqlEstatico` (que re-desserializa o `whql_catalog.json` embarcado) mesmo em `reverteratualizacaodriver`, que não usa nenhum dos dois.
  evidence: achado da revisão independente (`bmad-code-review`) pós-commit da Story 1.2. Custo hoje é baixo (catálogo pequeno), mas cresce se o catálogo crescer (ver item já deferido sobre curadoria do catálogo). Corrigir exigiria tornar `IProvedorFonteOficial` opcional/lazy no construtor de `AtualizadorDrivers` pra `reverteratualizacaodriver` não pagar esse custo — decidido não fazer sob pressão de tempo, pra não arriscar um refactor apressado no meio da revisão.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: `RoteadorIpc.VerificarSoftwareAsync` reconstrói `RepositorioVersoesSoftwareEstatico` (re-desserializa `software_catalog.json`) a cada chamada IPC, mesmo padrão já deferido para `CriarOrquestradorAtualizacao()` na Story 1.2. Também: a busca por nome em `RepositorioVersoesSoftwareEstatico` é substring case-insensitive contra ~8 entradas — funciona para o catálogo curado atual, mas não escala (linear scan) nem evita falso-positivo caso um nome de catálogo curto (ex. "Zoom") apareça como substring de um programa não relacionado.
  evidence: mesma decisão consciente da Story 1.2 — custo desprezível com catálogo pequeno; reavaliar junto do item já deferido sobre curadoria/crescimento do catálogo estático.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: a lista "Software desatualizado" em `DriversView.axaml` não é afetada pelo campo de filtro (`FiltroTexto`) já existente na tela — o filtro só se aplica à lista de drivers (`AplicarFiltro`/`Drivers`), não a `Software`.
  evidence: decisão de escopo desta história (spec só pedia a nova seção, sem estender o filtro); comportamento visível se a lista de software crescer. Baixo custo para adicionar depois, caso vire incômodo real.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: `VerificadorSoftware.VerificarAsync` compara versão por igualdade estrita de string (`oficial.VersaoDisponivel != programa.Versao`), sem normalizar caixa, espaços ou prefixo "v" — mesmo padrão já existente em `AtualizadorDrivers.VarrerAsync`. Para software especificamente o risco é maior que para driver: strings de `DisplayVersion` do registro variam mais (sufixos de arquitetura, formatação) do que HWID, então um programa já atualizado pode aparecer como "desatualizado" por diferença cosmética.
  evidence: achado do Blind Hunter (revisão independente da Story 1.3). Não é regressão nova — mirroreia o comportamento já aceito para driver. Corrigir exigiria normalização de versão (parsing semver-like), fora do escopo desta história; revisitar se o catálogo crescer o suficiente para o problema aparecer na prática.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: `AbrirDownloadSoftware` (e o `AbrirDownload` de driver, pré-existente) não têm teste do caminho feliz — só o guard de URL nula/ausente é testado. Nenhum teste prova que clicar em um item real de fato invoca `Process.Start` com a URL oficial e nada mais.
  evidence: achado do Verification Gap Reviewer (revisão independente da Story 1.3). `Process.Start` não tem seam de teste como escrito hoje (mesma limitação já presente no código de driver, não é regressão desta história). Corrigir exigiria uma abstração testável (ex. `ILancadorDeUrl`), avaliar se vale a pena isoladamente.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: `RoteadorIpc.VerificarSoftwareAsync` desserializa `programas` inteiro em uma única chamada (`JsonSerializer.Deserialize<List<ProgramaInstalado>>`); como `Nome` é `required`, uma única entrada malformada (nome nulo/ausente/tipo errado) derruba a requisição inteira com uma falha genérica, mesmo que as outras centenas de programas estivessem bem formados.
  evidence: achado do Edge Case Hunter (revisão independente da Story 1.3). Risco baixo na prática — os dados vêm do coletor interno confiável (`LeitorWindows`), não de rede externa — mas o code path em si não é resiliente a entradas parciais. Corrigir exigiria desserialização item-a-item com try/catch por entrada, como já ocorre em `VerificadorSoftware` para falhas do provedor.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: `DriversViewModel.VerificarSoftwareAsync` serializa o `ProgramaInstalado` inteiro (`UninstallString`, `QuietUninstallString`, `Fabricante`, `DataInstalacao`, `TamanhoMb`, `Bloatware`) no payload IPC, embora `VerificadorSoftware`/`ProvedorFonteOficialSoftware` só leiam `Nome`/`Versao` — payload desnecessariamente maior (inclui comandos de desinstalação/GUIDs) para listas grandes de programas instalados.
  evidence: achado do Edge Case Hunter (revisão independente da Story 1.3). Impacto hoje é baixo (IPC local via named pipe, não rede externa). Correção seria projetar para `{ nome, versao }` antes de enviar — barato, mas não crítico; avaliar se o payload real incomodar.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: nenhuma validação de esquema restringe `UrlDownload` a `http`/`https` entre o catálogo (`RepositorioVersoesSoftwareEstatico`/`ProvedorFonteOficialSoftware`) e `AbrirDownloadSoftware`, que chama `Process.Start(UseShellExecute = true)` diretamente com o valor do catálogo.
  evidence: achado do Edge Case Hunter (revisão independente da Story 1.3). Risco baixo hoje — as ~8 URLs são hardcoded/curadas no `software_catalog.json` embarcado no assembly — mas se o catálogo passar a ser atualizado remotamente (já cogitado no item de curadoria/crescimento do catálogo estático), uma `urlDownload` maliciosa (`file:`, `javascript:`) chegaria sem guard ao `ShellExecute`. Corrigir com allow-list de esquema antes de expor o catálogo a qualquer fonte não totalmente confiável.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: a linha `Drivers.PopularProgramas(inv.ProgramasInstalados)` em `ShellViewModel.cs` (fluxo real de conexão do scan com a verificação de software) não tem nenhum teste automatizado — não existe `ShellViewModelTests.cs` no repositório. Se essa linha (ou qualquer outra do mesmo callback `Home.Popular`) fosse removida por engano, nenhum teste falharia: a UI continuaria funcionando normalmente, só que sempre reportando "nenhum software desatualizado encontrado" com uma lista vazia de programas.
  evidence: achado independente pelo Blind Hunter e pelo Verification Gap Reviewer (revisão independente `bmad-code-review` da Story 1.3, dois lenses diferentes chegaram à mesma conclusão). Não é lacuna nova desta história — `ShellViewModel` nunca teve nenhum teste (o construtor cria um `DispatcherTimer` que depende do `Dispatcher.UIThread` do Avalonia, e o projeto `HardwareOptimizer.App.Tests` não tem nenhuma infraestrutura de teste headless do Avalonia configurada); toda a wiring do callback `Home.Popular` (`InfoSistema.Popular`, `OtimizadorWindows.Popular`, `Drivers.Popular`, `BiosGuide.Popular`, etc.) já era igualmente destestada antes desta história. Corrigir exigiria configurar `Avalonia.Headless` no projeto de testes — investimento de infraestrutura, não um fix pontual desta história.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: em `DriversViewModel.VerificarSoftwareAsync`, se `resp.Sucesso` for `true` mas `resp.Resultado` não for do tipo `IReadOnlyList<InfoSoftware>` (situação hoje inalcançável — o servidor sempre retorna esse tipo em caso de sucesso), o código cai no branch `else` e mostra `"Falha ao verificar software: "` com a mensagem vazia (já que `resp.Erro` é nulo quando `Sucesso` é `true`).
  evidence: achado do Edge Case Hunter (revisão independente `bmad-code-review` da Story 1.3). Puramente defensivo — não há caminho de produção que produza esse estado hoje. Corrigir exigiria um terceiro branch para "sucesso com formato de resposta inesperado", baixo valor no estado atual do código.

- source_spec: `docs/implementation-artifacts/spec-1-3-software-desatualizado.md`
  summary: `RepositorioVersoesSoftwareEstatico.CarregarCatalogo()` engole qualquer exceção da leitura/desserialização do recurso embarcado com `catch { return []; }`, sem nenhum log — diferente de `VerificadorSoftware`, que loga um warning em falha equivalente. Um `software_catalog.json` corrompido ou renomeado faria a feature reportar silenciosamente "nenhum software desatualizado" para sempre, sem rastro de diagnóstico.
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.3). Risco baixo — o catálogo é um recurso embarcado no assembly, não dado externo — mas o silêncio total dificultaria diagnosticar um build quebrado. Corrigir exigiria injetar `ILogger` no construtor (a classe hoje não recebe nenhuma dependência), consistente com o padrão já usado em `VerificadorSoftware`.

- source_spec: `docs/implementation-artifacts/spec-1-4-bios-alerta-risco.md`
  summary: `DriversViewModel.PopularBios` dispara `_ = VerificarBiosAsync()` em fire-and-forget; o método tem `try/finally` mas sem `catch`, então uma exceção não tratada de `_agente.TratarAsync` seria engolida silenciosamente pela task descartada, sem log nem estado de erro visível na UI.
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.4). Risco baixo hoje — `VerificadorBios` já captura `Exception` genericamente do lado servidor — e o mesmo padrão (`try/finally` sem `catch` em fire-and-forget) já existe em outros pontos do app (`OtimizadorWindowsViewModel.CarregarServicosAsync`/`CarregarEfeitosVisuaisAsync`), não é regressão introduzida por esta história.

- source_spec: `docs/implementation-artifacts/spec-1-4-bios-alerta-risco.md`
  summary: se `PopularBios` for chamado de novo enquanto a verificação de BIOS anterior ainda está em voo (reentrância), o novo `_ = VerificarBiosAsync()` retorna cedo por causa do guard `VerificandoBios`, sem enfileirar nem repetir — `InfoBiosAtual` fica desatualizado com base na placa antiga até o próximo SCAN completo. Nenhum `CancellationToken` real é passado nessa chamada IPC fire-and-forget, então nada cancela a consulta se a tela for descarregada ou o app fechar em pleno voo.
  evidence: achado do Edge Case Hunter (revisão independente `bmad-code-review` da Story 1.4). Probabilidade baixa na prática — a consulta é local/rápida (catálogo estático embarcado), bem mais rápida que a coleta completa de inventário que precede uma nova chamada de `PopularBios`. Corrigir exigiria fila/retry ou cancelamento do request anterior, desproporcional ao risco real hoje.

- source_spec: `docs/implementation-artifacts/spec-1-4-bios-alerta-risco.md`
  summary: `RoteadorIpc.VerificarBiosAsync` monta `VerificadorBios` com `NullLogger<VerificadorBios>.Instance`, então o `LogWarning` do guard anti-alucinação (falha do provedor) nunca é escrito em lugar nenhum em produção — mesmo padrão já usado (e já deferido) para `VerificarSoftwareAsync`/`RepositorioVersoesSoftwareEstatico` na Story 1.3.
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.4). Não é regressão nova desta história — `RoteadorIpc` constrói todo handler por-requisição com `NullLogger`, convenção uniforme em todo o arquivo. Corrigir exigiria injetar um `ILoggerFactory`/logger real no `RoteadorIpc`, mudança maior de composição, fora do escopo de uma história.

- source_spec: `docs/implementation-artifacts/spec-1-4-bios-alerta-risco.md`
  summary: o alerta de "BIOS desatualizada" só aparece na tela de Núcleo de Atualização — ao contrário do alerta de XMP/EXPO (`ShellViewModel.TemAlertaBios`, que já tem um badge visível na sidebar), um usuário que nunca abre essa tela não fica sabendo que a BIOS está desatualizada.
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.4). Decisão de escopo consciente da spec ("Superfície: tela do Núcleo de Atualização", mesma decisão das Stories 1.2/1.3) — estender pra um badge global na sidebar é uma melhoria de UX legítima, mas fora do escopo desta história; revisitar se o alerta de BIOS se mostrar ser regularmente perdido por usuários reais.

- source_spec: `docs/implementation-artifacts/spec-1-4-bios-alerta-risco.md`
  summary: `MensagemConfirmacaoBios` (texto de risco hardcoded em `AbrirConfirmacaoBios`) duplica, com palavras diferentes, o mesmo aviso que já está em `InfoBiosAtual.Avisos` (produzido por `GeradorGuiaBios`) — duas cópias mantidas independentemente do mesmo conteúdo de risco podem divergir com o tempo.
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.4). Baixo risco funcional (não é uma falha, é duplicação de conteúdo) — mas vale revisitar se o texto de `GeradorGuiaBios.AvisosPadrao` mudar sem que `MensagemConfirmacaoBios` seja atualizada junto.

- source_spec: `docs/implementation-artifacts/spec-1-4-bios-alerta-risco.md`
  summary: `ProvedorFonteOficialBios.ConsultarAsync` sempre mapeia `UrlDownload = info.Fonte`, nunca cai pra `info.LinkManual` quando `Fonte` estiver nula/vazia. As 3 entradas atuais do catálogo sempre populam `Fonte`, então o caminho de fallback nunca foi exercitado — uma futura entrada de catálogo só com `LinkManual` preenchido produziria um alerta de BIOS sem link de download.
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.4). Risco baixo hoje (dado curado, sob nosso controle) — mas barato de corrigir (`info.Fonte ?? info.LinkManual`) se o catálogo crescer com entradas incompletas.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: a linha da I/O Matrix "leitura do Event Log falha → lista vazia, log de warning, nunca propaga" não tem teste que force uma falha real dentro dos `try/catch` de `LerWhea`/`LerBsod`/`LerCrashAplicacao` (`LeitorEventLog.cs`) — só o caminho feliz (leitura real bem-sucedida) e o cancelamento (que ocorre antes do try, via `ThrowIfCancellationRequested`) são exercitados. A garantia de "nunca propaga" está coberta por inspeção de código (estrutura try/catch visível), não por um teste que force `EventLogException`/canal indisponível de verdade.
  evidence: achado do Verification Gap Reviewer (revisão independente `bmad-code-review` da Story 1.5). Forçar essa falha deterministicamente exigiria uma abstração testável em torno de `EventLogReader`/`EventLogQuery` (ex. uma interface `IExecutorConsultaEventLog` injetável) — mesma limitação estrutural já aceita pro `LeitorWindows` (chamadas de PowerShell/CIM também não têm seam de falha forçada, confirmado na investigação da Story 1.5). Corrigir exigiria introduzir essa abstração nova, desproporcional ao escopo desta história; revisitar se o leitor de Event Log crescer em complexidade.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: `LeitorEventLog.Consultar` não tem try/catch por iteração dentro do `while (reader.ReadEvent() ...)` — se `ReadEvent()` lançar depois de já ter lido vários eventos válidos (registro corrompido/permissão no meio do canal), o catch externo em `LerWhea`/`LerBsod`/`LerCrashAplicacao` descarta a query inteira (vira lista vazia), perdendo os eventos válidos já lidos.
  evidence: achado do Edge Case Hunter (revisão independente `bmad-code-review` da Story 1.5). Cenário raro na prática (Event Log corrompido no meio de uma leitura), e o comportamento atual ainda é seguro (nunca propaga, nunca inventa dado) — só perde sinal parcial. Corrigir exigiria mover o `try/catch` pra dentro do loop, por registro.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: a regex de extração de driver em BSOD (`([A-Za-z0-9_\-]+\.sys)`) usa `Regex.Match` (só a primeira ocorrência) — se a mensagem do evento citar múltiplos módulos `.sys`, pode não ser o driver realmente responsável pelo bugcheck. Além disso, a mensagem padrão do Event ID 1001 (WER-SystemErrorReporting) tipicamente não cita nome de módulo `.sys` nenhum (só código de bugcheck e caminho do dump), então essa extração provavelmente fica `null` na maioria dos BSODs reais. O Code Map da spec originalmente previa extrair do `EventData` estruturado, não de texto livre via regex — divergência documentada aqui.
  evidence: achado do Blind Hunter, Edge Case Hunter e Acceptance Auditor (revisão independente `bmad-code-review` da Story 1.5, achado convergente de três lentes). Não é bug — o campo é documentadamente opcional ("quando disponível") — mas reduz a utilidade prática da extração de driver em BSOD especificamente. Corrigir exigiria uma fonte de dado mais rica (ex. parsing de `EventData` estruturado ou minidump), fora do escopo desta história.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: `RoteadorIpc.DiagnosticarCausaRaizAsync` monta `LeitorEventLog` com `NullLogger<LeitorEventLog>.Instance`, então o "log de warning" que a I/O Matrix exige em caso de falha de leitura nunca é escrito de verdade em produção — mesmo padrão já usado (e já deferido) pra `VerificarSoftwareAsync`/`VerificarBiosAsync` nas Stories 1.3/1.4.
  evidence: achado do Acceptance Auditor (revisão independente `bmad-code-review` da Story 1.5). Não é regressão nova — `RoteadorIpc` constrói todo handler por-requisição com `NullLogger`, convenção uniforme já deferida antes. Corrigir exigiria injetar um logger real no `RoteadorIpc`, mudança de composição maior que uma história isolada.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: `LeitorEventLog.LerAsync` só checa `cancellationToken.ThrowIfCancellationRequested()` uma vez no início — o token nunca é passado pra `LerWhea`/`LerBsod`/`LerCrashAplicacao`/`Consultar`, então um cancelamento após a primeira checagem não interrompe a leitura em andamento.
  evidence: achado do Edge Case Hunter (revisão independente `bmad-code-review` da Story 1.5). Impacto baixo — leitura é local e agora limitada a 200 eventos por categoria (após o fix do cap) — mas ainda não é genuinamente cancelável. Corrigir exigiria checar o token dentro do loop de `Consultar`.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: o corte de 200 eventos por categoria (`MaxEventosPorCategoria`) não é sinalizado ao usuário — a UI só mostra "N evento(s) encontrado(s)", sem distinguir "N no total" de "atingi o limite, pode haver mais". Também não há teste que gere >200 eventos e verifique o corte de verdade (validado só por inspeção de código).
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.5). Cenário raro (precisaria de 200+ eventos reais de uma categoria em 30 dias) — mas se acontecer, o usuário não saberia que a lista está incompleta. Corrigir exigiria expor a contagem total antes do corte e uma mensagem de UI condicional.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: `DriversViewModel.Eventos`/`StatusTextDiagnostico` não são limpos quando `Popular`/`PopularProgramas`/`PopularBios` rodam de novo (novo SCAN) — depois de um re-scan em que um driver deixou de estar desatualizado, o diagnóstico anterior continua na tela apontando uma "Causa provável" pra esse driver, agora obsoleta.
  evidence: achado do Blind Hunter (revisão independente `bmad-code-review` da Story 1.5). Mesmo padrão de "resultado anterior persiste até nova ação explícita" já aceito pras seções de Software/BIOS (nenhuma delas limpa resultado em re-scan automaticamente também) — não é uma regressão isolada desta história, mas vale revisitar as três juntas se virar incômodo real.

- source_spec: `docs/implementation-artifacts/spec-1-5-causa-raiz-event-log.md`
  summary: pontos menores de robustez/UX não corrigidos nesta rodada: mensagem de causa por driver é só `driver.Descricao` (ex. "Causa provável: GeForce RTX 3060", menos autoexplicativa que "Causa provável: BIOS desatualizada"); teste ausente pro branch de `bios` com shape inválido em `RoteadorIpc.DiagnosticarCausaRaizAsync` (só `driversDesatualizados` inválido é testado); `record.TimeCreated` nulo cai silenciosamente pra `DateTimeOffset.Now` sem log, podendo distorcer a ordenação por mais-recente-primeiro; `CorrelacionadorCausaRaiz.DeterminarCausa` usa `FirstOrDefault` sem critério de desempate documentado quando o texto de um evento cita o fabricante de mais de um driver desatualizado; janela de 30 dias é hardcoded sem controle de UI; nenhum teste verifica `DiagnosticandoCausaRaiz == true` durante a operação em andamento (só o estado final).
  evidence achados do Blind Hunter (revisão independente `bmad-code-review` da Story 1.5). Todos de baixo risco individual — nenhum quebra o guard anti-alucinação nem os Boundaries da spec — registrados juntos aqui pra não perder o rastro; revisitar se o Diagnóstico de Travamentos crescer em uso/complexidade.

- source_spec: `docs/implementation-artifacts/spec-auditoria-legado-m1-m4.md`
  summary: os `catch (Exception)` genéricos tratados nesta rodada (limpeza, coleta de inventário, sensores) continuam capturando qualquer exceção — inclusive bugs de programação genuínos (ex. `NullReferenceException`) — e logando como se fosse sempre uma condição transitória esperada ("em uso?"). O log agora existe, mas a mensagem pode confundir a triagem de um defeito real com um "arquivo bloqueado" comum.
  evidence: achado do Blind Hunter (revisão independente do `bmad-build` desta rodada). Diferenciar exigiria filtrar por tipo de exceção esperado vs. genérico, o que muda o fluxo de tratamento de erro — fora do escopo de uma correção que deveria só adicionar rastreabilidade sem alterar comportamento.

- source_spec: `docs/implementation-artifacts/spec-auditoria-legado-m1-m4.md`
  summary: falhas por-arquivo/por-pasta dentro de `GerenciadorLimpeza.Limpar` (`LimparPasta`, `EsvaziarLixeira`, `LimparEventLogs`) são capturadas e logadas localmente, mas nunca chegam à lista `erros` de `ResultadoLimpeza` — o `catch` externo em `Limpar` só vê falhas que escapam dos handlers internos. Um usuário pode rodar uma limpeza, ver "0 erros" na UI, e ainda assim ter arquivos que não foram apagados (em uso, sem permissão).
  evidence: achado do Blind Hunter. Corrigir exigiria decidir um comportamento de produto (agregar falhas parciais em `erros`, e como apresentá-las na UI) — decisão de UX/negócio, não uma correção de qualidade de código.

- source_spec: `docs/implementation-artifacts/spec-auditoria-legado-m1-m4.md`
  summary: `RepositorioSqlite.ContarAsync` foi corrigido para usar `CommandText` literal por tabela via `switch`, mas os nomes das três tabelas (`inventarios`, `consentimentos`, `execucoes`) ainda são strings soltas, sem vínculo em tempo de compilação com os métodos públicos (`ContarInventariosAsync` etc.) que os produzem — um typo em qualquer um dos dois lados só falha em runtime.
  evidence: achado do Blind Hunter. Um enum ou constantes compartilhadas resolveria, mas é uma mudança de superfície pública desproporcional a uma correção pontual de SQL interpolado.

- source_spec: `docs/implementation-artifacts/spec-auditoria-legado-m1-m4.md`
  summary: `LeitorSensoresWindows.ExecutarPowerShell` não tem um `catch (Exception)` de segurança (só os três tipos específicos já tratados) e, quando `proc.WaitForExit(25_000)` expira, retorna `null` sem logar o timeout nem chamar `proc.Kill()` — o `powershell.exe` órfão continua rodando desacoplado.
  evidence: achado do Blind Hunter. É uma melhoria de confiabilidade real (processo órfão, exceção não mapeada propagando pro timer de sensores de 500ms), mas muda o comportamento de tratamento de erro do método — vale uma história própria de robustez do leitor de sensores, não uma correção de "adicionar log".

- source_spec: `docs/implementation-artifacts/spec-auditoria-legado-m1-m4.md`
  summary: nenhum dos quatro arquivos tocados nesta correção (`GerenciadorLimpeza`, `LeitorWindows`, `RepositorioSqlite`, `LeitorSensoresWindows`) tem cobertura de teste unitário direta — mesmo achado do item M3 da auditoria original (`docs/specs/auditoria-legado.md`).
  evidence: fora de escopo desta correção pontual, que deliberadamente não adicionou testes novos (decisão tomada antes de iniciar as mudanças, para manter o blast radius mínimo).
