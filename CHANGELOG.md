# Changelog

Todas as mudanças notáveis deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o
versionamento segue [SemVer](https://semver.org/lang/pt-BR/). Cada versão mapeia
fases do `roadmap_desenvolvimento` do documento de arquitetura (ver o mapa do
roadmap no [README](README.md#mapa-do-roadmap)).

## [Não lançado]

### Adicionado
- **Execução real no Windows** (`EstadoSistemaWindows`): traduz os alvos do
  catálogo em operações de registro, plano de energia (`powercfg`) e serviços
  (`sc.exe`), preservando `Ler`/`Escrever`/`Restaurar` e o rollback. Isolada por
  portas (`IAcessoRegistro`, `IExecutorProcesso`) e testável fora do Windows.
  Ativada por opt-in explícito (`HWOPT_EXECUCAO_REAL=1`, Windows elevado); o
  padrão segue sendo o modo simulado.
- **Sensores via LibreHardwareMonitor** no Windows (`LeitorSensoresLhm` +
  `FonteSensoresLhm`): clock, voltagem, fan, consumo e temperatura por
  componente. Encadeado por `LeitorSensoresComposto` com **fallback automático
  para WMI** quando não há driver/elevação. A fonte é abstraída
  (`IFonteSensoresLhm`), mantendo a lógica testável fora do Windows.

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
