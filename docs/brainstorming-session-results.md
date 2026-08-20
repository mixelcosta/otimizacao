# Brainstorming Session Results

**Session Date:** 2026-08-07 (parte 1) · 2026-08-18 (parte 2) · 2026-08-19–20 (parte 3, em andamento)
**Facilitator:** Business Analyst Mary 📊
**Participant:** Michel Filipe
**Status:** ⏸️ **SESSÃO PAUSADA — Parte 3, em andamento.** Técnica 3 (Assumption Reversal) e Técnica 4 (One Feature Only) concluídas. Técnica bônus How Might We em andamento (2 perguntas fechadas). Retomar dentro de How Might We — ver [Ponto exato de parada](#ponto-exato-de-parada) abaixo.
**Memlog desta parte:** `docs/brainstorming/brainstorm-upgrade-custo-beneficio-2026-08-19/.memlog.md`

---

## Executive Summary

**Topic:** Concepção do módulo de **sugestão de upgrade com foco em custo-benefício** para o projeto Agente de Otimização e Confiabilidade de Hardware (`otimizacao`, v0.1.0).

**Session Goals:** Ideação focada. Módulo tratado como **feature nova, concepção do zero** (confirmado pelo participante).

**Público-alvo declarado:** pessoas que usam computadores e notebooks para **jogos e trabalho**.

**Premissa de negócio declarada:** monetização por **parceria com lojas**, com receita vinda das vendas de hardware recomendadas pelo app.

**Techniques Planned:** Role Playing (#16) → Morphological Analysis (#12) → Assumption Reversal (#15) → Resource Constraints (#18) *(substituída — ver nota)*.

**Techniques Completed:** Role Playing ✅ · Morphological Analysis ✅ · Assumption Reversal ✅ · One Feature Only ✅ *(substitui Resource Constraints, ausente do catálogo atual)* · How Might We 🔄 **em andamento** (2 perguntas fechadas, técnica aberta por natureza)

**Total Ideas Generated:** 38 achados na Parte 2 + 22 novos registros nesta parte (ideias, insights e decisões da Técnica 3, Técnica 4 e How Might We).

### Key Themes Identified

- O usuário não pede desempenho — pede **confiança na máquina** e **fechamento de conta**.
- **Reversibilidade é requisito universal**: as três personas citaram, espontaneamente, medo de não conseguir voltar atrás.
- **Gamer e profissional são a mesma pessoa** em momentos diferentes do dia.
- A honestidade do produto se prova **recomendando o mais barato primeiro**, não declarando boa intenção.
- O app decide por **fato coletado**, nunca por diagnóstico perguntado.
- ⭐ **"100% do hardware" nunca foi um pedido técnico** — é o jeito errado de nomear "otimização de software". Esgotar o grátis antes do pago já é a arquitetura da T3 resolvida.
- ⭐ **Diagnóstico não pertence a nenhuma trilha** — é infraestrutura compartilhada entre otimização de SO e upgrade, o que sustenta os dois produtos ao mesmo tempo.
- **O núcleo mínimo do produto é manter drivers, softwares e BIOS atualizados** — sozinho, já ataca a causa-raiz da dor #1 (BSOD/travamento) das três personas.
- **Monetização é dupla, não única**: comissão de loja parceira (fechada: ML, Amazon, Kabum etc.) + assinatura mensal de módulos premium — a segunda cobre exatamente os casos (ex.: notebook com RAM soldada) onde a primeira não se aplica.

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

### Assumption Reversal (#15) — ✅ concluída

**Description:** Listar as premissas embutidas no problema, inverter cada uma ao seu oposto, e reconstruir uma solução sobre a base invertida.

**Alvo:** resolver a T3 (descompasso entre demanda declarada e dor real) e estressar as demais premissas candidatas.

**As 6 premissas invertidas:**

**1. "O usuário quer usar 100% do hardware."** *(alvo direto da T3)*

**Ideas Generated:**

36. Invertida, a frase não é sobre saturar o hardware — é sobre máquina rápida: menos lentidão na inicialização, abertura ágil de programas, hardware trabalhando leve.
37. O nome certo para o que o usuário pede é **"otimização de software"**, não "desempenho máximo do hardware".
38. Upgrade de peça só faz sentido, na visão invertida, em 3 casos concretos: **mais RAM, mais clock/processamento (CPU), troca de HD físico por SSD**.
39. Os três casos convergem na mesma dor: RAM carrega softwares com mais agilidade; clock maior sustenta processos simultâneos; SSD verifica arquivos mais rápido que mídia física — juntos, "ganho expressivo em velocidade", não uso pleno do hardware.
40. A ordem otimizar-antes-de-upgrade não é regra do app — é **decisão do usuário**. O app sempre apresenta as duas possibilidades (grátis e paga) e é o usuário quem decide o quanto tenta de graça antes de partir para peça.

**Insights Discovered:**

- ⭐ **T3 RESOLVIDA.** "Usar 100% do hardware" vira "aproveitar 100% do que a máquina já tem, via software, sem gastar" (prova: %+FPS no jogo nomeado) — e só depois disso esgotado entra a sugestão de upgrade de peça, com sua própria prova (ganho estimado da peça nova). A palavra "100%" nunca foi sobre saturar fisicamente o hardware; era sobre esgotar o grátis antes do pago. Não existe contradição entre pedido literal e dor real: o app nunca escolhe entre atender um ou outro — apresenta as duas trilhas e o usuário decide a ordem, coerente com P5 (app recomenda, usuário aprova).

**2. "O app deve sugerir upgrade para toda máquina que não seja a melhor existente."**

**Ideas Generated:**

41. O teto de upgrade não é "a melhor máquina do mercado" — é o **teto de compatibilidade da própria máquina**: capacidade máxima de RAM já atingida, melhor CPU aceito por aquela placa-mãe, melhor GPU já lançada, fonte mais potente compatível, melhor refrigeração possível, melhor SSD de leitura/gravação. Sugerir além disso é sugerir o que não instala.
42. Mesmo no teto de capacidade, ainda há upgrade: um **eixo de qualidade** em vez de potência bruta — RAM de menor latência/maior frequência na mesma capacidade, GPU considerada "melhor" por rodar mais fria (não por ser mais forte).

**Insights Discovered:**

- O catálogo de upgrade tem uma segunda dimensão além de "mais forte": **mais eficiente, mais silenciosa, mais fria**. O app dificilmente fica sem nada pra oferecer, mesmo no teto.

**3. "Otimização de SO é o produto; upgrade é o extra."**

**Ideas Generated:**

43. Se upgrade fosse o produto principal, a monetização não mudaria — **mudaria a tela** de abertura do app.
44. ⭐ Monetização detalhada pela primeira vez nesta sessão: **comissão via link direto para lojas parceiras + assinatura mensal para liberar módulos premium do próprio app**. Duas fontes de receita, não uma.

**Insights Discovered:**

- **P3 se dissolve como a T3.** Mesmo no mundo invertido (upgrade como produto principal), a leitura completa do hardware tem que vir antes de qualquer indicação de peça — senão o app recomenda algo incompatível. Diagnóstico não é "o produto" nem "o extra": é **infraestrutura compartilhada** que sustenta as duas trilhas. A ordem de tela muda; o que roda primeiro no motor, não.

**4. "O usuário precisa aprovar cada alteração."** *(decisão arquitetural travada e testada — inversão só como exercício, não candidata a mudança real)*

**Ideas Generated:**

45. As únicas categorias que o participante aceitaria sem aprovação prévia: **limpeza de arquivos temporários e atualizações do Windows**.

**Insights Discovered:**

- O critério é **risco zero**: só dispensaria aprovação o que não traz nenhum prejuízo possível ao usuário. Driver e configuração de energia não passam nesse crivo, porque podem gerar instabilidade — reforça por que a regra de aprovação universal existe para (quase) tudo o mais.

**5. "Vendedor conceituado é sinônimo de confiança."**

**Ideas Generated:**

46. Reputação da loja não basta: **preço absurdamente baixo, mesmo em vendedor conceituado, quebra a confiança** — o sinal de golpe/produto falsificado vem do preço fora da curva, não do nome da loja.
47. ⭐ **Lista de lojas parceiras nomeada pela primeira vez:** Mercado Livre, Amazon, Kabum etc. — vitrine fechada, não marketplace aberto.

**Insights Discovered:**

- P5 resolvida por **escopo fechado**: o risco de "vendedor conceituado com preço suspeito" fica restrito a essa lista pequena e pré-acordada de parceiras, não a qualquer seller de um marketplace aberto.

**6. "O app deve funcionar sozinho, sem comunidade ou dados de outros usuários."**

**Ideas Generated:**

48. Dado útil de outros usuários seria o **resultado real das recomendações de upgrade e dos ajustes de BIOS** — prova social agregada, não só a promessa do fabricante.

**Insights Discovered:**

- ⭐ P6 destrava a pergunta crítica em aberto desde a Técnica 2: sem dado de outros usuários, o app não tem parâmetro de comparação nenhum. **Dado agregado e anônimo pode virar a métrica de prova da Combinação C** (+X% no jogo XPTO) sem depender de benchmark manual do usuário sozinho.

---

### One Feature Only — ✅ concluída *(substitui "Resource Constraints #18", ausente do catálogo de técnicas atual; escolhida em conjunto com o participante por ser a que mais força priorização direta)*

**Description:** O módulo só pode manter uma única capacidade — tudo o mais é cortado — e essa capacidade precisa se sustentar sozinha.

**Ideas Generated:**

49. Candidatas levantadas: **verificação de drivers** e **otimização de efeitos visuais do Windows** — nenhuma tinha aparecido antes na sessão.
50. Escolhida: **verificação de drivers**. Escopo expandido em seguida para **manter todos os drivers e softwares do computador atualizados**, não só drivers isolados.
51. Ordem de prioridade revelada: depois desse núcleo, a próxima coisa que o participante lutaria para trazer de volta é o **diagnóstico de manutenção** (Combinação A).

**Insights Discovered:**

- ⭐ **A feature única passa no teste de dor.** Bug de driver de vídeo ou BIOS desatualizada podem ser causa-raiz de BSOD/travamento — manter drivers, softwares e BIOS atualizados sozinho já ataca a dor #1 das três personas. Não é feature decorativa: é o núcleo mínimo defensável do produto.
- **Ranking de prioridade emergiu sem ser perguntado diretamente:** 1º núcleo de atualização, 2º diagnóstico de manutenção — insumo direto para a convergência final.

---

### How Might We — 🔄 em andamento

**Description:** Reformular o problema como perguntas de oportunidade ("Como poderíamos...") antes de ideiar em cima delas.

**Alvo:** os dois buracos abertos em "Areas for Further Exploration" — descoberta da vitrine e conversão do notebook pós-corte de periféricos.

**HMW#1 — Como poderíamos fazer o usuário descobrir a vitrine sem que pareça propaganda? ✅ fechada**

**Ideas Generated:**

52. Proposta inicial: uma frase-convite ("Melhore aqui seu desempenho, FPS, etc...") dentro do relatório de resultado do diagnóstico.
53. ⚠️ Essa proposta esbarrou na decisão 33 (vitrine nunca no fluxo de diagnóstico ou tela de resultado) — tensão levantada e resolvida na mesma rodada.
54. **Resolução:** o relatório mostra **fato numérico neutro** para os dois caminhos — `Otimização do S.O. = X%` / `Upgrade hardware = X%` — sem preço, loja ou CTA de venda. O clique em "Upgrade hardware" é o que leva à vitrine (preço/loja), fora do relatório.

**Insights Discovered:**

- **Dado no relatório, venda na vitrine** — a separação da decisão 33 sobrevive intacta porque a linha é fato, não anúncio.

**HMW#2 — Como dar ao notebook um caminho de conversão, já que a decisão 28 cortou periféricos e a Carla só converte de novo se estiver fazendo live? ✅ fechada**

**Ideas Generated:**

55. Caminho combina peça (**troca de SSD, aumento de RAM**) com otimização de SO (ajuste de efeitos visuais, limpeza de disco, atualização de drivers/softwares) — o que sobra do notebook depois do corte de periféricos é exatamente RAM e SSD.
56. Caso limite testado: notebook com **RAM soldada**, sem slot pra trocar — resta só o pacote de otimização via SO, sem peça nenhuma.

**Insights Discovered:**

- ⭐ **A segunda perna do negócio cobre o buraco da primeira.** Notebook com RAM soldada perde a comissão de peça, mas não perde monetização — é exatamente o caso que a assinatura mensal de módulos premium cobre, já que ela não depende de venda de hardware nenhuma.

**Notable Connections:**

- Achado 44 (assinatura mensal) + achado 56 (RAM soldada) = a monetização dupla não era redundância, era **cobertura complementar** para o segmento que a comissão de peça não alcança.

---

## ⚠️ Tensões

### ~~T1 — O paradoxo da curadoria~~ ✅ **RESOLVIDA**

*Era:* o app conquista confiança filtrando lojas, que é exatamente o que a comissão paga para fazer.
*Resolução:* decisão 33 — vitrine separada do diagnóstico. Os fluxos não se misturam, então o incentivo comercial não contamina o parecer técnico.

### ~~T2 — A Carla não gera receita~~ ✅ **RESOLVIDA**

*Era:* persona de notebook corporativo não converte nunca.
*Resolução:* achado 15 — a Carla faz live e tira renda da máquina. Tem justificativa de investimento, dor pública de estabilidade e é canal de aquisição.
*Nota:* a decisão 28 (cortar periféricos) reduz parte dessa resolução. Vale revisitar se o notebook voltar a ficar sem caminho de conversão.

### ~~T3 — Descompasso entre demanda declarada e dor real~~ ✅ **RESOLVIDA**

*Era:* o usuário pede "100% do hardware" e descreve BSOD e superaquecimento. Atender ao pedido literal entrega a coisa errada; atender à dor contraria o pedido.
*Resolução:* Assumption Reversal, Premissa 1 — "usar 100% do hardware" nunca foi um pedido técnico de saturação; é o jeito errado de nomear "otimização de software". O app nunca precisa escolher entre pedido e dor: apresenta as duas trilhas (otimização grátis primeiro, upgrade pago depois, cada uma com sua prova) e o usuário decide a ordem. Não há mais contradição — pedido e dor apontam pro mesmo lugar.

---

## Próximos Passos — Retomada

### Ponto exato de parada

**Dentro da técnica bônus How Might We, logo após o fechamento da HMW#2** (conversão do notebook). Nenhuma pergunta pendente de resposta — a sessão foi pausada num ponto de fechamento limpo, a pedido do participante.

**Ao retomar, oferecer três caminhos** (não decidir sozinha):
1. Mais rodadas de How Might We — outros HMWs possíveis: descoberta da vitrine para quem *não* lê o relatório, persona Bruno (entusiasta) ainda não explorada, divulgação legal (CDC) da comissão.
2. Seguir para **convergência/síntese** (fechar a sessão de geração e organizar tudo em conclusões).
3. Escolher uma técnica nova do catálogo para abrir mais uma frente.

### Roteiro original — status final

| Etapa | Técnica | Resultado |
| --- | --- | --- |
| 3 | Assumption Reversal (#15) | ✅ Concluída — 6 de 6 premissas invertidas, **T3 resolvida** |
| 4 | Resource Constraints (#18) | ✅ Concluída como **One Feature Only** (técnica substituta, catálogo atual não tem "Resource Constraints") — núcleo mínimo definido: atualização de drivers/softwares/BIOS |
| bônus | How Might We | 🔄 Em andamento — 2 perguntas fechadas (descoberta da vitrine, conversão do notebook) |

### Preparação sugerida (opcional)

- How Might We e convergência rendem mais com **energia de organizar e decidir** do que de contestar — inverso da Técnica 3. Vale retomar num momento de cabeça fria.
- Trazer, se houver, qualquer material sobre as parcerias com lojas (ML, Amazon, Kabum): modelo de comissão exato, e sobre a assinatura mensal: faixas de preço e lista de módulos premium cogitados.

---

## Questions That Emerged

- ✅ **Como medir "+X% no jogo XPTO" sem benchmark manual?** Apontamento de resposta na Premissa 6: dado agregado e anônimo de outros usuários pode virar a métrica de prova, substituindo o benchmark individual. Falta detalhar o mecanismo de coleta.
- ✅ **Como o usuário descobre a vitrine?** Resolvida na HMW#1 — linha factual de ganho estimado (`Otimização do S.O. = X%` / `Upgrade hardware = X%`) no relatório de resultado, sem CTA de venda.
- ✅ **Como o app trata o notebook pós-corte de periféricos (decisão 28)?** Resolvida na HMW#2 — RAM/SSD como peça quando há slot; assinatura mensal de módulos premium quando não há (RAM soldada).
- Sem armazenamento no inventário (L4), o app pode recomendar SSD — provavelmente a melhor recomendação de custo-benefício disponível?
- O que autoriza o app a estimar ganho de uma peça que o usuário não possui? Que base de dados sustenta isso, e como ela envelhece a cada driver novo?
- Um app que recomenda hardware e ganha comissão precisa de que divulgação no Brasil (CDC, publicidade velada)?
- Se a dor real é instabilidade e temperatura, cooler e fonte deveriam vir antes de GPU e CPU na vitrine?
- **Nova:** o mecanismo de dado agregado entre usuários (Premissa 6) exige coleta de telemetria — que consentimento e que dado exatamente é enviado, dado que hoje a sanitização antes da nuvem já é regra do projeto?
- **Nova:** a assinatura mensal de módulos premium (decisão 44) precisa de definição — quais módulos ficam atrás do paywall, e isso não entra em tensão com a filosofia de honestidade já validada (achado 8)?

---

## Reflection & Follow-up

### What Worked Well

- A auditoria prévia do repositório evitou ideação sobre premissas falsas.
- O participante corrigiu a facilitadora duas vezes (rejeição da B, estranheza da F) — e ambas as correções viraram princípio de produto.
- A contradição da Carla (medo de danificar + querer overclock) foi mais produtiva que qualquer resposta coerente teria sido.
- Pausa documentada entre sessões funcionou: retomada sem recontextualização.

### Areas for Further Exploration

- **Persona entusiasta (Bruno):** ainda dispensada. Define o teto técnico e é o crítico público mais provável — candidata a HMW ou Role Playing futuro.
- **Mecanismo de dado agregado entre usuários:** a Premissa 6 abriu a ideia, mas telemetria/consentimento/formato ainda não foram desenhados.
- **Definição dos módulos premium da assinatura:** o que fica atrás do paywall, sem contradizer a honestidade já validada como princípio do produto.
- **Divulgação legal da comissão (CDC):** segue sem resposta.

### Recommended Follow-up Techniques

- Com T1, T2 e T3 resolvidas e o núcleo mínimo (One Feature Only) definido, considerar **`*create-project-brief`** para consolidar o módulo, ou **`*agent pm`** para transformar o resultado em PRD assim que a convergência final for feita.

### Next Session Planning

- **Suggested topics:** fechar How Might We (ou seguir direto pra convergência); sintetizar tudo em prioridades acionáveis; decidir entre PRD ou project brief como próximo artefato.
- **Recommended timeframe:** próxima sessão, sem necessidade de recontextualização além deste documento.
- **Preparation needed:** nenhuma obrigatória.

---

*Session facilitated using the BMAD-METHOD™ brainstorming framework*
