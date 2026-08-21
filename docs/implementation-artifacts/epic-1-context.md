# Epic 1 Context: Núcleo de Atualização

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Este épico entrega a capacidade mínima e defensável do produto: o usuário mantém drivers, softwares instalados e BIOS verificados contra fontes oficiais, entende a causa-raiz de travamentos (BSOD/WHEA/crash) via Event Log, e aprova cada atualização de driver com rollback disponível. É a resposta direta à dor nº 1 identificada nas personas (tela azul/travamento), e alimenta a linha `Otimização do S.O. = X%` da Trilha Grátis no Relatório de Resultado. Inclui, como pré-requisito técnico bloqueante, a correção de um bug de build (`.gitignore`) que hoje impede `Features.Upgrade` e `Features.Drivers` de compilar em clone novo do repositório — sem essa correção, nenhuma história deste épico (nem do Épico 3, que depende dos mesmos projetos) pode ser desenvolvida ou testada.

## Stories

- Story 1.1: Corrigir bug de build que impede compilação de Features.Upgrade e Features.Drivers
- Story 1.2: Usuário varre e aprova atualização de driver, com rollback
- Story 1.3: Usuário é alertado sobre software desatualizado via fonte oficial
- Story 1.4: Usuário é orientado a atualizar a BIOS com alerta de risco obrigatório
- Story 1.5: Usuário vê a causa-raiz de travamentos, correlacionada com o Event Log

## Requirements & Constraints

- Verificação de versão de driver, software e BIOS deve comparar exclusivamente contra fontes oficiais (site do fabricante/canal oficial) — nunca agregador de terceiros ou busca genérica na web.
- Para software de terceiros: o app apenas alerta e aponta o link oficial; download/instalação é sempre feito pelo usuário, fora do app.
- BIOS: o app nunca grava a BIOS — apenas identifica fabricante/modelo, compara versão e orienta. Alerta de risco (interrupção pode comprometer a placa-mãe, recomendação de profissional qualificado) é obrigatório e exibido **toda vez**, nunca só na primeira.
- Nenhuma atualização de driver é aplicada sem aprovação explícita do usuário por item; toda atualização aplicada cria ponto de restauração antes da alteração, com rollback acessível na mesma tela.
- Event Log (BSOD, WHEA, crash de aplicação) é lido sob demanda (app aberto, solicitação explícita) — nunca em background/daemon. Cada evento registra timestamp, tipo e driver/processo associado quando disponível, consultável por período.
- Correlação causa-raiz: quando existe correlação plausível entre driver/software/BIOS desatualizado e eventos do mesmo subsistema, o Diagnóstico nomeia a causa específica; quando não há correlação, mostra o achado sem atribuir causa forçada (guard anti-alucinação).
- Toda consulta a fonte oficial é restrita a uma allowlist de domínios permitidos — a lista exata de domínios e quem a mantém ainda não foi definida (gap em aberto, não bloqueia a história, mas precisa de decisão durante a implementação).
- O critério técnico exato de "correlação plausível" (Story 1.5) também não foi definido nas fontes — fica como decisão de implementação.

## Technical Decisions

- Nenhuma feature nova reinventa aprovação/rollback: toda ação mutante (driver) declara `PreCondicoes` e passa por `ExecutorControlado.AplicarPerfilAsync`; reaproveita `ServicoBackup` para o ponto de restauração. Nenhum caminho de aplicação paralelo.
- Novo componente único `IProvedorFonteOficial`, em `Features.Atualizacao/ProvedorFonteOficial/`, resolve versão-mais-recente para driver, software e BIOS contra a allowlist. `Features.Drivers` e `Core/Bios` não implementam consulta própria — chamam esse componente como dependência. `RepositorioWhqlEstatico` e `BancoCuradoBios` (catálogos estáticos atuais) viram *fallback* apenas quando a fonte oficial não responde, nunca fonte primária depois de a integração real existir.
- Novo leitor `Agent/EventLog/` lê o Event Log do Windows sob demanda (mesmo padrão de `ColetorInventario`/`ServicoSensores`, sem daemon). Resultado é um novo contrato em `Core` (`EventoInstabilidade.cs`), não um campo dentro de `Metricas`. Não confundir com `MedicaoEstresse`/`AnalisadorRegressao` (teste de estresse acionado pelo usuário) — são conceitos diferentes.
- A correlação causa-raiz (FR-5) é lógica de domínio em `Features.Atualizacao`, não no leitor de Event Log.
- Story 1.2 é responsável por criar os contratos `Core/GanhoEstimado.cs` (`Percentual`, `MargemConfianca`, `AtualizadoEm`) e `Core/Custo.cs` — usados depois pelas linhas do Relatório de Resultado e pela ordenação de custo do Épico 3 (Story 3.8). O componente `Confirmation Panel` nasce parametrizado por severidade nesta história (`driver`); `bios` (Story 1.4) e `manutencao` (Épico 2) reusam o mesmo componente.
- Novo projeto `Features.Atualizacao` orquestra FR-1 a FR-7. Todo caso de uso novo entra em `RoteadorIpc` como um novo valor de `Metodo`; a UI nunca chama `Agent`/`Features.*` diretamente.
- Convenções herdadas: nomes em português com sufixos existentes (`Servico*`, `Coletor*`, `Leitor*`, `Provedor*`), interfaces `I*`; DI manual via construtor com default; testes xUnit com fakes manuais (sem Moq/NSubstitute), projeto `tests/HardwareOptimizer.Features.Atualizacao.Tests` seguindo o padrão 1:1.
- Ainda não decidido (não bloqueia a implementação, mas é lacuna conhecida): a fonte de dado que alimenta a linha `Otimização do S.O. = X%` — candidato natural é `CalculadoraScore`, hoje uma nota de saúde 0-100, não um ganho percentual previsto.

## UX & Interaction Patterns

- **Confirmation Panel** (componente novo, não existe hoje no app): painel bloqueante inline no fluxo de scroll — nunca modal/popup. Borda de 2px na cor semântica (`accent`/`warning` para driver, `critical` para BIOS). Botão de ação primário fica **desabilitado** até a condição de aceite ser cumprida (para driver: checkbox "Entendi, aplicar atualização"); o botão de rollback aparece na mesma tela onde a alteração foi aprovada, nunca em menu separado.
- BIOS: o alerta de risco é exibido toda vez que uma atualização é orientada, mesmo que o usuário já tenha visto antes — nunca "não mostrar de novo". O botão de ação do painel de BIOS nunca executa gravação, sempre abre o guia de orientação. Por ser o painel de maior consequência de erro do módulo, precisa comunicar severidade por cor **e** texto, nunca só borda vermelha.
- Superfície: a tela do Núcleo de Atualização é alcançada pela sidebar em "Drivers" (renomeada/expandida); Drivers e Guia BIOS IA saem do bloqueio "Premium" da sidebar no V1 — ver a recomendação não é pago, só a compra em outra trilha é.
- Tom de voz: sempre factual e específico, nunca alarmista. Exemplo aprovado: "Driver de vídeo desatualizado — pode ser a causa dos travamentos" (não "⚠️ ALERTA! Seu driver está PERIGOSAMENTE desatualizado!").
- Versões técnicas (driver, BIOS) usam fonte monoespaçada (`Consolas`), seguindo o padrão já usado em `DriversView`/`ConfiguracoesView`.

## Cross-Story Dependencies

- Story 1.1 (fix do `.gitignore`) bloqueia todas as demais histórias deste épico e também o Épico 3 (mesmos projetos `Features.Upgrade`/`Features.Drivers`) — precisa ser a primeira a ser implementada.
- Story 1.2 cria os contratos `GanhoEstimado` e `Custo`, além do componente `Confirmation Panel` parametrizado por severidade — pré-requisito técnico para a Story 1.4 (severidade "bios") e para o Épico 2 (Story 2.3, severidade "manutencao") e Épico 3 (Story 3.4, Story 3.8).
- Story 1.5 (correlação causa-raiz) consome o resultado de driver/BIOS desatualizado já identificado nas Stories 1.2/1.4 — precisa delas para ter o que correlacionar com o Event Log.
