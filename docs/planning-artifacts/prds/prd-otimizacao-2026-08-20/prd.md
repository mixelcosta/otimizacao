---
title: Módulo de Sugestão de Upgrade com Foco em Custo-Benefício
status: final
created: 2026-08-20
updated: 2026-08-21
---

# PRD: Módulo de Sugestão de Upgrade com Foco em Custo-Benefício
*Working title — confirmar nome final do módulo antes do handoff para arquitetura/UX.*

> ⚠️ **Risco crítico em aberto:** a Vitrine de Upgrade ([§4.3](#43-vitrine-de-upgrade)) depende de trabalho de arquitetura ainda não feito (lacunas L4/L5) — pode tirar a Vitrine do V1. Detalhe em [§8.1](#81-em-escopo).

## 0. Documento — Propósito

Este PRD é para o time do projeto `otimizacao` (Agente de Otimização e Confiabilidade de Hardware, v0.1.0) — PM, arquitetura, UX e desenvolvimento — como base para as próximas etapas de planejamento (arquitetura, épicos/histórias). Ele é destilado inteiramente da sessão de brainstorming registrada em `docs/brainstorming-session-results.md` (5 técnicas: Role Playing, Morphological Analysis, Assumption Reversal, One Feature Only, How Might We), que segue como fonte de contexto e rationale — este documento não a duplica, apenas formaliza suas decisões em requisitos rastreáveis.

Vocabulário: os termos do [Glossário](#3-glossário) são usados literalmente em todo o documento — jornadas (UJ-N) e requisitos (FR-N) referenciam esses termos, nunca sinônimos. Suposições inferidas sem confirmação explícita do participante estão marcadas inline com `[ASSUMPTION]` e indexadas na [§11](#11-índice-de-suposições).

## 1. Visão

O `otimizacao` já é um agente maduro de confiabilidade de hardware — mas hoje ele só sabe dizer o que está errado, nunca o que fazer sobre isso quando a resposta é "compre uma peça". Este módulo fecha esse buraco: transforma o diagnóstico existente em uma recomendação acionável de custo-benefício, sempre honesta sobre o caminho mais barato primeiro.

A descoberta central da sessão de brainstorming é que o usuário nunca pediu "mais hardware" — ele pediu para parar de sentir o computador. "Quero 100% do hardware" é o jeito impreciso de descrever tela azul, boot lento e travamento em jogo. O módulo responde a isso com duas trilhas sempre visíveis lado a lado, nunca uma escondendo a outra: uma trilha grátis (otimização de software, incluindo o núcleo de manter drivers, softwares e BIOS verificados contra fontes oficiais, que já ataca a causa-raiz mais comum de instabilidade) e uma trilha paga (upgrade de peça, com prova numérica apresentada na Vitrine — sempre fora do fluxo de diagnóstico, sem viés de venda contaminando o parecer técnico). **A ordem entre elas é sempre escolha do usuário, nunca um gate imposto pelo app** — o Relatório de Resultado mostra as duas ao mesmo tempo, e é o usuário quem decide o quanto tenta de graça antes de considerar peça.

O produto se sustenta financeiramente sem comprometer essa honestidade: comissão de lojas parceiras fechadas (Mercado Livre, Amazon, Kabum) e, futuramente, assinatura de módulos premium — duas pernas de receita que se cobrem mutuamente, inclusive para os casos (como notebooks sem slot de RAM) onde uma delas não se aplica.

## 2. Usuário-Alvo

### 2.1 Jobs To Be Done

- **Funcional:** "Quando meu computador trava ou demora pra abrir programas, eu quero saber com certeza se o problema é o software ou se preciso trocar uma peça, para não gastar dinheiro à toa." *(Rafael, Diego)*
- **Econômico/emocional:** "Eu quero ter certeza de que qualquer ajuste no meu computador pode ser desfeito, porque não posso arriscar ficar um dia sem trabalhar." *(Carla)*
- **Social:** "Eu quero que minha máquina não me deixe na mão bem no meio de uma live ou de uma entrega de trabalho." *(Carla)*
- **Contextual:** "De dia eu preciso da máquina para o trabalho; de noite, para jogar — é o mesmo computador e a mesma pessoa, e a ferramenta precisa servir aos dois momentos sem eu ter que pedir duas coisas diferentes." *(Rafael/Carla — insight-chave da sessão: gamer e profissional são a mesma pessoa)*

### 2.2 Não-Usuários (v1)

- Frotas corporativas com gestão centralizada de TI — o módulo é desenhado para uso individual, não para administração em massa. `[ASSUMPTION]`
- Usuários fora do Windows — herdado da plataforma existente do `otimizacao`; não avaliado nesta sessão.
- Compradores que preferem lojas fora da lista de parceiras — ainda recebem diagnóstico e recomendação, mas sem o link de comissão (ver [§6](#6-monetização)).

### 2.3 Jornadas-Chave do Usuário

- **UJ-1. Rafael descobre que a tela azul não precisa de uma GPU nova.**
  - **Persona + contexto:** Rafael, gamer/desktop, mede "valeu a pena" em dinheiro gasto vs. FPS ganho; chegou desconfiado, pronto para comprar uma GPU de R$ 2.100.
  - **Estado de entrada:** abre o app depois de um travamento em jogo pesado.
  - **Caminho:** roda o diagnóstico → o app varre e anuncia a dor ("encontrei 3 telas azuis nos últimos 30 dias, driver de vídeo desatualizado") → relatório mostra `Otimização do S.O. = 18%` ao lado de `Upgrade hardware = 34%` → Rafael aceita a atualização de driver primeiro (grátis) → depois clica na linha de upgrade para ver a GPU na Vitrine, com prova em `%FPS` no jogo que ele joga.
  - **Clímax:** o app o contraria na primeira tela ("seu problema não é a GPU, é o driver") e isso gera alívio, não frustração — a evidência numérica é o que separa isso de propaganda.
  - **Resolução:** Rafael aplica a atualização de driver, sem travamento nas sessões seguintes; decide se ainda quer a GPU pela Vitrine, agora com decisão informada.
  - **Edge case:** se o driver mais recente já estiver instalado e o travamento persistir, o app precisa ter outra hipótese a oferecer (BIOS, superaquecimento) — não pode ficar sem resposta. `[NOTE FOR PM]`

- **UJ-2. Carla evita travar ao vivo sem arriscar o notebook.**
  - **Persona + contexto:** Carla, editora/notebook corporativo que também faz live à noite; medo é econômico (dano ao equipamento = dia parado sem renda), não técnico.
  - **Estado de entrada:** abre o app antes de uma sessão de live, querendo "SO mais rápido e leve".
  - **Caminho:** roda o diagnóstico → vê o núcleo de atualização (drivers/softwares/BIOS) sugerido, com relatório de ganho estimado e opção de backup/rollback visível antes de aceitar → aplica → se o notebook tiver slot de RAM livre, a Vitrine sugere RAM/SSD como peça; se a RAM for soldada, a Vitrine mostra apenas o caminho de otimização de software.
  - **Clímax:** o relatório de ganhos + o backup disponível são o que destrava a aprovação dela — sem isso, ela não deixaria o app agir.
  - **Resolução:** Carla faz a live sem travar; se algo desse errado, sabe que pode reverter.
  - **Edge case:** notebook com RAM soldada — sem caminho de receita no V1. Ver [§8.2](#82-fora-de-escopo-para-o-v1).

- **UJ-3. Diego aceita mexer no computador porque sabe que pode desfazer.**
  - **Persona + contexto:** Diego, leigo, usuário básico sem rotina de manutenção; hipótese a validar com usuário real (ver ressalva metodológica no brainstorming). `[ASSUMPTION]`
  - **Estado de entrada:** abre o app pela primeira vez, olha a tela de inventário — sente **felicidade e expectativa de melhorar a máquina** antes de qualquer medo aparecer (a tela deve ser desenhada para gerar entusiasmo, não só tranquilizar).
  - **Caminho:** o app pergunta apenas fato factual que ele sabe responder ("quando foi a última limpeza/troca de pasta térmica?", nunca "qual é o seu problema?") → detecta assinatura térmica de pasta ressecada (temperatura alta com carga baixa) → recomenda a troca de pasta térmica (R$ 40) ao lado — não escondida — de uma eventual sugestão de peça mais cara, deixando o contraste de preço falar por si.
  - **Clímax:** ver que a mudança tem prova de antes/depois de temperatura e pode ser desfeita remove o medo de "não conseguir voltar atrás".
  - **Resolução:** Diego aceita a manutenção barata primeiro; a confiança construída aí é o que sustenta uma eventual conversão de peça depois.

## 3. Glossário

- **Diagnóstico** — a leitura completa do hardware e do histórico do dispositivo (Inventário + Event Log), executada antes de qualquer recomendação. Infraestrutura compartilhada entre a Trilha Grátis e a Trilha Paga — não pertence a nenhuma das duas.
- **Trilha Grátis** — o caminho de otimização de software (Núcleo de Atualização + Diagnóstico de Manutenção), sem custo para o usuário.
- **Trilha Paga** — o caminho de sugestão de Upgrade de Peça, monetizado por comissão ou assinatura.
- **Núcleo de Atualização** — a capacidade mínima do produto: verificar e manter drivers, softwares instalados e BIOS atualizados via fontes oficiais, com prova de causa-raiz de instabilidade (BSOD/travamento) quando aplicável. A BIOS é orientada e alertada, nunca gravada pelo app.
- **Diagnóstico de Manutenção** — recomendação de serviço de baixo custo (ex.: troca de pasta térmica) baseada em assinatura térmica mensurável, não em pergunta ao usuário.
- **Vitrine** — aba própria de sugestão de Upgrade de Peça, com preço e link de compra. Nunca aparece dentro do fluxo de Diagnóstico ou na tela de Relatório de Resultado.
- **Relatório de Resultado** — a tela que resume o ganho de cada Trilha em percentual (`Otimização do S.O. = X%` / `Upgrade hardware = X%`), sem preço, loja ou chamada de venda. É o único ponto de entrada para a Vitrine.
- **Teto de Compatibilidade** — o limite de upgrade de uma máquina específica (capacidade de RAM, socket de CPU aceito, compatibilidade de GPU/fonte), não "a melhor máquina do mercado".
- **Eixo de Qualidade** — dimensão de upgrade além de potência bruta (menor latência, maior frequência, temperatura mais baixa), oferecida quando a máquina já está no Teto de Compatibilidade.
- **Loja Parceira** — vendedor de uma lista fechada e pré-acordada (V1: Mercado Livre, Amazon, Kabum) que sustenta a comissão do app. A Vitrine nunca é um marketplace aberto.
- **Assinatura Premium** — modelo de receita recorrente que libera módulos premium do app, independente de venda de hardware. *(Fase 2 — ver [§8.2](#82-fora-de-escopo-para-o-v1))*
- **Prova Social Agregada** — dado anônimo e agregado do resultado real de recomendações de outros usuários, usado como métrica de prova de ganho sem depender de benchmark manual individual. *(Fase 2)*
- **Event Log** — histórico de eventos do Windows (BSOD, WHEA, crashes de aplicação) coletado pelo Inventário; é o que transforma "está lento" em fato datado e contável.

## 4. Features

### 4.1 Núcleo de Atualização
**Descrição:** a capacidade mínima e defensável do produto (Glossário, §3) — validada isoladamente na Técnica "One Feature Only" contra a dor nº 1 das três personas (BSOD/travamento). Realiza UJ-1, UJ-2.

**Functional Requirements:**

#### FR-1: Varredura de drivers e softwares desatualizados via fontes oficiais
O sistema pode verificar a versão de drivers e softwares instalados no computador do usuário e compará-la com a versão mais recente disponível, consultando **exclusivamente fontes oficiais e seguras** (ex.: site do fabricante do driver, canal oficial de atualização do software).

**Consequences (testable):**
- O sistema lista cada item desatualizado com versão atual vs. versão oficial mais recente.
- O sistema nunca usa agregador de terceiros não-oficial como fonte de comparação de versão.
- Para software de terceiros, o sistema alerta e recomenda — a instalação da atualização é feita pelo usuário, fora do app, a partir do link oficial indicado.

**Out of Scope:**
- Download ou instalação automática de software de terceiros pelo app (ver FR-6, que cobre apenas atualizações que o app efetivamente aplica).

#### FR-2: Verificação de versão de BIOS via fonte oficial do fabricante
O sistema pode consultar o site oficial do fabricante da placa-mãe e comparar a versão de BIOS instalada com a versão mais recente disponível.

**Consequences (testable):**
- O sistema identifica o fabricante e modelo exato da placa-mãe a partir do Inventário antes de consultar a fonte oficial.
- O sistema nunca aponta uma versão de BIOS de fonte não-oficial.

#### FR-3: Orientação de atualização de BIOS com alerta de risco obrigatório
Quando a BIOS está desatualizada, o sistema pode orientar o usuário a baixar a versão mais recente disponível e prosseguir com a atualização — sempre precedido de um alerta obrigatório de risco.

**Consequences (testable):**
- O alerta informa explicitamente que a interrupção da atualização de BIOS pode comprometer o funcionamento da placa-mãe.
- O alerta recomenda buscar um profissional qualificado para realizar a atualização com segurança.
- A decisão de prosseguir sozinho ou não é sempre do usuário — o app não executa a gravação da BIOS por ele.
- O alerta é exibido a cada vez que uma atualização de BIOS é orientada, não apenas na primeira vez.

**Out of Scope:**
- O app não executa a atualização de BIOS dentro de si mesmo — apenas orienta e alerta (ver [§7](#7-não-objetivos)).

#### FR-4: Coleta de Event Log (BSOD, WHEA, crashes de aplicação)
O sistema pode coletar e armazenar o histórico de eventos críticos do Windows como parte do Inventário.

**Consequences (testable):**
- Cada evento registrado tem timestamp, tipo (BSOD/WHEA/crash) e, quando disponível, o driver ou processo associado.
- O histórico é consultável por período (ex.: "últimos 30 dias").

#### FR-5: Correlação causa-raiz no Diagnóstico
O sistema pode associar um driver, software ou BIOS desatualizados a eventos do Event Log, quando a correlação existir, e nomear essa causa no Diagnóstico. Realiza UJ-1.

**Consequences (testable):**
- Quando existe correlação plausível (driver do mesmo subsistema que aparece nos crashes), o texto do Diagnóstico nomeia o item específico, não uma mensagem genérica.
- Quando não há correlação, o sistema não inventa uma causa — mostra o achado sem atribuição forçada.

**Notes:** `[NOTE FOR PM]` Critério de "correlação plausível" fica para arquitetura — ver [§10](#10-questões-em-aberto), item 4.

#### FR-6: Aprovação obrigatória antes de qualquer atualização aplicada pelo app
O sistema não pode aplicar nenhuma atualização de driver sem aceite explícito do usuário por item. *(Software de terceiros e BIOS não são aplicados pelo app — ver FR-1 e FR-3; a aprovação ali é a própria decisão do usuário de seguir a orientação.)*

**Consequences (testable):**
- Nenhuma ação de atualização de driver executa sem um evento de aprovação do usuário registrado.
- *(Requisito herdado — já implementado e coberto por teste no `otimizacao`; este FR apenas confirma que o Núcleo de Atualização não abre exceção.)*

#### FR-7: Rollback por atualização de driver aplicada
O sistema pode reverter qualquer atualização de driver aplicada por ele, usando o mecanismo de backup já existente no `otimizacao`.

**Consequences (testable):**
- Toda atualização de driver aplicada gera um ponto de restauração antes da alteração.
- O usuário pode acionar o rollback a partir da mesma tela onde aprovou a atualização.

**Out of Scope:**
- Rollback de atualização de BIOS — fisicamente não coberto pelo mecanismo de backup do app. É por isso que FR-3 exige alerta de risco explícito em vez de oferecer reversibilidade.

**Feature-specific NFRs:**
- A varredura de drivers/software/BIOS/Event Log não pode rodar em background contínuo — só quando o app está aberto e o usuário solicita (herda o invariante de monitoramento opt-in já existente).
- Toda consulta a fonte externa (verificação de versão) é restrita a uma lista de domínios oficiais permitidos — nunca busca genérica na web.

---

### 4.2 Diagnóstico de Manutenção
**Descrição:** a segunda prioridade revelada na Técnica "One Feature Only" (Glossário, §3, para a definição completa). Realiza UJ-3.

**Functional Requirements:**

#### FR-8: Detecção de assinatura térmica de manutenção degradada
O sistema pode detectar temperatura alta sob carga baixa, usando os sensores já existentes no `otimizacao`, como sinal de pasta térmica ressecada ou necessidade de limpeza.

**Consequences (testable):**
- O sistema registra a leitura de temperatura em pelo menos dois momentos (idle e sob carga simulada/observada) antes de sinalizar o achado.

#### FR-9: Coleta de dado factual de manutenção (data da última troca/limpeza)
O sistema pode perguntar ao usuário, uma única vez por item, um dado factual que o sistema não consegue coletar sozinho (ex.: data da última troca de pasta térmica).

**Consequences (testable):**
- A pergunta nunca assume a forma "qual é o seu problema" — é sempre um dado datável e objetivo.
- A resposta fica salva no Inventário e não é perguntada de novo, a menos que o usuário a atualize.

#### FR-10: Recomendação de serviço ordenada por custo
Quando o sistema detecta a assinatura térmica de FR-8, ele pode recomendar o serviço de manutenção correspondente, exibido antes de qualquer sugestão de peça mais cara para o mesmo sintoma.

**Consequences (testable):**
- Se existir simultaneamente uma recomendação de Diagnóstico de Manutenção e uma de Upgrade de Peça para o mesmo sintoma, a de menor custo aparece primeiro.

#### FR-11: Prova de antes/depois de temperatura
Após o usuário confirmar que realizou a manutenção sugerida, o sistema pode mostrar a comparação de temperatura antes/depois.

**Consequences (testable):**
- A tela de confirmação exibe os dois valores de temperatura lado a lado, sem exigir benchmark manual.

---

### 4.3 Vitrine de Upgrade
**Descrição:** a Trilha Paga (Glossário, §3), descoberta através da linha factual do Relatório de Resultado. Realiza UJ-1, UJ-2.

**Functional Requirements:**

#### FR-12: Cálculo do Teto de Compatibilidade por máquina
O sistema pode determinar, para a máquina específica do usuário, o limite de upgrade por componente (capacidade e slots de RAM, socket de CPU aceito pela placa-mãe, compatibilidade de GPU com a fonte instalada).

**Consequences (testable):**
- O sistema nunca sugere uma peça que a máquina não aceita fisicamente (ex.: CPU fora do socket suportado).
- `[NOTE FOR PM]` Depende das lacunas técnicas L4/L5 — detalhe e decisão de escopo em [§8.1](#81-em-escopo).

#### FR-13: Sugestão no Eixo de Qualidade quando a máquina está no teto
Quando a máquina já está no Teto de Compatibilidade de um componente, o sistema pode oferecer upgrade no Eixo de Qualidade (menor latência, maior frequência na mesma capacidade, ou modelo que roda mais frio) em vez de silenciar a recomendação.

**Consequences (testable):**
- Uma máquina no teto de RAM ainda recebe pelo menos uma sugestão válida (ex.: RAM de menor latência), quando existir opção compatível.

#### FR-14: Linha factual de ganho no Relatório de Resultado
O Relatório de Resultado pode exibir duas linhas neutras — `Otimização do S.O. = X%` e `Upgrade hardware = X%` — sem preço, nome de loja ou texto de venda. Realiza UJ-1.

**Consequences (testable):**
- Nenhum termo comercial (preço, "compre", nome de loja) aparece na tela de Relatório de Resultado.
- A linha `Upgrade hardware` só aparece quando existe ao menos uma sugestão de peça válida sob o Teto de Compatibilidade.

**Notes:** o "ganho estimado" da linha `Upgrade hardware` **não tem o mesmo rigor** da correlação causa-raiz do Núcleo de Atualização (FR-5) — é estimativa com margem declarada, não prova medida, calculada a partir da base de benchmark de FR-19. O texto/UI precisa deixar essa diferença de precisão explícita (ex.: "~X%, estimativa" vs. o tom factual do restante do Relatório), para não se equiparar à evidência numérica que sustenta a honestidade do produto — e para reduzir o risco de propaganda enganosa levantado na Questão 9 (divulgação/CDC). `[NOTE FOR PM]`

#### FR-15: Navegação da linha de upgrade para a Vitrine
Clicar na linha `Upgrade hardware = X%` pode levar o usuário à Vitrine, em tela/aba separada.

**Consequences (testable):**
- A Vitrine nunca é renderizada dentro do fluxo de Diagnóstico ou na própria tela de Relatório de Resultado — apenas como destino de navegação a partir do clique.

#### FR-16: Listagem restrita a Lojas Parceiras
A Vitrine pode listar apenas produtos de Lojas Parceiras da lista fechada e pré-acordada (V1: Mercado Livre, Amazon, Kabum), com link direto de comissão.

**Consequences (testable):**
- Nenhum vendedor fora da lista pré-acordada aparece na Vitrine.
- Cada item exibido tem o link de comissão correspondente à Loja Parceira.

**Notes:** `[NOTE FOR PM]` Risco de preço suspeito em sellers individuais dentro de marketplaces como Mercado Livre/Amazon — ver [§10](#10-questões-em-aberto), item 10.

#### FR-17: Caminho de conversão para notebook
Para notebooks com slot de RAM/armazenamento disponível, a Vitrine pode sugerir RAM/SSD como peça, junto com o pacote de otimização de SO. Para notebooks com RAM soldada, o sistema pode reconhecer essa restrição e mostrar apenas o caminho de otimização de software, sem forçar uma sugestão de peça impossível.

**Consequences (testable):**
- O sistema nunca sugere troca de RAM para uma máquina identificada com RAM soldada.

**Out of Scope:**
- Monetização do caso "RAM soldada" — ver [§8.2](#82-fora-de-escopo-para-o-v1).

**Notes:** o Diagnóstico de Manutenção ([§4.2](#42-diagnóstico-de-manutenção)) também é um caminho de conversão válido para notebook, independente de ter slot de RAM livre — a decisão de cortar periféricos do catálogo (registrada no brainstorming original) deixou "serviço" e "otimização" como as rotas que sobram para essa classe de máquina. Vale considerar recomendação de serviço de limpeza/pasta térmica como conversão adicional para notebook com RAM soldada, mesmo sem comissão de peça.

#### FR-18: Requisitos de confiança na listagem da Vitrine
Cada item listado na Vitrine pode exibir, além de preço e link: opção de parcelamento (quando a Loja Parceira oferecer), prazo de entrega estimado, e selo/indicador de produto original vendido pela própria Loja Parceira. Realiza UJ-1.

**Consequences (testable):**
- Um item sem informação de parcelamento e prazo de entrega disponível na API/página da Loja Parceira não bloqueia a exibição, mas o campo aparece "a confirmar na loja" em vez de omitido silenciosamente.

**Notes:** origem: Rafael citou "local seguro, produto de qualidade, entrega rápida, parcelamento" como condição de confiança de compra (Role Playing). Sem isso, SM-3 (receita de comissão) fica exposta ao mesmo risco de conversão baixa que a sessão original identificou como bloqueio de confiança. `[NOTE FOR PM]`

#### FR-19: Base de benchmark para o ganho estimado
O sistema pode consultar uma base de benchmark curada, com **TechPowerUp (techpowerup.com) como fonte inicial**, para calcular o ganho percentual estimado (FR-14) de uma peça de upgrade que o usuário ainda não possui, comparando-a com o hardware atual do usuário.

**Consequences (testable):**
- Toda estimativa de ganho exibida em FR-14 carrega uma margem de confiança declarada e a data da última atualização da base de benchmark, visíveis ao usuário.
- Quando a peça sugerida ou o hardware atual do usuário não têm dado de benchmark correspondente na base, o sistema **não inventa um número** — a linha `Upgrade hardware = X%` é omitida em vez de estimada sem lastro. Isso é o que mantém FR-14 dentro do guard contra alucinação de LLM (§5).

**Notes:** `[NOTE FOR PM]` Mecanismo de extração/atualização é decisão de arquitetura — ver [§10](#10-questões-em-aberto), item 2.

## 5. Restrições e Guardrails

*Esta seção formaliza invariantes que já existem, implementados e cobertos por teste, no `otimizacao` — o módulo os herda, não os redesenha.*

**Segurança e reversibilidade**
- Nenhuma alteração de qualquer categoria é aplicada sem aceite explícito do usuário (FR-6) — é princípio arquitetural do projeto, não decisão deste módulo, e não abre exceção em nenhuma fase futura.
- Toda alteração de driver reversível tem rollback disponível via `ServicoBackup` (FR-7).
- **BIOS é a exceção explícita à reversibilidade.** Diferente de driver, uma atualização de BIOS não tem rollback possível via o app — se interrompida, pode comprometer fisicamente a placa-mãe. Por isso o app nunca executa a gravação por conta própria: apenas verifica, orienta e alerta (FR-2, FR-3), deixando a execução e a decisão final com o usuário, com recomendação explícita de buscar um profissional qualificado.
- Guard contra alucinação de LLM permanece ativo para qualquer recomendação gerada — nenhuma sugestão de peça, driver, software ou versão de BIOS pode ser inventada sem lastro em fonte oficial real (FR-1, FR-2), na base de benchmark (FR-19), ou no Inventário/Event Log (FR-4, FR-5). Quando a base de benchmark não cobre a peça, o sistema omite o número em vez de estimar sem lastro (FR-19).

**Privacidade e dados**
- Sanitização de dados antes de qualquer envio à nuvem é regra herdada do projeto e se aplica a qualquer telemetria futura deste módulo (inclusive à Prova Social Agregada, quando implementada na Fase 2).
- O monitoramento em tempo real permanece opt-in — só ocorre com o app aberto e mediante solicitação explícita; não há coleta em background/daemon.
- Toda verificação de versão (driver, software, BIOS) consulta apenas uma lista de domínios oficiais permitidos — nunca busca genérica ou fonte não verificada (FR-1, FR-2).

**Compatibilidade e catálogo**
- Toda sugestão de peça respeita o Teto de Compatibilidade calculado (FR-12) — o sistema não recomenda o que a máquina não aceita.
- O catálogo de peças hoje tem categorias vazias para CPU e Memória (lacuna L5 da auditoria) — pré-requisito técnico para FR-12/FR-13, fora do escopo de definição deste PRD.

**Conformidade (em aberto)**
- `[NOTE FOR PM]` A obrigação de divulgação legal no Brasil para recomendação de hardware com comissão (CDC, publicidade velada) não foi avaliada nesta sessão e precisa de revisão jurídica antes do lançamento pago. Ver [§10](#10-questões-em-aberto).

## 6. Monetização

O módulo tem duas fontes de receita complementares, não redundantes — a segunda cobre exatamente os casos onde a primeira não se aplica (ex.: notebook com RAM soldada).

- **Comissão de Loja Parceira** — via link direto de compra para uma lista fechada e pré-acordada de lojas (V1: Mercado Livre, Amazon, Kabum). Ativa em FR-16.
- **Assinatura Premium** *(Fase 2, fora do escopo deste V1)* — receita recorrente por liberação de módulos premium do app, independente de venda de hardware. `[ASSUMPTION]` A definição de quais módulos ficam atrás do paywall ainda não existe e precisa evitar contradizer a honestidade já validada como princípio do produto (achado 8 do brainstorming: evidência numérica é o que separa recomendação honesta de propaganda).

## 7. Não-Objetivos

*Consolidados nos Out of Scope de FR-1, FR-3, FR-6, FR-9, FR-16, no Glossário (§3) e em §5 — não repetidos aqui.*

## 8. Escopo do V1

### 8.1 Em Escopo

- Núcleo de Atualização completo (FR-1 a FR-7), incluindo verificação de drivers/software/BIOS via fontes oficiais, coleta de Event Log e correlação de causa-raiz.
- Diagnóstico de Manutenção completo (FR-8 a FR-11).
- Vitrine de Upgrade com Teto de Compatibilidade, Eixo de Qualidade, linha factual no Relatório de Resultado, lista fechada de Lojas Parceiras, caminho de conversão de notebook com RAM disponível, requisitos de confiança na listagem, e base de benchmark (TechPowerUp) para o ganho estimado (FR-12 a FR-19, exceto a monetização de notebook com RAM soldada).
- Comissão de Loja Parceira como único mecanismo de monetização ativo.

*Ordem definida a partir do ranking de prioridade que emergiu organicamente na Técnica "One Feature Only": núcleo → diagnóstico de manutenção → vitrine.*

**⚠️ Ressalva de dependência (Vitrine):** FR-12 (Teto de Compatibilidade) — o mecanismo central que sustenta toda a Feature 4.3 — depende de resolver as lacunas técnicas L4 (armazenamento ausente do Inventário) e L5 (catálogo vazio para CPU/Memória), que **não são cobertas por este PRD** (são pré-requisito de arquitetura). Listar a Vitrine como "Em Escopo" no V1 pressupõe que esse trabalho de arquitetura seja feito antes ou em paralelo — a Vitrine está condicionalmente em escopo, não incondicionalmente pronta para implementação direta a partir deste PRD.

### 8.2 Fora de Escopo para o V1

- **Prova Social Agregada** (dado anônimo de outros usuários como métrica de prova de ganho) — mecanismo de coleta, consentimento e formato de exibição não foram desenhados nesta sessão. Fase 2.
- **Assinatura Premium** — módulos, preço e paywall não definidos. Fase 2. `[NOTE FOR PM]` **Isso reabre a T2 original do brainstorming ("notebook não gera receita") para o segmento de RAM soldada.** A sessão já havia marcado essa resolução como parcial/frágil, não definitiva — sem a Assinatura Premium, o V1 real deixa esse segmento exatamente na situação que a T2 descrevia como problema. Revisitar prioridade se o timeline permitir.
- **Expansão do catálogo de peças além de RAM/CPU/SSD/GPU básicos** — depende de resolver as lacunas L4 (armazenamento no Inventário) e L5 (catálogo vazio para CPU/Memória) identificadas na auditoria técnica; tratado como pré-requisito de arquitetura, não como feature deste PRD.
- **Persona Bruno (entusiasta)** — não explorada na sessão de brainstorming; define o teto técnico do produto e é candidata a rodada futura de descoberta.
- **Divulgação legal (CDC)** — avaliação jurídica não incluída neste V1; ver [§5](#5-restrições-e-guardrails).

## 9. Métricas de Sucesso

*O participante confirmou que todas as métricas abaixo importam simultaneamente — não há uma única métrica "norte"; cada uma valida uma parte diferente da tese do produto.*

**Primária**
- **SM-1**: Redução de eventos de instabilidade (BSOD/WHEA/crash) por usuário ativo, comparando período pré/pós uso do Núcleo de Atualização. `[ASSUMPTION]` Meta numérica ainda não definida — produto está em fase de exploração; medir e calibrar meta após os primeiros dados reais. Valida FR-1, FR-2, FR-3, FR-4, FR-5.

**Secundárias**
- **SM-2**: Taxa de clique na linha `Upgrade hardware` do Relatório de Resultado que leva à Vitrine (funil de descoberta). Valida FR-14, FR-15.
- **SM-3**: Receita de comissão por compra confirmada via Loja Parceira. Valida FR-16, FR-18.
- **SM-4**: Retenção — usuários que reabrem o app dentro de uma janela definida após a primeira sessão. `[ASSUMPTION]` Janela de medição (7/14/30 dias) a definir.
- **SM-5**: Cobertura da base de benchmark — % de peças sugeridas na Vitrine que têm dado de ganho estimado disponível (vs. omitido por falta de cobertura). Valida FR-19.

**Contra-métricas (não otimizar)**
- **SM-C1**: Taxa de rejeição de sugestões de atualização/manutenção. Uma alta rejeição sinaliza recomendação ruim, não deve ser contornada empurrando clique — contrabalança SM-2.
- **SM-C2**: Reclamações de incompatibilidade de peça recomendada. Tolerância-alvo é zero; contrabalança SM-3 — receita nunca justifica recomendar peça errada.
- **SM-C3**: Baixa cobertura de SM-5 não deve ser "resolvida" inventando estimativa fora da base de benchmark — uma linha `Upgrade hardware` omitida por falta de dado real é honestidade (FR-19), não falha a corrigir a qualquer custo. Contrabalança SM-5.

## 10. Questões em Aberto

*Triadas por urgência: item 1 bloqueia a arquitetura de FR-12 — precisa de decisão antes ou durante o desenho técnico, não depois. Os demais podem ser resolvidos em paralelo pela arquitetura ou ficar para pesquisa futura sem travar o início do trabalho.*

**Bloqueia o início da arquitetura**

1. A Vitrine (FR-12 a FR-19) depende de resolver as lacunas técnicas L4 (armazenamento) e L5 (catálogo de CPU/Memória vazio) — ver ressalva em [§8.1](#81-em-escopo). A arquitetura precisa decidir se resolve isso como pré-requisito bloqueante ou se a Vitrine sai do V1 até lá.

**Arquitetura pode resolver em paralelo**

2. ~~Que base de dados sustenta a estimativa de ganho de uma peça que o usuário ainda não possui?~~ **Resolvido nesta sessão:** base de benchmark curada com TechPowerUp (techpowerup.com) como fonte inicial (FR-19). Em aberto apenas o mecanismo técnico de extração/atualização (scraping, API, curadoria manual) e a cadência de refresh — decisão de arquitetura.
3. Qual a lista exata de domínios oficiais permitidos para verificação de versão de driver/software/BIOS (FR-1, FR-2), e quem mantém essa lista atualizada conforme novos fabricantes entram no catálogo?
4. Qual o critério técnico exato de "correlação plausível" entre driver/software/BIOS desatualizados e evento do Event Log (FR-5)?
5. Dentro da própria Vitrine, cooler e fonte deveriam vir antes de GPU e CPU na ordem de exibição, já que a dor real mais citada é instabilidade/temperatura, não desempenho puro? Mesma lógica de honestidade que já sustenta FR-10 (serviço barato antes de peça cara), generalizada para dentro do catálogo de peças.
6. Como a Vitrine é descoberta por um usuário que nunca chega a ver o Relatório de Resultado (abandona o diagnóstico, ou nunca chega a rodá-lo)? FR-14/FR-15 resolvem descoberta só via essa única linha — a sessão de brainstorming chegou a levantar essa frente (How Might We) mas foi pausada antes de explorá-la; SM-2 mede apenas esse funil, então um segmento inteiro de usuário pode ficar sem caminho até a Vitrine sem que isso apareça em nenhuma métrica.

**Pesquisa/produto futuro — não trava nada agora**

7. Qual é o mecanismo técnico (telemetria, consentimento, formato) para a Prova Social Agregada entre usuários, dado que a sanitização antes da nuvem já é regra do projeto? *(Fase 2.)*
8. Quais módulos ficam atrás do paywall da Assinatura Premium, sem contradizer a honestidade como princípio de produto já validado? *(Fase 2.)*
9. Que divulgação legal (CDC, publicidade velada) é exigida para um app que recomenda hardware e recebe comissão no Brasil? *(Precisa de revisão jurídica antes do lançamento pago, não antes da arquitetura.)*
10. O risco de "vendedor conceituado com preço suspeito" (Premissa 5 do brainstorming) permanece real para sellers individuais dentro de marketplaces como Mercado Livre/Amazon — precisa de checagem de preço além da curadoria por loja?
11. Existe data ou janela alvo para sair da fase de exploração e lançar para usuários pagantes reais? *(Confirmado em aberto pelo participante nesta sessão.)*
12. Como a persona Bruno (entusiasta) muda os requisitos de Eixo de Qualidade e Teto de Compatibilidade, já que ele provavelmente já está no teto em tudo? Bruno também é o crítico público mais provável do produto (review, fórum, influencer de tecnologia) se o Eixo de Qualidade ou o Teto de Compatibilidade forem rasos — isso pode justificar puxar essa exploração para antes do lançamento, não depois.

## 11. Índice de Suposições

5 suposições `[ASSUMPTION]` marcadas inline: §2.2 (não-usuários), §2.3/UJ-3 (jornada de Diego), §6 (módulos da Assinatura Premium), §9/SM-1 e §9/SM-4 (metas numéricas ainda não definidas). Detalhe de cada uma no addendum, §F.
