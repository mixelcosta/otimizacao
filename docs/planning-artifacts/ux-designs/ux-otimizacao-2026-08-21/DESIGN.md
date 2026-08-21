---
name: Otimize Builder
description: Diagnóstico e otimização de hardware para desktop Windows. Dark-only, técnico, tom de ferramenta de PC building para entusiasta — nunca corporativo, nunca alarmista.
status: final
created: 2026-08-21
updated: 2026-08-21
sources:
  - docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/prd.md
  - docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/addendum.md
  - docs/planning-artifacts/architecture/architecture-otimizacao-2026-08-21/ARCHITECTURE-SPINE.md
colors:
  bg-root: '#030308'
  bg-section: '#06060F'
  bg-flat: '#09091A'
  card-gradient-top: '#0C0C1E'
  card-gradient-base: '#09091A'
  border-card: '#1E1E3C'
  border-structural: '#13132A'
  border-nav-inactive: '#141428'
  text-primary: '#E0E0F2'
  text-secondary: '#8080AA'
  text-secondary-hover: '#8888CC'
  text-label: '#484865'
  text-hint: '#282840'
  accent: '#00C8FF'
  premium: '#D4A017'
  premium-upsell-purple: '#9B59B6'
  status-success: '#00C870'
  status-success-alt: '#00FF88'
  status-warning: '#FFAA00'
  status-warning-alt: '#FF8C00'
  status-critical: '#CC3333'
  status-critical-alt: '#FF4444'
  status-critical-nav: '#FF3A5C'
typography:
  display:
    fontFamily: 'system (Segoe UI no Windows)'
    fontSize: 32-54px
    fontWeight: Black
    note: 'Números de destaque — valor de sensor, pontuação'
  heading:
    fontFamily: 'system (Segoe UI no Windows)'
    fontSize: 13-14px
    fontWeight: Bold/SemiBold
    letterSpacing: 2-3px
    note: 'CAPS sempre — título de tela/seção'
  body:
    fontFamily: 'system (Segoe UI no Windows)'
    fontSize: 12-13px
    fontWeight: Regular
  label:
    fontFamily: 'system (Segoe UI no Windows)'
    fontSize: 10-11px
    letterSpacing: 1-2px
    note: 'CAPS sempre — rótulo de campo, acima de um valor'
  caption:
    fontFamily: 'system (Segoe UI no Windows)'
    fontSize: 9-10px
  mono:
    fontFamily: 'Consolas, Courier New, monospace'
    note: 'Dado técnico literal — HWID, versão de driver, chave de licença'
rounded:
  sm: 6px
  md: 8px
  lg: 12px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
components:
  card:
    background: 'linear-gradient(180deg, {colors.card-gradient-top}, {colors.card-gradient-base})'
    border: '1px solid {colors.border-card}'
    rounded: '{rounded.md}'
  button-primary:
    background: '{colors.accent}18'
    foreground: '{colors.accent}'
    border: '1px solid {colors.accent}40'
  section-header:
    accent-bar: '3px solid {colors.accent}'
    title: '{typography.heading}, color {colors.text-label}'
  alert-banner:
    background: '{semantic-color}15 a {semantic-color}18'
    border: '1px solid {semantic-color}40 a 60'
    rounded: '{rounded.sm} a {rounded.md}'
    icon: 'emoji à esquerda, cor {semantic-color}'
    note: 'Sempre inline no scroll — nunca modal/popup. Padrão já usado em BiosGuideView, InfoSistemaView, VidaUtilView.'
  confirmation-panel:
    background: '{colors.bg-flat}'
    border: '2px solid {semantic-color}, geralmente {colors.status-warning} ou {colors.status-critical}'
    rounded: '{rounded.md}'
    note: 'NOVO — não existe hoje no app (achado da investigação: zero diálogos de confirmação em todo o codebase). Eleva o AlertBanner a estado bloqueante: painel full-width inline, botão de ação primário desabilitado até o usuário confirmar explicitamente. Nunca um modal — mantém a convenção "tudo inline no scroll" já estabelecida.'
---

## Brand & Style

Otimize Builder fala a língua de quem monta o próprio PC — não a de um software corporativo de TI. É a mesma pessoa que decide se joga ou trabalha na máquina hoje ({personas Rafael/Carla}, PRD §2.1), então a interface nunca finge ser dois produtos diferentes: é uma ferramenta técnica, direta, no registro "cyberpunk/tech" — fundo quase preto, um único acento ciano neon, tipografia em CAPS com letter-spacing largo para rótulos. É tom de CPU-Z/HWMonitor com copy mais cuidada, nunca tom de SaaS corporativo, nunca tom de propaganda.

Honestidade é estética, não só regra de produto (PRD §1: "sempre honesta sobre o caminho mais barato primeiro"). Isso significa: nada de badge festivo para uma recomendação de compra, nada de urgência artificial ("últimas unidades!"), nada de verde comemorativo pra fechar venda. O peso visual vai pro dado, não pro call-to-action.

## Colors

O app é **dark-only** — não existe modo claro, e este módulo não introduz um. `{colors.bg-root}` é o chão; `{colors.card-gradient-top}`→`{colors.card-gradient-base}` é o card padrão, repetido em toda tela existente e herdado aqui sem alteração.

`{colors.accent}` (ciano `#00C8FF`) é a única cor de marca — usada em barra de destaque de cabeçalho, item de navegação ativo, botão primário (sempre como fundo translúcido `accent+18`, nunca sólido). Este módulo usa `{colors.accent}` para o CTA de abrir a Vitrine e para a barra de progresso do Núcleo de Atualização — nunca para o preço ou para o botão de compra da loja parceira, que fica neutro (ver Do's and Don'ts).

`{colors.premium}` (dourado) é reservada para Assinatura Premium (Fase 2 do PRD) — este módulo V1 não a usa, já que a Vitrine V1 monetiza só por comissão.

Cores de status (`success`/`warning`/`critical`) seguem o uso já estabelecido: `success` para "sem gargalo"/"tudo atualizado", `warning` para recomendação de atenção não-crítica (driver desatualizado, pasta térmica a trocar), `critical` reservado para BSOD recorrente e para o alerta obrigatório de risco de BIOS (FR-3) — é a única situação nova que usa `critical` com painel bloqueante, não banner informativo.

## Typography

Sem fonte customizada — o app usa a fonte padrão do Windows (Segoe UI), e este módulo não introduz uma nova. `{typography.mono}` (Consolas) é reservada a dado técnico literal — versão de driver, versão de BIOS, HWID — reaproveitando o padrão já usado em `DriversView`/`ConfiguracoesView`.

O padrão "`{typography.label}` em CAPS apagado acima de `{typography.display}` em cor clara" é o idioma dominante do app e este módulo o herda para: a linha `Otimização do S.O. = X%` / `Upgrade hardware = X%` do Relatório de Resultado (rótulo CAPS pequeno + percentual grande), e a nota "PROVA" vs. "ESTIMATIVA" (ver Components).

## Layout & Spacing

Escala herdada sem alteração: `{spacing.1}`–`{spacing.6}` (4/8/12/16/24/32px). Cards usam `{spacing.4}` de padding interno; separação entre cards de uma mesma seção usa `{spacing.3}`; entre seções, `{spacing.5}`–`{spacing.6}`.

A Vitrine reaproveita o layout de 3 colunas já usado em `UpgradeView.axaml` (diagrama | lista de peças | chat) como ponto de partida, mas isso é decisão de EXPERIENCE.md (Component Patterns), não de token visual.

## Elevation & Depth

Nenhuma sombra no app hoje — profundidade vem só do gradiente sutil `{card-gradient-top}`→`{card-gradient-base}` e da borda `{colors.border-card}`. Este módulo não introduz elevação/sombra; o painel de confirmação bloqueante (novo) se distingue por borda de 2px na cor semântica, não por sombra.

## Shapes

`{rounded.sm}` (6px) para banners/alertas pequenos. `{rounded.md}` (8px) é o raio padrão de card, herdado sem alteração. `{rounded.lg}` (12px) para painéis maiores — reservado para o novo painel de confirmação bloqueante, que precisa se distinguir visualmente de um card comum.

## Components

- **Card** — gradiente + borda, herdado sem alteração (ver frontmatter `components.card`). Usado para cada item da Vitrine e para o resumo de Diagnóstico de Manutenção.
- **Alert Banner** (herdado) — banner informativo inline, nunca modal. Reaproveitado sem alteração para avisos não-bloqueantes (ex.: "driver desatualizado detectado").
- **Confirmation Panel** (NOVO) — não existe hoje no app; é o achado central da investigação (zero diálogos de confirmação em todo o codebase). Eleva o Alert Banner a estado bloqueante: painel full-width inline no fluxo de scroll, borda 2px na cor semântica, botão de ação primário **desabilitado** até o usuário marcar aceite explícito. Usado em: alerta de risco de BIOS (FR-3, borda `critical`), aprovação de atualização de driver/software (FR-4/FR-6, borda `accent` ou `warning` conforme risco). Nunca um modal/popup — mantém a convenção "tudo inline" já estabelecida no resto do app.
- **Estimate Tag** (NOVO) — pequeno rótulo/pill junto a qualquer número de "ganho estimado" (FR-14/FR-19), distinto visualmente do número de "prova" do Núcleo de Atualização. Usa `{typography.caption}` em `{colors.text-secondary}`, nunca a cor de destaque — o objetivo é ser discreto o suficiente pra não competir com o número, mas presente o suficiente pra nunca ser confundido com prova medida.
- **Vitrine Item Card** (NOVO) — variação de Card com: nome da peça, `{typography.mono}` para specs técnicas, preço em destaque neutro (não `accent`, não `success` — ver Do's and Don'ts), badges de confiança (parcelamento/entrega/loja) de FR-18 como `{typography.caption}` discretos, nunca badges festivos.

## Do's and Don'ts

- **Faça** reaproveitar o vocabulário visual do Alert Banner para qualquer aviso novo — não inventar um segundo padrão de banner.
- **Faça** manter todo painel de confirmação inline no scroll, nunca como modal/popup — é a convenção estabelecida em 100% das telas existentes.
- **Faça** usar `{typography.mono}` para qualquer versão/identificador técnico novo (versão de BIOS, benchmark ID), igual ao padrão já usado para driver/HWID.
- **Não** use `{colors.accent}` (ciano) no preço ou no botão de compra da Vitrine — a cor de marca comunica "recurso do app", não "oferta". Preço fica em `{colors.text-primary}` neutro.
- **Não** use `{colors.status-success}` (verde) para celebrar uma recomendação de peça cara — é a cor reservada para "sem problema encontrado"; usá-la numa venda cria viés visual de que comprar é sempre a resposta certa, o oposto do princípio de honestidade do PRD.
- **Não** crie um segundo padrão de modal — o app não usa popup em lugar nenhum; todo bloqueio de fluxo é painel inline (Confirmation Panel).
- **Não** misture o `Estimate Tag` com o número de prova do Núcleo de Atualização — são visualmente distintos de propósito (FR-14/FR-19 exigem essa distinção de rigor).

**Risco conhecido, não resolvido nesta sessão:** o app não tem nenhum suporte de acessibilidade hoje (sem `AutomationProperties`, sem ajuste de contraste/fonte, vários rótulos usam contraste propositalmente baixo). Este módulo herda essa limitação sem agravá-la, mas não a resolve — ver `EXPERIENCE.md`, Accessibility Floor.
