# PRD Quality Review — Módulo de Sugestão de Upgrade com Foco em Custo-Benefício

## Overall verdict

This PRD shows unusually disciplined planning hygiene — testable per-FR consequences, honestly-named trade-offs, and clean ID/glossary/assumption bookkeeping — that most PRDs at this stage lack. Its risk is structural rather than cosmetic: the Vitrine feature is marked "Em Escopo" for V1 (§8.1) while its core mechanism (Teto de Compatibilidade, FR-12) is gated on inventory/catalog work the PRD itself says is unresolved and out of scope, and the "ganho estimado" percentage that anchors the product's central honesty claim has no stated data source — a tension the PRD admits (Open Question 9) but does not resolve, and which directly threatens the anti-hallucination guardrail the product is built on (§5). Both should close, or be explicitly re-scoped, before architecture handoff; everything else here is in strong shape.

## Decision-readiness — strong

Trade-offs are named with real cost, not smoothed to neutral. §8.2's note on Assinatura Premium is the clearest example: *"sem a Assinatura Premium, o V1 real deixa esse segmento exatamente na situação que a T2 descrevia como problema"* — this reopens a tension the brainstorming session had marked "parcial/frágil, não definitiva" rather than pretending it was resolved. §10's eleven Open Questions are genuinely open (e.g. Q6 on whether persona Bruno's absence risks a public critic finding the product shallow; Q11 on whether an entire user segment has no discovery path to the Vitrine), not rhetorical questions answered in the next sentence. `[NOTE FOR PM]` callouts land at real tensions (FR-16's unresolved "preço suspeito mesmo em loja parceira" risk; FR-18's note that missing trust signals expose SM-3 to the same low-conversion risk the original session flagged) rather than safe checkpoints.

One placement gap undercuts this strength.

### Findings
- **high** Vitrine scope claim doesn't carry its own caveat forward (§8.1 vs. FR-12) — §8.1 "Em Escopo" states "*Vitrine de Upgrade com Teto de Compatibilidade... (FR-12 a FR-18...)*" as committed V1 scope. FR-12's own `[NOTE FOR PM]` says this mechanism "*depende de resolver a lacuna L4 (armazenamento ausente do Inventário)... e de expandir o catálogo de peças (lacuna L5)... ambas identificadas... como pré-requisito técnico, não coberto neste PRD*." A reader of §8.1 alone would conclude Vitrine ships in V1; only a reader who also reaches FR-12's notes learns its core mechanism has no defined path to exist. *Fix:* add the L4/L5 dependency as an explicit caveat inside §8.1 itself (not only inside FR-12), or state a decision on whether Vitrine is conditionally in scope pending that architecture work.

## Substance over theater — strong

No findings. Three personas (Rafael, Carla, Diego), each driving specific FRs — Rafael's trust conditions become FR-18 verbatim ("*local seguro, produto de qualidade, entrega rápida, parcelamento*" → Requisitos de confiança na listagem), Diego's factual-question constraint becomes FR-9 and a Não-Objetivo. Bruno is explicitly deferred with a sharp, non-decorative rationale (§8.2, Q6: he's framed as "*o crítico público mais provável do produto*" if Eixo de Qualidade is shallow) rather than padded in as a fourth "considered" persona. The Vision (§1) is specific to this product's discovery ("*o usuário nunca pediu 'mais hardware' — ele pediu para parar de sentir o computador*"), not swappable boilerplate. Feature-specific NFRs (§4.1) are concrete and bounded ("*não pode rodar em background contínuo*", "*restrita a uma lista de domínios oficiais permitidos*"), not "system must be scalable/secure" filler.

## Strategic coherence — strong

The thesis is explicit and the feature order follows it, not ease: §8.1 states scope order came from the "One Feature Only" ranking (núcleo → diagnóstico de manutenção → vitrine), and that ranking mirrors the honesty-first thesis (cheapest fix surfaced before the expensive one — the same logic that drives FR-10's cost ordering). Counter-metrics are present and specific (SM-C1 rejection rate, SM-C2 zero-tolerance on incompatibility complaints), not decorative — SM-C2 explicitly states "*receita nunca justifica recomendar peça errada*," directly countering SM-3. No findings; SM-4 (retention, window still undefined) is already carried as an `[ASSUMPTION]` in §9/§11, so it doesn't need a separate flag here.

## Done-ness clarity — adequate

Most FRs carry genuinely testable "Consequences" — this is the PRD's strongest mechanical habit and unusual for this stage (e.g. FR-1: "*nunca usa agregador de terceiros não-oficial*"; FR-17: "*nunca sugere troca de RAM para uma máquina identificada com RAM soldada*"). No "handles gracefully" / "reasonable performance" language found in FR text. But one FR pair central to V1 scope has no stated basis for its output at all.

### Findings
- **critical** FR-13/FR-14's "ganho estimado" has no defined data source (§4.3, FR-14 Notes; §10 Q9) — FR-14 displays `Upgrade hardware = X%` as one of the two headline numbers the whole Relatório de Resultado's honesty claim rests on (per §1: "*sempre honesta*", "*prova numérica e sem viés de venda*"). FR-14's own Notes admit this number "*não tem o mesmo rigor da correlação causa-raiz*" and is "*estimativa com margem declarada, não prova medida*" — but no FR anywhere states what data grounds that estimate for a part the user doesn't own. Open Question 9 makes the risk explicit: "*Pergunta original do brainstorming, nunca respondida — risco direto com o guard contra alucinação de LLM (§5) se não houver base real por trás do número.*" An engineer cannot implement FR-14's number today without inventing the methodology themselves — the exact outcome §5's hallucination guard exists to prevent. *Fix:* either define the data source (e.g., a benchmark database, its refresh cadence, and confidence bounds) as a prerequisite FR before FR-14 ships, or explicitly gate FR-14/FR-13 behind that data source in §8.1 rather than listing them as unconditional V1 scope.
- **low** FR-5's "correlação plausível" threshold left to architecture (FR-5 Notes; §10 Q7) — appropriately deferred with a `[NOTE FOR PM]`, and the FR's pass/fail behavior is testable independent of the exact algorithm ("*nomeia o item específico*" vs. "*não inventa uma causa*"), so this is a reasonable planning-stage deferral rather than a gap. Noted only because it compounds with the FR-14 gap above under the same guardrail.

## Scope honesty — strong

Exceptional tagging discipline: `[ASSUMPTION]` (5 inline instances: §2.2, §2.3/UJ-3, §6, §9/SM-1, §9/SM-4), `[NOTE FOR PM]` (8 instances), and an explicit "Out of Scope" subsection inside four separate FRs (FR-1, FR-3, FR-7, FR-17) in addition to the dedicated §7/§8.2. De-scoping is done openly, not silently — §8.2's Assinatura Premium entry doesn't just log the deferral, it names the consequence it reopens (the T2 tension for RAM-soldada notebooks).

### Findings
- **medium** Open-item density is high for a PRD heading into architecture/stories (§0, §10, §11) — 11 Open Questions + 8 `[NOTE FOR PM]` + 5 `[ASSUMPTION]` tags across 18 FRs. Individually each is well-placed and honest (this is exactly what the tagging discipline is for), but in aggregate two of the three features (Vitrine's core mechanism, the monetization model for RAM-soldada notebooks) still have open load-bearing questions. §0 frames this PRD as input to "*arquitetura, épicos/histórias*," so some deferral is expected — but the volume here means architecture will inherit questions (Q7, Q8, Q9) that gate whether FR-5/FR-12/FR-13/FR-14 can even be built as specified. *Fix:* before handoff, triage §10 into "must resolve before architecture starts" vs. "architecture can resolve in parallel" so the next workflow doesn't have to rediscover the sequencing risk itself.

## Downstream usability — strong

Glossary (§3) terms are used identically across FRs and UJs (Trilha Grátis/Paga, Teto de Compatibilidade, Eixo de Qualidade, Loja Parceira, Vitrine, Relatório de Resultado all appear consistently, never with a synonym substituted). FR IDs (FR-1–FR-18), UJ IDs (UJ-1–UJ-3), and SM IDs (SM-1–SM-4, SM-C1–SM-C2) are contiguous with no gaps or duplicates. Cross-document references to the addendum's audit IDs (L4, L5) are self-sufficient — every inline use glosses the lacuna in parentheses at the point of reference (e.g. FR-12 Notes: "*lacuna L4 (armazenamento ausente do Inventário)*"), so a reader of prd.md alone isn't stranded. No findings.

## Shape fit — strong

This is a consumer/prosumer, meaningful-UX brownfield extension, and the PRD is shaped accordingly: three UJs with named, contextualized protagonists carry real weight (not decorative — UJ-1's edge case directly produced a `[NOTE FOR PM]`; UJ-3's emotional curve is preserved into the addendum for UX). Persona count (3 active + 1 explicitly deferred) doesn't tip into persona theater. Brownfield references are specific and accurate, not hand-waved — FR-6/FR-7 correctly cite existing, tested mechanisms (`ServicoBackup`, approval-before-apply) as inherited rather than redesigned, and §5 states this explicitly: "*formaliza invariantes que já existem, implementados e cobertos por teste... o módulo os herda, não os redesenha.*" No findings.

## Mechanical notes

- **Glossary drift:** none of structural concern. One soft naming overlap worth a UX-copy pass, not a PRD fix: "Diagnóstico" (§3, the shared infrastructure term) and "Diagnóstico de Manutenção" (§4.2, a specific feature) share a root word and could read as the same concept on a skim — the PRD itself uses each correctly and consistently, so this is a low-risk note for downstream naming, not a PRD defect.
- **ID continuity:** FR-1→FR-18 contiguous, no gaps/duplicates. UJ-1→UJ-3 contiguous. SM-1→SM-4 plus SM-C1/SM-C2 contiguous. Clean.
- **Cross-references:** spot-checked anchors (§3, §6, §7, §8.2, §10, §11) all resolve against their Markdown headers correctly.
- **Assumptions Index roundtrip:** clean. All 5 inline `[ASSUMPTION]` tags (§2.2, §2.3/UJ-3, §6, §9/SM-1, §9/SM-4) are indexed in §11 (SM-1 and SM-4 correctly grouped into one bullet); no orphaned index entries, no unindexed inline tags.
- **UJ protagonist naming:** all three UJs (Rafael, Carla, Diego) carry a named protagonist with persona+context stated inline. No floating UJs.
- **Required sections:** complete for the stakes — Vision, Target User (JTBD + non-users + UJs), Glossary, Features/FRs, Constraints, Monetization, Non-Goals, Scope, Success Metrics, Open Questions, Assumptions Index. Nothing expected for a chain-top, brownfield, consumer-facing PRD is missing.
