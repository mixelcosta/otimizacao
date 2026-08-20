# Brainstorming Session Results

**Session Date:** 2026-08-07 (parte 1) · 2026-08-18 (parte 2) · 2026-08-19 (parte 3, iniciada e pausada)
**Facilitator:** Business Analyst Mary 📊
**Participant:** Michel Filipe
**Status:** ⏸️ **SESSÃO PAUSADA — Parte 3 de 3, em andamento.** Retomar dentro da Técnica 3 (Assumption Reversal), na primeira premissa. Ver [Ponto exato de parada](#ponto-exato-de-parada) abaixo.
**Memlog desta parte:** `docs/brainstorming/brainstorm-upgrade-custo-beneficio-2026-08-19/.memlog.md`

---

## Executive Summary

**Topic:** Concepção do módulo de **sugestão de upgrade com foco em custo-benefício** para o projeto Agente de Otimização e Confiabilidade de Hardware (`otimizacao`, v0.1.0).

**Session Goals:** Ideação focada. Módulo tratado como **feature nova, concepção do zero** (confirmado pelo participante).

**Público-alvo declarado:** pessoas que usam computadores e notebooks para **jogos e trabalho**.

**Premissa de negócio declarada:** monetização por **parceria com lojas**, com receita vinda das vendas de hardware recomendadas pelo app.

**Techniques Planned:** Role Playing (#16) → Morphological Analysis (#12) → Assumption Reversal (#15) → Resource Constraints (#18).

**Techniques Completed:** Role Playing ✅ · Morphological Analysis ✅ · Assumption Reversal 🔄 **em andamento** (iniciada, 0 de 6 premissas respondidas)

**Total Ideas Generated:** 38 achados registrados.

### Key Themes Identified

- O usuário não pede desempenho — pede **confiança na máquina** e **fechamento de conta**.
- **Reversibilidade é requisito universal**: as três personas citaram, espontaneamente, medo de não conseguir voltar atrás.
- **Gamer e profissional são a mesma pessoa** em momentos diferentes do dia.
- A honestidade do produto se prova **recomendando o mais barato primeiro**, não declarando boa intenção.
- O app decide por **fato coletado**, nunca por diagnóstico perguntado.

---

## Contexto: Auditoria do Repositório

Análise do artefato `otimizacao-0.1.0` (182 arquivos, ~27.400 linhas, 160 testes verdes, build com warnings como erros).

**Avaliação:** projeto maduro em engenharia. Fases 0–10 entregues. Regras invariantes de segurança codificadas *e cobertas por teste*. Guard contra alucinação de LLM, rollback por categoria, sanitização antes da nuvem, modo simulação como padrão.

**Diagnóstico central:** o projeto está **engenheirado demais para o produto que é hoje** e **sub-especificado para o produto descrito pelo participante**.

### Lacunas encontradas (ausências de escopo, não bugs)

| # | Lacuna | Evidência |
| --- | --- | --- |
| L1 | **Sugestão de upgrade não existe** | Zero ocorrências de `upgrade`/`gargalo`/`bottleneck` em código, docs e schemas. Ausente do roadmap de 12 fases. |
| L2 | **Custo-benefício não existe como conceito** | Sem preço, orçamento, moeda ou retorno por investimento. `CalculadoraScore` mede **estado atual**, não **oportunidade**. |
| L3 | **Notebook não é classe de dispositivo** | Uma única menção, em texto solto no guia de BIOS. `Inventario` não tem chassi, bateria, TDP nem estado de energia. |
| L4 | **Armazenamento ausente do inventário** | `Inventario` cobre placa, CPU, memória, GPU, SO e rede. Sem disco. |
| L5 | **Catálogo não toca hardware** | 8 ações, todas de SO/energia/driver/serviços/rede. Categorias `Cpu` e `Memoria` existem no enum mas estão vazias. |
| L6 | **Event Log não é coletado** | O coletor lê **estado**, nunca **história**. Sem BSOD/WHEA/crashes, não há evidência factual de instabilidade. ⭐ *identificada na parte 2* |

---

## Technique Sessions

### Role Playing (#16) — ✅ concluída

**Description:** O participante brainstormeia em primeira pessoa, na pele de diferentes perfis de usuário.

**Elenco:** Rafael (gamer/desktop) ✅ · Carla (editora/notebook) ✅ · Diego (leigo) ✅ · Bruno (entusiasta) ⏭️ dispensado

---

#### 🎮 Persona 1 — Rafael

**Ideas Generated:**

1. A pergunta espontânea ao ver o resultado é **"valeu a pena? Qual foi meu ganho vs. meu custo?"**
2. "Custo" é **exclusivamente o dinheiro gasto na peça**.
3. A prova exigida: **"+X% no jogo XPTO"** — percentual E carga de trabalho nomeada.
4. Requisitos de confiança na compra: **local seguro, produto de qualidade, entrega rápida, parcelamento**.
5. O app deve listar **somente vendedores de e-commerce conceituados** — curador de vendedor.
6. Dores reais: **tela azul, travamento em jogos e programas pesados, boot lento, altas temperaturas de CPU e GPU**.
7. Ser **contrariado na primeira tela** gera **alívio**, não frustração.
8. O que separa recomendação honesta de propaganda é **a evidência numérica**. Com número, diagnóstico. Sem número, anúncio.
9. O usuário clica no link **porque confia no software** — a confiança é transferida do app para o vendedor.

**Insights Discovered:**

- **"Valeu a pena?" é pergunta de contabilidade, não de performance.** O módulo não é um recomendador de peças, é um **prestador de contas**.
- **O usuário chegou pedindo desempenho e descreveu instabilidade.** Nenhuma das quatro dores é problema de velocidade.
- **O gargalo de confiança está na loja, não na recomendação.** O risco percebido é comercial, não técnico.
- **Franqueza é recurso de produto, não risco.**

---

#### 💻 Persona 2 — Carla

**Ideas Generated:**

10. O medo dela não é técnico, é econômico: **danificar o equipamento e perder dinheiro por não poder trabalhar**. Custo medido em **dia parado**, não em peça.
11. A vitória dela: **SO mais rápido e leve, sem programas e processos atrapalhando** — exatamente o que o catálogo atual já faz.
12. Ela mede sucesso em **FPS, velocidade e resposta rápida aos comandos**. *Pede meio, mede fim.*
13. Para deixar o app agir: **relatório demonstrando os ganhos** + **arquivo de backup com rollback de tudo**.
14. A culpa do travamento é "**do computador por completo**" — ela se descreve como **usuária básica que não faz manutenção de rotina**.
15. **Ela faz live jogando e tira renda extra disso.**
16. No travamento ao vivo, a saída é **reiniciar a máquina ou matar processo no gerenciador de tarefas**.
17. Ela quer **overclock** — *"mas sem comprometer nenhum equipamento"*.

**Insights Discovered:**

- ⭐ **Gamer e profissional não são dois públicos.** São a mesma pessoa em dois momentos do dia. O público-alvo declarado na abertura não era união de segmentos: era descrição de uma pessoa só.
- **A live dissolve a T2.** Com renda de stream, a Carla tem justificativa econômica de compra, dor de estabilidade *em público*, e é canal de aquisição por si só.
- ⭐ **A persona mais avessa a risco reinventou a filosofia do projeto.** "Overclock sem comprometer" é, palavra por palavra, o *"maior desempenho sustentável e validado, nunca o maior desempenho possível"* já codificado no repositório. A proposta de valor não precisa ser vendida — já é o que o usuário quer.
- **O momento de maior dor é *durante*, e o app está ausente dele.** Tudo que existe é antes (diagnóstico) ou depois (relatório) — apesar de a leitura de sensores em tempo real já estar pronta.
- **Reiniciar no meio da live é derrota pública.** Prevenir vale mais que qualquer ganho de FPS.

---

#### 🖥️ Persona 3 — Diego

**Ideas Generated:**

18. Olhando a tela técnica de inventário, sente **felicidade e expectativa de melhorar a máquina**.
19. O que o impede de aceitar: **não conseguir voltar atrás** após um ajuste no SO.
20. Ele **compra** a peça recomendada, pois busca melhor desempenho.

**Insights Discovered:**

- ⭐ **Reversibilidade apareceu nas três personas.** Rafael queria evidência, Carla pediu backup com rollback, Diego teme não poder desfazer. Deixou de ser preferência e virou **requisito universal** — e já está construído, mas invisível ao usuário.
- ⚠️ **Ressalva metodológica:** as respostas do Diego vieram *de fora*, descrevendo o personagem, não vestindo a pele — e descrevem o usuário ideal (feliz, confiante, comprador). **Tratar como hipótese a validar com usuário real, não como achado.**

---

### Morphological Analysis (#12) — ✅ concluída

**Description:** Decomposição do módulo em parâmetros independentes, seguida de combinação sistemática.

#### Parâmetros mapeados

| ID | Parâmetro | Opções |
| --- | --- | --- |
| P1 | O que é recomendado | peça interna · ~~periférico/acessório~~ · cooler/refrigeração · fonte · nada (só otimização) · serviço (limpeza, pasta térmica) |
| P2 | Qual dor é atacada | instabilidade/BSOD · temperatura · boot lento · FPS · tempo de render · travamento em uso |
| P3 | Quando o app age | antes (diagnóstico) · durante (opt-in) · depois (relatório) · contínuo (por execução) |
| P4 | Como a prova é entregue | % em jogo nomeado · antes/depois de temperatura · tempo economizado · nota 0-100 · ~~dinheiro poupado~~ · ausência de travamento no tempo |
| P5 | Quem decide | app recomenda e usuário aprova · app só informa · usuário pergunta · ~~app age sozinho~~ |
| P6 | Como monetiza | comissão de loja · assinatura · gratuito com tier pago · licença B2B · sem monetização direta |
| P7 | Classe de máquina | desktop montado · desktop pronto · notebook pessoal · notebook corporativo |
| P8 ⭐ | Estado de manutenção | data da última limpeza · **data da troca de pasta térmica** · idade do equipamento · horas de uso · nunca informado |

*Riscado = descartado durante a sessão.*

#### Combinações avaliadas

| # | Nome | Composição | Veredito |
| --- | --- | --- | --- |
| A | **Diagnóstico de manutenção** | P1 serviço + P2 temperatura + P3 antes + P4 antes/depois °C + P8 pasta | ✅ **É o produto** |
| B | **Guardião da live** | P3 durante + P5 app age sozinho | ❌ **Rejeitada por princípio arquitetural** |
| C | **A conta fechada** | P1 peça + P2 FPS + P4 % em jogo + P6 comissão | ✅ **É o produto** (com métrica podada) |
| D | **Histórico que vira prova** | P3 contínuo + P4 ausência de travamento + P8 idade | ✅ Aprovada |
| E | **Manutenção como upgrade de entrada** | P1 serviço + P8 nunca informado + vitrine | ✅ Aprovada |
| F | **Perfil de uso declarado** | P2 e P4 definidos pelo usuário | 🔄 **Reformulada** — vira varredura, não pergunta |

**Ideas Generated:**

21. **A e C não competem — são sequência.** A é o que o app faz primeiro (manutenção barata, prova de honestidade); C é o que ele faz depois (peça, com a conta fechada).
22. **Pasta térmica é a intervenção mais barata do catálogo.** R$ 40 resolvendo o que o Rafael tentaria resolver com GPU de R$ 2.100.
23. Pasta ressecada tem **assinatura térmica mensurável** — temperatura alta com carga baixa — detectável com os sensores já existentes.
24. Na Combinação E, o **contraste de preços faz o trabalho sozinho**: R$ 40 no topo da vitrine, ao lado de R$ 2.100.
25. Na Combinação D, o **SQLite já existente vira prova de valor sem custo novo**: "desde a otimização de 12/08, zero travamentos. Antes: 4 em duas semanas."
26. A prova de "valeu a pena" pode ser **ausência de dor ao longo do tempo**, sem benchmark nenhum.
27. Combinação F reformulada: o app **varre e anuncia** — *"encontrei 3 telas azuis nos últimos 30 dias e sua CPU está a 89°C em idle"* — em vez de perguntar a dor.

#### Decisões estruturais tomadas

28. ❌ **Periféricos e acessórios descartados de P1.** *Consequência registrada: era o caminho que resolvia o notebook fechado. Carla e notebook voltam a depender de "serviço" e "refrigeração" para converter.*
29. ⭐ **P8 criado a partir de proposta do próprio participante:** o usuário informa a data da última manutenção. Primeiro dado da sessão que exige input humano.
30. 🔒 **P5 tem opção proibida por arquitetura, não por escolha de produto:** *"não faço nenhuma alteração sem o aceite do usuário"*. Está codificado e coberto por teste no repositório.
31. ❌ **"Dinheiro poupado por não comprar a peça errada" descartado** — avaliado pelo próprio participante como *"retórica bonita sem lastro"*. Exigiria um contrafactual que o sistema não mede.
32. ✅ **Monitoramento em tempo real é opt-in:** só com o app aberto e mediante solicitação explícita do usuário. Não há daemon nem vigilância em background.
33. ⭐ **Vitrine separada (Caminho 2):** a sugestão de upgrade existe sempre e para qualquer máquina, mas em **aba própria**, nunca no fluxo de diagnóstico ou na tela de resultado.
34. ⭐ **Régua de produto — pergunta fato, nunca diagnóstico:** perguntar dado factual inalcançável pelo sistema (data da pasta térmica) é aceitável; perguntar *"qual é o seu problema?"* não é — **é para isso que o app existe**.
35. ⭐ **Event Log do Windows entra no escopo do coletor** (BSOD, WHEA, crashes de aplicação).

**Insights Discovered:**

- ⭐ **A vitrine separada mata a T1 por arquitetura, não por promessa.** O paradoxo da curadoria vivia da mistura entre recomendação comercial e diagnóstico técnico. Separados os fluxos, o diagnóstico fica limpo e quem entra na vitrine sabe que está numa vitrine.
- **A exigência de precisão na estimativa de ganho cai muito na vitrine.** Fora do diagnóstico, "ganho estimado" é informação de apoio à compra com margem declarada — não promessa do produto.
- ⭐ **O coletor lê estado; Event Log é história.** É a primeira ampliação qualitativa do `ColetorInventario` que sai desta sessão, e é o que permite afirmar a dor nº 1 de duas personas com fato.
- **Event Log + SQLite = Combinação D ativada.** Histórico do passado + registro a partir de agora = a prova que o Rafael pediu.
- **A ordem "barato antes de caro" prova honestidade melhor que qualquer declaração.**

**Notable Connections:**

- Achado 3 (prova = % em jogo nomeado) + decisão 31 (sem dinheiro poupado) deixam a Combinação C com **uma única métrica de prova**, e a pergunta de como medi-la sem benchmark manual segue aberta para a Técnica 4.
- Achado 13 (Carla pede backup) + achado 19 (Diego teme não voltar atrás) + `ServicoBackup` bloqueante já implementado = **feature pronta, comunicação ausente**.
- Achado 17 (overclock sem comprometer) + filosofia codificada no README = **o usuário validou a tese central do projeto sem ser induzido**.

---

### Assumption Reversal (#15) — 🔄 em andamento

**Description:** Listar as premissas embutidas no problema, inverter cada uma ao seu oposto, e reconstruir uma solução sobre a base invertida.

**Alvo:** resolver a T3 (descompasso entre demanda declarada e dor real) e estressar as demais premissas candidatas.

**Mecânica combinada:** a facilitadora apresenta uma premissa por vez; o participante inverte e desenvolve as implicações. Uma pergunta por mensagem, sem menus.

**Fila de premissas (ordem de trabalho):**

1. ⏳ **"O usuário quer usar 100% do hardware."** *(alvo direto da T3)* — **pergunta feita, resposta pendente**
2. ⬜ "O app deve sugerir upgrade para toda máquina que não seja a melhor existente"
3. ⬜ "Otimização de SO é o produto; upgrade é o extra"
4. ⬜ "O usuário precisa aprovar cada alteração" *(decisão arquitetural — inverter apenas como exercício)*
5. ⬜ "Vendedor conceituado é sinônimo de confiança"
6. ⬜ "O app deve funcionar sozinho, sem comunidade ou dados de outros usuários"

**Pergunta em aberto no ponto de parada (reproduzir literalmente ao retomar):**

> Premissa 1: **"O usuário quer usar 100% do hardware."** Inverte. Se essa frase fosse **falsa** — se o usuário, no fundo, não quisesse usar 100% do hardware nunca — o que ele estaria pedindo de verdade quando diz "quero desempenho máximo"?

**Ideas Generated:** nenhuma ainda — sessão pausada antes da primeira resposta do participante.

**Insights Discovered:** nenhum ainda.

---

## ⚠️ Tensões

### ~~T1 — O paradoxo da curadoria~~ ✅ **RESOLVIDA**

*Era:* o app conquista confiança filtrando lojas, que é exatamente o que a comissão paga para fazer.
*Resolução:* decisão 33 — vitrine separada do diagnóstico. Os fluxos não se misturam, então o incentivo comercial não contamina o parecer técnico.

### ~~T2 — A Carla não gera receita~~ ✅ **RESOLVIDA**

*Era:* persona de notebook corporativo não converte nunca.
*Resolução:* achado 15 — a Carla faz live e tira renda da máquina. Tem justificativa de investimento, dor pública de estabilidade e é canal de aquisição.
*Nota:* a decisão 28 (cortar periféricos) reduz parte dessa resolução. Vale revisitar se o notebook voltar a ficar sem caminho de conversão.

### T3 — Descompasso entre demanda declarada e dor real ⏳ **EM ABERTO**

O usuário pede "100% do hardware" e descreve BSOD e superaquecimento. Atender ao pedido literal entrega a coisa errada; atender à dor contraria o pedido — e o teste com o Rafael sugere que isso gera **alívio**.

**Alvo da Técnica 3 — em trabalho.** A primeira pergunta de inversão já foi colocada (ver seção da Técnica 3 acima); resposta ainda não recebida.

---

## Próximos Passos — Retomada

### Ponto exato de parada

**Dentro da Técnica 3 — Assumption Reversal (#15), na Premissa 1 de 6.** A facilitadora já fez a primeira pergunta de inversão (reproduzida na íntegra na seção "Assumption Reversal (#15) — 🔄 em andamento" acima). **Pergunta pendente de resposta do participante** — retomar reapresentando essa pergunta antes de prosseguir. Nenhuma ideia foi gerada ainda nesta técnica.

### Premissas candidatas a inversão

Ordem de trabalho definida (a 1ª já está em curso, ver acima):

- 1️⃣ "O usuário quer usar 100% do hardware" (T3) — **em curso, pergunta feita**
- 2️⃣ "O app deve sugerir upgrade para toda máquina que não seja a melhor existente"
- 3️⃣ "Otimização de SO é o produto; upgrade é o extra"
- 4️⃣ "O usuário precisa aprovar cada alteração" *(decisão arquitetural — inverter apenas como exercício)*
- 5️⃣ "Vendedor conceituado é sinônimo de confiança"
- 6️⃣ "O app deve funcionar sozinho, sem comunidade ou dados de outros usuários"

### Roteiro restante

| Etapa | Técnica | Objetivo |
| --- | --- | --- |
| 3 | Assumption Reversal (#15) | 🔄 Em andamento — estressar as 6 premissas (1 de 6 em curso) e resolver T3 |
| 4 | Resource Constraints (#18) | Convergir e priorizar sob restrição forçada |

### Preparação sugerida (opcional)

- A Técnica 3 rende mais com **energia de contestar** do que de concordar. Vale retomar descansado.
- Trazer, se houver, qualquer material sobre as parcerias com lojas: modelo de comissão, lojas-alvo, conversas em andamento.

---

## Questions That Emerged

- Como medir "+X% no jogo XPTO" sem instalar nada invasivo e sem depender de benchmark manual do usuário? **(crítica — única métrica de prova restante da Combinação C)**
- **Como o usuário descobre que a aba de vitrine existe?** Vitrine que ninguém visita não monetiza.
- Sem armazenamento no inventário (L4), o app pode recomendar SSD — provavelmente a melhor recomendação de custo-benefício disponível?
- O que autoriza o app a estimar ganho de uma peça que o usuário não possui? Que base de dados sustenta isso, e como ela envelhece a cada driver novo?
- Um app que recomenda hardware e ganha comissão precisa de que divulgação no Brasil (CDC, publicidade velada)?
- Se a dor real é instabilidade e temperatura, cooler e fonte deveriam vir antes de GPU e CPU na vitrine?
- Como o app trata o notebook depois do corte de periféricos (decisão 28)?

---

## Reflection & Follow-up

### What Worked Well

- A auditoria prévia do repositório evitou ideação sobre premissas falsas.
- O participante corrigiu a facilitadora duas vezes (rejeição da B, estranheza da F) — e ambas as correções viraram princípio de produto.
- A contradição da Carla (medo de danificar + querer overclock) foi mais produtiva que qualquer resposta coerente teria sido.
- Pausa documentada entre sessões funcionou: retomada sem recontextualização.

### Areas for Further Exploration

- **Persona entusiasta (Bruno):** dispensada nesta sessão. Define o teto técnico e é o crítico público mais provável.
- **Métrica de ganho estimado:** é o elo mais fraco da Combinação C.
- **Descoberta da vitrine:** problema de produto ainda não atacado.
- **Notebook pós-corte de periféricos:** caminho de conversão ficou frágil.

### Recommended Follow-up Techniques

- Após a Técnica 4, considerar **`*create-project-brief`** para consolidar o módulo, ou **`*agent pm`** para transformar o resultado em PRD.

### Next Session Planning

- **Suggested topics:** Técnicas 3 e 4 completas; resolução da T3; priorização final sob restrição.
- **Recommended timeframe:** próxima sessão, sem necessidade de recontextualização além deste documento.
- **Preparation needed:** nenhuma obrigatória.

---

*Session facilitated using the BMAD-METHOD™ brainstorming framework*
