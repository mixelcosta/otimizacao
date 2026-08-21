# Addendum — Módulo de Sugestão de Upgrade com Foco em Custo-Benefício

Material de apoio ao PRD: profundidade técnica, matrizes de decisão e alternativas rejeitadas que não pertencem à narrativa do PRD, mas servem de referência para arquitetura, UX e histórias de desenvolvimento. Fonte completa: `docs/brainstorming-session-results.md`.

**A.** Auditoria técnica (lacunas L1-L6) · **B.** Morphological Analysis (parâmetros, combinações, convergência) · **C.** Personas — profundidade completa · **D.** Decisões estruturais herdadas · **F.** Detalhe das suposições do PRD

## A. Auditoria técnica original (lacunas, não bugs)

Levantada antes da ideação, sobre o artefato `otimizacao-0.1.0` (182 arquivos, ~27.400 linhas, 160 testes verdes):

| # | Lacuna | Evidência | Relevância para este PRD |
| --- | --- | --- | --- |
| L1 | Sugestão de upgrade não existe | Zero ocorrências de `upgrade`/`gargalo`/`bottleneck` em código, docs e schemas | É o que este módulo inteiro resolve |
| L2 | Custo-benefício não existe como conceito | Sem preço, orçamento, moeda ou retorno sobre investimento. `CalculadoraScore` mede estado atual, não oportunidade | Base para FR-12 a FR-18 (Feature 4.3, Vitrine de Upgrade) |
| L3 | Notebook não é classe de dispositivo | Uma única menção solta no guia de BIOS. `Inventario` não tem chassi, bateria, TDP nem estado de energia | Pré-requisito técnico para FR-17 (caminho de conversão do notebook) |
| L4 | Armazenamento ausente do inventário | `Inventario` cobre placa, CPU, memória, GPU, SO e rede — sem disco | Bloqueia FR-12 (Teto de Compatibilidade) para SSD. Ver nota abaixo. |
| L5 | Catálogo não toca hardware | 8 ações, todas de SO/energia/driver/serviços/rede. Categorias `Cpu` e `Memoria` existem no enum mas estão vazias | Bloqueia FR-12/FR-13 (recomendação de peça) |
| L6 | Event Log não é coletado | O coletor lê estado, nunca história. Sem BSOD/WHEA/crashes | Resolvida como requisito em FR-4 |

**Nota (L4):** a pergunta original do brainstorming era se SSD, mesmo fora do inventário hoje, já seria "provavelmente a melhor recomendação de custo-benefício disponível" — usar como critério de priorização ao resolver esta lacuna, não só como bloqueio técnico neutro.

## B. Morphological Analysis — parâmetros e combinações completas

### Parâmetros mapeados

| ID | Parâmetro | Opções (~~descartadas~~) |
| --- | --- | --- |
| P1 | O que é recomendado | peça interna · ~~periférico/acessório~~ · cooler/refrigeração · fonte · nada (só otimização) · serviço (limpeza, pasta térmica) |
| P2 | Qual dor é atacada | instabilidade/BSOD · temperatura · boot lento · FPS · tempo de render · travamento em uso |
| P3 | Quando o app age | antes (diagnóstico) · durante (opt-in) · depois (relatório) · contínuo (por execução) |
| P4 | Como a prova é entregue | % em jogo nomeado · temperatura antes/depois · tempo economizado · nota 0-100 · ~~dinheiro poupado~~ · ausência de travamento no tempo |
| P5 | Quem decide | app recomenda e usuário aprova · app só informa · usuário pergunta · ~~app age sozinho~~ |
| P6 | Como monetiza | comissão de loja · assinatura · gratuito com tier pago · licença B2B · sem monetização direta |
| P7 | Classe de máquina | desktop montado · desktop pronto · notebook pessoal · notebook corporativo |
| P8 | Estado de manutenção | data da última limpeza · data da troca de pasta térmica · idade do equipamento · horas de uso · nunca informado |

### Combinações avaliadas

| # | Nome | Composição | Veredito | Onde foi para o PRD |
| --- | --- | --- | --- | --- |
| A | Diagnóstico de manutenção | P1 serviço + P2 temperatura + P3 antes + P4 antes/depois °C + P8 pasta | ✅ É o produto | Feature 4.2 |
| B | Guardião da live | P3 durante + P5 app age sozinho | ❌ Rejeitada por princípio arquitetural | Não entra — viola FR-6 |
| C | A conta fechada | P1 peça + P2 FPS + P4 % em jogo + P6 comissão | ✅ É o produto (com métrica podada) | Feature 4.3 |
| D | Histórico que vira prova | P3 contínuo + P4 ausência de travamento + P8 idade | ✅ Aprovada | Parcialmente em FR-4/FR-5 (captura); narrativa histórica completa é polish de Fase 2. Ver nota abaixo. |
| E | Manutenção como upgrade de entrada | P1 serviço + P8 nunca informado + vitrine | ✅ Aprovada | Refletida em FR-10 (ordem por custo) |
| F | Perfil de uso declarado (reformulada) | P2 e P4 definidos pelo usuário → varre e anuncia | 🔄 Reformulada | Refletida em FR-1/FR-5 (varredura e atribuição de causa, não pergunta) |

**Nota de UX (Fase 2, combinação D):** exemplo de copy já validado na sessão — *"desde a otimização de 12/08, zero travamentos. Antes: 4 em duas semanas."* Preservar esse tom quando a narrativa histórica completa for implementada.

### Por que B foi rejeitada

Testado literalmente: *"não faço nenhuma alteração sem o aceite do usuário"* — está codificado e coberto por teste no repositório (`otimizacao`). Qualquer combinação que dependa de P5 = "app age sozinho" é inviável por arquitetura, não por preferência de produto. Isso inclui qualquer ideia futura de automação em tempo real durante uma sessão de jogo/live.

### Por que "dinheiro poupado" foi descartado de P4

Avaliado pelo próprio participante como "retórica bonita sem lastro" — exigiria um contrafactual que o sistema não mede (quanto o usuário teria gasto numa peça errada, hipoteticamente). Vale revisitar apenas se o produto ganhar uma base de dados de preços históricos confiável.

### Convergência não formalizada

A sessão chegou a propor um agrupamento por Affinity Clustering (3 clusters: Trilha grátis / Prova e confiança / Vitrine e monetização), mas o participante pulou direto para o PRD antes de validar o agrupamento ou rodar MoSCoW. A ordem de escopo do V1 (§8 do PRD) vem do ranking que emergiu do One Feature Only, não de uma convergência formal — revisitar com MoSCoW se novos candidatos de escopo aparecerem.

## C. Personas — profundidade completa (Role Playing)

*Usar como referência de tom/voz para UX; os JTBDs e UJs do PRD são a versão destilada.*

**Rafael (gamer/desktop):**
- Requisitos de confiança na compra: local seguro, produto de qualidade, entrega rápida, parcelamento. → promovidos a FR-18 (Requisitos de confiança na listagem da Vitrine) após a reconciliação com o brainstorming original.
- Dores nomeadas: tela azul, travamento em jogos e programas pesados, boot lento, altas temperaturas de CPU e GPU.
- "Custo" é exclusivamente o dinheiro gasto na peça — não inclui tempo, risco ou incerteza.
- O usuário clica no link porque confia no software — a confiança é transferida do app para o vendedor. Isso é o motivo pelo qual a curadoria de Loja Parceira (FR-16) é estrutural, não decorativa.

**Carla (editora/notebook, também faz live):**
- Mede sucesso em FPS, velocidade e resposta rápida aos comandos — mas a dor real dela é instabilidade, não velocidade bruta ("pede meio, mede fim").
- No travamento ao vivo hoje, a única saída é reiniciar a máquina ou matar processo no gerenciador de tarefas — o app está ausente do momento de maior dor.
- Quer overclock, "mas sem comprometer nenhum equipamento" — validação espontânea, não induzida, da filosofia já codificada no projeto ("maior desempenho sustentável e validado, nunca o maior desempenho possível").
- **Nuance de rationale:** o app fica ausente do "durante" — o momento exato da live, quando o travamento dói mais — porque a sessão pesou os dois lados e decidiu que prevenção antecipada supera intervenção ao vivo ("reiniciar no meio da live é derrota pública; prevenir vale mais que qualquer ganho de FPS"). Decisão de produto, não lacuna técnica — registrar para que ninguém reabra "monitoramento durante a live" como pendência.

**Diego (leigo):**
- ⚠️ Ressalva metodológica do brainstorming original: as respostas de Diego vieram *de fora*, descrevendo o personagem, não vestindo a pele — tratar como hipótese a validar com usuário real, não como achado confirmado. UJ-3 do PRD carrega essa mesma ressalva.
- Curva emocional completa a preservar em UX: **felicidade e expectativa** ao ver a tela de inventário → só depois vem o **medo** de não conseguir voltar atrás → **alívio** ao ver que dá pra desfazer. A tela de inventário deve ser desenhada para ser empolgante, não só tranquilizadora — reduzir a curva de 3 estados a só medo/alívio perde a primeira metade dela.

**Bruno (entusiasta, ainda não explorado):**
- Não é só "mais uma persona a validar depois" — é, pela própria leitura da sessão original, **o crítico público mais provável do produto** (review, fórum, influencer de tecnologia) caso o Eixo de Qualidade ou o Teto de Compatibilidade sejam rasos, já que ele provavelmente já está no teto de tudo. Esse framing de risco pode justificar puxar a exploração de Bruno para antes do lançamento, não depois — reflexo disso já está na Questão em Aberto 12 do PRD.

## D. Decisões estruturais herdadas (não redecidir)

Estas já estavam travadas antes da concepção deste módulo e são citadas no PRD por referência — não abrir de novo sem motivo forte:

- P5 tem opção proibida por arquitetura: nenhuma alteração sem aceite do usuário (→ FR-6, §5 do PRD).
- Monitoramento em tempo real é opt-in: só com o app aberto e mediante solicitação explícita — sem daemon, sem vigilância em background (→ §5 do PRD).
- Vitrine separada do diagnóstico (Caminho 2): existe sempre e para qualquer máquina, mas em aba própria, nunca no fluxo de diagnóstico ou na tela de resultado (→ FR-15).
- Régua de produto: perguntar dado factual inalcançável pelo sistema é aceitável; perguntar "qual é o seu problema?" não é (→ FR-9).

## F. Detalhe das suposições `[ASSUMPTION]` do PRD

Índice completo — o PRD (§11) traz só a lista resumida, que aponta para cá:

- §2.2 (Não-Usuários) — Não-usuários incluem frotas corporativas geridas centralmente; não avaliado na sessão de brainstorming.
- §2.3 / UJ-3 (jornada de Diego) — hipótese a validar com usuário real, não achado confirmado (ressalva já registrada no brainstorming original — as respostas de Diego vieram *de fora*, descrevendo o personagem, não vestindo a pele).
- §6 (Monetização) — definição de quais módulos ficam atrás do paywall da Assinatura Premium ainda não existe; tratada como placeholder de receita futura, precisa evitar contradizer a honestidade já validada como princípio do produto.
- §9 / SM-1 e SM-4 (Métricas de Sucesso) — metas numéricas ainda não definidas; produto em fase de exploração, a calibrar com os primeiros dados reais.
