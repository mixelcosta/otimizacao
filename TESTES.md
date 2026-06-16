# Documento de Testes — HardwareOptimizer / Otimize Builder

Data de execução: 2026-06-15

---

## 1. Testes Automatizados

Executados com `dotnet test --configuration Release`.

### Resultado Geral

| Assembly | Aprovados | Falhas | Ignorados | Duração |
|---|---|---|---|---|
| HardwareOptimizer.Core.Tests | 87 | 0 | 0 | ~17 ms |
| HardwareOptimizer.Agent.Tests | 86 | 0 | 0 | ~150 ms |
| HardwareOptimizer.Cerebro.Tests | 26 | 0 | 0 | ~111 ms |
| HardwareOptimizer.Ipc.Tests | 15 | 0 | 0 | ~141 ms |
| HardwareOptimizer.Features.Upgrade.Tests | 20 | 0 | 0 | ~47 ms |
| HardwareOptimizer.Features.LifeCounter.Tests | 8 | 0 | 0 | ~10 ms |
| HardwareOptimizer.Features.Licensing.Tests | 7 | 0 | 0 | ~5 ms |
| HardwareOptimizer.App.Tests | 36 | 0 | 0 | ~235 ms |
| **TOTAL** | **285** | **0** | **0** | |

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

### 3.6 Outros Módulos

| # | Ação | Resultado Esperado |
|---|---|---|
| 25 | Sidebar → Dashboard | Cards de CPU / GPU / RAM em tempo real |
| 26 | Sidebar → IA Copiloto | Chat funcional com análise do inventário |
| 27 | Sidebar → UPGRADE (Premium) | Tela de compatibilidade de hardware |
| 28 | Sidebar → Vida Útil (Premium) | Dados S.M.A.R.T. dos discos |
| 29 | Sidebar → Drivers (Premium) | Lista de drivers instalados |

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
- **RoteadorIpc**: rotas `obterservicos`, `iniciarservico`, `pararservico` — cobertas por `HardwareOptimizer.Ipc.Tests`
- **ColetorServicos**: lógica WMI em `SimplificarGrupo` — coberta implicitamente via `HardwareOptimizer.Agent.Tests`
- **ViewModels**: todas as computed properties de `ServicoViewModel` e fluxos de `OtimizadorWindowsViewModel` — 36 testes

---

*Gerado automaticamente após execução de `dotnet test --configuration Release` em 2026-06-15.*
