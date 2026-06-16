# Changelog

Todas as mudanças notáveis deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o
versionamento segue [SemVer](https://semver.org/lang/pt-BR/). Cada versão mapeia
fases do `roadmap_desenvolvimento` do documento de arquitetura (ver o mapa do
roadmap no [README](README.md#mapa-do-roadmap)).

## [Não lançado]

### Adicionado

#### UI — Redesign visual "Otimize Builder" (2026-06-15)

Redesign completo da interface gráfica Avalonia com estética tecnológica e moderna,
estabelecendo um sistema de design coeso para o produto **Otimize Builder**.

**Sistema de cores estabelecido:**

| Token | Cor | Uso |
| --- | --- | --- |
| Fundo janela | `#030308` | `Window.Background` |
| Fundo sidebar | `#07070F` | `LinearGradientBrush` lateral |
| Superfície card | `#0C0C1E` → `#09091A` | `LinearGradientBrush` vertical |
| Acento primário | `#00C8FF` | Ciano — item ativo, headers, separadores |
| Acento métrica | `#00FF88` | Verde — valor estável nos sensores |
| Acento premium | `#D4A017` | Dourado — módulos PRO |
| Crítico | `#FF3A5C` | Alertas e valores críticos |
| Texto primário | `#E0E0F2` | Texto ativo |
| Texto mudo | `#484865` | Labels inativas |
| Texto escuro | `#282840` | Items bloqueados (Premium) |

**`ShellWindow.axaml` — Sidebar redesenhada:**
- Fundo com `LinearGradientBrush` (`#07070F` → `#06060D`)
- Logo "OTIMIZE" (branco) + "BUILDER" (ciano) com linha degrade que desvanece à direita
- **Barra de acento ciano no item ativo** via `LinearGradientBrush` no `ContentPresenter`
  (0–1.8% = `#00C8FF`, 1.9–100% = `#111128`): simula borda esquerda sem code-behind
- Ícones geométricos Unicode: `■ ⚡ ◉ ○ ◌ ▲ ◆ ≡ ▸`
- Badge `PRO` (dourado, fundo âmbar escuro `#1A1000`) na seção Premium
- Badge de alerta IA: `Border Background="#3D0000"` com `!` em `#FF3A5C`
- Estilo `Button.nav-premium` (acento dourado no ativo) e `Button.nav-locked` (não interativo)

**`ShellViewModel.cs` — Estado ativo da navegação:**
- 9 propriedades computadas `PaginaEhXxx` (bool) derivadas de `PaginaAtual`
- `partial void OnPaginaAtualChanged(ObservableObject value)` notifica todas as 9
- Cada botão nav usa `Classes.ativo="{Binding PaginaEhXxx}"` para aplicar o estilo ativo

**`HomeView.axaml` — Tela de scan:**
- Fundo `RadialGradientBrush` (`#060618` centro → `#030308` borda)
- Cards laterais com `LinearGradientBrush` vertical e borda com gradiente
- Dois anéis de radar decorativos (`Ellipse` concêntricos) ao redor do botão SCAN
- Subtexto do botão SCAN em ciano `#00C8FF`

**`SensorCard.axaml` / `SensorCard.axaml.cs` — Card de sensor:**
- Barra lateral de 3px (`Rectangle x:Name="AccentRect"`) colorida via `AtualizarCor()`
- Fundo com `LinearGradientBrush` escuro (`#0C0C1E` → `#09091A`)
- Valor em 32px Black (era 28px Bold); separador fino degrade entre valor e subtítulo
- `StackPanel` embrulhado em `Border Padding="16,16,16,14"` (Avalonia: `StackPanel` não tem `Padding`)
- `AtualizarCor()` agora colore `AccentRect.Fill` além de `ValorText.Foreground`
- Cor crítico atualizada para `#FF3A5C` (era `#FF3333`)

**`DashboardView.axaml` — Dashboard:**
- Headers de seção com `Border Background="#00C8FF"` (3px, `CornerRadius="2"`) + `LetterSpacing`
- 4 cards em `Grid ColumnDefinitions="*,12,*,12,*,12,*"` (era `WrapPanel`)
- Gráficos de temperatura e clock em `Border CornerRadius="10" ClipToBounds="True"` com fundo em `LinearGradientBrush`

#### UI — Tela Info Sistema expandida (2026-06-15)

`InfoSistemaView.axaml` e `InfoSistemaViewModel.cs` expandidos para exibir
**todas as especificações disponíveis** no contrato `Inventario`:

- **Sistema Operacional:** nome, versão, build, arquitetura, Secure Boot
- **Processador:** modelo, núcleos, threads, clock base, temperatura idle
- **Placa-mãe:** fabricante, modelo, chipset, soquete, form factor
- **BIOS:** fabricante, versão, data, modo (UEFI/Legacy)
- **Memória RAM:** total, módulos, tipo DDR, velocidade, dual-channel
- **Placa de Vídeo:** modelo, VRAM, temperatura idle, clock
- **Armazenamento** *(condicional — `IsVisible="{Binding TemDiscos}"`)* — por drive: modelo, capacidade, espaço usado, tipo (SSD/HDD), interface
- **Saúde S.M.A.R.T.** *(condicional — `IsVisible="{Binding TemSaudeDiscos}"`)* — por disco: status colorido (`#00C870` Bom / `#FFAA00` Atenção / `#CC3333` Crítico), TBW estimado, horas de uso
- **Interfaces de Rede** *(condicional — `IsVisible="{Binding TemRede}"`)* — por adaptador: nome, tipo, velocidade, MAC, IP

**Novos ViewModels auxiliares:** `DadosDiscoVm`, `InterfaceRedeVm`, `SaudeDiscoVm`
com propriedades derivadas de cor e formatação.

### Corrigido

- **`Rectangle` sem `CornerRadius` no Avalonia 12** — substituído por `Border`
  com `Background` (em vez de `Fill`) em `DashboardView.axaml` e
  `ShellWindow.axaml`.
- **`StackPanel` sem `Padding` no Avalonia** — embrulhado em `Border` em
  `SensorCard.axaml`.
- **Data da BIOS** vinha como `/Date(1754611200000)/` (formato legado do
  `ConvertTo-Json` do Windows PowerShell). Novo `NormalizadorData` converte para
  ISO `yyyy-MM-dd`; cobre também CIM DATETIME e o DMI do Linux (validado em
  hardware real: ASUS X570, BIOS 2025-08-08).
- **Sensores zerados** poluíam a leitura no Windows sem elevação: a CPU
  reportava `0 °C` / `0 MHz` (MSR exige Ring0). `LeitorSensoresLhm` agora
  descarta temperatura/clock ≤ 0 (mantendo tensão/fan/potência em 0, que são
  válidos).

### A fazer
- Assinatura de código (EV) do binário — passo operacional de distribuição.

---

## [0.1.0] — 2026-06-08

Primeira versão funcional do MVP. Entrega o **Agente Local**, o **núcleo de
domínio** (onde vivem as garantias de segurança) e o **Cérebro** (local ou LLM),
com toda a suíte de testes verde (**160 testes**) e build limpa (warnings =
erros).

### Adicionado

**Fundação (Fase 0)**
- Solução .NET 8 com 6 projetos `src` + 5 de teste, `Directory.Build.props`
  compartilhado (nullable, analisadores, warnings como erros).
- Contratos de dados imutáveis + JSON Schemas (draft 2020-12) em `schemas/`.
- CI (`.github/workflows/ci.yml`): build + test a cada push/PR.

**Coletor e sensores (Fases 1–2)**
- `ColetorInventario` read-only: leitor Linux real (`/sys`, `/proc`) e
  Windows/CIM estruturado.
- `ServicoSensores`: leitura em tempo real — Linux `/sys/class/hwmon` + cpufreq;
  Windows via WMI.

**UI e IPC (Fase 3)**
- Camada IPC com protocolo JSON, `RoteadorIpc` e named pipe
  (servidor/cliente, modo Byte cross-platform).
- UI desktop **Avalonia (MVVM)** com inventário, sensores, matriz de decisão e
  **aprovação por ação**.

**Cérebro / LLM (Fase 4)**
- `MatrizDecisao` priorizada; `CerebroLocal` (offline, padrão) e `CerebroLlm`
  (SDK oficial da Anthropic).
- **Guard** (`LeitorRespostaCerebro`): descarta ações fora do catálogo e força
  parâmetros à faixa segura — robusto a alucinação.
- Sanitização aplicada **antes** de qualquer envio à nuvem.

**Módulo BIOS (Fase 5)**
- Identificação, normalização de fabricante, comparação de versão, banco curado
  + cache SQLite e geração de guia conservador. **Nunca aplica** — só orienta.

**Visão (Fase 6)**
- Pipeline multimodal: leitura estruturada + confiança + próximo passo;
  `ConferenciaVisual` cruza com o inventário e pede nova foto se a confiança for
  baixa.

**Backup e executor (Fases 7–8)**
- `ServicoBackup` **bloqueante** com verificação de integridade.
- `ExecutorControlado`: catálogo whitelisted, validador de limites em três
  níveis, perfis seguro/customizado, consentimento (2 checkboxes) e **rollback
  por categoria**.

**Validação e relatório (Fases 9–10)**
- `RunnerValidacao`: parser de estresse + análise de regressão
  (WHEA/memória/artefatos/TDR/BSOD/temperatura/queda de score) ligada ao
  rollback automático.
- `CalculadoraScore`: notas 0-100 por domínio + nota final ponderada +
  `RelatorioExecutivo`.

**Distribuição e documentação (Fase 11)**
- `scripts/publish.sh` (self-contained multiplataforma), `Dockerfile` e workflow
  de release.
- Conjunto de documentação técnica e de usuário em `docs/` (ver
  [docs/README.md](docs/README.md)).

### Segurança
- Regras invariantes codificadas e **cobertas por teste**: bloqueio rígido no
  limite absoluto; perfil seguro por padrão; consentimento para risco assumido;
  sem backup nada prossegue; uma categoria por vez com rollback; LLM só escolhe
  do catálogo; inventário sanitizado antes da nuvem; BIOS sempre manual.
- Sanitização de PII (remoção) e de correlacionáveis (hash) — ver
  [docs/SEGURANCA.md](docs/SEGURANCA.md).
- Logging com `Microsoft.Extensions.Logging` em todo o processo para apontar o
  ponto exato de falhas.

### Notas
- O ID do modelo LLM **não é fixado no código**: vem da variável de ambiente
  `HWOPT_LLM_MODELO`. Sem ela (e sem `ANTHROPIC_API_KEY`), o sistema roda 100%
  offline com o cérebro local.

[Não lançado]: https://github.com/mixelcosta/otimizacao/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/mixelcosta/otimizacao/releases/tag/v0.1.0
