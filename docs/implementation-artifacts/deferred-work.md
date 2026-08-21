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
