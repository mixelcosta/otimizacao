# Sprint Change Proposal — 2026-08-22

## 1. Resumo do problema

Durante a implementação autônoma (`bmad-build-auto`) da Story 2.1 (Épico 2), a spec dessa história afirmou — com base numa premissa que se provou errada — que a nova aba "Diagnóstico de Manutenção" não precisava de gate `FuncionalidadePremium`, citando "o mesmo tratamento dado a Drivers/BiosGuide no V1 (explicitamente tirados do bloqueio Premium)". A revisão independente confirmou, checando o código, que `IrParaDrivers`/`IrParaBiosGuide` (`ShellViewModel.cs`) de fato chamam `_licenca.TemAcesso(...)` e SÃO gated — contradizendo a premissa. A correção aplicada na hora foi adicionar gate também em Diagnóstico de Manutenção, seguindo o padrão do código como estava.

Investigação mais profunda (via `bmad-sprint-planning` → readiness gate → `bmad-correct-course`) revelou a causa raiz real: existe uma decisão de UX já **adotada e finalizada** — `docs/planning-artifacts/ux-designs/ux-otimizacao-2026-08-21/EXPERIENCE.md` (`status: final`), linha 18:

> `[ADOPTED]` **Drivers, Guia BIOS IA e a aba de visualização da Vitrine saem do bloqueio Premium no V1** — a compra em si é paga (comissão da loja), mas ver a recomendação não é. `Vida Útil` não faz parte deste módulo e mantém seu status atual.

E o PRD trata **Núcleo de Atualização** (que inclui Drivers/BiosGuide) e **Diagnóstico de Manutenção** explicitamente como **Trilha Grátis** — a tese central do produto.

Ou seja: não é uma lacuna de planejamento (a decisão existe e é clara) — é o **código** que diverge da decisão já tomada. `IrParaDrivers`, `IrParaBiosGuide` e `IrParaUpgrade` (a aba de visualização da Vitrine) estão gated quando não deveriam. `IrParaVidaUtil` está correto (deveria continuar gated, e está). E o gate que eu mesmo adicionei em `IrParaDiagnosticoManutencao` (Story 2.1, corrigido por engano na revisão) também está errado pela mesma razão.

## 2. Evidência

- `src/HardwareOptimizer.App/ViewModels/ShellViewModel.cs:148,164,171,184`: `IrParaUpgrade`, `IrParaDrivers`, `IrParaBiosGuide`, `IrParaDiagnosticoManutencao` chamam `_licenca.TemAcesso(FuncionalidadePremium.X)` — todos deveriam ser navegação livre, exceto nenhum desses quatro.
- `src/HardwareOptimizer.App/ViewModels/ShellViewModel.cs:157`: `IrParaVidaUtil` chama `TemAcesso(FuncionalidadePremium.ContadorVidaUtil)` — correto, deve continuar gated.
- `docs/planning-artifacts/ux-designs/ux-otimizacao-2026-08-21/EXPERIENCE.md:18` — decisão `[ADOPTED]`, documento `status: final`.
- PRD (`prd.md`) — Núcleo de Atualização e Diagnóstico de Manutenção como Trilha Grátis (tese central, T3 do brainstorming original).

## 3. Impacto em épicos/artefatos

- **PRD, Espinha de Arquitetura, EXPERIENCE.md/DESIGN.md**: nenhuma mudança necessária — todos já estão corretos e consistentes entre si. A "Assinatura Premium" como modelo de receita recorrente (Fase 2, preço/módulos futuros) continua legitimamente em aberto — isso é uma questão diferente de "quais telas são navegáveis sem assinatura hoje", que o EXPERIENCE.md já resolveu.
- **Épico 1 (concluído)**: as Stories 1.2 (Drivers) e 1.4 (BIOS) foram implementadas e revisadas com o gate incorreto já presente no código — não introduzido por essas histórias (o gate já existia antes desta sessão), mas nenhuma delas o corrigiu porque não estava no escopo investigado na época.
- **Épico 2, Story 2.1 (concluída nesta sessão)**: o gate que adicionei em `DiagnosticoManutencao` precisa ser revertido.
- **Épico 3 (não iniciado)**: a Vitrine de Upgrade (`IrParaUpgrade`) também precisa do fix — relevante porque o Épico 3 vai construir em cima dessa tela.
- **Futuras histórias**: nenhuma pergunta em aberto nova — a regra fica clara: Trilha Grátis (Núcleo de Atualização, Diagnóstico de Manutenção, visualização da Vitrine) nunca é gated; só `Vida Útil` e a futura Assinatura Premium (Fase 2, módulos ainda não definidos) o são.

## 4. Caminho escolhido

**Opção 1 — Ajuste direto.** Remover o gate `TemAcesso` de `IrParaDrivers`, `IrParaBiosGuide`, `IrParaUpgrade` e `IrParaDiagnosticoManutencao`, e o valor `DiagnosticoManutencao` do enum `FuncionalidadePremium` (nunca deveria ter existido). Reverter os elementos de UI `nav-locked` correspondentes na sidebar, voltando pro padrão `nav-premium` sempre visível (mesmo tratamento das telas Trilha Grátis já existentes). Esforço: baixo. Risco: baixo (mudança mecânica, bem precedenciada pelo próprio `EXPERIENCE.md`, sem ambiguidade de implementação).

Nenhum rollback de história necessária, nenhuma redução de escopo do MVP — é uma correção de alinhamento código↔decisão-já-adotada.

## 5. Handoff

**Escopo: Minor.** Implementado diretamente nesta sessão (Developer/Amelia), sem necessidade de replanejamento PM/Arquiteto — a decisão de produto já existe e está documentada; isto é execução, não nova decisão.

## 6. Aprovação

Aprovado por Michel em 2026-08-22, modo Batch — corrigir os dois itens (Story 2.1 + bug pré-existente de Drivers/BiosGuide/Upgrade) na mesma sessão.
