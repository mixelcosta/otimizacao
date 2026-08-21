# Deferred Work

Itens encontrados durante o desenvolvimento que não pertencem à história atual — pré-existentes, não causados pela mudança em curso.

- source_spec: `docs/planning-artifacts/epics.md` (Épico 1, Story 1.1)
  summary: `HardwareOptimizer.Features.LifeCounter.csproj` referencia `EmbeddedResource Include="Data\tbw_database.json"`, mas esse arquivo nunca existiu no repositório (não só nesta cópia — não há histórico dele no git). O build de `HardwareOptimizer.sln` falha com `CS1566` no mesmo padrão que a Story 1.1 corrigiu para Features.Upgrade/Features.Drivers.
  evidence: achado do Blind Hunter (revisão de bmad-build) rodando `dotnet build HardwareOptimizer.sln` completo — fora do escopo da Story 1.1, que só cobria Features.Upgrade e Features.Drivers. Precisa de história própria: localizar/recriar `tbw_database.json` (usado por `Features.LifeCounter`, provavelmente para cálculo de vida útil de SSD — TBW).

- source_spec: `docs/planning-artifacts/epics.md` (Épico 1, Story 1.1)
  summary: os dois catálogos JSON recém-versionados têm shapes inconsistentes entre si — `whql_catalog.json` é um array no nível raiz; `hardware_catalog.json` é um objeto com três arrays nomeados (`processadores`, `gpus`, `placasMae`). Não há schema/convenção compartilhada entre catálogos do produto.
  evidence: achado do Blind Hunter. Corrigir exigiria alterar o parsing em `RepositorioWhqlEstatico` e/ou `ValidadorCompatibilidade`, fora do escopo de um fix de build. Vale revisitar quando o catálogo estático crescer (ver ARCHITECTURE-SPINE.md, Deferred — "Curadoria/crescimento do catálogo estático de compatibilidade").
