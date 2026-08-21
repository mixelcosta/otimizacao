---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/prd.md
  - docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/addendum.md
  - docs/planning-artifacts/architecture/architecture-otimizacao-2026-08-21/ARCHITECTURE-SPINE.md
  - docs/planning-artifacts/ux-designs/ux-otimizacao-2026-08-21/DESIGN.md
  - docs/planning-artifacts/ux-designs/ux-otimizacao-2026-08-21/EXPERIENCE.md
---

# otimizacao - Epic Breakdown

## Overview

Este documento decompõe o Módulo de Sugestão de Upgrade com Foco em Custo-Benefício em épicos e histórias implementáveis, a partir do PRD, da Espinha de Arquitetura e dos spines de UX (DESIGN.md/EXPERIENCE.md).

## Requirements Inventory

### Functional Requirements

FR1: Varredura de drivers e softwares desatualizados via fontes oficiais.
FR2: Verificação de versão de BIOS via fonte oficial do fabricante.
FR3: Orientação de atualização de BIOS com alerta de risco obrigatório (nunca grava BIOS).
FR4: Coleta de Event Log (BSOD, WHEA, crashes de aplicação).
FR5: Correlação causa-raiz no Diagnóstico, a partir do Event Log.
FR6: Aprovação obrigatória antes de qualquer atualização de driver aplicada pelo app.
FR7: Rollback por atualização de driver aplicada.
FR8: Detecção de assinatura térmica de manutenção degradada.
FR9: Coleta de dado factual de manutenção (data da última troca/limpeza).
FR10: Recomendação de serviço ordenada por custo (Manutenção antes de Upgrade quando concorrem).
FR11: Prova de antes/depois de temperatura.
FR12: Cálculo do Teto de Compatibilidade por máquina.
FR13: Sugestão no Eixo de Qualidade quando a máquina está no teto.
FR14: Linha factual de ganho no Relatório de Resultado (`Otimização do S.O. = X%` / `Upgrade hardware = X%`).
FR15: Navegação da linha de upgrade para a Vitrine.
FR16: Listagem restrita a Lojas Parceiras (Mercado Livre, Amazon, Kabum).
FR17: Caminho de conversão para notebook (RAM/SSD quando há slot; sem monetização quando RAM soldada).
FR18: Requisitos de confiança na listagem da Vitrine (parcelamento, entrega, selo de qualidade).
FR19: Base de benchmark TechPowerUp para o ganho estimado.

### NonFunctional Requirements

NFR1 (Segurança/reversibilidade): nenhuma alteração de qualquer categoria é aplicada sem aceite explícito do usuário; toda atualização de driver tem rollback via `ServicoBackup`; BIOS nunca é gravada pelo app — só orientada.
NFR2 (Guard anti-alucinação): nenhuma sugestão de peça, driver, software, versão de BIOS ou ganho estimado pode ser inventada sem lastro em fonte oficial real, na base de benchmark, ou no Inventário/Event Log.
NFR3 (Privacidade/dados): sanitização de dados antes de qualquer envio à nuvem; monitoramento em tempo real permanece opt-in (sem daemon/background); toda verificação de versão consulta só uma lista de domínios oficiais permitidos.
NFR4 (Compatibilidade): toda sugestão de peça respeita o Teto de Compatibilidade calculado — nunca recomenda o que a máquina não aceita fisicamente.
NFR5 (Honestidade de dado): quando a base de benchmark não cobre uma peça, o sistema omite o número em vez de estimar sem lastro; toda estimativa carrega margem de confiança e data de atualização.

### Additional Requirements

Extraídos da Espinha de Arquitetura (`ARCHITECTURE-SPINE.md`, AD-1 a AD-10):

- **Pré-requisito bloqueante, antes de qualquer história:** corrigir o bug de build do `.gitignore` que apaga `Features.Upgrade/Data/` e `Features.Drivers/Data/` do controle de versão — impede os dois projetos de compilar em clone novo.
- Consolidar toda lógica de sugestão de upgrade em `Features.Upgrade`; `UpgradeViewModel` perde seu catálogo hardcoded próprio (AD-2).
- Papel fixo de cada fonte de dados: TechPowerUp (camada gratuita) para ganho estimado; catálogo estático curado para compatibilidade física; BuildCores isolado no chat, nunca fonte de FR (AD-3).
- Novo componente `IProvedorFonteOficial` (único, em `Features.Atualizacao`) para verificação de versão de driver/software/BIOS contra allowlist de domínios — substitui gradualmente os catálogos estáticos hoje usados como fallback (AD-4).
- Novo leitor de Event Log em `Agent/EventLog/`, sob demanda, sem daemon; correlação causa-raiz é lógica de domínio em `Features.Atualizacao` (AD-5).
- Novo componente `Armazenamento` no `Inventario` (capacidade, interface, slots livres), separado de `SaudeDisco` — resolve a lacuna L4 (AD-6).
- `CategoriaAcao` (otimização de SO) e `TipoPecaUpgrade` (peça de upgrade) continuam catálogos separados — não confundir (AD-7).
- `Features.Atualizacao` é o único ponto que compõe e ordena recomendações de Manutenção e Vitrine por um tipo `Custo` comparável (AD-8).
- L5 (catálogo de peças CPU/Memória) não bloqueia a primeira história da Vitrine — cobertura estreita é aceitável no V1 e cresce depois. L4 segue bloqueante especificamente para a parte de SSD, até o coletor de `Agent/Storage/` existir (AD-9).
- Novo componente `IProvedorLojaParceira` (em `Features.Upgrade/LojasParceiras/`) para preço/estoque/prazo/link de comissão — mecanismo exato por loja (API de afiliado, feed, contrato) é decisão comercial, não técnica (AD-10).
- Todo novo caso de uso entra em `RoteadorIpc` como um novo valor de `Metodo`; testes seguem o padrão xUnit + fakes manuais, um projeto `tests/HardwareOptimizer.<Nome>.Tests` por projeto novo.

### UX Design Requirements

Extraídos de `EXPERIENCE.md` (Component Patterns, State Patterns) e `DESIGN.md` (Components):

UX-DR1: **Confirmation Panel** (novo componente) — painel inline bloqueante (nunca modal) para aprovação de driver e alerta de risco de BIOS; botão de ação primário desabilitado até a condição de aceite ser cumprida; alerta de BIOS exibido toda vez, nunca "não mostrar de novo".
UX-DR2: **Estimate Tag** (novo componente) — pill discreto junto de todo `GanhoEstimado`, mostrando margem de confiança e data de atualização; nunca aparece sem o número que qualifica.
UX-DR3: **Vitrine Item Card** (novo componente) — preço em destaque neutro (nunca cor de marca/sucesso); badges de confiança (parcelamento, prazo, loja) sempre visíveis, nunca atrás de hover.
UX-DR4: **Pergunta Factual** (padrão novo) — uma pergunta por vez, formato de data, nunca texto livre nem múltipla escolha de sintoma; pergunta só uma vez por item.
UX-DR5: **Linha do Relatório de Resultado** — as duas linhas (`Otimização do S.O.` / `Upgrade hardware`) sempre lado a lado, mesma hierarquia visual; só a linha de upgrade é clicável.
UX-DR6: **Desbloqueio de navegação Premium** — Drivers, Guia BIOS IA e a aba de visualização da Vitrine saem do bloqueio "Premium" da sidebar no V1 (decisão confirmada com o usuário).
UX-DR7: **Estado "sem cobertura"** — linha `Upgrade hardware` omitida por completo quando a base de benchmark não cobre a peça (nunca "N/A" ou zero).
UX-DR8: **Estado "no teto de compatibilidade"** — nunca tela vazia; cai para sugestão no Eixo de Qualidade.
UX-DR9: **Curva emocional da tela de inventário** (primeira visita) — tom de descoberta antes de qualquer aviso de risco; achados de manutenção aparecem conforme o scan progride, não como lista pronta.
UX-DR10: **Voice and Tone** — microcópia sempre factual e específica, nunca alarmista nem vendedora (ver tabela Faça/Não faça em `EXPERIENCE.md`).

### FR Coverage Map

FR1: Epic 1 - Varredura de drivers/software via fonte oficial
FR2: Epic 1 - Verificação de versão de BIOS via fonte oficial
FR3: Epic 1 - Orientação de atualização de BIOS com alerta de risco
FR4: Epic 1 - Coleta de Event Log
FR5: Epic 1 - Correlação causa-raiz no Diagnóstico
FR6: Epic 1 - Aprovação obrigatória antes de atualização de driver
FR7: Epic 1 - Rollback de atualização de driver
FR8: Epic 2 - Detecção de assinatura térmica degradada
FR9: Epic 2 - Coleta de dado factual de manutenção
FR10: Epic 3 - Ordenação por custo entre Manutenção e Vitrine (composição implementada no Epic 3; Epic 2 entrega valor completo mesmo antes do Epic 3 existir)
FR11: Epic 2 - Prova de antes/depois de temperatura
FR12: Epic 3 - Cálculo do Teto de Compatibilidade
FR13: Epic 3 - Sugestão no Eixo de Qualidade
FR14: Epic 3 - Linha factual de ganho no Relatório de Resultado
FR15: Epic 3 - Navegação da linha de upgrade para a Vitrine
FR16: Epic 3 - Listagem restrita a Lojas Parceiras
FR17: Epic 3 - Caminho de conversão para notebook
FR18: Epic 3 - Requisitos de confiança na listagem
FR19: Epic 3 - Base de benchmark TechPowerUp

## Epic List

### Epic 1: Núcleo de Atualização
Usuário mantém drivers, software e BIOS verificados contra fontes oficiais, entende a causa-raiz de travamentos via Event Log, e aprova cada atualização com rollback disponível. Inclui, como primeira história, a correção do bug de build (`.gitignore`) que hoje impede `Features.Upgrade`/`Features.Drivers` de compilar em clone novo — pré-requisito técnico para qualquer trabalho nos dois projetos.
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR6, FR7

### Epic 2: Diagnóstico de Manutenção
Usuário recebe recomendação de manutenção de baixo custo (ex.: troca de pasta térmica) baseada em assinatura térmica real, com prova de antes/depois — sem precisar responder pergunta de diagnóstico, só um dado factual que o sistema não alcança sozinho.
**FRs covered:** FR8, FR9, FR11 *(FR10 entra formalmente com o Epic 3 — ver nota na Coverage Map)*

### Epic 3: Vitrine de Upgrade
Usuário vê sugestão de peça compatível com sua máquina, com ganho estimado honesto (via TechPowerUp, omitido quando não há cobertura), comprável em loja parceira de confiança. Consolida a lógica de upgrade em `Features.Upgrade` (aposentando o catálogo hardcoded da `UpgradeViewModel`) e implementa a ordenação por custo entre Diagnóstico de Manutenção e Vitrine (FR10).
**FRs covered:** FR10, FR12, FR13, FR14, FR15, FR16, FR17, FR18, FR19

## Epic 1: Núcleo de Atualização

Usuário mantém drivers, software e BIOS verificados contra fontes oficiais, entende a causa-raiz de travamentos via Event Log, e aprova cada atualização com rollback disponível.

### Story 1.1: Corrigir bug de build que impede compilação de Features.Upgrade e Features.Drivers

Pré-requisito técnico — sem isso nenhuma história seguinte deste épico (nem do Épico 3) pode ser desenvolvida ou testada em um clone novo do repositório.

**Acceptance Criteria:**

**Given** um clone novo do repositório `otimizacao`
**When** o desenvolvedor executa build de `HardwareOptimizer.Features.Upgrade` e `HardwareOptimizer.Features.Drivers`
**Then** o build completa sem erro `CS1566`, com `Data/hardware_catalog.json` e `Data/whql_catalog.json` presentes e versionados
**And** a regra do `.gitignore` que causava a exclusão (`data/`) é substituída por uma âncora que não afeta as pastas `Data/` dos dois projetos

### Story 1.2: Usuário varre e aprova atualização de driver, com rollback

As a usuário do Otimize Builder,
I want ver quais drivers estão desatualizados, comparados contra a fonte oficial do fabricante,
So that eu possa aprovar a atualização sabendo que existe rollback se algo der errado.

**Acceptance Criteria:**

**Given** que o usuário abriu a tela de Núcleo de Atualização com o app aberto
**When** o sistema varre os drivers instalados
**Then** cada driver desatualizado aparece com versão atual vs. versão oficial mais recente, consultada via `IProvedorFonteOficial` contra uma allowlist de domínios (nunca busca genérica)

**Given** um driver desatualizado listado
**When** o usuário clica em atualizar
**Then** o `Confirmation Panel` (severidade "driver") aparece inline — nunca como modal — com o botão de aplicar desabilitado até o usuário confirmar
**And**, após confirmação, um ponto de restauração é criado antes da alteração e o rollback fica acessível na mesma tela

**Given** que este é o primeiro fluxo do módulo a produzir um resultado comparável
**When** a história é implementada
**Then** os contratos `Core/GanhoEstimado.cs` e `Core/Custo.cs` são criados como parte do escopo técnico desta história (não como história separada), e o componente `Confirmation Panel` nasce parametrizado por severidade (`driver` neste momento, `bios` e `manutencao` reusam o mesmo componente nas histórias seguintes)

### Story 1.3: Usuário é alertado sobre software desatualizado via fonte oficial

As a usuário do Otimize Builder,
I want ser avisado quando um software instalado está desatualizado, com o link oficial pra baixar a versão nova,
So that eu decida se e quando atualizar, sem o app instalar nada por mim.

**Acceptance Criteria:**

**Given** que o usuário rodou a varredura do Núcleo de Atualização
**When** o sistema encontra software desatualizado via `IProvedorFonteOficial` (mesmo componente da Story 1.2)
**Then** o item aparece na lista com versão atual vs. oficial e um link direto pra fonte oficial
**And** nenhum download ou instalação é feito pelo app — a ação é só do usuário, fora do app

### Story 1.4: Usuário é orientado a atualizar a BIOS com alerta de risco obrigatório

As a usuário do Otimize Builder,
I want saber quando minha BIOS está desatualizada e ser claramente avisado do risco antes de prosseguir,
So that eu decida com informação completa se atualizo sozinho ou busco um profissional.

**Acceptance Criteria:**

**Given** que o sistema identificou o fabricante e modelo da placa-mãe
**When** consulta a versão mais recente de BIOS via `IProvedorFonteOficial`
**Then** compara com a versão instalada e sinaliza se está desatualizada

**Given** uma BIOS desatualizada sinalizada
**When** o usuário opta por ver a orientação de atualização
**Then** o `Confirmation Panel` (severidade "bios") aparece — sempre, mesmo que o usuário já tenha visto antes — informando que a interrupção durante a atualização pode comprometer a placa-mãe e recomendando um profissional qualificado
**And** o app nunca executa a gravação da BIOS — só orienta; a decisão de prosseguir sozinho é sempre do usuário

### Story 1.5: Usuário vê a causa-raiz de travamentos, correlacionada com o Event Log

As a usuário do Otimize Builder,
I want que o app me diga qual driver ou BIOS provavelmente está causando meus travamentos,
So that eu resolva a causa real em vez de adivinhar.

**Acceptance Criteria:**

**Given** que o app está aberto e o usuário solicita a leitura
**When** o sistema lê o Event Log do Windows (BSOD, WHEA, crash de aplicação)
**Then** cada evento é registrado com timestamp, tipo, e driver/processo associado quando disponível — consulta sob demanda, nunca em background/daemon

**Given** um driver ou BIOS desatualizado (Stories 1.2/1.4) e eventos do Event Log no mesmo período
**When** existe correlação plausível entre os dois (mesmo subsistema)
**Then** o Diagnóstico nomeia a causa específica, não uma mensagem genérica
**And** quando não há correlação, o sistema mostra o achado sem inventar uma causa

## Epic 2: Diagnóstico de Manutenção

Usuário recebe recomendação de manutenção de baixo custo (ex.: troca de pasta térmica) baseada em assinatura térmica real, com prova de antes/depois — sem precisar responder pergunta de diagnóstico, só um dado factual que o sistema não alcança sozinho.

### Story 2.1: Usuário vê detecção de pasta térmica ressecada

As a usuário do Otimize Builder,
I want que o app detecte se minha pasta térmica está ressecada,
So that eu saiba disso antes de considerar qualquer upgrade de peça pra resolver superaquecimento.

**Acceptance Criteria:**

**Given** que o usuário solicitou o diagnóstico de manutenção
**When** o sistema lê os sensores já existentes em pelo menos dois momentos (idle e sob carga simulada/observada)
**Then** temperatura alta sob carga baixa é sinalizada como possível pasta térmica ressecada ou necessidade de limpeza
**And** nenhuma pergunta de diagnóstico é feita ao usuário — a detecção é só por sensor

### Story 2.2: Usuário informa a data da última manutenção, uma única vez

As a usuário do Otimize Builder,
I want informar quando foi minha última limpeza/troca de pasta térmica,
So that o app tenha esse dado sem precisar me perguntar de novo depois.

**Acceptance Criteria:**

**Given** que o sistema precisa de um dado que não consegue coletar sozinho
**When** pergunta ao usuário a data da última troca/limpeza
**Then** a pergunta é sempre em formato de data — nunca "qual é o seu problema?"
**And** a resposta fica salva no Inventário e não é perguntada de novo, a menos que o usuário a atualize

### Story 2.3: Usuário confirma a manutenção feita e vê prova de antes/depois de temperatura

As a usuário do Otimize Builder,
I want ver a comparação de temperatura antes e depois de fazer a manutenção recomendada,
So that eu tenha prova de que valeu a pena, sem precisar rodar nenhum benchmark.

**Acceptance Criteria:**

**Given** uma recomendação de manutenção detectada (Story 2.1)
**When** o usuário confirma que realizou a manutenção, via `Confirmation Panel` (severidade "manutencao", reusando o componente da Story 1.2)
**Then** a tela de confirmação exibe temperatura antes e depois lado a lado
**And** esta história entrega valor completa e sozinha — a ordenação formal contra sugestões de peça (FR10) é implementada depois, na Story 3.8, sem exigir nenhuma mudança nesta história

## Epic 3: Vitrine de Upgrade

Usuário vê sugestão de peça compatível com sua máquina, com ganho estimado honesto, comprável em loja parceira de confiança.

### Story 3.1: Consolidar a sugestão de upgrade em Features.Upgrade

As a usuário do Otimize Builder,
I want que a tela de Upgrade sempre reflita validação real de compatibilidade,
So that eu nunca receba uma sugestão de peça que não caiba fisicamente na minha máquina.

**Acceptance Criteria:**

**Given** a tela de Upgrade existente (`UpgradeViewModel`)
**When** esta história é implementada
**Then** o catálogo hardcoded próprio da ViewModel é removido, e ela passa a consumir exclusivamente `ValidadorCompatibilidade`/`GeradorSugestoes`/`CalculadoraGargalo` de `Features.Upgrade`
**And** nenhum dado novo de peça (RAM, SSD) entra por switch novo na ViewModel — só pelo catálogo de `Features.Upgrade`

### Story 3.2: Cálculo do Teto de Compatibilidade por máquina

As a usuário do Otimize Builder,
I want ver só peças que realmente cabem na minha máquina,
So that eu não perca tempo considerando algo incompatível.

**Acceptance Criteria:**

**Given** o Inventário do usuário
**When** o sistema calcula o Teto de Compatibilidade (RAM, socket de CPU, GPU vs. fonte)
**Then** nenhuma peça fisicamente incompatível é sugerida

**Given** que o Armazenamento não existe hoje como componente do Inventário (lacuna L4)
**When** esta história é implementada
**Then** o novo componente `Armazenamento` (capacidade, interface, slots livres) é adicionado ao `Inventario`, com coletor em `Agent/Storage/` — sem isso, sugestão de SSD fica de fora do Teto de Compatibilidade
**And** o catálogo estático atual (~15 peças) é aceito como suficiente para esta primeira história — cobertura estreita não bloqueia (AD-9)

### Story 3.3: Sugestão no Eixo de Qualidade quando a máquina está no teto

As a usuário do Otimize Builder,
I want ainda receber uma sugestão útil mesmo quando minha máquina já está no limite de upgrade,
So that o app nunca me deixe sem nenhuma opção.

**Acceptance Criteria:**

**Given** uma máquina no Teto de Compatibilidade de um componente (Story 3.2)
**When** existe opção compatível no Eixo de Qualidade (menor latência, maior frequência, ou modelo mais frio)
**Then** essa sugestão aparece no lugar de uma tela vazia

### Story 3.4: Base de benchmark TechPowerUp para o ganho estimado

As a usuário do Otimize Builder,
I want ver o ganho estimado de uma peça antes de decidir comprá-la,
So that eu tome a decisão com dado real, não com número inventado.

**Acceptance Criteria:**

**Given** uma peça sugerida pela Vitrine
**When** o sistema consulta a camada gratuita da TechPowerUp
**Then** o resultado preenche o contrato `GanhoEstimado` (já criado na Story 1.2) com `Percentual`, `MargemConfianca` e `AtualizadoEm`

**Given** uma peça sem cobertura na base de benchmark
**When** o Relatório/Vitrine tentaria exibir o ganho
**Then** o número é omitido por completo — nunca "N/A" ou zero

### Story 3.5: Linha factual de ganho no Relatório de Resultado, com navegação pra Vitrine

As a usuário do Otimize Builder,
I want ver lado a lado quanto ganho de otimização de software e quanto ganho de upgrade de peça,
So that eu decida com informação igual pras duas opções, sem viés de venda.

**Acceptance Criteria:**

**Given** um diagnóstico concluído (Núcleo de Atualização e/ou Diagnóstico de Manutenção)
**When** o Relatório de Resultado é exibido
**Then** as linhas `Otimização do S.O. = X%` e `Upgrade hardware = X%` aparecem lado a lado, mesma hierarquia visual, sem preço/loja/texto de venda
**And** a linha `Upgrade hardware` só aparece quando existe ao menos uma sugestão válida sob o Teto de Compatibilidade (Story 3.2)

**Given** a linha `Upgrade hardware` visível
**When** o usuário clica nela
**Then** a Vitrine abre em tela separada — nunca dentro do fluxo de Diagnóstico ou do próprio Relatório

### Story 3.6: Listagem restrita a Lojas Parceiras, com requisitos de confiança

As a usuário do Otimize Builder,
I want comprar a peça sugerida numa loja confiável, com parcelamento e prazo claros,
So that eu tenha segurança pra concluir a compra.

**Acceptance Criteria:**

**Given** uma peça sugerida na Vitrine
**When** o sistema busca dados comerciais via `IProvedorLojaParceira`
**Then** só aparecem Mercado Livre, Amazon e Kabum — nenhum vendedor fora da lista pré-acordada
**And** cada item mostra link de comissão, parcelamento (quando disponível), prazo de entrega estimado e indicador de produto original

### Story 3.7: Caminho de conversão para notebook

As a usuário de notebook do Otimize Builder,
I want ver uma sugestão de upgrade que faça sentido pro meu notebook,
So that eu não veja uma sugestão de peça impossível de instalar.

**Acceptance Criteria:**

**Given** um notebook com slot de RAM/armazenamento disponível
**When** a Vitrine gera sugestões
**Then** RAM/SSD aparecem como peça, junto com o pacote de otimização de SO

**Given** um notebook com RAM soldada (sem slot)
**When** a Vitrine gera sugestões
**Then** só o caminho de otimização de software aparece — nenhuma sugestão de troca de RAM impossível

### Story 3.8: Ordenação por custo entre Diagnóstico de Manutenção e Vitrine

As a usuário do Otimize Builder,
I want ver a recomendação mais barata primeiro quando manutenção e upgrade de peça competem pelo mesmo sintoma,
So that eu confie que o app não está me empurrando pro caminho mais caro.

**Acceptance Criteria:**

**Given** uma recomendação de Diagnóstico de Manutenção (Épico 2) e uma de Vitrine (Stories 3.2–3.7) para o mesmo sintoma
**When** `Features.Atualizacao` compõe o resultado
**Then** a recomendação de menor `Custo` (contrato criado na Story 1.2) aparece primeiro
**And** nem `Features.Manutencao` nem `Features.Upgrade` decidem essa ordem sozinhas — só o orquestrador
