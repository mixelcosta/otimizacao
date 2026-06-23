# Documento de Testes — HardwareOptimizer / Otimize Builder

Data de execução: 2026-06-15

---

## 1. Testes Automatizados

Executados com `dotnet test --configuration Release`.

### Resultado Geral

| Assembly | Aprovados | Falhas | Ignorados | Duração |
|---|---|---|---|---|
| HardwareOptimizer.Core.Tests | 87 | 0 | 0 | ~31 ms |
| HardwareOptimizer.Agent.Tests | 151 | 0 | 0 | ~321 ms |
| HardwareOptimizer.Cerebro.Tests | 26 | 0 | 0 | ~210 ms |
| HardwareOptimizer.Ipc.Tests | 24 | 0 | 0 | ~14 s |
| HardwareOptimizer.Features.Upgrade.Tests | 20 | 0 | 0 | ~74 ms |
| HardwareOptimizer.Features.LifeCounter.Tests | 8 | 0 | 0 | ~17 ms |
| HardwareOptimizer.Features.Licensing.Tests | 20 | 0 | 0 | ~45 ms |
| HardwareOptimizer.Features.Drivers.Tests | 17 | 0 | 0 | ~46 ms |
| HardwareOptimizer.WindowsService.Tests | 10 | 0 | 0 | ~43 ms |
| HardwareOptimizer.App.Tests | 83 | 0 | 0 | ~372 ms |
| **TOTAL** | **446** | **0** | **0** | |

> Última execução com 0 falhas: 2026-06-23.

---

## 2. Testes por Módulo

### 2.1 HardwareOptimizer.App.Tests — OtimizadorWindowsViewModelTests

#### Inicialização (Startup)

| # | Teste | Descrição | Resultado |
|---|---|---|---|
| 1 | `Popular_com_entradas_popula_lista` | `Popular()` com 2 entradas deve popular `EntradasStartup` com Spotify e Steam | ✅ |
| 2 | `Popular_sem_entradas_lista_vazia` | `Popular()` com lista vazia não deve adicionar itens | ✅ |
| 3 | `Popular_ordena_por_impacto_decrescente` | Ordem: Alto → Médio → Baixo | ✅ |
| 4 | `Toggle_entrada_sucesso_inverte_ativo` | Ao clicar em DESATIVAR com resposta de sucesso, `Ativo` muda de `true` para `false` | ✅ |
| 5 | `Toggle_entrada_falha_mantem_estado` | Ao clicar em DESATIVAR com falha IPC, `Ativo` permanece `true` | ✅ |

#### InicializacaoEntradaViewModel

| # | Teste | Descrição | Resultado |
|---|---|---|---|
| 6 | `InicializacaoEntradaViewModel_cor_impacto_alto_e_vermelho` | Impacto Alto → `CorImpacto = #FF4444` | ✅ |
| 7 | `InicializacaoEntradaViewModel_cor_impacto_medio_e_amarelo` | Impacto Médio → `CorImpacto = #FFCC00` | ✅ |

#### Serviços

| # | Teste | Descrição | Resultado |
|---|---|---|---|
| 8 | `CarregarServicos_sucesso_popula_lista` | Ao navegar para aba Serviços, os 3 serviços retornados pelo fake são exibidos | ✅ |
| 9 | `FiltroServicos_filtra_por_nome` | Filtrar por "spooler" retorna apenas o Print Spooler | ✅ |
| 10 | `ToggleServico_running_para_servico` | Serviço em Running → toggle deve chamar `pararservico` e Status mudar para Stopped | ✅ |

#### ServicoViewModel

| # | Teste | Descrição | Resultado |
|---|---|---|---|
| 11 | `ServicoViewModel_running_exibe_botao_parar` | Status Running → `TextoBotao = "PARAR"`, `Rodando = true` | ✅ |
| 12 | `ServicoViewModel_stopped_exibe_botao_iniciar` | Status Stopped → `TextoBotao = "INICIAR"`, `Rodando = false` | ✅ |
| 13 | `ServicoViewModel_pid_zero_exibe_traco` | PID = 0 → `PidTexto = "—"` | ✅ |

---

## 2.2 HardwareOptimizer.App.Tests — ConfiguracoesViewModelTests (2026-06-23)

| # | Teste | Descrição | Resultado |
|---|---|---|---|
| 1 | `Status_inicial_gratuita_quando_licenca_gratuita` | `EPremium = false`, `StatusLicenca` contém "bloqueados" | ✅ |
| 2 | `Status_inicial_premium_quando_licenca_premium` | `EPremium = true`, `StatusLicenca` contém "desbloqueados" | ✅ |
| 3 | `Ativar_com_sucesso_marca_EPremium_true` | Após ativação bem-sucedida, `EPremium = true` | ✅ |
| 4 | `Ativar_com_sucesso_exibe_nome_do_cliente_na_mensagem` | `MensagemAtivacao` contém o nome do cliente; `NomeCliente` e `EmailCliente` preenchidos | ✅ |
| 5 | `Ativar_com_sucesso_sem_nome_exibe_mensagem_generica` | Sem nome de cliente, mensagem contém "sucesso" | ✅ |
| 6 | `Ativar_com_sucesso_limpa_campo_de_chave` | `ChaveAtivacao` fica vazio após ativação | ✅ |
| 7 | `Ativar_com_falha_nao_marca_premium` | Resposta de falha mantém `EPremium = false` | ✅ |
| 8 | `Ativar_com_falha_exibe_mensagem_de_erro` | `MensagemAtivacao` contém o texto de erro retornado | ✅ |
| 9 | `Desativar_reverte_EPremium_para_false` | Após desativação, `EPremium = false` | ✅ |
| 10 | `Desativar_exibe_mensagem_de_reversao` | `MensagemAtivacao` contém "Gratuita" | ✅ |
| 11 | `ValidarOnline_com_licenca_valida_mantém_premium_e_exibe_confirmacao` | `EPremium = true`, `MensagemAtivacao` contém "válida" | ✅ |
| 12 | `ValidarOnline_com_assinatura_expirada_reverte_para_gratuita` | `EPremium = false`, `MensagemAtivacao` contém "expirada" | ✅ |

## 2.3 HardwareOptimizer.Features.Licensing.Tests — LicencaGateTests — Novos (2026-06-23)

| # | Teste | Descrição | Resultado |
|---|---|---|---|
| 1 | `ResultadoAtivacao_Ok_com_nome_e_email_preenche_campos` | `Ok(Premium, "João", "joao@test.com")` preenche `NomeCliente` e `EmailCliente` | ✅ |
| 2 | `ResultadoAtivacao_Ok_sem_nome_mantem_null` | `Ok(Premium)` sem argumentos deixa nome/email como `null` | ✅ |
| 3 | `ResultadoAtivacao_Falhar_nao_tem_nome_nem_email` | `Falhar("msg")` deixa nome/email como `null` | ✅ |
| 4 | `LicencaConfig_url_compra_nao_e_vazia` | `LicencaConfig.UrlCompra` não é string vazia | ✅ |
| 5 | `LicencaConfig_grace_period_e_positivo` | `LicencaConfig.DiasGracePeriodo > 0` | ✅ |
| 6 | `LicencaGratuita_validar_online_retorna_gratuita` | Fake Gratuita → `ValidarOnlineAsync` retorna `TipoLicenca.Gratuita` | ✅ |
| 7 | `LicencaPremium_validar_online_retorna_premium` | Fake Premium → `ValidarOnlineAsync` retorna `TipoLicenca.Premium` | ✅ |

---

## 3. Testes Manuais — Roteiro

O aplicativo foi iniciado com `dotnet run --project src/HardwareOptimizer.App --configuration Release`.

### 3.1 Tela Inicial — SCAN

| # | Ação | Resultado Esperado |
|---|---|---|
| 1 | Abrir o aplicativo | Tela de scan com botão central e logo |
| 2 | Clicar em SCAN | Animação de progresso inicia; botão exibe % crescente |
| 3 | Aguardar conclusão (~30 s) | Botão exibe "100% · concluído"; sidebar aparece |

### 3.2 Aba Otimizador Windows → Exibições Gráficas

| # | Ação | Resultado Esperado |
|---|---|---|
| 4 | Clicar em "Otimizador Windows" na sidebar | Tela de Otimizador com 4 abas |
| 5 | Aba "Exibições Gráficas" está ativa por padrão | Lista de efeitos visuais com checkboxes |
| 6 | Marcar efeitos e clicar "Confirmar e desativar selecionados" | StatusBar exibe sucesso |

### 3.3 Aba Programas Instalados

| # | Ação | Resultado Esperado |
|---|---|---|
| 7 | Clicar na aba "Programas Instalados" | Lista de programas instalados |
| 8 | Digitar nome na busca | Lista filtrada em tempo real |
| 9 | Selecionar programa(s) via checkbox | Label do botão mostra quantidade |
| 10 | Clicar "Desinstalar X selecionado(s)" | Desinstalador nativo é aberto |

### 3.4 Aba Inicialização

| # | Ação | Resultado Esperado |
|---|---|---|
| 11 | Clicar na aba "Inicialização" | Lista com colunas NOME / FORNECEDOR / STATUS / IMPACTO |
| 12 | Verificar programas habilitados | Botão "DESATIVAR" visível |
| 13 | Verificar programas desabilitados | Botão "ATIVAR" visível |
| 14 | Clicar "DESATIVAR" em um programa habilitado | UAC pode pedir elevação; Status muda para Desabilitado |
| 15 | Clicar "ATIVAR" em um programa desabilitado | Status muda para Habilitado |

### 3.5 Aba Serviços *(nova)*

| # | Ação | Resultado Esperado |
|---|---|---|
| 16 | Clicar na aba "Serviços" | Indicador "Carregando serviços…" aparece; lista carrega automaticamente |
| 17 | Aguardar carregamento | Tabela com colunas NOME / DESCRIÇÃO / PID / STATUS / GRUPO exibida |
| 18 | Verificar serviços em execução | STATUS = "Em execução" em verde, botão "PARAR" vermelho |
| 19 | Verificar serviços parados | STATUS = "Parado" cinza, botão "INICIAR" verde |
| 20 | Digitar "spooler" na busca | Lista filtrada para Print Spooler |
| 21 | Clicar "↺ Atualizar" | Lista recarregada do sistema |
| 22 | Clicar "PARAR" em serviço não-crítico (ex: Print Spooler) | UAC solicita elevação; após aprovação, Status muda para Parado |
| 23 | Clicar "INICIAR" no mesmo serviço | UAC solicita elevação; após aprovação, Status muda para Em execução |
| 24 | Cancelar UAC ao parar/iniciar | Status não muda; StatusBar exibe "Operação cancelada (UAC negado)" |

### 3.6 Exportação de Relatório HTML (2026-06-23)

| # | Ação | Resultado Esperado |
|---|---|---|
| 25 | Após scan, clicar "↓ Exportar Relatório HTML" | `StatusExport` exibe "Gerando relatório..."; arquivo salvo na Área de Trabalho |
| 26 | Aguardar geração | Navegador abre o relatório dark-themed; seções: SO, CPU, RAM, GPU, BIOS, Armazenamento, S.M.A.R.T., Rede |

### 3.7 Notificações Win32

| # | Ação | Resultado Esperado |
|---|---|---|
| 27 | Minimizar o app e aguardar anomalia de CPU/RAM | Balloon tip aparece na bandeja do sistema com título "Otimize Builder — Anomalia Detectada" |

### 3.8 Módulo UPGRADE — Sugestões concretas

| # | Ação | Resultado Esperado |
|---|---|---|
| 28 | Acessar módulo UPGRADE → Analisar gargalo | Seção "SUGESTÕES DE UPGRADE" exibe cards com nome, specs, motivo e categoria colorida |

### 3.9 Configurações — Licença LemonSqueezy

| # | Ação | Resultado Esperado |
|---|---|---|
| 29 | Abrir Configurações com plano Gratuita | Card "Assinar Premium →" visível com lista de benefícios e botão com gradiente roxo→azul |
| 30 | Clicar "Assinar Premium →" | Navegador abre a página de checkout no LemonSqueezy |
| 31 | Colar chave válida e clicar "Ativar" | `MensagemAtivacao` exibe "Bem-vindo, [Nome]! Licença Premium ativada com sucesso." |
| 32 | Com Premium ativo, clicar "Verificar online" | `MensagemAtivacao` exibe "Licença verificada e válida." |

### 3.10 Outros Módulos

| # | Ação | Resultado Esperado |
|---|---|---|
| 33 | Sidebar → Dashboard | Cards de CPU / GPU / RAM em tempo real; gráficos de temperatura e clock |
| 34 | Sidebar → IA Copiloto | Chat funcional com análise do inventário; badge "!" some ao abrir |
| 35 | Sidebar → UPGRADE (Premium) | Análise de gargalo + sugestões concretas por socket/geração |
| 36 | Sidebar → Vida Útil (Premium) | Dados S.M.A.R.T. dos discos |
| 37 | Sidebar → Drivers (Premium) | Lista de drivers instalados |

### 3.7 Tela Info Sistema — Especificações completas

| # | Ação | Resultado Esperado |
|---|---|---|
| 30 | Sidebar → Info Sistema | Todas as 7+ seções visíveis: SO, CPU, Placa-mãe, BIOS, RAM, GPU |
| 31 | Verificar seção Sistema Operacional | Nome, versão, build, arquitetura e Secure Boot exibidos |
| 32 | Verificar seção Processador | Modelo, núcleos, threads, clock base, temperatura idle |
| 33 | Verificar seção Armazenamento | Aparece apenas se discos foram detectados; exibe capacidade e espaço usado |
| 34 | Verificar seção S.M.A.R.T. | Aparece apenas se dados disponíveis; status colorido (verde/amarelo/vermelho) |
| 35 | Verificar seção Interfaces de Rede | Aparece apenas se adaptadores detectados; exibe IP, MAC, velocidade |

### 3.8 Visual — Otimize Builder (redesign)

| # | Ação | Resultado Esperado |
|---|---|---|
| 36 | Verificar sidebar após scan | Item ativo tem barra ciana de 3px na esquerda; item inativo em cinza |
| 37 | Trocar de página na sidebar | Barra ciana migra para o novo item ativo; anterior volta ao estado inativo |
| 38 | Verificar tela Home | Fundo gradiente radial azul; dois anéis de radar ao redor do botão SCAN |
| 39 | Verificar cards do Dashboard | Cada card tem barra lateral colorida por nível de alerta (verde/amarelo/vermelho) |
| 40 | Verificar badge IA na sidebar | Badge "!" vermelho exibido quando há alerta pendente; some ao acessar IA Copiloto |
| 41 | Verificar seção Premium na sidebar | Badge "PRO" dourado visível; módulos bloqueados em cinza sem interação |

---

## 4. Cenários de Regressão

| # | Cenário | Resultado Esperado |
|---|---|---|
| R1 | Abrir app sem conexão de rede | App carrega; apenas funcionalidades locais afetadas |
| R2 | Navegar entre todas as 4 abas rapidamente | Sem crash; conteúdo correto em cada aba |
| R3 | Filtrar na aba Programas e depois trocar de aba | Filtro persiste ao voltar |
| R4 | Parar um serviço crítico do sistema | Operação concluída ou erro informativo |
| R5 | Aplicativo sem privilégios de admin | Operações que requerem UAC solicitam elevação corretamente |

---

## 5. Cobertura de Código — Resumo

- **Contratos**: `ServicoWindows`, `InicializacaoEntrada`, `ProgramaInstalado` — cobertos por testes unitários
- **RoteadorIpc**: rotas `obterservicos`, `iniciarservico`, `pararservico`, `obterstatuslicenca` — cobertas por `HardwareOptimizer.Ipc.Tests`
- **ColetorServicos**: lógica WMI em `SimplificarGrupo` — coberta implicitamente via `HardwareOptimizer.Agent.Tests`
- **ViewModels**: `ServicoViewModel`, `OtimizadorWindowsViewModel`, `ConfiguracoesViewModel` (ativação, desativação, validação online, compra) — 83 testes em `HardwareOptimizer.App.Tests`
- **Licenciamento**: `ResultadoAtivacao` (todos os campos), `ValidadorChaveLicenca`, `LicencaConfig`, gate Freemium/Premium — 20 testes em `HardwareOptimizer.Features.Licensing.Tests`
- **Não cobertos por testes automatizados** (requerem Windows/DPAPI/HTTP real):
  - `ServicoLicencaLemonSqueezy` — testado via fake em `ConfiguracoesViewModelTests`
  - `ServicoNotificacaoWindows` — requer HWND real (testado manualmente)
  - `ServicoRelatorio.ExportarHtmlAsync` — requer sistema de arquivos Windows (testado manualmente)

---

*Atualizado em 2026-06-23 após execução de `dotnet test --configuration Release` com 446 testes, 0 falhas.*
