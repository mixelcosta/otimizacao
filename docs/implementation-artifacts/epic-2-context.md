# Epic 2 Context: Diagnóstico de Manutenção

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Este épico entrega a segunda prioridade do produto (depois do Núcleo de Atualização): uma recomendação de manutenção de baixo custo (ex.: troca de pasta térmica) baseada em assinatura térmica real medida pelos sensores já existentes — nunca em pergunta de diagnóstico ao usuário. O único dado que o sistema não consegue coletar sozinho (data da última troca/limpeza) é perguntado uma única vez, sempre em formato de data. Depois que o usuário confirma ter feito a manutenção, o app prova que valeu a pena com uma comparação de temperatura antes/depois, sem exigir nenhum benchmark manual. O épico entrega valor completo e sozinho — a ordenação formal contra sugestões de peça da Vitrine (FR10) só é implementada depois, no Épico 3, sem exigir nenhuma mudança retroativa aqui.

## Stories

- Story 2.1: Usuário vê detecção de pasta térmica ressecada
- Story 2.2: Usuário informa a data da última manutenção, uma única vez
- Story 2.3: Usuário confirma a manutenção feita e vê prova de antes/depois de temperatura

## Requirements & Constraints

- A detecção de pasta térmica ressecada é feita só por leitura de sensor (temperatura em pelo menos dois momentos — idle e sob carga simulada/observada) — nenhuma pergunta de diagnóstico é feita ao usuário para chegar a esse achado.
- O único dado factual pedido ao usuário é a data da última troca/limpeza; a pergunta é sempre em formato de data, nunca "qual é o seu problema?" nem múltipla escolha de sintoma. É perguntada uma única vez por item, e a resposta fica salva no Inventário (não é perguntada de novo, a menos que o usuário a atualize).
- Depois da confirmação de manutenção feita pelo usuário, a tela exibe temperatura antes e depois lado a lado — sem exigir rodar nenhum benchmark.
- Guard anti-alucinação: nenhum achado de manutenção pode ser inventado sem lastro em leitura real de sensor.
- Nenhuma alteração de qualquer categoria é aplicada sem aceite explícito do usuário — mesmo princípio herdado do resto do produto, aqui aplicado à confirmação de que a manutenção física foi feita.
- Monitoramento de sensores continua opt-in — só ocorre com o app aberto e mediante solicitação explícita, nunca em background/daemon.
- FR10 (recomendação de menor custo aparece primeiro quando Manutenção e Vitrine competem pelo mesmo sintoma) não é responsabilidade deste épico — é composição feita depois pelo orquestrador de outro épico; este épico só precisa expor um valor de custo comparável em sua recomendação, sem decidir ordem nenhuma sozinho.

## Technical Decisions

- Nova fatia vertical `Features.Manutencao`, cobrindo FR8–FR11, seguindo o mesmo padrão arquitetural (`Core` domínio puro / `Agent` I/O de plataforma / `Features.*` fatia vertical) já usado no restante do produto.
- Reutiliza os sensores já existentes no `Agent` (mesmo padrão sob-demanda dos coletores existentes, sem daemon) — este épico não introduz um novo mecanismo de leitura de sensor, só a lógica de interpretação da assinatura térmica.
- A recomendação de manutenção retorna um valor de `Custo` comparável (contrato já definido no Épico 1) — necessário para a futura composição de ordenação por custo, mesmo que essa ordenação não seja implementada aqui.
- Todo novo caso de uso entra no roteador de IPC como um novo valor de `Metodo`; a UI nunca chama a fatia de features diretamente.
- Testes seguem o padrão xUnit + fakes manuais (sem Moq/NSubstitute), um projeto de testes dedicado por projeto novo.

## UX & Interaction Patterns

- **Confirmation Panel** (componente já nascido no Épico 1) é reusado aqui com severidade "manutencao": painel inline bloqueante, nunca modal, com o botão de ação primário desabilitado até a confirmação explícita do usuário. É nesse painel que a comparação de temperatura antes/depois é exibida.
- **Pergunta Factual** (componente novo introduzido por este épico): uma pergunta por vez, sempre em formato de data — nunca campo de texto livre nem múltipla escolha de sintoma; pergunta só uma vez por item, com resposta anterior pré-preenchida se já existir.
- Curva emocional da primeira visita à tela de inventário: tom de descoberta antes de qualquer aviso de risco (headline maior, sem banner de risco visível de imediato); achados de manutenção aparecem conforme o scan progride, não como uma lista de problemas já pronta.
- Voice and tone: microcópia sempre factual e específica, nunca alarmista nem vendedora — ex. "temperatura alta sob carga baixa — pode ser pasta térmica ressecada", nunca "⚠️ ALERTA! Superaquecimento!". O contraste de preço (ex. troca de pasta térmica barata ao lado de uma peça cara) deve falar por si, sem comentário adicional tipo "economize trocando só a pasta térmica!".
- Onde exatamente o Diagnóstico de Manutenção vive na navegação (aba nova ou sub-seção de Info Sistema) ainda não foi decidido nos artefatos de planejamento — ambos os caminhos são plausíveis.

## Cross-Story Dependencies

- Story 2.3 reusa o componente `Confirmation Panel` nascido na Story 1.2 (Épico 1), apenas com uma nova severidade ("manutencao") — não recria o componente.
- Story 2.3 depende do achado de manutenção detectado na Story 2.1 e, tipicamente, da data coletada na Story 2.2, mas a Story 2.3 já entrega valor completo e sozinha, independente da Story 3.8 (Épico 3) — a ordenação formal por custo entre Manutenção e Vitrine é aditiva, não um requisito para este épico funcionar.
