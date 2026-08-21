---
name: Otimize Builder — Módulo de Upgrade
status: final
created: 2026-08-21
updated: 2026-08-21
sources:
  - docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/prd.md
  - docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/addendum.md
  - docs/planning-artifacts/architecture/architecture-otimizacao-2026-08-21/ARCHITECTURE-SPINE.md
---

# Otimize Builder — Experience Spine (Módulo de Upgrade)

## Foundation

Desktop Windows, janela única (`ShellWindow`), Avalonia + CommunityToolkit.Mvvm. `DESIGN.md` é a referência de identidade visual — dark-only, sem modo claro. Views são pré-instanciadas e trocadas por `IsVisible`, não por stack de navegação real; este módulo segue o mesmo padrão. Usuário único por instalação, sem multi-tenant.

Hoje, `Upgrade`, `Vida Útil`, `Drivers` e `Guia BIOS IA` aparecem na sidebar sob uma seção "Premium", bloqueados por cadeado (`nav-locked`) sem assinatura ativa. O PRD trata Núcleo de Atualização e Diagnóstico de Manutenção como **Trilha Grátis** — a tese central do produto (T3 resolvida no brainstorming: "grátis antes de pago é escolha do usuário, nunca gate do app"). `[ADOPTED]` **Drivers, Guia BIOS IA e a aba de visualização da Vitrine saem do bloqueio Premium no V1** — a compra em si é paga (comissão da loja), mas ver a recomendação não é. `Vida Útil` não faz parte deste módulo e mantém seu status atual.

## Information Architecture

| Superfície | Alcançada de | Propósito |
| --- | --- | --- |
| Núcleo de Atualização | Sidebar → "Drivers" (renomeada/expandida) | Varredura de drivers/software/BIOS, lista de desatualizados, correlação com Event Log (FR-1–FR-5) |
| Confirmação de Atualização | A partir de um item do Núcleo de Atualização | Confirmation Panel — aprovação explícita antes de aplicar (driver) ou alerta de risco antes de orientar (BIOS) (FR-3, FR-4, FR-6) |
| Diagnóstico de Manutenção | Sidebar → seção nova, ou sub-aba de Info Sistema | Assinatura térmica, pergunta factual de manutenção, recomendação de serviço (FR-8–FR-11) |
| Relatório de Resultado | Ao final de qualquer diagnóstico (Núcleo, Manutenção ou scan geral) | As duas linhas factuais — `Otimização do S.O. = X%` / `Upgrade hardware = X%` — único ponto de entrada para a Vitrine (FR-14, FR-15) |
| Vitrine de Upgrade | Clique na linha `Upgrade hardware` do Relatório, ou Sidebar → "Upgrade" | Sugestões de peça com Teto de Compatibilidade, Eixo de Qualidade, lojas parceiras (FR-12, FR-13, FR-16–FR-19) |

A Vitrine nunca é renderizada dentro do fluxo de Núcleo de Atualização/Diagnóstico ou do próprio Relatório de Resultado — só como destino de navegação a partir do clique (herdado do PRD, Glossário "Vitrine"). Modal stacks não existem no app (nenhum popup em lugar nenhum); todo bloqueio de fluxo é painel inline, nunca uma superfície empilhada sobre outra.

## Voice and Tone

Microcopy. Voz de marca e postura estética vivem em `DESIGN.md`. Tom herdado do app: direto, técnico, factual — nunca alarmista, nunca vendedor.

| Faça | Não faça |
| --- | --- |
| "Driver de vídeo desatualizado — pode ser a causa dos travamentos" | "⚠️ ALERTA! Seu driver está PERIGOSAMENTE desatualizado!" |
| "Otimização do S.O. = 18% · Upgrade hardware = 34%" | "Ganhe até 34%!! Veja como 🚀" |
| "A interrupção durante a gravação de BIOS pode comprometer o funcionamento da placa-mãe. Recomendamos um profissional qualificado." | "Cuidado! Isso pode destruir seu computador!" |
| "R$ 40 — troca de pasta térmica" ao lado de "R$ 2.100 — GPU" sem comentário adicional | "Economize milhares trocando só a pasta térmica!" |
| "Sem cobertura de benchmark para esta peça" (quando a base não tem dado) | Inventar um percentual aproximado |

## Component Patterns

Comportamental. Especificação visual vive em `DESIGN.md.Components`.

| Componente | Onde | Regras comportamentais |
| --- | --- | --- |
| Confirmation Panel | Núcleo de Atualização (driver), risco de BIOS | Aparece inline, substitui o botão de ação até resolvido. Driver: checkbox "Entendi, aplicar atualização" habilita o botão primário. BIOS: o alerta de risco (FR-3) é exibido **toda vez**, mesmo que o usuário já tenha visto antes — nunca "não mostrar de novo". Botão de ação do BIOS nunca executa a gravação — sempre abre o guia de orientação (FR-3, Out of Scope). |
| Estimate Tag | Relatório de Resultado, Vitrine | Pill discreto junto de qualquer `GanhoEstimado` (contrato `Core`, ver espinha AD-3) mostrando `MargemConfiança` e `AtualizadoEm`. Nunca aparece sozinho — sempre colado ao número que qualifica. |
| Pergunta Factual | Diagnóstico de Manutenção | Uma pergunta por vez, sempre com formato de data (nunca campo de texto livre nem múltipla escolha de sintoma). Pergunta só uma vez por item; resposta anterior pré-preenchida se já existir. |
| Vitrine Item Card | Vitrine | Preço em destaque neutro (`DESIGN.md` Do's/Don'ts). Badges de confiança (parcelamento, prazo, "vendido por {Loja Parceira}") sempre visíveis, nunca escondidos atrás de hover — reflete FR-18 direto. |
| Linha do Relatório | Relatório de Resultado | As duas linhas (`Otimização do S.O.` / `Upgrade hardware`) sempre lado a lado, nunca uma acima escondendo a outra — reflete a decisão do PRD §1 de que a ordem é escolha do usuário, não gate do app. Clique só é ativo na linha `Upgrade hardware`; a linha `Otimização do S.O.` não navega (a ação dela já aconteceu no Núcleo de Atualização). |

## State Patterns

| Estado | Superfície | Tratamento |
| --- | --- | --- |
| Sem cobertura de benchmark | Vitrine, linha `Upgrade hardware` | Linha omitida por completo (FR-14/FR-19) — nunca "N/A" ou zero, que pareceria erro em vez de ausência de dado. |
| Sem correlação causa-raiz | Relatório, item do Núcleo de Atualização | Mostra o achado (driver desatualizado) sem atribuir causa — nunca inventa "provavelmente é isso" (FR-5). |
| RAM soldada (notebook) | Vitrine, conversão de notebook | Some a sugestão de peça de RAM; mostra só o caminho de otimização de software, sem espaço vazio no lugar da peça que não existe (FR-17). |
| Máquina no Teto de Compatibilidade | Vitrine | Nunca fica sem nenhuma sugestão — cai para o Eixo de Qualidade (RAM de menor latência, GPU mais fria) em vez de mostrar tela vazia (FR-13). |
| Aguardando resposta do usuário (Confirmation Panel) | Núcleo de Atualização, BIOS | Botão de ação primário fica visivelmente desabilitado (não escondido) até a condição de aceite ser cumprida — o usuário sempre vê o que falta. |
| Primeira vez vs. já visto | Tela de inventário (Diego, UJ-3) | Primeira leitura da tela de inventário favorece o tom "felicidade e expectativa" (headline maior, sem nenhum aviso de risco visível ainda) — avisos aparecem conforme o scan encontra achados, não todos de uma vez no topo. |

## Interaction Primitives

Mouse-first — é um app desktop de produtividade técnica, não um app com atalho de teclado como hábito estabelecido hoje (a investigação não encontrou nenhum `KeyBinding`/atalho customizado no codebase). Este módulo não introduz atalhos novos.

- Clique único em qualquer card de item do Núcleo de Atualização expande detalhe/ação — reaproveita o padrão já usado nas telas de Drivers/Otimizador Windows.
- Scroll vertical único por tela — sem paginação, sem infinite scroll (o app inteiro já é `ScrollViewer` vertical simples).
- Nenhum modal, nenhum popup, em lugar nenhum — reforçado explicitamente porque é a única convenção que este módulo poderia quebrar por engano ao introduzir o Confirmation Panel.

## Accessibility Floor

Comportamental. Contraste visual vive em `DESIGN.md`.

- `[NOTE FOR UX]` **Piso real hoje é baixo** — o app não tem nenhum suporte de acessibilidade (sem `AutomationProperties`, sem ajuste de contraste/fonte). Este módulo não piora esse piso, mas também não o eleva — não é escopo do PRD atual.
- Único compromisso novo: o Confirmation Panel de risco de BIOS (FR-3) usa cor **e** texto para comunicar severidade (nunca só borda vermelha) — é o painel com maior consequência de erro do módulo inteiro, então não pode depender só de percepção de cor.
- Textos de erro/aviso (Voice and Tone) são sempre frase completa, nunca só ícone — mantém o padrão já usado no app (`InfoSistemaView`, `VidaUtilView`).

## Key Flows

Mirror direto das Jornadas do PRD (§2.3) — aqui só o beat de UX que cada uma força.

- **Rafael — do travamento à decisão informada** (= UJ-1 do PRD). Climax de UX: a linha `Otimização do S.O.` aparece **antes** da linha `Upgrade hardware`, na mesma tela, mesmo tamanho — o contraste é o que faz o trabalho de convencer, não hierarquia visual forçada.
- **Carla — otimizar sem medo antes da live** (= UJ-2 do PRD). Climax de UX: o Confirmation Panel mostra o botão de rollback **na mesma tela** onde ela aprovou a mudança — nunca em um menu separado que ela precisaria descobrir.
- **Diego — aceitar porque pode desfazer** (= UJ-3 do PRD). Climax de UX: a tela de inventário abre em tom de descoberta (títulos grandes, sem nenhum banner de risco visível ainda) — os achados de manutenção aparecem conforme o scan progride, não como uma lista de problemas já pronta esperando por ele.

## Open Items

1. Onde exatamente o Diagnóstico de Manutenção vive na IA — aba nova ou sub-seção de Info Sistema — não foi decidido; ambos são plausíveis dado o padrão de tela já existente.
2. Nenhum mock/wireframe foi produzido nesta rodada (Fast path, sem ferramentas criativas) — Confirmation Panel e Vitrine Item Card são as duas superfícies com maior risco de ambiguidade visual sem um mock de referência.
