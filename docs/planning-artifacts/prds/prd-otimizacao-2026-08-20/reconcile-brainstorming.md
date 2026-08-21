# Reconciliação de Input — Brainstorming vs. PRD + Addendum

**Input original:** `docs/brainstorming-session-results.md`
**Comparado contra:** `docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/prd.md` + `.../addendum.md`
**Método:** leitura integral dos três documentos, item a item (ideias numeradas, insights, decisões, tensões, perguntas em aberto), verificando se cada um tem destino explícito no PRD ou no addendum.

Os gaps estão ordenados por peso de produto (do que mais importa para o mais cosmético).

---

## 1. Contradição não resolvida: "ordem grátis-antes-de-pago" é regra do app ou escolha do usuário?

**O que estava no brainstorming:** Idea 40 (Assumption Reversal, Premissa 1) é explícita: *"A ordem otimizar-antes-de-upgrade não é regra do app — é decisão do usuário. O app sempre apresenta as duas possibilidades (grátis e paga) e é o usuário quem decide o quanto tenta de graça antes de partir para peça."* A resolução da T3 reforça isso: "o app nunca escolhe entre atender um ou outro — apresenta as duas trilhas e o usuário decide a ordem."

**O que o PRD diz:** A §1 Visão descreve a Trilha Paga como "upgrade de peça, **só depois de esgotada a primeira**" — linguagem de gate/sequência obrigatória, não de opção lado a lado. Isso contradiz o texto-fonte, mesmo que os FRs (FR-14/FR-15) implementem corretamente a versão "usuário decide" (as duas linhas aparecem juntas no Relatório, o clique é livre).

**Por que importa:** é uma inversão do texto para o oposto do que a sessão decidiu depois de resolver a T3 explicitamente. Se um arquiteto ou dev ler só a §1, pode implementar um gate real (ex.: esconder a Vitrine até a Trilha Grátis ser tentada), o que violaria a decisão 33 (vitrine sempre existe) e o espírito de "o usuário decide".

**Destino:** PRD principal — corrigir a frase da §1 Visão (e possivelmente a entrada do Glossário "Trilha Paga") para não sugerir sequência obrigatória.

---

## 2. Pergunta em aberto perdida: o que autoriza o app a estimar ganho de uma peça que o usuário não possui?

**O que estava no brainstorming:** em "Questions That Emerged": *"O que autoriza o app a estimar ganho de uma peça que o usuário não possui? Que base de dados sustenta isso, e como ela envelhece a cada driver novo?"* — nunca foi respondida na sessão, ficou como pergunta aberta genuína.

**O que o PRD diz:** a §10 Questões em Aberto tem 8 itens, nenhum deles é esta pergunta. FR-13/FR-14 assumem que existe "ganho estimado" (Eixo de Qualidade, linha `Upgrade hardware = X%`) sem que a fonte/metodologia de estimativa apareça em lugar nenhum do documento.

**Por que importa:** é uma lacuna de integridade de dado, não um detalhe — sustenta a promessa central da Vitrine (FR-13, FR-14) e tem risco direto de "guard contra alucinação de LLM" (§5) se não houver base real. Sem resposta, é possível implementar um número inventado sem violar nenhum FR escrito.

**Destino:** PRD principal — adicionar como item 9 em §10 Questões em Aberto.

---

## 3. Pergunta em aberto perdida: ordem cooler/fonte antes de GPU/CPU na Vitrine

**O que estava no brainstorming:** *"Se a dor real é instabilidade e temperatura, cooler e fonte deveriam vir antes de GPU e CPU na vitrine?"* — pergunta aberta, nunca respondida.

**O que o PRD diz:** FR-10 resolve ordenação por custo apenas entre Diagnóstico de Manutenção e Upgrade de Peça (o serviço barato vem antes da peça cara). Não existe nenhum FR ou questão em aberto sobre ordenação **dentro** do catálogo de peças da Vitrine (cooler/fonte vs. GPU/CPU).

**Por que importa:** é a mesma lógica de honestidade que sustenta FR-10 ("recomendar o mais barato primeiro prova honestidade"), generalizada para dentro da própria Vitrine — e o tema central da sessão é justamente que a dor real (instabilidade/temperatura) não é a mesma coisa que o pedido literal (desempenho). Deixar essa pergunta cair contradiz o cuidado que o resto do documento tem com esse ponto.

**Destino:** PRD principal — adicionar como item 10 em §10 Questões em Aberto.

---

## 4. Requisito de confiança do Rafael não virou FR: parcelamento, entrega rápida, "produto de qualidade"

**O que estava no brainstorming:** Idea 4: *"Requisitos de confiança na compra: local seguro, produto de qualidade, entrega rápida, parcelamento."* Repetida no addendum (§C) como referência de tom, mas nunca promovida a requisito funcional.

**O que o PRD diz:** FR-16 (Listagem restrita a Lojas Parceiras) cobre curadoria de vendedor e link de comissão, mas nada sobre exibir opções de parcelamento, prazo de entrega ou selo de qualidade do produto na própria listagem da Vitrine — que foi exatamente o que o Rafael, a persona-âncora de UJ-1, listou como condição de confiança para comprar.

**Por que importa:** parcelamento em especial é um requisito concreto e comercialmente relevante (ticket de GPU/CPU costuma ser alto) que tem peso de produto, não é só cor local — sem ele, uma parte real do "requisito de confiança" documentado na sessão fica sem rastreamento em nenhum FR.

**Destino:** PRD principal — como um FR novo em 4.3 Vitrine de Upgrade (ou nota de escopo em FR-16), já que afeta a taxa de conversão que SM-3 mede.

---

## 5. Tensão simplificada: a ressalva da T2 (decisão 28 fragiliza a resolução) some no PRD

**O que estava no brainstorming:** a T2 é marcada "✅ RESOLVIDA" mas carrega uma nota explícita: *"a decisão 28 (cortar periféricos) reduz parte dessa resolução. Vale revisitar se o notebook voltar a ficar sem caminho de conversão."* — ou seja, a própria sessão registrou que a resolução é parcial/frágil, não definitiva.

**O que o PRD diz:** UJ-2 e FR-17 tratam a conversão do notebook como resolvida de forma limpa (RAM/SSD quando há slot; Assinatura Premium na Fase 2 quando não há). A ressalva de fragilidade da T2 — que essa resolução pode não se sustentar sozinha — não aparece em nenhum lugar do PRD nem do addendum.

**Por que importa:** o PRD monta a Assinatura Premium (Fase 2) como a peça que fecha esse buraco, mas a Assinatura Premium está inteiramente fora do escopo do V1 (§8.2) e sem definição de módulos/preço (Questão 2). Isso significa que, no V1 real, o notebook com RAM soldada volta a ficar exatamente na situação que a T2 original descrevia como problema (sem monetização) — e o documento não sinaliza esse risco como tensão viva, só como nota lateral em §8.2.

**Destino:** addendum — reforçar na tabela de Tensões (ou criar uma) que T2 é "resolvida apenas a partir da Fase 2"; e no PRD principal, tornar mais explícito em §8.2/§10 que o V1 reintroduz o problema original da T2 para o segmento de RAM soldada.

---

## 6. Nuance emocional perdida: "o momento de maior dor é durante, e o app está ausente dele"

**O que estava no brainstorming:** insight da Persona Carla: *"O momento de maior dor é durante, e o app está ausente dele. Tudo que existe é antes (diagnóstico) ou depois (relatório) — apesar de a leitura de sensores em tempo real já estar pronta."* E o insight complementar: *"Reiniciar no meio da live é derrota pública. Prevenir vale mais que qualquer ganho de FPS."*

**O que o PRD diz:** o JTBD "Social" de §2.1 comprime isso em uma frase neutra ("não me deixe na mão bem no meio de uma live"), e §7 Não-Objetivos justifica a ausência de monitoramento em tempo real só pelo lado arquitetural (sem daemon/background). A tensão qualitativa — o app sabe que o pior momento é o "durante" e conscientemente escolhe não estar lá, e por quê isso é aceitável (prevenção > ganho de FPS, não é omissão por limitação técnica) — não é articulada em lugar nenhum. Isso é justamente o tipo de nuance que a estrutura rígida de FR tende a descartar.

**Por que importa:** sem essa nuance registrada, um leitor futuro do PRD pode reabrir a discussão de "monitoramento durante a live" como se fosse lacuna não-resolvida, quando na verdade a sessão já pesou os dois lados e decidiu conscientemente que prevenção antecipada supera intervenção ao vivo.

**Destino:** addendum — como nota de rationale na seção de Personas (§C) ou numa nova entrada em "Decisões estruturais herdadas" (§D), citando explicitamente por que o app permanece ausente do "durante" por decisão, não por limitação.

---

## 7. Caminho de conversão do notebook via "serviço/refrigeração" não conectado ao FR-17

**O que estava no brainstorming:** a decisão 28 registra a consequência de cortar periféricos: *"Carla e notebook voltam a depender de 'serviço' e 'refrigeração' para converter."* A Diagnóstico de Manutenção (Combinação A: pasta térmica) é agnóstica de classe de máquina — funciona tanto para desktop quanto notebook.

**O que o PRD diz:** FR-17 (Caminho de conversão para notebook) só fala de RAM/SSD (quando há slot) e Assinatura Premium (quando não há). O Diagnóstico de Manutenção (4.2, FR-8 a FR-11) nunca é mencionado como caminho de conversão válido para notebook — mesmo sendo, pela própria decisão 28, um dos dois caminhos que sobrariam para essa classe de máquina depois do corte de periféricos.

**Por que importa:** para um notebook com RAM soldada, hoje o PRD só oferece "otimização de software, sem caminho de receita" (§8.2) — mas a Diagnóstico de Manutenção (serviço de limpeza/pasta térmica) pode ser um terceiro caminho de conversão monetizável (mesmo sem comissão de peça, pode gerar recomendação de serviço pago a terceiro, ou ao menos reforçar confiança/retenção) que a sessão já havia cogitado e o PRD não amarrou explicitamente a essa jornada.

**Destino:** PRD principal — cross-referenciar FR-17 com 4.2 Diagnóstico de Manutenção como caminho aplicável a qualquer classe de máquina, incluindo notebook com RAM soldada.

---

## 8. Pergunta em aberto perdida: como o usuário descobre a Vitrine se não lê o Relatório de Resultado?

**O que estava no brainstorming:** em "Próximos Passos — Retomada", listado como uma das frentes de How Might We ainda não exploradas: *"descoberta da vitrine para quem não lê o relatório."* A sessão foi pausada antes de abrir essa frente — ou seja, é uma pergunta genuinamente sem resposta, não uma decisão tomada.

**O que o PRD diz:** FR-14/FR-15 resolvem a descoberta da Vitrine apenas via a linha factual do Relatório de Resultado. Não há menção, nem como requisito nem como questão em aberto (§10), do caminho alternativo de descoberta para quem nunca chega a ver esse relatório (ex.: abandona antes do diagnóstico terminar, ou nunca roda diagnóstico).

**Por que importa:** SM-2 (taxa de clique na linha Upgrade hardware) mede só esse único funil de descoberta — se ele for o único caminho e uma fração relevante de usuários nunca o alcançar, a Vitrine fica invisível para esse segmento, sem que o PRD reconheça isso como risco em aberto.

**Destino:** PRD principal — adicionar como item na §10 Questões em Aberto (a sessão original não respondeu, então não deveria aparecer como resolvido).

---

## 9. Nuance perdida: a precisão da estimativa "cai" fora do diagnóstico — vira "margem declarada", não promessa

**O que estava no brainstorming:** insight da Morphological Analysis: *"A exigência de precisão na estimativa de ganho cai muito na vitrine. Fora do diagnóstico, 'ganho estimado' é informação de apoio à compra com margem declarada — não promessa do produto."*

**O que o PRD diz:** FR-13/FR-14 tratam o "ganho estimado" da Vitrine com o mesmo tom factual/preciso do resto do documento ("linha factual", "sem viés de venda"), sem carregar a distinção de que, ali, o número é estimativa com margem declarada, e não a mesma prova rigorosa que sustenta o Diagnóstico (FR-5, correlação causa-raiz).

**Por que importa:** essa distinção de precisão tem implicação legal direta (Questão 3, divulgação/CDC) — se o app apresenta "ganho estimado" da Vitrine com o mesmo peso factual do Relatório de Resultado, sem deixar claro que é estimativa com margem, o risco de propaganda enganosa aumenta exatamente no ponto em que a sessão já havia identificado a necessidade de suavizar a exigência de precisão.

**Destino:** addendum — nota técnica associada a FR-13, explicitando que a estimativa da Vitrine deve vir com margem declarada, diferenciando-a da prova de causa-raiz do Núcleo de Atualização.

---

## 10. Tom perdido: reação positiva do Diego ("felicidade e expectativa") reduzida só ao medo

**O que estava no brainstorming:** Idea 18 (Role Playing, Diego): *"Olhando a tela técnica de inventário, sente felicidade e expectativa de melhorar a máquina."* Só depois vem o medo de não conseguir voltar atrás (Idea 19).

**O que o PRD diz:** UJ-3 (Diego) começa direto no medo/reversibilidade ("aceita mexer no computador porque sabe que pode desfazer") — o tom inicial de entusiasmo/expectativa ao ver a tela de inventário não aparece. A jornada perde a curva emocional completa (entusiasmo → medo → alívio) e fica só com medo → alívio.

**Por que importa:** é uma perda pequena de nuance de tom que afeta diretamente UX/copy — a tela de inventário deveria ser desenhada para gerar "felicidade e expectativa", não just neutralizar medo; sem essa nota, a equipe de UX pode desenhar essa tela só como "tranquilizadora" e perder a oportunidade de ser também empolgante.

**Destino:** addendum — nota de tom em §C (Personas — profundidade completa), reforçando a curva emocional completa de Diego.

---

## 11. Framing perdido: Bruno é o "crítico público mais provável", não só uma persona não explorada

**O que estava no brainstorming:** "Areas for Further Exploration": *"Persona entusiasta (Bruno): ainda dispensada. Define o teto técnico e é o crítico público mais provável — candidata a HMW ou Role Playing futuro."*

**O que o PRD diz:** §8.2 e Questão 6 tratam Bruno apenas como persona não explorada / candidata a rodada futura de descoberta, sem carregar o framing de risco: ele é quem provavelmente vai criticar publicamente o produto (ex.: review, fórum, review de tech influencer) se o Eixo de Qualidade ou o Teto de Compatibilidade forem rasos.

**Por que importa:** isso muda a prioridade de quando explorar Bruno — não é só "mais uma persona a validar depois", é "a pessoa que mais provavelmente vai testar os limites do produto publicamente e comentar sobre isso", o que pode justificar puxar essa exploração para antes do lançamento, não depois.

**Destino:** addendum — reforçar essa frase no §C ou §D como nota de risco, não só como item de escopo futuro.

---

## 12. Achados menores / consistência (não centrais, mas vale registrar)

- **Numeração de FR trocada no addendum:** §C do addendum atribui a curadoria de Loja Parceira ao "FR-14" ("a curadoria de Loja Parceira (FR-14) é estrutural"), mas Loja Parceira é definida em **FR-16**; FR-14 é a linha factual do Relatório. Mesma classe de erro na tabela de L3 ("Pré-requisito técnico para FR-15" — a referência correta parece ser FR-17, caminho de conversão do notebook). Vale corrigir as referências cruzadas do addendum numa passada de revisão.
- **Narrativa histórica da Combinação D não ilustrada:** o achado 25 traz um exemplo de copy pronto ("desde a otimização de 12/08, zero travamentos. Antes: 4 em duas semanas.") como prova de valor via SQLite — o addendum só cita a combinação em tabela resumida, sem preservar esse exemplo de tom/copy para uso futuro em UX (Fase 2).
- **"SSD provavelmente a melhor recomendação de custo-benefício":** a pergunta original sobre L4 ("Sem armazenamento no inventário, o app pode recomendar SSD — provavelmente a melhor recomendação de custo-benefício disponível?") carrega uma hipótese de priorização (SSD como upgrade de maior custo-benefício) que se perde na tabela de lacunas do addendum, que trata L4 só como bloqueio técnico, sem essa pista de priorização de catálogo.
