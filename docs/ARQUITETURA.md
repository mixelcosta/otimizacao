# Arquitetura

Documento técnico da arquitetura do sistema. Para o documento de referência
original, veja [`arquitetura_otimizador.json`](arquitetura_otimizador.json).

## Sumário
- [Visão em três planos](#visão-em-três-planos)
- [Projetos e dependências](#projetos-e-dependências)
- [Regras de camadas](#regras-de-camadas)
- [Fluxo de dados ponta a ponta](#fluxo-de-dados-ponta-a-ponta)
- [Decisões de design](#decisões-de-design)

---

## Visão em três planos

```
┌──────────────────────────┐    IPC     ┌─────────────────┐    JSON     ┌────────────────┐
│   UI — Otimize Builder   │ ─────────▶ │  Agente Local   │ ──────────▶ │     Cérebro    │
│      (Avalonia MVVM)     │ ◀───────── │   (.NET 8)      │ ◀────────── │ (local ou LLM) │
└──────────────────────────┘            └─────────────────┘             └────────────────┘
```

- **UI (Otimize Builder)** — interface desktop Avalonia/MVVM com tema escuro tecnológico.
  Sidebar de navegação com estado ativo dinâmico, dashboard de sensores em tempo real,
  tela de scan, info sistema completa, otimizador Windows e módulos Premium com gating de licença.
  Coleta a **aprovação explícita** do usuário antes de qualquer execução.
- **Agente Local** — processo .NET 8 que coleta inventário, lê sensores, faz
  backup, executa mudanças aprovadas e roda a validação. Hospeda o IPC.
- **Cérebro** — seleciona e prioriza ações do catálogo (offline ou via LLM).
  **Nunca gera comandos** — só escolhe IDs do catálogo.

---

## Projetos e dependências

```
HardwareOptimizer.Core          (domínio puro, sem efeitos colaterais, sem deps externas)
        ▲        ▲       ▲
        │        │       │
   Agent│   Cerebro│  Features.*│
        │        │       │
HardwareOptimizer.Agent   HardwareOptimizer.Cerebro   (efeitos colaterais / LLM)
HardwareOptimizer.Features.Licensing                   (gating Freemium/Premium)
HardwareOptimizer.Features.Upgrade                     (compatibilidade, gargalo, links)
HardwareOptimizer.Features.LifeCounter                 (S.M.A.R.T., estimativa de vida)
HardwareOptimizer.Features.Drivers                     (HWID, catálogo WHQL, pnputil)
        ▲        ▲         ▲         ▲(Features.*)
        └────────┴────┬────┴─────────┘
                      │
            HardwareOptimizer.Ipc          (composição: protocolo + roteador + transporte)
                 ▲          ▲
                 │          │
   HardwareOptimizer.App   HardwareOptimizer.Cli    (UI desktop / linha de comando)

HardwareOptimizer.WindowsService   (Worker background, monitor de anomalias — Windows)
```

| Projeto | Papel | Depende de |
| --- | --- | --- |
| `HardwareOptimizer.Core` | Domínio puro: contratos, catálogo, validação, perfis, consentimento, privacidade, BIOS, score. | (só BCL) |
| `HardwareOptimizer.Agent` | Efeitos colaterais: coletor, sensores, backup, executor, validação, persistência SQLite, HWID, startup scanner, S.M.A.R.T. | Core |
| `HardwareOptimizer.Cerebro` | Cérebro: matriz de decisão, guard, local/LLM, visão multimodal. | Core, SDK Anthropic |
| `HardwareOptimizer.Features.Licensing` | Gating Freemium/Premium: `IServicoLicenca`, `ServicoLicencaLocal` (DPAPI), `FuncionalidadePremium`, `TipoLicenca`. | Core |
| `HardwareOptimizer.Features.Upgrade` | Módulo UPGRADE: compatibilidade, cálculo de gargalo, agente LLM de upgrade. | Core, Cerebro |
| `HardwareOptimizer.Features.LifeCounter` | Módulo Vida Útil: `CalculadoraVidaUtil`, banco TBW por modelo. | Core, Agent |
| `HardwareOptimizer.Features.Drivers` | Módulo Drivers: `AtualizadorDrivers` (pnputil), repositório WHQL. | Core, Agent |
| `HardwareOptimizer.Ipc` | Protocolo, roteador (dispatcher) e transporte named pipe. | Core, Agent, Cerebro, Features.* |
| `HardwareOptimizer.App` | UI desktop Avalonia (MVVM) — produto **Otimize Builder**. Sidebar, dashboard, info sistema, otimizador Windows, módulos Premium. | Ipc |
| `HardwareOptimizer.Cli` | Linha de comando (orquestra tudo). | Core, Agent, Cerebro, Ipc |
| `HardwareOptimizer.WindowsService` | Worker Windows Service: polling 500ms, detecção de anomalias, notificação via named pipe. | Core, Agent |

### Mapa de módulos (resumo)
- **Core:** `Common` (Resultado, enums), `Contracts`, `Catalog`, `Profiles`,
  `Consent`, `Privacy`, `Bios`, `Reporting`.
- **Agent:** `Collector`, `Sensors`, `Backup`, `Execution`
  (+ `Execution/Windows` para a execução real), `Validation`, `Bios`,
  `Persistence`, `Platform` (portas de registro e processo),
  `Drivers` (HWID/ColetorHwid), `Smart` (LeitorSmart), `Startup` (VerificadorInicializacao, GerenciadorInicializacao), `Services` (ColetorServicos).
- **Cerebro:** raiz (matriz, guard, local/LLM, cliente Anthropic) + `Visao`
  (pipeline multimodal: `ModuloVisao`, `ClienteVisaoAnthropic`, `ConferenciaVisual`).
- **Features.Licensing:** `IServicoLicenca`, `ServicoLicencaLocal`, `FuncionalidadePremium` enum.
- **Features.Upgrade:** `AgenteUpgrade` (LLM), `ValidadorCompatibilidade`, `CalculadoraGargalo`.
- **Features.LifeCounter:** `CalculadoraVidaUtil`, `tbw_database.json`.
- **Features.Drivers:** `AtualizadorDrivers`, `IRepositorioDriversWhql`.
- **Ipc:** protocolo, `RoteadorIpc` (dispatcher centralizado), `ServidorNamedPipe`, `ClienteNamedPipe`.

---

## Regras de camadas

1. **Core não tem efeitos colaterais** nem dependências externas: é lógica pura
   e determinística (fácil de testar). Toda regra invariante de segurança vive
   aqui.
2. **Agent concentra E/S** (arquivos, processos, SQLite) e o executor. O LLM
   nunca entra aqui — o executor só roda `comando_interno` versionados do
   catálogo.
3. **Cerebro isola o LLM** e a serialização de prompts; a saída do modelo é
   sempre tratada por um **guard** antes de virar decisão.
4. **Ipc é composição** (não tem regra de negócio): traduz mensagens em chamadas
   aos módulos.
5. **App/Cli são apresentação**: não contêm regra de negócio.

---

## Fluxo de dados ponta a ponta

```
Coleta (read-only)
   └─▶ Sanitização (remove/hasheia PII)
         └─▶ Cérebro propõe (matriz de decisão; só IDs do catálogo)
               └─▶ Perfil seguro / customizado (+ consentimento se necessário)
                     └─▶ Backup obrigatório (bloqueante)
                           └─▶ Executor (uma categoria por vez)
                                 └─▶ Validação (estresse → regressão?)
                                       ├─ sim ─▶ Rollback automático da categoria
                                       └─ não ─▶ Próxima categoria
                                             └─▶ Relatório + score (0-100)
```

Eventos relevantes são registrados em log (ver
[DESENVOLVIMENTO.md](DESENVOLVIMENTO.md) §Logging) com a classe de origem,
permitindo localizar o ponto exato de qualquer falha.

---

## Decisões de design

| Decisão | Motivo |
| --- | --- |
| **Catálogo fechado + guard do LLM** | O LLM só escolhe IDs; qualquer alucinação é descartada pelo guard. É o coração da segurança. |
| **`Resultado<T>` em vez de exceções** | Fluxo de validação legível para UI e auditoria, sem exceções de controle. |
| **Modo simulação (dry-run) padrão** | O executor opera sobre `IEstadoSistema` abstrato; o simulado torna executor e rollback totalmente testáveis sem tocar o sistema real. A execução real do Windows (`EstadoSistemaWindows`) implementa a mesma interface e só é ativada por opt-in explícito (`HWOPT_EXECUCAO_REAL`). |
| **Execução real isolada por portas** | `EstadoSistemaWindows` traduz os alvos do catálogo em registro/powercfg/serviços através de `IAcessoRegistro` e `IExecutorProcesso`, mantendo a lógica testável fora do Windows e o executor inalterado. |
| **Domínio em português** | Alinhado ao público; schemas refletem a serialização (camelCase). |
| **Warnings tratados como erros** | Qualidade reforçada pelo compilador (exceto o XAML gerado da UI). |
| **`ILogger` opcional (default `NullLogger`)** | Logging sem acoplar bibliotecas a um provider; a CLI conecta o destino em arquivo. |
| **IPC desacoplado por `IRoteadorIpc`** | A UI fala com o agente em processo ou remoto (named pipe) de forma intercambiável; ViewModels testáveis. |

Para o detalhamento das regras invariantes, veja [SEGURANCA.md](SEGURANCA.md).

---

## UI — Sistema de design (Otimize Builder)

### Paleta de cores

| Token | Valor | Uso |
| --- | --- | --- |
| Fundo janela | `#030308` | `Window.Background` e `ContentControl.Background` |
| Fundo sidebar | `#07070F` → `#06060D` | `LinearGradientBrush` horizontal |
| Superfície card | `#0C0C1E` → `#09091A` | `LinearGradientBrush` vertical |
| Borda card | `#1E1E3C` → `#12122A` | `LinearGradientBrush` vertical |
| Acento ativo (nav) | `#111128` | Fundo do item ativo da sidebar |
| Acento primário | `#00C8FF` | Ciano — barra ativo, headers, separadores |
| Acento métrica | `#00FF88` | Verde — sensor em nível estável |
| Acento premium | `#D4A017` | Dourado — módulos PRO |
| Crítico | `#FF3A5C` | Alerta e valores críticos |
| Texto ativo | `#E0E0F2` | Labels ativas, valores |
| Texto mudo | `#484865` | Labels inativas |
| Texto escuro | `#282840` | Items bloqueados (Premium) |
| Fundo badge PRO | `#1A1000` | Border do badge "PRO" na sidebar |
| Fundo badge alerta | `#3D0000` | Border do badge "!" de alerta IA |

### Cores semânticas (separadas do acento)

| Token | Valor | Uso |
| --- | --- | --- |
| Sucesso / aplicado | `#00C870` | Verde — status ATUALIZADO, otimizações aplicadas |
| Alerta / gargalo | `#FF8C00` | Laranja — bottleneck, avisos XMP |
| Erro / destrutivo | `#FF4444` | Vermelho — erro, botão de desinstalar |

> Manter as cores semânticas separadas do acento primário (`#00C8FF`) preserva
> a consistência: verde semântico (`#00C870`) ≠ verde de métrica (`#00FF88`).

### Padrão: barra de acento lateral sem code-behind

Item ativo na sidebar usa `LinearGradientBrush` no `ContentPresenter` do botão
em vez de `BorderThickness` (que exigiria template override):

```xml
<Style Selector="Button.nav.ativo /template/ ContentPresenter">
  <Setter Property="Background">
    <Setter.Value>
      <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,0%">
        <GradientStop Color="#00C8FF" Offset="0.000" />
        <GradientStop Color="#00C8FF" Offset="0.018" />  <!-- ~3px em 184px -->
        <GradientStop Color="#111128" Offset="0.019" />
        <GradientStop Color="#111128" Offset="1.000" />
      </LinearGradientBrush>
    </Setter.Value>
  </Setter>
</Style>
```

### Padrão: estado ativo do item de navegação

`ShellViewModel` expõe 9 propriedades booleanas derivadas de `PaginaAtual`:

```csharp
public bool PaginaEhDashboard  => PaginaAtual == Dashboard;
// … (PaginaEhOtimizador, PaginaEhInfoSistema, etc.)

partial void OnPaginaAtualChanged(ObservableObject value)
{
    OnPropertyChanged(nameof(PaginaEhDashboard));
    // … notifica todas as 9 propriedades
}
```

Cada botão de navegação aplica a classe com binding:
```xml
<Button Classes="nav" Classes.ativo="{Binding PaginaEhDashboard}" … />
```

### Restrições do Avalonia 12 a observar

| Elemento | Restrição | Solução |
| --- | --- | --- |
| `Rectangle` | Sem `CornerRadius` | Substituir por `Border` com `Background` |
| `StackPanel` | Sem `Padding` | Embrulhar em `Border Padding="…"` |
| `Button` | `BorderThickness` unilateral via estilo exige template override | Usar `LinearGradientBrush` no `ContentPresenter` |
