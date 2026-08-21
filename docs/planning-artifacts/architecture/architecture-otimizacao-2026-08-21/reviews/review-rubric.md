# Rubric Review — ARCHITECTURE-SPINE.md
**Módulo de Sugestão de Upgrade com Foco em Custo-Benefício**

Reviewer: rubric walker (bmad-architecture good-spine checklist)
Target: `docs/planning-artifacts/architecture/architecture-otimizacao-2026-08-21/ARCHITECTURE-SPINE.md`
Context input: `docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/prd.md` + `addendum.md`
Codebase verified against: `C:\Users\Michel\Documents\GitHub\otimizacao` (git-tracked source, not just docs mirror)

## Verdict

Solid, well-verified spine that correctly ratifies the brownfield codebase and resolves the one PRD-flagged blocking question (L4/L5 vs. Vitrine), but it silently drops the entire data-sourcing/ops story for the Vitrine's paid track (partner-store pricing/stock/delivery data for FR-16/17/18) and leaves one AD's Rule (AD-8) naming two possible owners for the exact divergence it claims to prevent — both are real gaps a level-below story could hit without any convergent rule to fall back on.

## Findings

### 1. [CRITICAL] Partner-store data sourcing (FR-16, FR-17, FR-18) has no architectural owner at all
**File:** ARCHITECTURE-SPINE.md — AD-3, Structural Seed, Capability → Architecture Map

AD-3 ("Papel fixo de cada fonte de dados de hardware") is precisely the mechanism this spine uses to prevent a data source from appearing ad-hoc or two features assuming different roles for it — and it is careful to enumerate TechPowerUp, the static catalog, BuildCores Open DB, `RepositorioWhqlEstatico`, and `BancoCuradoBios`. It says nothing about where the Vitrine's commercial data comes from: price, stock/availability, delivery estimate, "produto original" seal, parcelamento terms, and the commission link itself for the three named partner stores (Mercado Livre, Amazon, Kabum) — FR-16 and FR-18's entire payload. The Structural Seed's only new node under `Features.Upgrade` is `Benchmark/` (TechPowerUp, FR-19); there is no `Lojas/`, `Parceiros/`, or equivalent for FR-16–18. The Stack section explicitly claims "nenhuma dependência nova precisa entrar no stack" and names only TechPowerUp and the driver/BIOS official-source integration as new HTTP integrations — omitting the three-store integration that FR-18's testable consequence ("um item sem informação de parcelamento e prazo de entrega... aparece 'a confirmar na loja' em vez de omitido") implies is a live, per-store data fetch.

Verified in the actual repo: no code, config, or reference to Mercado Livre/Amazon/Kabum/affiliate integration exists anywhere in `src/` today (checked via grep across the full tree) — this is 100% new surface for this PRD, unlike TechPowerUp or the driver/BIOS checks which at least have an existing "MVP offline; produção usa REST API" comment to anchor against.

**Failure scenario:** one story implements FR-16 against a live per-store API client (three different integrations, three sets of credentials/rate limits), another story (or a later maintainer) assumes a manually curated static price list analogous to `hardware_catalog.json` — both are "compliant" with the spine as written, and they produce incompatible component boundaries, config/secrets handling, and refresh cadence. This is exactly the class of divergence AD-3 exists to prevent for every *other* data source in the feature, just not this one — and it funds SM-3, the PRD's only committed V1 revenue metric.

**Recommendation:** add an AD (or extend AD-3) naming the component that owns partner-store data, whether it's live-queried per Loja Parceira, and where credentials/affiliate IDs live — or, at minimum, add it to Deferred with the same explicit PRD cross-reference treatment given to the TechPowerUp mechanism.

### 2. [HIGH] TechPowerUp benchmark base: delivery mechanism ambiguous, not tied to the "no background daemon" invariant
**File:** ARCHITECTURE-SPINE.md — Stack note, AD-3, AD-4, Deferred

The Stack section states TechPowerUp is "uma integração HTTP, coberta pelo runtime já presente," which reads as a live, per-request query. Deferred then reopens the question with three structurally different options ("scraping, API, curadoria manual") plus "cadência de refresh" — i.e., the spine has not actually decided whether this is a live runtime call or a periodically-curated static asset shipped with releases (the pattern already used for `hardware_catalog.json`, ~15 peças). AD-4 explicitly extends the existing "sob demanda, nunca poll em background/daemon" invariant to the driver/software/BIOS official-source checker, but never states whether that same invariant governs TechPowerUp refresh. A live scraper polling techpowerup.com on a cadence would require new background infrastructure that contradicts the project's established no-daemon paradigm (`§5` of the PRD, "Decisões estruturais herdadas" in the addendum); a release-bundled curated file would not.

**Failure scenario:** one implementer builds a background/scheduled scraping job (new infra, violates the existing opt-in-only invariant that every other AD in this spine takes pains to preserve); another treats it as a static data file refreshed only at app-release time (no new infra, consistent with `hardware_catalog.json`'s pattern). Both are defensible readings of the current spine text.

**Recommendation:** state explicitly that TechPowerUp data (like the compatibility catalog) is a versioned static asset shipped with app releases, not a live per-session query — or, if live querying is actually intended, say so and reconcile it with the no-daemon invariant.

### 3. [MEDIUM] AD-8's Rule names two possible owners for the exact divergence it exists to prevent
**File:** ARCHITECTURE-SPINE.md — AD-8

AD-8's Prevents clause: "`Features.Manutencao` e `Features.Upgrade` decidirem a ordem de exibição cada uma por conta própria... gerando resultado inconsistente." Its Rule: "`Features.Atualizacao` (**ou** o consumidor de IPC que monta o Relatório de Resultado) é o único ponto que compõe..." Naming two candidate owners with "ou" reopens the same category of ambiguity the AD is written to close — nothing in the Rule forces the ordering logic into one specific class/module. This is not merely stylistic: `Features.Atualizacao` and "the IPC consumer that assembles the Relatório de Resultado" are different projects/layers in this codebase (a `Features.*` slice vs. `HardwareOptimizer.Ipc`), so the two readings produce genuinely different unit boundaries and testability.

**Recommendation:** pick one owner explicitly (the spine's own diagram already draws `Ipc` as the thing that "monta Relatório de Resultado, compõe (AD-8)" — that arrow suggests `Ipc`/its consumer was the intended single owner; if so, drop the `Features.Atualizacao` alternative from the Rule text, or vice versa).

### 4. [MEDIUM] PRD open question 5 (ordering *within* the Vitrine catalog) is dropped without a trace
**File:** ARCHITECTURE-SPINE.md — Deferred; PRD §10 item 5

PRD §10 lists six "arquitetura pode resolver em paralelo" questions (items 2–6, plus the blocking item 1). Items 2 (TechPowerUp mechanism), 3 (domain allowlist), and 4 (correlation criteria) are each explicitly carried into the spine's Deferred section with a `PRD §10 item N` cross-reference. Item 5 — whether cooler/fonte should rank before GPU/CPU inside the Vitrine's own item ordering, generalizing the same "cheapest/most-honest first" logic that AD-10/AD-8 already applies *between* Manutenção and Upgrade — has no corresponding AD, Deferred entry, or open-question carry-over anywhere in the spine. Item 6 (Vitrine discovery outside the Relatório) is arguably UX/product scope and reasonably out of an architecture spine's remit, but item 5 is squarely a `GeradorSugestoes`/catalog-ordering concern, the same domain AD-8 already governs one level up.

**Failure scenario:** two stories implementing the Vitrine's item list order it differently (one by raw price, one by category priority) with no rule to converge them, and no flag anywhere that this was a known open question at PRD hand-off — unlike items 2–4, which at least warn the next builder that a decision is still owed.

**Recommendation:** add item 5 to Deferred (or to an explicit "open questions" line) with the same PRD cross-reference treatment as items 2–4.

### 5. [LOW] Capability → Architecture Map has minor traceability gaps against the spine's own AD text
**File:** ARCHITECTURE-SPINE.md — Capability → Architecture Map

- FR-12/FR-13 row cites only AD-2, AD-3, AD-9 — but AD-9's own text says "L4 (Armazenamento, AD-6) segue bloqueante para a parte de SSD do Teto de Compatibilidade," i.e., AD-6 is load-bearing for FR-12 too and isn't listed in that row.
- FR-14/FR-15 row cites only AD-2 — but FR-14's "linha factual" is explicitly required to expose `MargemConfianca`/`AtualizadoEm`, the exact contract AD-3 establishes (`GanhoEstimado { Percentual, MargemConfianca, AtualizadoEm }`); AD-3 isn't listed for that row.

Not a substantive gap (the governing ADs exist and say the right thing elsewhere in the document), but the map is the single table meant to give a downstream reader one-glance FR→AD traceability, and it's not fully self-consistent with the AD text above it.

## What checked out well (no finding)

- **PRD coverage:** all 19 FRs are present in `binds`, the Capability → Architecture Map, and are traceable to at least one AD. No FR is silently missing.
- **Blocking PRD question resolved:** PRD §10 item 1 (does L4/L5 block the Vitrine?) — the single item the PRD explicitly flags as blocking architecture — is resolved concretely by AD-9 (L5 doesn't block; L4 blocks only the SSD-specific part of the Teto de Compatibilidade, deferred to the AD-6 story). This is the spine's strongest piece of work.
- **Brownfield ratification verified against real code**, not just asserted: confirmed present and matching the spine's description — `ExecutorControlado`, `RoteadorIpc`, `ServicoBackup`, `ColetorInventario`, `ServicoSensores`, `ModuloBios`/`GeradorGuiaBios`, `TipoPecaUpgrade` enum (`Cpu/Gpu/Ram/SsdM2/Fonte`, exact match), `CategoriaAcao` enum (`Cpu`/`Memoria` present with no corresponding optimization actions, exact match), `RepositorioWhqlEstatico`, `BancoCuradoBios`, `SaudeDisco`. The Deferred item "correção do bug de build" (`.gitignore` eating `Features.Upgrade/Data/` and `Features.Drivers/Data/`) was independently confirmed real: `.gitignore` line 20 (`data/`) does match `Data/` case-insensitively on this repo's git config, and `hardware_catalog.json` is confirmed **not tracked** by git (`git ls-files` returns nothing for that path) despite existing on disk.
- **Stack is accurate against the actual csproj/Directory.Build.props**: `net8.0`/`net8.0-windows`, `LangVersion 12`, `Nullable enable`, `TreatWarningsAsErrors=true`, xUnit 2.4.2 + coverlet.collector 6.0.0, Avalonia 12.0.4 + CommunityToolkit.Mvvm 8.4.2 — all confirmed by direct file read, not assumed. Keeping .NET 8 (rather than a newer TFM) is the correct call per the skill's own brownfield rule (ratify, don't reinvent) since it's the existing, shared `Directory.Build.props` setting; worth flagging only as an advisory note that .NET 8's LTS support window ends Nov 2026, a few months after this spine's date, and the spine doesn't mention this as a future-roadmap concern even though `Directory.Build.props`'s own comment references a "Fase 0 do roadmap" implying later phases were anticipated. Not a defect in this spine — just worth a line if a roadmap doc exists to point to.
- **No Deferred item creates unbounded divergence risk** except where noted above (#1, #2): the catalog-curation-process, official-domain-list-ownership, and correlation-criteria deferrals are all single-owner concerns (one component already fixed by an AD), so leaving the *exact* criteria to story level is safe — a future story can't put that logic in the wrong place even without the detail.
- **Operational/environmental envelope**: correctly scoped as "no new infrastructure" for the driver/BIOS/allowlist checks (client-side HTTP, no daemon, consistent with existing invariants) — this dimension is *not* silent for that slice. It is silent for the two items above (#1 partner-store data, #2 TechPowerUp delivery mechanism), which is where the operational-envelope blind spot the checklist warns about actually landed in this spine.
