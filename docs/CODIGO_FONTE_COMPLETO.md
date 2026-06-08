# Documento Técnico Completo — Agente de Otimização e Confiabilidade de Hardware

> **Propósito deste documento.** Este é o "livro do código": uma referência única
> e autossuficiente que reúne a **arquitetura explicada** e **todo o código-fonte**
> do sistema, na ordem de dependência. Foi escrito para que **uma pessoa ou um
> agente de IA** consiga entender, manter e evoluir o software no futuro sem
> precisar de contexto externo.
>
> Gerado a partir do código real do repositório (branch `main`). Para a visão por
> tópicos, veja também os documentos em [docs/](README.md).

---

## 1. Sobre este documento

- **Parte I (narrativa):** o que o sistema faz, como está organizado, as regras de
  segurança invariantes, o fluxo ponta a ponta, e como compilar/testar/publicar.
- **Parte II (código-fonte):** cada projeto, em ordem de dependência; dentro de
  cada projeto, o `.csproj` primeiro e depois os arquivos por diretório. Cada
  arquivo aparece com o **caminho relativo** como título e o conteúdo integral.
- **Convenção de blocos:** o código vem em blocos cercados por **quatro crases**
  (` ```` `) para conviver com qualquer crase tripla eventualmente presente no
  conteúdo.
- **Idioma:** o domínio é modelado em **português** (tipos, métodos, comentários);
  nomes em inglês do documento de arquitetura original permanecem como referência
  semântica.

---

## 2. Visão geral do sistema

Sistema que **diagnostica, recomenda e aplica** otimizações de desempenho em
computadores Windows (o coletor também roda em Linux, usado como ambiente de
validação). Três responsabilidades, três planos:

- A **UI** exibe inventário, sensores e a matriz de decisão e coleta a
  **aprovação explícita** do usuário.
- O **Agente Local** (.NET 8, este repositório) coleta inventário, lê sensores,
  faz backup, **executa apenas o que foi aprovado** e valida estabilidade.
- O **Cérebro** (local determinístico ou via LLM) **propõe** otimizações — nunca
  gera comandos; só seleciona IDs de um **catálogo fechado**.

A **BIOS é sempre ajustada manualmente** pelo usuário, com orientação do sistema
(o software nunca aplica BIOS).

### Filosofia operacional (inegociável)

**ESTABILIDADE → SEGURANÇA → EFICIÊNCIA → DESEMPENHO.**
Busca-se o maior desempenho **sustentável e validado**, nunca o maior possível.

---

## 3. Arquitetura

```
┌──────────────┐     IPC      ┌─────────────────┐    JSON     ┌────────────────┐
│      UI      │ ───────────▶ │  Agente Local   │ ──────────▶ │     Cérebro    │
│  (Avalonia)  │ ◀─────────── │   (.NET 8)      │ ◀────────── │ (local ou LLM) │
└──────────────┘              └─────────────────┘             └────────────────┘
```

### Grafo de dependências dos projetos

```
HardwareOptimizer.Core      (domínio puro, sem efeitos colaterais, sem deps externas)
        ▲        ▲
        │        │
   Agent│   Cerebro│
        │        │
HardwareOptimizer.Agent   HardwareOptimizer.Cerebro   (efeitos colaterais / LLM)
        ▲        ▲
        └────────┴────┬──────────────┐
                      │              │
            HardwareOptimizer.Ipc    │   (protocolo + roteador + transporte)
                 ▲          ▲        │
                 │          │        │
   HardwareOptimizer.App   HardwareOptimizer.Cli   (UI desktop / linha de comando)
```

| Projeto | Papel | Depende de |
| --- | --- | --- |
| `HardwareOptimizer.Core` | Domínio puro: contratos, catálogo, validação, perfis, consentimento, privacidade, BIOS, score. | (só BCL) |
| `HardwareOptimizer.Agent` | Efeitos colaterais: coletor, sensores, backup, executor, validação, persistência, execução real (Windows). | Core, Microsoft.Win32.Registry, LibreHardwareMonitorLib, SQLite |
| `HardwareOptimizer.Cerebro` | Cérebro: matriz de decisão, guard anti-alucinação, local/LLM, visão. | Core, SDK Anthropic |
| `HardwareOptimizer.Ipc` | Protocolo, roteador (dispatcher) e transporte named pipe. | Core, Agent, Cerebro |
| `HardwareOptimizer.App` | UI desktop Avalonia (MVVM). | Ipc |
| `HardwareOptimizer.Cli` | Linha de comando (orquestra tudo). | Core, Agent, Cerebro, Ipc |

### Regras de camadas

1. **Core não tem efeitos colaterais** nem dependências externas: lógica pura e
   determinística. Toda regra invariante de segurança vive aqui.
2. **Agent concentra E/S** (arquivos, processos, registro, SQLite) e o executor.
   O LLM nunca entra aqui — o executor só roda `comando_interno` versionados.
3. **Cerebro isola o LLM**; a saída do modelo passa sempre por um **guard** antes
   de virar decisão.
4. **Ipc é composição** (sem regra de negócio): traduz mensagens em chamadas.
5. **App/Cli são apresentação**: sem regra de negócio.

---

## 4. Mapa de módulos

- **Core/Common** — `Resultado`/`Resultado<T>` (padrão de resultado), enums
  (`CategoriaAcao`, `NivelRisco`, `SituacaoParametro`, `TipoPerfil`, …).
- **Core/Contracts** — registros imutáveis: `Inventario`, `Recomendacao`,
  `ResultadoValidacao`, `LeituraSensores`.
- **Core/Catalog** — o coração da segurança: `AcaoOtimizacao`, `CatalogoAcoes`,
  `CatalogoPadrao` (versão `2024.06-mvp`, 8 ações), parâmetros (`ParametroNumerico`
  com três faixas, `ParametroListaBranca`) e `ValidadorAcao`.
- **Core/Profiles** — `ConstrutorPerfil` (perfil seguro/customizado), `Perfil`.
- **Core/Consent** — `AvaliadorConsentimento`, `TermoConsentimento` (aviso + 2
  checkboxes + confirmação + auditoria).
- **Core/Privacy** — `Sanitizador` (hash de serial/uuid/MAC, remoção de PII).
- **Core/Bios** — normalização de fabricante, comparação de versão, decisão
  conservadora e geração de guia por fabricante.
- **Core/Reporting** — `CalculadoraScore` (notas 0-100 por domínio) e relatório.
- **Agent/Collector** — `ColetorInventario` + leitores Linux/Windows (read-only) e
  `NormalizadorData` (datas da BIOS em ISO).
- **Agent/Sensors** — `ServicoSensores`; no Windows usa **LibreHardwareMonitor**
  (`LeitorSensoresLhm`/`FonteSensoresLhm`) com **fallback WMI**, encadeados por
  `LeitorSensoresComposto`; no Linux, `/sys/class/hwmon`.
- **Agent/Backup** — `ServicoBackup` (obrigatório, bloqueante).
- **Agent/Execution** — `ExecutorControlado` (uma categoria por vez, rollback),
  `RegistroComandos`, `ComandoEstadoSistema`, `IEstadoSistema` (+
  `EstadoSistemaSimulado`), `VerificadorPreCondicoes`. **Execution/Windows**:
  `EstadoSistemaWindows` (execução real: registro/powercfg/sc.exe).
- **Agent/Platform** — portas `IAcessoRegistro`/`AcessoRegistroWindows` e
  `IExecutorProcesso`/`ExecutorProcesso` (isolam registro e processos).
- **Agent/Validation** — `RunnerValidacao` (parser de estresse + análise de
  regressão: WHEA/memória/artefatos/TDR/BSOD/temperatura/queda de score).
- **Agent/Persistence** — `RepositorioSqlite` (inventário, auditoria, cache BIOS).
- **Cerebro** — `MatrizDecisao`, `ICerebro`, `CerebroLocal`/`CerebroLlm`,
  `ConstrutorPrompt`, `LeitorRespostaCerebro` (**guard**), `ClienteLlmAnthropic`;
  **Visao/** (leitura de fotos + conferência com o inventário).
- **Ipc** — `ProtocoloIpc`, `RoteadorIpc`, `ServidorNamedPipe`/`ClienteNamedPipe`.
- **App** — `Program`, `App.axaml(.cs)`, `Views/MainWindow`, `ViewModels`.
- **Cli** — `Program` (comandos: `coletar`, `sanitizar`, `catalogo`, `sensores`,
  `relatorio`, `proposta`, `bios`, `visao`, `demo`, **`aplicar`**, `servir`,
  `ipc-demo`).

---

## 5. Regras invariantes de segurança (rastreabilidade)

Cada regra é **garantida em código e coberta por teste**:

| Regra | Onde é garantida | Teste |
| --- | --- | --- |
| O LLM nunca gera comandos; só escolhe IDs do catálogo | `ValidadorAcao`, `LeitorRespostaCerebro`, `RegistroComandos` | `ValidadorAcaoTests`, `GuardRespostaTests` |
| Nenhum valor ultrapassa o `limite_absoluto` (bloqueio rígido) | `ParametroNumerico.Validar` | `ValidadorAcaoTests`, `ConstrutorPerfilTests` |
| Perfil seguro por padrão (usa a `faixa_segura`) | `ConstrutorPerfil.CriarPerfilSeguro` | `ConstrutorPerfilTests` |
| Perfil customizado exige consentimento (aviso + 2 checkboxes + confirmação) | `AvaliadorConsentimento` | `ConsentimentoTests` |
| Sem backup confirmado, nada prossegue | `VerificadorPreCondicoes` | `ExecutorControladoTests` |
| Uma categoria por vez, com rollback por categoria | `ExecutorControlado` | `ExecutorControladoTests` |
| Regressão validada reverte a categoria | `RunnerValidacao` + `ExecutorControlado` | `ValidacaoTests` |
| Inventário sanitizado antes da nuvem | `Sanitizador`, `CerebroLlm` | `SanitizadorTests`, `CerebroTests` |
| BIOS é sempre manual (o sistema só orienta) | `ModuloBios` | `ModuloBiosTests` |
| Execução real só com opt-in explícito (`HWOPT_EXECUCAO_REAL`) | `EstadoSistemaWindows.Selecionar` | `EstadoSistemaWindowsTests` |

### Parametrização em três níveis

```
              faixa_segura            faixa_permitida           limite_absoluto
   ───────────[==========]──────────[==================]──────────────|──────────▶
              ↑ aceito             ↑ risco assumido     ↑ rejeitado    ↑ bloqueio
                                     (consentimento)                     rígido
```

Invariante do catálogo: `faixa_segura ⊆ faixa_permitida` e
`faixa_permitida.max ≤ limite_absoluto` (verificado em `CatalogoAcoes`).

---

## 6. Fluxo de dados ponta a ponta

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
                                             └─▶ Relatório + score (0-100) + auditoria
```

O comando `aplicar` da CLI executa exatamente esse fluxo; o `EstadoSistemaWindows`
o aplica de verdade (sob `HWOPT_EXECUCAO_REAL=1` em Windows elevado), enquanto o
`EstadoSistemaSimulado` (padrão) reproduz a semântica sem tocar o sistema.

---

## 7. Como compilar, testar, publicar e rodar

Pré-requisito: **.NET 8 SDK**.

```powershell
# Compilar (warnings tratados como erros) e testar tudo
dotnet build HardwareOptimizer.sln -c Release
dotnet test  HardwareOptimizer.sln -c Release

# Diagnóstico (read-only)
dotnet run --project src/HardwareOptimizer.Cli -- coletar
dotnet run --project src/HardwareOptimizer.Cli -- sensores
dotnet run --project src/HardwareOptimizer.Cli -- relatorio
dotnet run --project src/HardwareOptimizer.Cli -- bios

# Fluxo de otimização (SIMULAÇÃO por padrão)
dotnet run --project src/HardwareOptimizer.Cli -- aplicar

# Aplicar DE VERDADE no Windows (terminal Administrador):
#   $env:HWOPT_EXECUCAO_REAL = "1"; HardwareOptimizer.Cli.exe aplicar

# UI desktop
dotnet run --project src/HardwareOptimizer.App

# Publicar binários self-contained (não exigem .NET na máquina alvo)
scripts\publish.ps1            # Windows (CLI win-x64)
scripts\publish.ps1 -ComUI     # inclui a UI
```

Variáveis de ambiente: `ANTHROPIC_API_KEY` + `HWOPT_LLM_MODELO` habilitam o
cérebro LLM e a visão (sem elas, tudo roda local/offline); `HWOPT_EXECUCAO_REAL=1`
habilita a execução real no Windows.

---

## 8. Decisões de design

- **Catálogo fechado + guard do LLM:** o LLM só escolhe IDs; alucinações são
  descartadas. É o coração da segurança.
- **`Resultado<T>` em vez de exceções** para o fluxo de validação.
- **Modo simulação (dry-run) é o padrão**; a execução real (`EstadoSistemaWindows`)
  implementa a mesma `IEstadoSistema` e é opt-in.
- **Portas para registro e processos** (`IAcessoRegistro`, `IExecutorProcesso`) e
  para sensores (`IFonteSensoresLhm`): a lógica específica de Windows fica
  testável fora do Windows com fakes.
- **Warnings tratados como erros** + analisadores .NET (`Directory.Build.props`).
- **`ILogger` opcional (default `NullLogger`)**; a CLI grava log em arquivo.

---

## 9. Convenções de código

- Domínio em português; `Nullable` habilitado; `ImplicitUsings`.
- Contratos imutáveis (`record`); coleções `IReadOnly*`.
- `CultureInfo.InvariantCulture` em parsing/format numérico.
- Sem efeitos colaterais no Core; E/S e processos só no Agent.
- Testes em xUnit; o que é específico de plataforma é abstraído por portas e
  testado com fakes.

---

## 10. Índice do código-fonte (Parte II)

Ordem de leitura (dependência): **Core → Agent → Cerebro → Ipc → App → Cli**, e
depois os projetos de **testes** (que documentam o comportamento esperado).
Encerra com um apêndice de **schemas** (contratos JSON) e arquivos de build.

---

# Parte II — Código-fonte completo

> Cada seção `##` é um projeto; cada `###` é um arquivo (com o caminho relativo).
> Os blocos usam quatro crases. O conteúdo abaixo é o código real do repositório.

## Configuração de build (raiz)

### `Directory.Build.props`

````xml
<Project>
  <!--
    Configurações compartilhadas por todos os projetos da solução.
    Mantém a stack alinhada à Fase 0 do roadmap (.NET 8, nullable e analisadores
    ligados) para que as regras invariantes sejam apoiadas pelo compilador.
  -->
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
    <InvariantGlobalization>true</InvariantGlobalization>
    <Deterministic>true</Deterministic>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
````

### `HardwareOptimizer.sln`

````text

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{64ECA693-93D1-4726-B357-0FABBD0F5776}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Core", "src\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj", "{8D74C898-7973-48B0-813B-9B13B1C15350}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Agent", "src\HardwareOptimizer.Agent\HardwareOptimizer.Agent.csproj", "{8A363550-F36E-49CB-BACC-32C9A7BFEF16}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Cli", "src\HardwareOptimizer.Cli\HardwareOptimizer.Cli.csproj", "{4D8B1681-8D82-4D60-9918-3FC82A7AA6BB}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{5E9DE1B1-2FF0-4D76-97DF-5906B8D5E1F5}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Core.Tests", "tests\HardwareOptimizer.Core.Tests\HardwareOptimizer.Core.Tests.csproj", "{E62EFD74-581B-4B0E-ACF3-C20158989D12}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Agent.Tests", "tests\HardwareOptimizer.Agent.Tests\HardwareOptimizer.Agent.Tests.csproj", "{EF7F6C1B-6784-464F-83C2-70C539F12B1E}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Cerebro", "src\HardwareOptimizer.Cerebro\HardwareOptimizer.Cerebro.csproj", "{BFFDBDC2-D12A-4062-B5DC-3F54CB13847F}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Cerebro.Tests", "tests\HardwareOptimizer.Cerebro.Tests\HardwareOptimizer.Cerebro.Tests.csproj", "{D7400844-450D-4B7A-BD7C-B77AB6CB60E9}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Ipc", "src\HardwareOptimizer.Ipc\HardwareOptimizer.Ipc.csproj", "{365E3882-8526-41DB-A266-4AC5A961A482}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.Ipc.Tests", "tests\HardwareOptimizer.Ipc.Tests\HardwareOptimizer.Ipc.Tests.csproj", "{D5A4ABFE-B323-448D-947E-3FDBC58A1918}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.App", "src\HardwareOptimizer.App\HardwareOptimizer.App.csproj", "{18F90665-1268-4617-AD40-220E53A2C0AA}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HardwareOptimizer.App.Tests", "tests\HardwareOptimizer.App.Tests\HardwareOptimizer.App.Tests.csproj", "{92B80DB0-44A8-4D6E-975F-CBE96F521A4C}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{8D74C898-7973-48B0-813B-9B13B1C15350}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{8D74C898-7973-48B0-813B-9B13B1C15350}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{8D74C898-7973-48B0-813B-9B13B1C15350}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{8D74C898-7973-48B0-813B-9B13B1C15350}.Release|Any CPU.Build.0 = Release|Any CPU
		{8A363550-F36E-49CB-BACC-32C9A7BFEF16}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{8A363550-F36E-49CB-BACC-32C9A7BFEF16}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{8A363550-F36E-49CB-BACC-32C9A7BFEF16}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{8A363550-F36E-49CB-BACC-32C9A7BFEF16}.Release|Any CPU.Build.0 = Release|Any CPU
		{4D8B1681-8D82-4D60-9918-3FC82A7AA6BB}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{4D8B1681-8D82-4D60-9918-3FC82A7AA6BB}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{4D8B1681-8D82-4D60-9918-3FC82A7AA6BB}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{4D8B1681-8D82-4D60-9918-3FC82A7AA6BB}.Release|Any CPU.Build.0 = Release|Any CPU
		{E62EFD74-581B-4B0E-ACF3-C20158989D12}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{E62EFD74-581B-4B0E-ACF3-C20158989D12}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{E62EFD74-581B-4B0E-ACF3-C20158989D12}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{E62EFD74-581B-4B0E-ACF3-C20158989D12}.Release|Any CPU.Build.0 = Release|Any CPU
		{EF7F6C1B-6784-464F-83C2-70C539F12B1E}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{EF7F6C1B-6784-464F-83C2-70C539F12B1E}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{EF7F6C1B-6784-464F-83C2-70C539F12B1E}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{EF7F6C1B-6784-464F-83C2-70C539F12B1E}.Release|Any CPU.Build.0 = Release|Any CPU
		{BFFDBDC2-D12A-4062-B5DC-3F54CB13847F}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{BFFDBDC2-D12A-4062-B5DC-3F54CB13847F}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{BFFDBDC2-D12A-4062-B5DC-3F54CB13847F}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{BFFDBDC2-D12A-4062-B5DC-3F54CB13847F}.Release|Any CPU.Build.0 = Release|Any CPU
		{D7400844-450D-4B7A-BD7C-B77AB6CB60E9}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D7400844-450D-4B7A-BD7C-B77AB6CB60E9}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D7400844-450D-4B7A-BD7C-B77AB6CB60E9}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D7400844-450D-4B7A-BD7C-B77AB6CB60E9}.Release|Any CPU.Build.0 = Release|Any CPU
		{365E3882-8526-41DB-A266-4AC5A961A482}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{365E3882-8526-41DB-A266-4AC5A961A482}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{365E3882-8526-41DB-A266-4AC5A961A482}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{365E3882-8526-41DB-A266-4AC5A961A482}.Release|Any CPU.Build.0 = Release|Any CPU
		{D5A4ABFE-B323-448D-947E-3FDBC58A1918}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D5A4ABFE-B323-448D-947E-3FDBC58A1918}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D5A4ABFE-B323-448D-947E-3FDBC58A1918}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D5A4ABFE-B323-448D-947E-3FDBC58A1918}.Release|Any CPU.Build.0 = Release|Any CPU
		{18F90665-1268-4617-AD40-220E53A2C0AA}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{18F90665-1268-4617-AD40-220E53A2C0AA}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{18F90665-1268-4617-AD40-220E53A2C0AA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{18F90665-1268-4617-AD40-220E53A2C0AA}.Release|Any CPU.Build.0 = Release|Any CPU
		{92B80DB0-44A8-4D6E-975F-CBE96F521A4C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{92B80DB0-44A8-4D6E-975F-CBE96F521A4C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{92B80DB0-44A8-4D6E-975F-CBE96F521A4C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{92B80DB0-44A8-4D6E-975F-CBE96F521A4C}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{8D74C898-7973-48B0-813B-9B13B1C15350} = {64ECA693-93D1-4726-B357-0FABBD0F5776}
		{8A363550-F36E-49CB-BACC-32C9A7BFEF16} = {64ECA693-93D1-4726-B357-0FABBD0F5776}
		{4D8B1681-8D82-4D60-9918-3FC82A7AA6BB} = {64ECA693-93D1-4726-B357-0FABBD0F5776}
		{E62EFD74-581B-4B0E-ACF3-C20158989D12} = {5E9DE1B1-2FF0-4D76-97DF-5906B8D5E1F5}
		{EF7F6C1B-6784-464F-83C2-70C539F12B1E} = {5E9DE1B1-2FF0-4D76-97DF-5906B8D5E1F5}
		{BFFDBDC2-D12A-4062-B5DC-3F54CB13847F} = {64ECA693-93D1-4726-B357-0FABBD0F5776}
		{D7400844-450D-4B7A-BD7C-B77AB6CB60E9} = {5E9DE1B1-2FF0-4D76-97DF-5906B8D5E1F5}
		{365E3882-8526-41DB-A266-4AC5A961A482} = {64ECA693-93D1-4726-B357-0FABBD0F5776}
		{D5A4ABFE-B323-448D-947E-3FDBC58A1918} = {5E9DE1B1-2FF0-4D76-97DF-5906B8D5E1F5}
		{18F90665-1268-4617-AD40-220E53A2C0AA} = {64ECA693-93D1-4726-B357-0FABBD0F5776}
		{92B80DB0-44A8-4D6E-975F-CBE96F521A4C} = {5E9DE1B1-2FF0-4D76-97DF-5906B8D5E1F5}
	EndGlobalSection
EndGlobal
````


## HardwareOptimizer.Core

### `src/HardwareOptimizer.Core/HardwareOptimizer.Core.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
  </ItemGroup>

</Project>
````

### `src/HardwareOptimizer.Core/Bios/AnalisadorBios.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Decisão conservadora sobre atualizar a BIOS. Recomenda atualização apenas
/// quando há ganho real de estabilidade/compatibilidade e a versão atual é mais
/// antiga. Atualização de BIOS é arriscada por natureza: o risco nunca é menor
/// que Médio quando há flash envolvido.
/// </summary>
public sealed class AnalisadorBios
{
    public DecisaoBios Decidir(IdentificacaoBios identificacao, InfoBiosFabricante? info)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        if (info is null)
        {
            return new DecisaoBios
            {
                RecomendaAtualizar = false,
                Ganho = GanhoEstimado.Nenhum,
                Risco = NivelRisco.Medio,
                Justificativa =
                    "Sem informação do fabricante para este modelo. Mantenha a versão atual e "
                    + "verifique manualmente a página oficial de suporte.",
                VersaoAtual = identificacao.VersaoAtual,
            };
        }

        var comparacao = VersaoBios.Comparar(identificacao.VersaoAtual, info.VersaoMaisRecente);

        if (comparacao >= 0)
        {
            return new DecisaoBios
            {
                RecomendaAtualizar = false,
                Ganho = GanhoEstimado.Nenhum,
                Risco = NivelRisco.Nenhum,
                Justificativa = "A BIOS já está na versão mais recente conhecida (ou superior).",
                Fonte = info.Fonte,
                VersaoAtual = identificacao.VersaoAtual,
                VersaoRecomendada = info.VersaoMaisRecente,
            };
        }

        // Há versão mais nova, mas sem ganho real: postura conservadora — não recomenda.
        if (info.Ganho == GanhoEstimado.Nenhum)
        {
            return new DecisaoBios
            {
                RecomendaAtualizar = false,
                Ganho = GanhoEstimado.Nenhum,
                Risco = NivelRisco.Medio,
                Justificativa =
                    "Existe versão mais recente, porém sem ganho real de estabilidade ou "
                    + "compatibilidade. Atualização não recomendada (risco sem benefício claro).",
                Fonte = info.Fonte,
                VersaoAtual = identificacao.VersaoAtual,
                VersaoRecomendada = info.VersaoMaisRecente,
            };
        }

        return new DecisaoBios
        {
            RecomendaAtualizar = true,
            Ganho = info.Ganho,
            Risco = NivelRisco.Medio,
            Justificativa = info.Motivo
                ?? "Atualização recomendada por ganho de estabilidade/compatibilidade.",
            Fonte = info.Fonte,
            VersaoAtual = identificacao.VersaoAtual,
            VersaoRecomendada = info.VersaoMaisRecente,
        };
    }
}
````

### `src/HardwareOptimizer.Core/Bios/GeradorGuiaBios.cs`

````csharp
namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Gera o guia passo a passo específico do fabricante: tecla de setup,
/// utilitário de flash, procedimento, avisos de segurança e ajustes
/// recomendados (perfil de memória, Resizable BAR) com seu risco.
/// </summary>
public sealed class GeradorGuiaBios
{
    private static readonly IReadOnlyList<string> AvisosPadrao = new[]
    {
        "NÃO desligue nem reinicie o computador durante a gravação da BIOS.",
        "Use nobreak (ou bateria carregada, em notebooks) para evitar queda de energia.",
        "Baixe o arquivo apenas da página oficial do modelo EXATO da sua placa.",
        "Uma falha durante o flash pode inutilizar a placa (brick).",
    };

    private static readonly IReadOnlyList<string> AjustesPadrao = new[]
    {
        "Perfil de memória XMP (Intel) / EXPO ou DOCP (AMD): habilita a velocidade anunciada da RAM. Risco: Baixo a Médio — validar com teste de memória.",
        "Resizable BAR / Smart Access Memory: pode melhorar desempenho de GPU quando CPU e placa de vídeo suportam. Risco: Baixo.",
    };

    public GuiaBios Gerar(IdentificacaoBios identificacao)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        var (tecla, utilitario) = ProcedimentoFabricante(identificacao.Fabricante);

        var passos = new[]
        {
            $"Acesse a página oficial de suporte do modelo {identificacao.Modelo} e baixe a versão de BIOS desejada.",
            "Extraia o arquivo e copie-o para um pendrive formatado em FAT32.",
            $"Reinicie e pressione {tecla} para entrar no setup da BIOS/UEFI.",
            $"Abra o utilitário {utilitario}.",
            "Selecione o arquivo de BIOS no pendrive e confirme a atualização.",
            "Aguarde a conclusão sem interromper; o sistema reiniciará automaticamente.",
            "Após reiniciar, confirme a nova versão (o sistema relê a versão pelo inventário).",
        };

        return new GuiaBios
        {
            TeclaSetup = tecla,
            Utilitario = utilitario,
            Passos = passos,
            Avisos = AvisosPadrao,
            AjustesRecomendados = AjustesPadrao,
        };
    }

    private static (string Tecla, string Utilitario) ProcedimentoFabricante(string fabricante) =>
        fabricante switch
        {
            "ASUS" => ("Del (ou F2)", "ASUS EZ Flash 3 (menu Tool/Advanced)"),
            "Gigabyte" => ("Del", "Q-Flash (tecla End no boot ou via BIOS)"),
            "MSI" => ("Del", "M-Flash"),
            "ASRock" => ("F2 ou Del", "ASRock Instant Flash"),
            _ => ("Del ou F2 (varia por fabricante)", "utilitário de atualização do próprio fabricante"),
        };
}
````

### `src/HardwareOptimizer.Core/Bios/IProvedorInfoBios.cs`

````csharp
namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Fonte de informação de BIOS do fabricante. A implementação padrão é um banco
/// curado em memória; uma futura implementação pode buscar na web (priorizando
/// o domínio do fabricante) e cachear o resultado.
/// </summary>
public interface IProvedorInfoBios
{
    Task<InfoBiosFabricante?> ObterAsync(string chaveBusca, CancellationToken cancellationToken = default);
}

/// <summary>
/// Banco curado das placas mais comuns (passo "Verificação com fabricante", via
/// banco curado). Chaveado pela mesma chave de busca normalizada do inventário.
/// </summary>
public sealed class BancoCuradoBios : IProvedorInfoBios
{
    private readonly IReadOnlyDictionary<string, InfoBiosFabricante> _entradas;

    public BancoCuradoBios(IReadOnlyDictionary<string, InfoBiosFabricante>? entradas = null)
    {
        _entradas = entradas ?? Padrao();
    }

    public Task<InfoBiosFabricante?> ObterAsync(
        string chaveBusca, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entradas.TryGetValue(chaveBusca, out var info);
        return Task.FromResult(info);
    }

    private static IReadOnlyDictionary<string, InfoBiosFabricante> Padrao() =>
        new Dictionary<string, InfoBiosFabricante>(StringComparer.OrdinalIgnoreCase)
        {
            ["asus|rog strix b550-f"] = new InfoBiosFabricante
            {
                Fabricante = "ASUS",
                Modelo = "ROG STRIX B550-F",
                VersaoMaisRecente = "3405",
                DataMaisRecente = "2023-06-01",
                Changelog = "Atualiza AGESA e melhora a estabilidade de memória e compatibilidade de CPU.",
                LinkManual = "https://www.asus.com/support/",
                Fonte = "https://www.asus.com/motherboards-components/motherboards/rog/rog-strix-b550-f-gaming/helpdesk_bios/",
                Ganho = GanhoEstimado.Medio,
                Motivo = "Correção de estabilidade de memória e compatibilidade de CPU.",
            },
            ["msi|mag b550 tomahawk"] = new InfoBiosFabricante
            {
                Fabricante = "MSI",
                Modelo = "MAG B550 TOMAHAWK",
                VersaoMaisRecente = "7C91vH9",
                DataMaisRecente = "2023-08-10",
                Changelog = "Atualiza AGESA ComboAM4v2PI; melhora compatibilidade com CPUs Ryzen 5000.",
                LinkManual = "https://www.msi.com/Motherboard/MAG-B550-TOMAHAWK/support",
                Fonte = "https://www.msi.com/Motherboard/MAG-B550-TOMAHAWK/support",
                Ganho = GanhoEstimado.Medio,
                Motivo = "Melhora de compatibilidade de CPU e estabilidade.",
            },
            ["gigabyte|b550 aorus elite"] = new InfoBiosFabricante
            {
                Fabricante = "Gigabyte",
                Modelo = "B550 AORUS ELITE",
                VersaoMaisRecente = "F16",
                DataMaisRecente = "2022-11-20",
                Changelog = "Atualiza AGESA; correções gerais de estabilidade.",
                LinkManual = "https://www.gigabyte.com/Motherboard/B550-AORUS-ELITE-rev-10/support",
                Fonte = "https://www.gigabyte.com/Motherboard/B550-AORUS-ELITE-rev-10/support",
                Ganho = GanhoEstimado.Baixo,
                Motivo = "Correções gerais de estabilidade.",
            },
        };
}
````

### `src/HardwareOptimizer.Core/Bios/ModelosBios.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Bios;

/// <summary>Ganho estimado de uma atualização de BIOS.</summary>
public enum GanhoEstimado
{
    Nenhum = 0,
    Baixo = 1,
    Medio = 2,
    Alto = 3,
}

/// <summary>Dados identificados da BIOS atual (passo "Identificação").</summary>
public sealed record IdentificacaoBios
{
    public required string FabricanteBruto { get; init; }

    /// <summary>Fabricante normalizado (ex.: "ASUS").</summary>
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public string? VersaoAtual { get; init; }

    public string? Data { get; init; }

    public string? Modo { get; init; }

    public bool? SecureBoot { get; init; }

    public required string ChaveBusca { get; init; }

    public static IdentificacaoBios DeInventario(Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        var placa = inventario.Placa;

        return new IdentificacaoBios
        {
            FabricanteBruto = placa.Fabricante,
            Fabricante = NormalizadorFabricante.Normalizar(placa.Fabricante),
            Modelo = placa.Modelo,
            VersaoAtual = placa.VersaoBios,
            Data = placa.DataBios,
            Modo = placa.Modo,
            SecureBoot = placa.SecureBoot,
            ChaveBusca = NormalizadorFabricante.GerarChaveBusca(placa.Fabricante, placa.Modelo),
        };
    }
}

/// <summary>Informação obtida do fabricante (passo "Verificação com fabricante").</summary>
public sealed record InfoBiosFabricante
{
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public required string VersaoMaisRecente { get; init; }

    public string? DataMaisRecente { get; init; }

    public string? Changelog { get; init; }

    public string? LinkManual { get; init; }

    /// <summary>Fonte sempre visível (exigência do documento).</summary>
    public required string Fonte { get; init; }

    public GanhoEstimado Ganho { get; init; }

    public string? Motivo { get; init; }
}

/// <summary>Decisão conservadora sobre atualizar ou não (passo "Decisão conservadora").</summary>
public sealed record DecisaoBios
{
    public required bool RecomendaAtualizar { get; init; }

    public required GanhoEstimado Ganho { get; init; }

    public required NivelRisco Risco { get; init; }

    public required string Justificativa { get; init; }

    public string? Fonte { get; init; }

    public string? VersaoAtual { get; init; }

    public string? VersaoRecomendada { get; init; }
}

/// <summary>Guia passo a passo específico do fabricante (passo "Guia passo a passo").</summary>
public sealed record GuiaBios
{
    public required string TeclaSetup { get; init; }

    public required string Utilitario { get; init; }

    public IReadOnlyList<string> Passos { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AjustesRecomendados { get; init; } = Array.Empty<string>();
}

/// <summary>Relatório consolidado do módulo BIOS, ponta a ponta.</summary>
public sealed record RelatorioBios
{
    public required IdentificacaoBios Identificacao { get; init; }

    public InfoBiosFabricante? InfoFabricante { get; init; }

    public required DecisaoBios Decisao { get; init; }

    public required GuiaBios Guia { get; init; }

    /// <summary>Houve correspondência no fabricante/banco curado?</summary>
    public bool FonteEncontrada => InfoFabricante is not null;
}
````

### `src/HardwareOptimizer.Core/Bios/NormalizadorFabricante.cs`

````csharp
namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Limpa e padroniza as strings sujas do SMBIOS (passo "Normalização" do
/// fluxo_bios). Ex.: "ASUSTeK Computer Inc." → "ASUS". Gera uma chave de busca
/// estável para o lookup do fabricante e o cache.
/// </summary>
public static class NormalizadorFabricante
{
    // Correspondência exata (após lower) para siglas curtas e ambíguas.
    private static readonly IReadOnlyDictionary<string, string> Exatos =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hp"] = "HP",
            ["msi"] = "MSI",
            ["asus"] = "ASUS",
        };

    // Correspondência por fragmento contido na string suja.
    private static readonly (string Fragmento, string Canonico)[] Fragmentos =
    {
        ("asustek", "ASUS"),
        ("asus", "ASUS"),
        ("gigabyte", "Gigabyte"),
        ("giga-byte", "Gigabyte"),
        ("micro-star", "MSI"),
        ("msi", "MSI"),
        ("asrock", "ASRock"),
        ("hewlett", "HP"),
        ("packard", "HP"),
        ("lenovo", "Lenovo"),
        ("dell", "Dell"),
        ("acer", "Acer"),
        ("biostar", "Biostar"),
        ("intel", "Intel"),
    };

    public static string Normalizar(string? fabricante)
    {
        if (string.IsNullOrWhiteSpace(fabricante))
        {
            return "Desconhecido";
        }

        var limpo = fabricante.Trim();
        var lower = limpo.ToLowerInvariant();

        if (Exatos.TryGetValue(lower, out var exato))
        {
            return exato;
        }

        foreach (var (fragmento, canonico) in Fragmentos)
        {
            if (lower.Contains(fragmento, StringComparison.Ordinal))
            {
                return canonico;
            }
        }

        return limpo;
    }

    /// <summary>Gera a chave de busca "fabricante|modelo" normalizada e minúscula.</summary>
    public static string GerarChaveBusca(string? fabricante, string? modelo)
    {
        var fab = Normalizar(fabricante).ToLowerInvariant();
        var mod = ColapsarEspacos((modelo ?? string.Empty).Trim().ToLowerInvariant());
        return $"{fab}|{mod}";
    }

    private static string ColapsarEspacos(string texto) =>
        string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
````

### `src/HardwareOptimizer.Core/Bios/VersaoBios.cs`

````csharp
using System.Globalization;

namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Comparação de versões de BIOS tolerante aos formatos reais do mercado:
/// numéricos puros ("2806" vs "3405"), com prefixo ("F10" vs "F12") e
/// pontuados ("P3.60" vs "P3.70"). Compara token a token, números por valor e
/// texto por ordem, evitando o erro clássico de comparar versões como strings.
/// </summary>
public static class VersaoBios
{
    /// <summary>Retorna &lt;0 se a &lt; b, 0 se iguais, &gt;0 se a &gt; b.</summary>
    public static int Comparar(string? a, string? b)
    {
        var tokensA = Tokenizar(a ?? string.Empty);
        var tokensB = Tokenizar(b ?? string.Empty);

        var total = Math.Max(tokensA.Count, tokensB.Count);
        for (var i = 0; i < total; i++)
        {
            if (i >= tokensA.Count)
            {
                return -1;
            }

            if (i >= tokensB.Count)
            {
                return 1;
            }

            var comparacao = CompararToken(tokensA[i], tokensB[i]);
            if (comparacao != 0)
            {
                return comparacao;
            }
        }

        return 0;
    }

    /// <summary>Verdadeiro se <paramref name="candidata"/> é mais nova que <paramref name="atual"/>.</summary>
    public static bool EhMaisRecente(string? atual, string? candidata) =>
        Comparar(candidata, atual) > 0;

    private static int CompararToken(string a, string b)
    {
        var numericoA = long.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var valorA);
        var numericoB = long.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var valorB);

        if (numericoA && numericoB)
        {
            return valorA.CompareTo(valorB);
        }

        // Um token numérico ordena antes de um token textual de mesma posição.
        if (numericoA != numericoB)
        {
            return numericoA ? -1 : 1;
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> Tokenizar(string versao)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < versao.Length)
        {
            // Ignora separadores comuns.
            if (versao[i] is '.' or '-' or '_' or ' ')
            {
                i++;
                continue;
            }

            var ehDigito = char.IsDigit(versao[i]);
            var inicio = i;
            while (i < versao.Length && char.IsDigit(versao[i]) == ehDigito
                && versao[i] is not ('.' or '-' or '_' or ' '))
            {
                i++;
            }

            var token = versao[inicio..i];
            // Remove zeros à esquerda de tokens numéricos para a comparação por valor.
            tokens.Add(ehDigito ? token.TrimStart('0') is { Length: > 0 } t ? t : "0" : token);
        }

        return tokens;
    }
}
````

### `src/HardwareOptimizer.Core/Catalog/AcaoOtimizacao.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Ação de otimização pré-construída, auditada e parametrizada (entrada do
/// catálogo whitelisted). O LLM seleciona o <see cref="Id"/> e define parâmetros
/// dentro das faixas; o agente determinístico executa o <see cref="ComandoInternoId"/>.
/// </summary>
public sealed class AcaoOtimizacao
{
    public required string Id { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    public required string Titulo { get; init; }

    public required string Descricao { get; init; }

    public IReadOnlyList<Parametro> Parametros { get; init; } = Array.Empty<Parametro>();

    /// <summary>
    /// Identificador do comando interno determinístico e versionado que aplica a
    /// ação. NUNCA é fornecido pelo LLM; resolvido pelo agente local.
    /// </summary>
    public required string ComandoInternoId { get; init; }

    /// <summary>Descrição da ação inversa correspondente, usada no rollback.</summary>
    public required string Reversao { get; init; }

    public required NivelRisco Risco { get; init; }

    public bool RequerAprovacao { get; init; } = true;

    public bool RequerReinicio { get; init; }

    /// <summary>Checagens obrigatórias antes de aplicar (ex.: backup_confirmado).</summary>
    public IReadOnlyList<string> PreCondicoes { get; init; } = Array.Empty<string>();

    public Parametro? ObterParametro(string nome) =>
        Parametros.FirstOrDefault(p => string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>Valida a coerência interna de todos os parâmetros desta ação.</summary>
    public Resultado VerificarCoerencia()
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
        {
            erros.Add("Ação sem Id.");
        }

        if (string.IsNullOrWhiteSpace(ComandoInternoId))
        {
            erros.Add($"Ação '{Id}' sem comando interno associado.");
        }

        foreach (var parametro in Parametros)
        {
            var coerencia = parametro.VerificarCoerencia();
            if (coerencia.Falha)
            {
                erros.AddRange(coerencia.Erros);
            }
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }
}
````

### `src/HardwareOptimizer.Core/Catalog/CatalogoAcoes.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Conjunto fechado e versionado de ações de otimização. Nenhuma ação fora deste
/// catálogo pode ser executada. O LLM só pode referenciar IDs aqui presentes.
/// </summary>
public sealed class CatalogoAcoes
{
    private readonly IReadOnlyDictionary<string, AcaoOtimizacao> _acoes;

    public CatalogoAcoes(string versao, IEnumerable<AcaoOtimizacao> acoes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentNullException.ThrowIfNull(acoes);

        Versao = versao;
        var mapa = new Dictionary<string, AcaoOtimizacao>(StringComparer.OrdinalIgnoreCase);
        foreach (var acao in acoes)
        {
            if (!mapa.TryAdd(acao.Id, acao))
            {
                throw new ArgumentException($"Id de ação duplicado no catálogo: '{acao.Id}'.", nameof(acoes));
            }
        }

        _acoes = mapa;
    }

    public string Versao { get; }

    public IReadOnlyCollection<AcaoOtimizacao> Todas => (IReadOnlyCollection<AcaoOtimizacao>)_acoes.Values;

    public bool Contem(string acaoId) => acaoId is not null && _acoes.ContainsKey(acaoId);

    public AcaoOtimizacao? Obter(string acaoId) =>
        acaoId is not null && _acoes.TryGetValue(acaoId, out var acao) ? acao : null;

    public IEnumerable<AcaoOtimizacao> PorCategoria(CategoriaAcao categoria) =>
        _acoes.Values.Where(a => a.Categoria == categoria);

    /// <summary>Valida a coerência de todas as ações (usado em testes e no startup).</summary>
    public Resultado VerificarCoerencia()
    {
        var erros = new List<string>();
        foreach (var acao in _acoes.Values)
        {
            var r = acao.VerificarCoerencia();
            if (r.Falha)
            {
                erros.AddRange(r.Erros);
            }
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }
}
````

### `src/HardwareOptimizer.Core/Catalog/CatalogoPadrao.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Fábrica do catálogo embutido. Cada ação aqui é auditada e versionada; os
/// comandos internos correspondentes vivem no agente local (nunca no LLM).
/// </summary>
public static class CatalogoPadrao
{
    /// <summary>Versão do catálogo, registrada na auditoria de consentimento.</summary>
    public const string Versao = "2024.06-mvp";

    public static CatalogoAcoes Criar() => new(Versao, ConstruirAcoes());

    private static IEnumerable<AcaoOtimizacao> ConstruirAcoes()
    {
        yield return new AcaoOtimizacao
        {
            Id = "PWR_PLANO_ALTO_DESEMPENHO",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Ativar plano de energia de alto desempenho",
            Descricao = "Seleciona o plano de energia de alto desempenho do Windows (powercfg).",
            ComandoInternoId = "cmd.pwr.plano_alto_desempenho.v1",
            Reversao = "Restaurar plano de energia anterior exportado no backup.",
            Risco = NivelRisco.MuitoBaixo,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "PWR_USB_SUSPENSAO_SELETIVA",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Desativar suspensão seletiva de USB",
            Descricao = "Impede que o Windows suspenda dispositivos USB, reduzindo microcortes de periféricos.",
            ComandoInternoId = "cmd.pwr.usb_suspensao_seletiva.v1",
            Reversao = "Reativar a suspensão seletiva de USB.",
            Risco = NivelRisco.MuitoBaixo,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "SO_EFEITOS_VISUAIS_DESEMPENHO",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Ajustar efeitos visuais para desempenho",
            Descricao = "Desativa animações e efeitos visuais não essenciais da interface.",
            ComandoInternoId = "cmd.so.efeitos_visuais.v1",
            Reversao = "Restaurar a configuração anterior de efeitos visuais.",
            Risco = NivelRisco.Nenhum,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        // Parâmetro numérico exemplar (menor = mais agressivo). Padrão do Windows = 20.
        yield return new AcaoOtimizacao
        {
            Id = "SO_SYSTEM_RESPONSIVENESS",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Ajustar reserva de responsividade do sistema",
            Descricao = "Define o percentual de CPU reservado a tarefas de segundo plano " +
                        "(registro SystemResponsiveness). Reduzir prioriza primeiro plano.",
            Parametros = new Parametro[]
            {
                new ParametroNumerico(
                    nome: "percentual_reserva",
                    descricao: "Percentual reservado a tarefas de baixa prioridade.",
                    faixaSegura: new FaixaNumerica(10, 20),
                    faixaPermitida: new FaixaNumerica(0, 20),
                    limiteAbsoluto: 20,
                    padraoSeguro: 20,
                    unidade: "%"),
            },
            ComandoInternoId = "cmd.so.system_responsiveness.v1",
            Reversao = "Restaurar o valor anterior de SystemResponsiveness.",
            Risco = NivelRisco.Baixo,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        // Parâmetro numérico exemplar (maior = mais tolerante, porém mascara falhas). Padrão = 2s.
        yield return new AcaoOtimizacao
        {
            Id = "GPU_TDR_DELAY",
            Categoria = CategoriaAcao.Gpu,
            Titulo = "Ajustar tempo de recuperação do driver de vídeo (TDR)",
            Descricao = "Define o tempo (s) antes de o Windows reiniciar o driver de vídeo travado " +
                        "(registro TdrDelay). Valores altos mascaram instabilidade real.",
            Parametros = new Parametro[]
            {
                new ParametroNumerico(
                    nome: "tempo_segundos",
                    descricao: "Tempo de espera antes do reset do driver de vídeo.",
                    faixaSegura: new FaixaNumerica(2, 8),
                    faixaPermitida: new FaixaNumerica(2, 60),
                    limiteAbsoluto: 60,
                    padraoSeguro: 2,
                    unidade: "s"),
            },
            ComandoInternoId = "cmd.gpu.tdr_delay.v1",
            Reversao = "Restaurar o valor anterior de TdrDelay.",
            Risco = NivelRisco.Medio,
            RequerAprovacao = true,
            RequerReinicio = true,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "GPU_HAGS",
            Categoria = CategoriaAcao.Gpu,
            Titulo = "Ativar agendamento de GPU acelerado por hardware (HAGS)",
            Descricao = "Habilita o Hardware Accelerated GPU Scheduling, quando suportado pelo driver.",
            ComandoInternoId = "cmd.gpu.hags.v1",
            Reversao = "Desativar o agendamento de GPU acelerado por hardware.",
            Risco = NivelRisco.Baixo,
            RequerAprovacao = true,
            RequerReinicio = true,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        // Lista branca de serviços considerados seguros de desativar (conservadora).
        yield return new AcaoOtimizacao
        {
            Id = "SRV_DESATIVAR_SERVICO",
            Categoria = CategoriaAcao.Servicos,
            Titulo = "Desativar serviço não essencial",
            Descricao = "Desativa um serviço presente na lista branca de serviços seguros.",
            Parametros = new Parametro[]
            {
                new ParametroListaBranca(
                    nome: "nome_servico",
                    descricao: "Nome do serviço do Windows a desativar.",
                    valoresSeguros: new[]
                    {
                        "SysMain", "DiagTrack", "Fax", "RetailDemo",
                        "MapsBroker", "XblGameSave", "XboxNetApiSvc",
                    },
                    padraoSeguro: "DiagTrack"),
            },
            ComandoInternoId = "cmd.srv.desativar_servico.v1",
            Reversao = "Reativar o serviço com o tipo de inicialização anterior.",
            Risco = NivelRisco.Medio,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado", "servico_consta_na_lista_segura" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "NET_THROTTLING_DESABILITAR",
            Categoria = CategoriaAcao.Rede,
            Titulo = "Desabilitar limitação de rede (NetworkThrottlingIndex)",
            Descricao = "Remove a limitação de throughput de rede imposta pelo agendador multimídia.",
            ComandoInternoId = "cmd.net.throttling_index.v1",
            Reversao = "Restaurar o NetworkThrottlingIndex anterior (padrão 10).",
            Risco = NivelRisco.Baixo,
            RequerAprovacao = true,
            RequerReinicio = true,
            PreCondicoes = new[] { "backup_confirmado" },
        };
    }
}
````

### `src/HardwareOptimizer.Core/Catalog/FaixaNumerica.cs`

````csharp
using System.Globalization;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>Intervalo numérico fechado [Minimo, Maximo].</summary>
public sealed record FaixaNumerica
{
    public FaixaNumerica(double minimo, double maximo)
    {
        if (maximo < minimo)
        {
            throw new ArgumentException(
                $"Faixa inválida: máximo ({maximo}) menor que mínimo ({minimo}).", nameof(maximo));
        }

        Minimo = minimo;
        Maximo = maximo;
    }

    public double Minimo { get; }

    public double Maximo { get; }

    public bool Contem(double valor) => valor >= Minimo && valor <= Maximo;

    /// <summary>Verdadeiro se este intervalo está inteiramente contido em <paramref name="externa"/>.</summary>
    public bool EstaContidaEm(FaixaNumerica externa) =>
        Minimo >= externa.Minimo && Maximo <= externa.Maximo;

    public override string ToString() =>
        $"[{Minimo.ToString(CultureInfo.InvariantCulture)}, {Maximo.ToString(CultureInfo.InvariantCulture)}]";
}
````

### `src/HardwareOptimizer.Core/Catalog/Parametro.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Parâmetro de uma ação do catálogo. Cada parâmetro sabe validar um valor
/// proposto sob um determinado perfil, aplicando as regras invariantes do
/// documento. A implementação é fechada (apenas os tipos deste arquivo).
/// </summary>
public abstract class Parametro
{
    protected Parametro(string nome, string descricao)
    {
        Nome = nome;
        Descricao = descricao;
    }

    public string Nome { get; }

    public string Descricao { get; }

    /// <summary>Valor padrão usado pelo perfil seguro (sempre dentro da faixa segura).</summary>
    public abstract string ValorPadraoSeguro { get; }

    /// <summary>Valida o valor proposto considerando o tipo de perfil.</summary>
    public abstract ResultadoParametro Validar(string valorBruto, TipoPerfil perfil);

    /// <summary>Checa a coerência interna do próprio parâmetro (sanidade do catálogo).</summary>
    public abstract Resultado VerificarCoerencia();
}

/// <summary>
/// Parâmetro numérico com três níveis de controle:
/// faixa segura (padrão recomendado), faixa permitida (mais ampla, perfil
/// customizado) e limite absoluto (teto técnico que NENHUM perfil ultrapassa).
/// </summary>
public sealed class ParametroNumerico : Parametro
{
    public ParametroNumerico(
        string nome,
        string descricao,
        FaixaNumerica faixaSegura,
        FaixaNumerica faixaPermitida,
        double limiteAbsoluto,
        double padraoSeguro,
        string? unidade = null)
        : base(nome, descricao)
    {
        FaixaSegura = faixaSegura;
        FaixaPermitida = faixaPermitida;
        LimiteAbsoluto = limiteAbsoluto;
        PadraoSeguro = padraoSeguro;
        Unidade = unidade;
    }

    public FaixaNumerica FaixaSegura { get; }

    public FaixaNumerica FaixaPermitida { get; }

    public double LimiteAbsoluto { get; }

    public double PadraoSeguro { get; }

    public string? Unidade { get; }

    public override string ValorPadraoSeguro => PadraoSeguro.ToString(CultureInfo.InvariantCulture);

    public override ResultadoParametro Validar(string valorBruto, TipoPerfil perfil)
    {
        if (!double.TryParse(valorBruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
        {
            return ResultadoParametro.Rejeitado(Nome, valorBruto, $"'{valorBruto}' não é um número válido.");
        }

        // 1) Limite absoluto: bloqueio rígido. Vale para QUALQUER perfil.
        if (valor > LimiteAbsoluto)
        {
            return ResultadoParametro.BloqueioLimiteAbsoluto(
                Nome, valorBruto, $"{Formatar(valor)} > limite absoluto {Formatar(LimiteAbsoluto)}.");
        }

        // 2) Fora da faixa permitida (abaixo do mínimo ou acima do máximo): inválido.
        if (!FaixaPermitida.Contem(valor))
        {
            return ResultadoParametro.Rejeitado(
                Nome, valorBruto, $"{Formatar(valor)} fora da faixa permitida {FaixaPermitida}.");
        }

        // 3) Perfil seguro só aceita valores dentro da faixa segura.
        if (perfil == TipoPerfil.Seguro && !FaixaSegura.Contem(valor))
        {
            return ResultadoParametro.Rejeitado(
                Nome, valorBruto,
                $"Perfil seguro exige faixa segura {FaixaSegura}; {Formatar(valor)} está fora dela.");
        }

        // 4) Dentro da permitida, fora da segura: risco assumido (apenas perfil customizado chega aqui).
        if (!FaixaSegura.Contem(valor))
        {
            return ResultadoParametro.RiscoAssumido(
                Nome, valorBruto, $"{Formatar(valor)} fora da faixa segura {FaixaSegura}.");
        }

        return ResultadoParametro.Aceito(Nome, valorBruto);
    }

    public override Resultado VerificarCoerencia()
    {
        var erros = new List<string>();

        if (!FaixaSegura.EstaContidaEm(FaixaPermitida))
        {
            erros.Add($"Parâmetro '{Nome}': faixa segura {FaixaSegura} não está contida na permitida {FaixaPermitida}.");
        }

        if (FaixaPermitida.Maximo > LimiteAbsoluto)
        {
            erros.Add($"Parâmetro '{Nome}': máximo da faixa permitida {Formatar(FaixaPermitida.Maximo)} ultrapassa o limite absoluto {Formatar(LimiteAbsoluto)}.");
        }

        if (!FaixaSegura.Contem(PadraoSeguro))
        {
            erros.Add($"Parâmetro '{Nome}': padrão seguro {Formatar(PadraoSeguro)} fora da faixa segura {FaixaSegura}.");
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }

    private static string Formatar(double valor) => valor.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Parâmetro cujo valor deve constar em uma lista branca fechada de opções
/// seguras (ex.: nome de serviço passível de ser desativado).
/// </summary>
public sealed class ParametroListaBranca : Parametro
{
    private readonly IReadOnlyList<string> _valoresSeguros;

    public ParametroListaBranca(
        string nome,
        string descricao,
        IReadOnlyList<string> valoresSeguros,
        string padraoSeguro)
        : base(nome, descricao)
    {
        _valoresSeguros = valoresSeguros;
        PadraoSeguro = padraoSeguro;
    }

    public IReadOnlyList<string> ValoresSeguros => _valoresSeguros;

    public string PadraoSeguro { get; }

    public override string ValorPadraoSeguro => PadraoSeguro;

    public override ResultadoParametro Validar(string valorBruto, TipoPerfil perfil)
    {
        _ = perfil; // a lista branca vale igualmente para perfil seguro e customizado.

        return _valoresSeguros.Contains(valorBruto, StringComparer.OrdinalIgnoreCase)
            ? ResultadoParametro.Aceito(Nome, valorBruto)
            : ResultadoParametro.Rejeitado(
                Nome, valorBruto, $"'{valorBruto}' não consta na lista segura.");
    }

    public override Resultado VerificarCoerencia()
    {
        if (_valoresSeguros.Count == 0)
        {
            return Resultado.Falhar($"Parâmetro '{Nome}': lista branca vazia.");
        }

        return _valoresSeguros.Contains(PadraoSeguro, StringComparer.OrdinalIgnoreCase)
            ? Resultado.Ok()
            : Resultado.Falhar($"Parâmetro '{Nome}': padrão seguro '{PadraoSeguro}' não consta na lista branca.");
    }
}
````

### `src/HardwareOptimizer.Core/Catalog/ResultadoParametro.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Desfecho da validação de um valor proposto para um parâmetro, segundo as
/// regras do documento (faixa_segura / faixa_permitida / limite_absoluto).
/// </summary>
public sealed record ResultadoParametro
{
    private ResultadoParametro(string parametro, string valor, SituacaoParametro situacao, string mensagem)
    {
        Parametro = parametro;
        Valor = valor;
        Situacao = situacao;
        Mensagem = mensagem;
    }

    public string Parametro { get; }

    public string Valor { get; }

    public SituacaoParametro Situacao { get; }

    public string Mensagem { get; }

    /// <summary>Pode ser persistido/aplicado? Falso para rejeição e bloqueio rígido.</summary>
    public bool Aplicavel => Situacao is SituacaoParametro.Aceito or SituacaoParametro.RiscoAssumido;

    /// <summary>Exige consentimento explícito por estar fora da faixa segura.</summary>
    public bool ExigeConsentimento => Situacao == SituacaoParametro.RiscoAssumido;

    public static ResultadoParametro Aceito(string parametro, string valor) =>
        new(parametro, valor, SituacaoParametro.Aceito, "Dentro da faixa segura.");

    public static ResultadoParametro RiscoAssumido(string parametro, string valor, string detalhe) =>
        new(parametro, valor, SituacaoParametro.RiscoAssumido,
            "Fora da faixa segura, dentro da permitida — risco assumido pelo usuário. " + detalhe);

    public static ResultadoParametro Rejeitado(string parametro, string valor, string motivo) =>
        new(parametro, valor, SituacaoParametro.Rejeitado, motivo);

    public static ResultadoParametro BloqueioLimiteAbsoluto(string parametro, string valor, string detalhe) =>
        new(parametro, valor, SituacaoParametro.BloqueioLimiteAbsoluto,
            "Bloqueio rígido: ultrapassa o limite absoluto. " + detalhe);
}
````

### `src/HardwareOptimizer.Core/Catalog/ResultadoValidacaoAcao.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>Resultado da validação de uma seleção de ação (id + parâmetros) contra o catálogo.</summary>
public sealed class ResultadoValidacaoAcao
{
    public ResultadoValidacaoAcao(
        string acaoId,
        bool acaoConhecida,
        IReadOnlyList<ResultadoParametro> parametros,
        IReadOnlyList<string> erros)
    {
        AcaoId = acaoId;
        AcaoConhecida = acaoConhecida;
        Parametros = parametros;
        Erros = erros;
    }

    public string AcaoId { get; }

    /// <summary>A ação consta no catálogo whitelisted?</summary>
    public bool AcaoConhecida { get; }

    public IReadOnlyList<ResultadoParametro> Parametros { get; }

    public IReadOnlyList<string> Erros { get; }

    /// <summary>Algum parâmetro foi barrado pelo limite absoluto (bloqueio rígido).</summary>
    public bool TemBloqueioRigido =>
        Parametros.Any(p => p.Situacao == SituacaoParametro.BloqueioLimiteAbsoluto);

    /// <summary>Algum parâmetro está fora da faixa segura (risco assumido) e exige consentimento.</summary>
    public bool ExigeConsentimento => Parametros.Any(p => p.ExigeConsentimento);

    /// <summary>A ação pode ser aplicada: está no catálogo, sem erros e com todos os parâmetros aplicáveis.</summary>
    public bool Aplicavel =>
        AcaoConhecida && Erros.Count == 0 && Parametros.All(p => p.Aplicavel);

    public Resultado ComoResultado() => Aplicavel ? Resultado.Ok() : Resultado.Falhar(ReunirErros());

    private IReadOnlyList<string> ReunirErros()
    {
        var erros = new List<string>(Erros);
        if (!AcaoConhecida)
        {
            erros.Add($"Ação '{AcaoId}' não consta no catálogo whitelisted.");
        }

        erros.AddRange(
            Parametros.Where(p => !p.Aplicavel).Select(p => $"{p.Parametro}: {p.Mensagem}"));

        return erros;
    }
}
````

### `src/HardwareOptimizer.Core/Catalog/ValidadorAcao.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Guarda determinística do catálogo. Recusa qualquer ação fora do catálogo e
/// qualquer valor acima do limite absoluto. É o ponto único por onde toda
/// seleção do LLM precisa passar antes de virar execução.
/// </summary>
public sealed class ValidadorAcao
{
    private readonly CatalogoAcoes _catalogo;

    public ValidadorAcao(CatalogoAcoes catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        _catalogo = catalogo;
    }

    public ResultadoValidacaoAcao Validar(
        string acaoId,
        IReadOnlyDictionary<string, string> parametros,
        TipoPerfil perfil)
    {
        ArgumentNullException.ThrowIfNull(parametros);

        var acao = _catalogo.Obter(acaoId);
        if (acao is null)
        {
            return new ResultadoValidacaoAcao(
                acaoId,
                acaoConhecida: false,
                parametros: Array.Empty<ResultadoParametro>(),
                erros: new[] { $"Ação '{acaoId}' não consta no catálogo whitelisted." });
        }

        var erros = new List<string>();

        // Parâmetros informados que a ação não declara são rejeitados (catálogo fechado).
        foreach (var nome in parametros.Keys)
        {
            if (acao.ObterParametro(nome) is null)
            {
                erros.Add($"Parâmetro desconhecido '{nome}' para a ação '{acaoId}'.");
            }
        }

        // Todo parâmetro declarado precisa de um valor válido.
        var resultados = new List<ResultadoParametro>();
        foreach (var parametro in acao.Parametros)
        {
            if (!parametros.TryGetValue(parametro.Nome, out var valor))
            {
                erros.Add($"Parâmetro obrigatório '{parametro.Nome}' não foi fornecido para '{acaoId}'.");
                continue;
            }

            resultados.Add(parametro.Validar(valor, perfil));
        }

        return new ResultadoValidacaoAcao(acaoId, acaoConhecida: true, resultados, erros);
    }
}
````

### `src/HardwareOptimizer.Core/Common/Enums.cs`

````csharp
namespace HardwareOptimizer.Core.Common;

/// <summary>Sistemas operacionais suportados. MVP prioriza Windows 11.</summary>
public enum SistemaOperacionalTipo
{
    Desconhecido = 0,
    Windows = 1,
    Linux = 2,
}

/// <summary>
/// Categorias de otimização. A ordem dos membros segue exatamente
/// <c>categorias_ordem</c> do documento de arquitetura, de modo que a ordenação
/// natural do enum já corresponde à ordem de execução por categoria.
/// </summary>
public enum CategoriaAcao
{
    Cpu = 0,
    Memoria = 1,
    Gpu = 2,
    SistemaOperacional = 3,
    Drivers = 4,
    Servicos = 5,
    Rede = 6,
}

/// <summary>Classificação de risco de uma ação, do documento.</summary>
public enum NivelRisco
{
    Nenhum = 0,
    MuitoBaixo = 1,
    Baixo = 2,
    Medio = 3,
    Alto = 4,
}

/// <summary>Perfil de parametrização: seguro (padrão) ou customizado pelo usuário.</summary>
public enum TipoPerfil
{
    Seguro = 0,
    Customizado = 1,
}

/// <summary>Desfecho da validação de um único valor de parâmetro.</summary>
public enum SituacaoParametro
{
    /// <summary>Dentro da faixa segura: aprovado sem ressalvas.</summary>
    Aceito = 0,

    /// <summary>Dentro da faixa permitida, porém fora da faixa segura: risco assumido pelo usuário.</summary>
    RiscoAssumido = 1,

    /// <summary>Valor inválido ou fora da faixa permitida: rejeitado.</summary>
    Rejeitado = 2,

    /// <summary>Ultrapassa o limite absoluto: bloqueio rígido, sem opção de prosseguir.</summary>
    BloqueioLimiteAbsoluto = 3,
}
````

### `src/HardwareOptimizer.Core/Common/Resultado.cs`

````csharp
namespace HardwareOptimizer.Core.Common;

/// <summary>
/// Resultado de uma operação que pode falhar, sem recorrer a exceções para o
/// fluxo de validação. Mantém a lista de erros legível para a UI e a auditoria.
/// </summary>
public sealed class Resultado
{
    private Resultado(bool sucesso, IReadOnlyList<string> erros)
    {
        Sucesso = sucesso;
        Erros = erros;
    }

    public bool Sucesso { get; }

    public bool Falha => !Sucesso;

    public IReadOnlyList<string> Erros { get; }

    public string MensagemErro => string.Join(" | ", Erros);

    public static Resultado Ok() => new(true, Array.Empty<string>());

    public static Resultado Falhar(params string[] erros) =>
        new(false, erros.Length == 0 ? new[] { "Falha não especificada." } : erros);

    public static Resultado Falhar(IReadOnlyList<string> erros) => new(false, erros);
}

/// <summary>Variante de <see cref="Resultado"/> que carrega um valor em caso de sucesso.</summary>
public sealed class Resultado<T>
{
    private Resultado(bool sucesso, T? valor, IReadOnlyList<string> erros)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erros = erros;
    }

    public bool Sucesso { get; }

    public bool Falha => !Sucesso;

    public T? Valor { get; }

    public IReadOnlyList<string> Erros { get; }

    public string MensagemErro => string.Join(" | ", Erros);

    public T ValorObrigatorio => Sucesso && Valor is not null
        ? Valor
        : throw new InvalidOperationException("Resultado sem valor: " + MensagemErro);

    public static Resultado<T> Ok(T valor) => new(true, valor, Array.Empty<string>());

    public static Resultado<T> Falhar(params string[] erros) =>
        new(false, default, erros.Length == 0 ? new[] { "Falha não especificada." } : erros);

    public static Resultado<T> Falhar(IReadOnlyList<string> erros) => new(false, default, erros);
}
````

### `src/HardwareOptimizer.Core/Consent/AvaliadorConsentimento.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Core.Consent;

/// <summary>
/// Aplica as regras do diálogo de consentimento: o botão "Confirmar alteração"
/// só é válido com todos os checkboxes obrigatórios marcados e a confirmação
/// final acionada. Produz o registro de auditoria correspondente.
/// </summary>
public sealed class AvaliadorConsentimento
{
    private readonly TermoConsentimento _termo;
    private readonly ILogger _log;

    public AvaliadorConsentimento(TermoConsentimento? termo = null, ILogger? logger = null)
    {
        _termo = termo ?? TermoConsentimento.Padrao();
        _log = logger ?? NullLogger.Instance;
    }

    public TermoConsentimento Termo => _termo;

    /// <summary>
    /// Regra de habilitação do botão "Go": todos os checkboxes obrigatórios marcados.
    /// </summary>
    public bool PodeHabilitarConfirmacao(IEnumerable<string> checkboxesMarcados)
    {
        ArgumentNullException.ThrowIfNull(checkboxesMarcados);

        var obrigatorios = _termo.CheckboxesObrigatorios;
        if (obrigatorios.Count == 0)
        {
            // Termo sem aceites obrigatórios não habilita a confirmação (postura conservadora).
            return false;
        }

        var marcados = new HashSet<string>(checkboxesMarcados, StringComparer.OrdinalIgnoreCase);
        return obrigatorios.All(marcados.Contains);
    }

    /// <summary>
    /// Avalia a resposta completa. Em caso de sucesso, devolve o registro de
    /// auditoria pronto para persistir. Não muta o perfil; o chamador decide
    /// marcar <see cref="Perfil.ConsentimentoRegistrado"/>.
    /// </summary>
    public Resultado<RegistroConsentimento> Avaliar(
        RespostaConsentimento resposta, Perfil perfil, string versaoCatalogo)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        ArgumentNullException.ThrowIfNull(perfil);

        if (!PodeHabilitarConfirmacao(resposta.CheckboxesMarcados))
        {
            _log.LogWarning(
                "Consentimento RECUSADO para o perfil '{Perfil}': checkboxes obrigatórios não marcados.",
                perfil.Nome);
            return Resultado<RegistroConsentimento>.Falhar(
                "Consentimento incompleto: é necessário marcar todos os checkboxes obrigatórios.");
        }

        if (!resposta.ConfirmacaoFinal)
        {
            _log.LogWarning(
                "Consentimento RECUSADO para o perfil '{Perfil}': confirmação final não acionada.", perfil.Nome);
            return Resultado<RegistroConsentimento>.Falhar(
                "Confirmação final não acionada: o usuário não confirmou a alteração.");
        }

        var registro = new RegistroConsentimento
        {
            NomePerfil = perfil.Nome,
            VersaoCatalogo = versaoCatalogo,
            CheckboxesMarcados = resposta.CheckboxesMarcados.ToList(),
            ValoresEscolhidos = AchatarValores(perfil),
        };

        _log.LogInformation(
            "Consentimento CONCEDIDO para o perfil '{Perfil}' (catálogo {Versao}).",
            perfil.Nome, versaoCatalogo);
        return Resultado<RegistroConsentimento>.Ok(registro);
    }

    private static IReadOnlyList<string> AchatarValores(Perfil perfil)
    {
        var linhas = new List<string>();
        foreach (var selecao in perfil.Selecoes)
        {
            if (selecao.Parametros.Count == 0)
            {
                linhas.Add(selecao.AcaoId);
                continue;
            }

            foreach (var (nome, valor) in selecao.Parametros)
            {
                linhas.Add($"{selecao.AcaoId}.{nome} = {valor}");
            }
        }

        return linhas;
    }
}
````

### `src/HardwareOptimizer.Core/Consent/RegistroConsentimento.cs`

````csharp
namespace HardwareOptimizer.Core.Consent;

/// <summary>
/// Resposta do usuário ao diálogo de consentimento: quais checkboxes marcou e se
/// acionou a confirmação final (botão "Confirmar alteração / Go").
/// </summary>
public sealed class RespostaConsentimento
{
    public RespostaConsentimento(IEnumerable<string> checkboxesMarcados, bool confirmacaoFinal)
    {
        CheckboxesMarcados = new HashSet<string>(checkboxesMarcados, StringComparer.OrdinalIgnoreCase);
        ConfirmacaoFinal = confirmacaoFinal;
    }

    public IReadOnlySet<string> CheckboxesMarcados { get; }

    /// <summary>Usuário acionou o botão "Confirmar alteração".</summary>
    public bool ConfirmacaoFinal { get; }
}

/// <summary>
/// Registro de auditoria do consentimento, para rastreabilidade. Guarda
/// data/hora, perfil, valores escolhidos e a versão do catálogo.
/// </summary>
public sealed record RegistroConsentimento
{
    public required string NomePerfil { get; init; }

    public required string VersaoCatalogo { get; init; }

    public DateTimeOffset RegistradoEm { get; init; } = DateTimeOffset.UtcNow;

    public required IReadOnlyList<string> CheckboxesMarcados { get; init; }

    /// <summary>Pares "AcaoId.parametro = valor" escolhidos pelo usuário.</summary>
    public required IReadOnlyList<string> ValoresEscolhidos { get; init; }
}
````

### `src/HardwareOptimizer.Core/Consent/TermoConsentimento.cs`

````csharp
namespace HardwareOptimizer.Core.Consent;

/// <summary>Item de aceite obrigatório no diálogo de consentimento.</summary>
public sealed record Checkbox(string Id, string Texto, bool Obrigatorio = true);

/// <summary>
/// Termo de consentimento exibido ao salvar/aplicar um perfil customizado.
/// Reproduz <c>fluxo_consentimento_customizado</c> do documento: aviso de
/// responsabilidade + dois checkboxes obrigatórios.
/// </summary>
public sealed class TermoConsentimento
{
    public const string IdAceiteRiscos = "aceite_riscos";
    public const string IdDesejoProsseguir = "desejo_prosseguir";

    public TermoConsentimento(string titulo, IReadOnlyList<string> corpoAviso, IReadOnlyList<Checkbox> checkboxes)
    {
        Titulo = titulo;
        CorpoAviso = corpoAviso;
        Checkboxes = checkboxes;
    }

    public string Titulo { get; }

    public IReadOnlyList<string> CorpoAviso { get; }

    public IReadOnlyList<Checkbox> Checkboxes { get; }

    public IReadOnlyList<string> CheckboxesObrigatorios =>
        Checkboxes.Where(c => c.Obrigatorio).Select(c => c.Id).ToList();

    /// <summary>Termo padrão, com os textos definidos no documento de arquitetura.</summary>
    public static TermoConsentimento Padrao() => new(
        titulo: "Aviso de responsabilidade - parametrização manual",
        corpoAviso: new[]
        {
            "Você está definindo valores manualmente, fora do perfil seguro recomendado pelo sistema.",
            "Esses valores NÃO foram validados pelo sistema e podem causar instabilidade, travamentos, "
                + "tela azul, perda de dados, superaquecimento ou, em casos extremos, dano ao hardware.",
            "Parâmetros fora da faixa segura podem afetar garantia e estabilidade.",
            "A responsabilidade pela escolha dos valores e configurações é inteiramente sua.",
            "Recomendamos manter o backup gerado pelo sistema antes de prosseguir e validar com "
                + "testes de estresse após aplicar.",
        },
        checkboxes: new[]
        {
            new Checkbox(IdAceiteRiscos, "Li e aceito os riscos de parametrizar as configurações manualmente."),
            new Checkbox(IdDesejoProsseguir, "Desejo prosseguir com as modificações."),
        });
}
````

### `src/HardwareOptimizer.Core/Contracts/Inventario.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Inventário normalizado do equipamento (contrato "inventario").
/// É a "impressão digital" da máquina: campos sensíveis ficam agrupados em
/// <see cref="Identificadores"/> para que a camada de privacidade os trate
/// antes de qualquer envio ao cérebro na nuvem.
/// </summary>
public sealed record Inventario
{
    public required PlacaMae Placa { get; init; }

    public required Processador Cpu { get; init; }

    public IReadOnlyList<ModuloMemoria> Memoria { get; init; } = Array.Empty<ModuloMemoria>();

    public IReadOnlyList<PlacaVideo> Gpu { get; init; } = Array.Empty<PlacaVideo>();

    public required SistemaOperacionalInfo SistemaOperacional { get; init; }

    public IReadOnlyList<InterfaceRede> Rede { get; init; } = Array.Empty<InterfaceRede>();

    /// <summary>Identificadores sensíveis. Nulo após a sanitização.</summary>
    public IdentificadoresSensiveis? Identificadores { get; init; }

    public DateTimeOffset ColetadoEm { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PlacaMae
{
    public required string Fabricante { get; init; }

    public required string Modelo { get; init; }

    public string? VersaoBios { get; init; }

    public string? DataBios { get; init; }

    /// <summary>UEFI ou Legacy.</summary>
    public string? Modo { get; init; }

    public bool? SecureBoot { get; init; }
}

public sealed record Processador
{
    public required string Nome { get; init; }

    public int? Nucleos { get; init; }

    public int? Threads { get; init; }

    public double? TempIdleC { get; init; }
}

public sealed record ModuloMemoria
{
    public int? TamanhoGb { get; init; }

    public int? VelocidadeMhz { get; init; }

    public string? Fabricante { get; init; }
}

public sealed record PlacaVideo
{
    public required string Nome { get; init; }

    public double? TempIdleC { get; init; }

    public string? VersaoDriver { get; init; }
}

public sealed record SistemaOperacionalInfo
{
    public required SistemaOperacionalTipo Tipo { get; init; }

    public string? Nome { get; init; }

    public string? Versao { get; init; }

    public string? Arquitetura { get; init; }
}

public sealed record InterfaceRede
{
    public required string Nome { get; init; }

    public string? Tipo { get; init; }

    /// <summary>Endereço MAC: sensível. Nulo/hasheado após a sanitização.</summary>
    public string? EnderecoMac { get; init; }
}

/// <summary>
/// Campos que identificam unicamente o equipamento ou o usuário.
/// Correspondem a <c>campos_sensiveis</c> do documento.
/// </summary>
public sealed record IdentificadoresSensiveis
{
    public string? NumeroSerie { get; init; }

    public string? UuidPlaca { get; init; }

    public string? NomeMaquina { get; init; }

    public string? NomeUsuario { get; init; }

    public string? ChaveProdutoWindows { get; init; }
}
````

### `src/HardwareOptimizer.Core/Contracts/LeituraSensores.cs`

````csharp
namespace HardwareOptimizer.Core.Contracts;

/// <summary>Grandeza de um sensor de hardware.</summary>
public enum TipoSensor
{
    Temperatura = 0,
    Clock = 1,
    Voltagem = 2,
    Fan = 3,
    Potencia = 4,
    Outro = 5,
}

/// <summary>Leitura de um único sensor.</summary>
public sealed record Sensor
{
    public required string Nome { get; init; }

    public required TipoSensor Tipo { get; init; }

    public required double Valor { get; init; }

    public required string Unidade { get; init; }
}

/// <summary>
/// Leitura instantânea dos sensores (temperatura, clock, voltagem, rotação de
/// fan e consumo), em tempo real. Saída do módulo de sensores.
/// </summary>
public sealed record LeituraSensores
{
    public DateTimeOffset Momento { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<Sensor> Sensores { get; init; } = Array.Empty<Sensor>();

    public IEnumerable<Sensor> PorTipo(TipoSensor tipo) => Sensores.Where(s => s.Tipo == tipo);

    /// <summary>Maior temperatura lida (°C), ou nulo se não houver sensor de temperatura.</summary>
    public double? TemperaturaMaxC
    {
        get
        {
            var temperaturas = PorTipo(TipoSensor.Temperatura).Select(s => s.Valor).ToList();
            return temperaturas.Count == 0 ? null : temperaturas.Max();
        }
    }
}
````

### `src/HardwareOptimizer.Core/Contracts/Recomendacao.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Recomendação proposta pelo cérebro (contrato "recomendacao"). O LLM apenas
/// seleciona e prioriza ações do catálogo; nunca gera o comando interno.
/// </summary>
public sealed record Recomendacao
{
    public required string Categoria { get; init; }

    /// <summary>Identificador da ação no catálogo whitelisted que materializa esta recomendação.</summary>
    public string? AcaoId { get; init; }

    public string? ValorAtual { get; init; }

    public string? ValorRecomendado { get; init; }

    public required string Acao { get; init; }

    public required string Justificativa { get; init; }

    public NivelRisco Risco { get; init; }

    public string? GanhoEsperado { get; init; }

    /// <summary>Fonte sempre visível (exigência do documento para verificação com fabricante).</summary>
    public string? Fonte { get; init; }

    public IReadOnlyList<string> PassosUsuario { get; init; } = Array.Empty<string>();
}
````

### `src/HardwareOptimizer.Core/Contracts/ResultadoValidacao.cs`

````csharp
namespace HardwareOptimizer.Core.Contracts;

/// <summary>
/// Resultado de um teste de estresse comparando antes/depois (contrato
/// "resultado_validacao"). Alimenta a decisão de manter ou reverter a categoria.
/// </summary>
public sealed record ResultadoValidacao
{
    public required string Categoria { get; init; }

    public required string Ferramenta { get; init; }

    public MedicaoTeste? Antes { get; init; }

    public MedicaoTeste? Depois { get; init; }

    public bool Regressao { get; init; }

    public IReadOnlyList<string> Erros { get; init; } = Array.Empty<string>();

    /// <summary>Ex.: "Totalmente validado", "Validado com ressalvas", "Reprovado".</summary>
    public required string Estabilidade { get; init; }
}

public sealed record MedicaoTeste
{
    public double? Score { get; init; }

    public double? TempMaxC { get; init; }

    public double? ClockMhz { get; init; }

    public double? ConsumoW { get; init; }
}
````

### `src/HardwareOptimizer.Core/Privacy/ResultadoSanitizacao.cs`

````csharp
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Privacy;

/// <summary>Ação aplicada a um campo durante a sanitização.</summary>
public enum AcaoSanitizacao
{
    /// <summary>Campo removido por completo (dado de identificação pessoal).</summary>
    Removido = 0,

    /// <summary>Substituído por hash, preservando correlação sem expor o valor.</summary>
    Hasheado = 1,
}

/// <summary>Registro de um campo sensível tratado, para o log do que foi enviado.</summary>
public sealed record CampoSanitizado(string Campo, AcaoSanitizacao Acao);

/// <summary>
/// Resultado do pipeline de sanitização: a versão "segura para nuvem" do
/// inventário e o relatório do que foi alterado.
/// </summary>
public sealed class ResultadoSanitizacao
{
    public ResultadoSanitizacao(Inventario inventarioSeguro, IReadOnlyList<CampoSanitizado> camposAlterados)
    {
        InventarioSeguro = inventarioSeguro;
        CamposAlterados = camposAlterados;
    }

    public Inventario InventarioSeguro { get; }

    public IReadOnlyList<CampoSanitizado> CamposAlterados { get; }
}
````

### `src/HardwareOptimizer.Core/Privacy/Sanitizador.cs`

````csharp
using System.Security.Cryptography;
using System.Text;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Core.Privacy;

/// <summary>
/// Pipeline de sanitização entre o coletor e o cérebro. Gera uma versão do
/// inventário "segura para nuvem": dados de baixo risco (modelo de placa, versão
/// de BIOS) são preservados; identificadores únicos correlacionáveis são
/// hasheados; dados de identificação pessoal são removidos.
/// </summary>
public sealed class Sanitizador
{
    private readonly string _sal;
    private readonly ILogger _log;

    /// <param name="sal">
    /// Sal aplicado ao hash. Por padrão, um sal por execução, de modo que os
    /// hashes não sejam correlacionáveis entre máquinas/sessões distintas.
    /// </param>
    /// <param name="logger">Logger opcional para registrar o resumo da sanitização.</param>
    public Sanitizador(string? sal = null, ILogger? logger = null)
    {
        _sal = sal ?? Guid.NewGuid().ToString("N");
        _log = logger ?? NullLogger.Instance;
    }

    public ResultadoSanitizacao Sanitizar(Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        var alteracoes = new List<CampoSanitizado>();

        // Identificadores correlacionáveis (serial, uuid) são preservados apenas
        // como hash; dados de identificação pessoal (nomes, chave) são removidos.
        IdentificadoresSensiveis? identificadoresSeguros = null;
        if (inventario.Identificadores is { } ident)
        {
            identificadoresSeguros = new IdentificadoresSensiveis
            {
                NumeroSerie = HashearCampo("identificadores.numero_serie", ident.NumeroSerie, alteracoes),
                UuidPlaca = HashearCampo("identificadores.uuid_placa", ident.UuidPlaca, alteracoes),
                NomeMaquina = RemoverCampo("identificadores.nome_maquina", ident.NomeMaquina, alteracoes),
                NomeUsuario = RemoverCampo("identificadores.nome_usuario", ident.NomeUsuario, alteracoes),
                ChaveProdutoWindows = RemoverCampo(
                    "identificadores.chave_produto_windows", ident.ChaveProdutoWindows, alteracoes),
            };

            // Nada a preservar? Não emite o bloco.
            if (identificadoresSeguros is
                { NumeroSerie: null, UuidPlaca: null, NomeMaquina: null, NomeUsuario: null, ChaveProdutoWindows: null })
            {
                identificadoresSeguros = null;
            }
        }

        // MAC de cada interface é hasheado.
        var redeSegura = new List<InterfaceRede>(inventario.Rede.Count);
        for (var i = 0; i < inventario.Rede.Count; i++)
        {
            var nic = inventario.Rede[i];
            if (!string.IsNullOrWhiteSpace(nic.EnderecoMac))
            {
                alteracoes.Add(new CampoSanitizado($"rede[{i}].endereco_mac", AcaoSanitizacao.Hasheado));
                redeSegura.Add(nic with { EnderecoMac = Hashear(nic.EnderecoMac) });
            }
            else
            {
                redeSegura.Add(nic);
            }
        }

        var inventarioSeguro = inventario with
        {
            // Identificadores correlacionáveis ficam como hash; PII é removida.
            Identificadores = identificadoresSeguros,
            Rede = redeSegura,
        };

        _log.LogInformation(
            "Sanitização concluída: {Qtd} campo(s) sensível(is) tratado(s) antes do envio à nuvem.",
            alteracoes.Count);

        return new ResultadoSanitizacao(inventarioSeguro, alteracoes);
    }

    private string? HashearCampo(string campo, string? valor, List<CampoSanitizado> alteracoes)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        alteracoes.Add(new CampoSanitizado(campo, AcaoSanitizacao.Hasheado));
        return Hashear(valor);
    }

    private static string? RemoverCampo(string campo, string? valor, List<CampoSanitizado> alteracoes)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            alteracoes.Add(new CampoSanitizado(campo, AcaoSanitizacao.Removido));
        }

        return null;
    }

    /// <summary>Hash SHA-256 salgado e truncado, suficiente para correlação sem revelar o valor.</summary>
    public string Hashear(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var bytes = Encoding.UTF8.GetBytes(_sal + ":" + valor);
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
````

### `src/HardwareOptimizer.Core/Profiles/ConstrutorPerfil.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Core.Profiles;

/// <summary>
/// Constrói e valida perfis contra o catálogo. Implementa a regra
/// <c>validacao_ao_salvar</c>: bloqueia limite absoluto, marca riscos assumidos
/// e sinaliza quando o fluxo de consentimento é obrigatório.
/// </summary>
public sealed class ConstrutorPerfil
{
    private readonly CatalogoAcoes _catalogo;
    private readonly ValidadorAcao _validador;
    private readonly ILogger _log;

    public ConstrutorPerfil(CatalogoAcoes catalogo, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        _catalogo = catalogo;
        _validador = new ValidadorAcao(catalogo);
        _log = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Cria o perfil seguro (padrão): cada ação recebe o valor padrão seguro de
    /// seus parâmetros. Não exige consentimento além da aprovação por categoria.
    /// </summary>
    public ResultadoConstrucaoPerfil CriarPerfilSeguro(string nome, IEnumerable<string> acaoIds)
    {
        ArgumentNullException.ThrowIfNull(acaoIds);

        var selecoes = new List<SelecaoAcao>();
        var bloqueios = new List<string>();

        foreach (var id in acaoIds)
        {
            var acao = _catalogo.Obter(id);
            if (acao is null)
            {
                bloqueios.Add($"Ação '{id}' não consta no catálogo whitelisted.");
                continue;
            }

            var parametros = acao.Parametros.ToDictionary(
                p => p.Nome, p => p.ValorPadraoSeguro, StringComparer.OrdinalIgnoreCase);

            selecoes.Add(new SelecaoAcao { AcaoId = id, Parametros = parametros });
        }

        return Montar(nome, TipoPerfil.Seguro, "sistema", selecoes, bloqueios);
    }

    /// <summary>
    /// Cria um perfil customizado a partir das seleções do usuário. Sempre exige
    /// o fluxo de consentimento ao salvar/aplicar.
    /// </summary>
    public ResultadoConstrucaoPerfil CriarPerfilCustomizado(
        string nome, string autor, IEnumerable<SelecaoAcao> selecoes)
    {
        ArgumentNullException.ThrowIfNull(selecoes);
        return Montar(nome, TipoPerfil.Customizado, autor, selecoes.ToList(), new List<string>());
    }

    private ResultadoConstrucaoPerfil Montar(
        string nome,
        TipoPerfil tipo,
        string autor,
        IReadOnlyList<SelecaoAcao> selecoes,
        List<string> bloqueios)
    {
        var validacoes = new List<ResultadoValidacaoAcao>();
        var riscos = new List<RiscoAssumido>();

        // Perfil customizado sempre exige consentimento explícito ao salvar/aplicar.
        var exigeConsentimento = tipo == TipoPerfil.Customizado;

        foreach (var selecao in selecoes)
        {
            var validacao = _validador.Validar(selecao.AcaoId, selecao.Parametros, tipo);
            validacoes.Add(validacao);

            if (!validacao.Aplicavel)
            {
                bloqueios.AddRange(validacao.ComoResultado().Erros);
            }

            if (validacao.ExigeConsentimento)
            {
                exigeConsentimento = true;
                foreach (var p in validacao.Parametros.Where(p => p.ExigeConsentimento))
                {
                    riscos.Add(new RiscoAssumido(selecao.AcaoId, p.Parametro, p.Valor, p.Mensagem));
                }
            }
        }

        var sucesso = bloqueios.Count == 0;

        if (!sucesso)
        {
            _log.LogWarning(
                "Perfil '{Nome}' ({Tipo}) NÃO salvo: {Qtd} bloqueio(s) -> {Bloqueios}",
                nome, tipo, bloqueios.Count, string.Join(" | ", bloqueios));
        }
        else
        {
            _log.LogInformation(
                "Perfil '{Nome}' ({Tipo}) válido. Risco assumido em {Riscos} parâmetro(s); exige consentimento={Consent}.",
                nome, tipo, riscos.Count, exigeConsentimento);
        }

        Perfil? perfil = sucesso
            ? new Perfil
            {
                Nome = nome,
                Tipo = tipo,
                Autor = autor,
                Selecoes = selecoes,
                ConsentimentoRegistrado = false,
            }
            : null;

        return new ResultadoConstrucaoPerfil(sucesso, perfil, exigeConsentimento, validacoes, bloqueios, riscos);
    }
}
````

### `src/HardwareOptimizer.Core/Profiles/Perfil.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Profiles;

/// <summary>Uma ação escolhida para um perfil, com os valores de seus parâmetros.</summary>
public sealed record SelecaoAcao
{
    public required string AcaoId { get; init; }

    public IReadOnlyDictionary<string, string> Parametros { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Perfil de parametrização. O perfil seguro usa sempre a faixa segura de cada
/// ação; o customizado carrega valores definidos pelo usuário e só é válido
/// após o fluxo de consentimento (<see cref="ConsentimentoRegistrado"/>).
/// </summary>
public sealed record Perfil
{
    public required string Nome { get; init; }

    public required TipoPerfil Tipo { get; init; }

    public DateTimeOffset DataCriacao { get; init; } = DateTimeOffset.UtcNow;

    public string Autor { get; init; } = "sistema";

    /// <summary>Para perfis customizados, indica se o consentimento já foi registrado.</summary>
    public bool ConsentimentoRegistrado { get; init; }

    public required IReadOnlyList<SelecaoAcao> Selecoes { get; init; }

    public bool Customizado => Tipo == TipoPerfil.Customizado;

    /// <summary>Customizado só pode ser aplicado após consentimento registrado.</summary>
    public bool PodeAplicar => !Customizado || ConsentimentoRegistrado;
}
````

### `src/HardwareOptimizer.Core/Profiles/ResultadoConstrucaoPerfil.cs`

````csharp
using HardwareOptimizer.Core.Catalog;

namespace HardwareOptimizer.Core.Profiles;

/// <summary>
/// Desfecho da construção/validação de um perfil. Distingue bloqueios (impedem
/// salvar) de riscos assumidos (permitidos mediante consentimento).
/// </summary>
public sealed class ResultadoConstrucaoPerfil
{
    public ResultadoConstrucaoPerfil(
        bool sucesso,
        Perfil? perfil,
        bool exigeConsentimento,
        IReadOnlyList<ResultadoValidacaoAcao> validacoes,
        IReadOnlyList<string> bloqueios,
        IReadOnlyList<RiscoAssumido> riscosAssumidos)
    {
        Sucesso = sucesso;
        Perfil = perfil;
        ExigeConsentimento = exigeConsentimento;
        Validacoes = validacoes;
        Bloqueios = bloqueios;
        RiscosAssumidos = riscosAssumidos;
    }

    /// <summary>Verdadeiro se o perfil é válido (sem bloqueios). Pode ainda exigir consentimento.</summary>
    public bool Sucesso { get; }

    public Perfil? Perfil { get; }

    /// <summary>Exige o fluxo de consentimento antes de persistir/aplicar.</summary>
    public bool ExigeConsentimento { get; }

    public IReadOnlyList<ResultadoValidacaoAcao> Validacoes { get; }

    /// <summary>Motivos que impedem salvar (limite absoluto, ação fora do catálogo, valor inválido).</summary>
    public IReadOnlyList<string> Bloqueios { get; }

    /// <summary>Parâmetros fora da faixa segura aceitos sob responsabilidade do usuário.</summary>
    public IReadOnlyList<RiscoAssumido> RiscosAssumidos { get; }
}

/// <summary>Um parâmetro marcado como "risco assumido pelo usuário".</summary>
public sealed record RiscoAssumido(string AcaoId, string Parametro, string Valor, string Detalhe);
````

### `src/HardwareOptimizer.Core/Reporting/CalculadoraScore.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Reporting;

/// <summary>
/// Calcula as notas 0-100 por domínio a partir do inventário, dos resultados de
/// validação e dos domínios efetivamente otimizados. As heurísticas (v1) são
/// transparentes: cada critério contribui pontos explicáveis, somados e
/// limitados a [0, 100]. É lógica pura e determinística.
/// </summary>
public sealed class CalculadoraScore
{
    // Pesos dos domínios primários na nota final. Estabilidade pesa mais,
    // refletindo a ordem ESTABILIDADE > ... > DESEMPENHO da filosofia.
    private static readonly IReadOnlyDictionary<Dominio, double> Pesos = new Dictionary<Dominio, double>
    {
        [Dominio.Estabilidade] = 2.0,
        [Dominio.Cpu] = 1.5,
        [Dominio.Ram] = 1.2,
        [Dominio.Gpu] = 1.0,
        [Dominio.Bios] = 1.0,
        [Dominio.Windows] = 1.0,
    };

    public ResultadoScore Calcular(
        Inventario inventario,
        IReadOnlyList<ResultadoValidacao> validacoes,
        ISet<Dominio> dominiosOtimizados)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        ArgumentNullException.ThrowIfNull(validacoes);
        ArgumentNullException.ThrowIfNull(dominiosOtimizados);

        var bios = Bios(inventario);
        var cpu = Cpu(inventario);
        var gpu = Gpu(inventario);
        var ram = Ram(inventario);
        var windows = Windows(inventario, dominiosOtimizados);
        var estabilidade = Estabilidade(validacoes);
        var hardware = Hardware(cpu, gpu, ram, bios);

        var scores = new[] { hardware, bios, cpu, gpu, ram, windows, estabilidade };
        return new ResultadoScore { Scores = scores, NotaFinal = NotaFinal(scores) };
    }

    private static Score Bios(Inventario inv)
    {
        var criterios = new List<string>();
        var v = 0;

        switch (inv.Placa.Modo)
        {
            case "UEFI":
                v += 50;
                criterios.Add("Modo UEFI (+50)");
                break;
            case "Legacy":
                v += 20;
                criterios.Add("Modo Legacy (+20)");
                break;
            default:
                v += 25;
                criterios.Add("Modo de firmware desconhecido (+25)");
                break;
        }

        v += inv.Placa.SecureBoot switch
        {
            true => Pontuar(criterios, 30, "Secure Boot ativo"),
            false => Pontuar(criterios, 5, "Secure Boot inativo"),
            null => Pontuar(criterios, 15, "Secure Boot desconhecido"),
        };

        v += string.IsNullOrWhiteSpace(inv.Placa.VersaoBios)
            ? Pontuar(criterios, 0, "Versão de BIOS desconhecida")
            : Pontuar(criterios, 20, "Versão de BIOS identificada");

        return Montar(Dominio.Bios, v, criterios);
    }

    private static Score Cpu(Inventario inv)
    {
        var criterios = new List<string> { "Base (50)" };
        var v = 50;

        v += inv.Cpu.TempIdleC switch
        {
            null => Pontuar(criterios, 12, "Temperatura de repouso indisponível"),
            <= 45 => Pontuar(criterios, 25, "Temperatura de repouso <= 45 C"),
            <= 60 => Pontuar(criterios, 15, "Temperatura de repouso <= 60 C"),
            <= 75 => Pontuar(criterios, 8, "Temperatura de repouso <= 75 C"),
            _ => Pontuar(criterios, 2, "Temperatura de repouso elevada"),
        };

        v += inv.Cpu.Nucleos switch
        {
            null => Pontuar(criterios, 10, "Nº de núcleos indisponível"),
            >= 8 => Pontuar(criterios, 25, ">= 8 núcleos"),
            >= 6 => Pontuar(criterios, 18, ">= 6 núcleos"),
            >= 4 => Pontuar(criterios, 12, ">= 4 núcleos"),
            >= 2 => Pontuar(criterios, 6, ">= 2 núcleos"),
            _ => Pontuar(criterios, 3, "1 núcleo"),
        };

        return Montar(Dominio.Cpu, v, criterios);
    }

    private static Score Gpu(Inventario inv)
    {
        var criterios = new List<string>();
        if (inv.Gpu.Count == 0)
        {
            criterios.Add("Sem GPU dedicada detectada (gráficos integrados) (60)");
            return Montar(Dominio.Gpu, 60, criterios);
        }

        var v = 70;
        criterios.Add("GPU dedicada presente (70)");
        var principal = inv.Gpu[0];

        v += principal.TempIdleC switch
        {
            null => Pontuar(criterios, 10, "Temperatura de GPU indisponível"),
            <= 45 => Pontuar(criterios, 20, "Temperatura de GPU <= 45 C"),
            <= 60 => Pontuar(criterios, 12, "Temperatura de GPU <= 60 C"),
            <= 75 => Pontuar(criterios, 6, "Temperatura de GPU <= 75 C"),
            _ => Pontuar(criterios, 2, "Temperatura de GPU elevada"),
        };

        if (!string.IsNullOrWhiteSpace(principal.VersaoDriver))
        {
            v += Pontuar(criterios, 10, "Driver de vídeo identificado");
        }

        return Montar(Dominio.Gpu, v, criterios);
    }

    private static Score Ram(Inventario inv)
    {
        var criterios = new List<string> { "Base (15)" };
        var v = 15;

        var totalGb = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
        v += totalGb switch
        {
            >= 32 => Pontuar(criterios, 50, $"{totalGb} GB totais"),
            >= 16 => Pontuar(criterios, 40, $"{totalGb} GB totais"),
            >= 8 => Pontuar(criterios, 25, $"{totalGb} GB totais"),
            >= 4 => Pontuar(criterios, 12, $"{totalGb} GB totais"),
            _ => Pontuar(criterios, 5, $"{totalGb} GB totais"),
        };

        var velocidade = inv.Memoria.Count == 0 ? 0 : inv.Memoria.Max(m => m.VelocidadeMhz ?? 0);
        v += velocidade switch
        {
            0 => Pontuar(criterios, 18, "Velocidade indisponível"),
            >= 3600 => Pontuar(criterios, 35, $"{velocidade} MHz"),
            >= 3200 => Pontuar(criterios, 30, $"{velocidade} MHz"),
            >= 2666 => Pontuar(criterios, 20, $"{velocidade} MHz"),
            >= 2133 => Pontuar(criterios, 12, $"{velocidade} MHz"),
            _ => Pontuar(criterios, 8, $"{velocidade} MHz"),
        };

        return Montar(Dominio.Ram, v, criterios);
    }

    private static Score Windows(Inventario inv, ISet<Dominio> dominiosOtimizados)
    {
        var criterios = new List<string> { "Base (65)" };
        var v = 65;

        var arquitetura = inv.SistemaOperacional.Arquitetura;
        v += arquitetura is not null && arquitetura.Contains("64", StringComparison.OrdinalIgnoreCase)
            ? Pontuar(criterios, 15, "Sistema 64 bits")
            : Pontuar(criterios, 5, "Arquitetura não confirmada como 64 bits");

        v += dominiosOtimizados.Contains(Dominio.Windows)
            ? Pontuar(criterios, 20, "Otimizações de sistema aplicadas")
            : Pontuar(criterios, 0, "Sem otimizações de sistema aplicadas");

        return Montar(Dominio.Windows, v, criterios);
    }

    private static Score Estabilidade(IReadOnlyList<ResultadoValidacao> validacoes)
    {
        if (validacoes.Count == 0)
        {
            return Montar(Dominio.Estabilidade, 70, new[] { "Nenhum teste de estresse executado (70)" });
        }

        if (validacoes.Any(v => v.Regressao))
        {
            return Montar(Dominio.Estabilidade, 30, new[] { "Regressão detectada em ao menos uma categoria (30)" });
        }

        var todasValidadas = validacoes.All(
            v => string.Equals(v.Estabilidade, "Totalmente validado", StringComparison.OrdinalIgnoreCase));

        return todasValidadas
            ? Montar(Dominio.Estabilidade, 100, new[] { "Todas as categorias totalmente validadas (100)" })
            : Montar(Dominio.Estabilidade, 75, new[] { "Validado com ressalvas (75)" });
    }

    private static Score Hardware(Score cpu, Score gpu, Score ram, Score bios)
    {
        var media = (int)Math.Round((cpu.Valor + gpu.Valor + ram.Valor + bios.Valor) / 4.0);
        var criterios = new[]
        {
            $"Média de CPU ({cpu.Valor}), GPU ({gpu.Valor}), RAM ({ram.Valor}) e BIOS ({bios.Valor})",
        };
        return Montar(Dominio.Hardware, media, criterios);
    }

    private static int NotaFinal(IReadOnlyList<Score> scores)
    {
        double soma = 0;
        double pesoTotal = 0;
        foreach (var score in scores)
        {
            if (Pesos.TryGetValue(score.Dominio, out var peso))
            {
                soma += score.Valor * peso;
                pesoTotal += peso;
            }
        }

        return pesoTotal == 0 ? 0 : (int)Math.Round(soma / pesoTotal);
    }

    /// <summary>Registra o critério e devolve os pontos, para uso fluente nas somas.</summary>
    private static int Pontuar(List<string> criterios, int pontos, string descricao)
    {
        criterios.Add(string.Create(
            CultureInfo.InvariantCulture, $"{descricao} (+{pontos})"));
        return pontos;
    }

    private static Score Montar(Dominio dominio, int valor, IReadOnlyList<string> criterios)
    {
        var limitado = Math.Clamp(valor, 0, 100);
        return new Score
        {
            Dominio = dominio,
            Valor = limitado,
            Classificacao = Score.Classificar(limitado),
            Criterios = criterios,
        };
    }
}
````

### `src/HardwareOptimizer.Core/Reporting/GeradorRelatorio.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Reporting;

/// <summary>
/// Monta o relatório executivo final: calcula as notas, redige o resumo e
/// consolida destaques e o antes/depois das alterações. Lógica pura.
/// </summary>
public sealed class GeradorRelatorio
{
    private readonly CalculadoraScore _calculadora;

    public GeradorRelatorio(CalculadoraScore? calculadora = null)
    {
        _calculadora = calculadora ?? new CalculadoraScore();
    }

    public RelatorioExecutivo Gerar(
        Inventario inventario,
        IReadOnlyList<ResultadoValidacao> validacoes,
        IReadOnlyList<AlteracaoResumo> alteracoes,
        ISet<Dominio> dominiosOtimizados)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        ArgumentNullException.ThrowIfNull(validacoes);
        ArgumentNullException.ThrowIfNull(alteracoes);
        ArgumentNullException.ThrowIfNull(dominiosOtimizados);

        var resultado = _calculadora.Calcular(inventario, validacoes, dominiosOtimizados);
        var regressao = validacoes.Any(v => v.Regressao);
        var classificacao = Score.Classificar(resultado.NotaFinal);

        var destaques = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"Nota final: {resultado.NotaFinal}/100 ({classificacao})"),
            string.Create(CultureInfo.InvariantCulture, $"{alteracoes.Count} alteração(ões) aplicada(s)"),
            regressao ? "Regressão detectada" : "Nenhuma regressão detectada",
        };

        var estabilidade = resultado.Obter(Dominio.Estabilidade);
        var resumo = string.Create(
            CultureInfo.InvariantCulture,
            $"Nota final {resultado.NotaFinal}/100 ({classificacao}). " +
            $"{alteracoes.Count} alteração(ões) aplicada(s); " +
            $"{(regressao ? "houve regressão" : "sem regressões")}. " +
            $"Estabilidade: {estabilidade?.Classificacao ?? "n/d"}.");

        return new RelatorioExecutivo
        {
            ResumoExecutivo = resumo,
            NotaFinal = resultado.NotaFinal,
            Classificacao = classificacao,
            Scores = resultado.Scores,
            Alteracoes = alteracoes,
            Destaques = destaques,
            RegressaoDetectada = regressao,
        };
    }
}
````

### `src/HardwareOptimizer.Core/Reporting/RelatorioExecutivo.cs`

````csharp
namespace HardwareOptimizer.Core.Reporting;

/// <summary>Resumo de uma alteração aplicada (antes/depois), neutro de plataforma.</summary>
public sealed record AlteracaoResumo(string Alvo, string? Antes, string? Depois);

/// <summary>Conjunto de notas por domínio + nota final consolidada.</summary>
public sealed record ResultadoScore
{
    public required IReadOnlyList<Score> Scores { get; init; }

    /// <summary>Nota final 0-100 (média ponderada dos domínios primários).</summary>
    public required int NotaFinal { get; init; }

    public Score? Obter(Dominio dominio) => Scores.FirstOrDefault(s => s.Dominio == dominio);
}

/// <summary>
/// Relatório executivo final: resumo, notas por domínio, nota final 0-100,
/// destaques e o antes/depois das alterações (contrato da Fase 10).
/// </summary>
public sealed record RelatorioExecutivo
{
    public DateTimeOffset GeradoEm { get; init; } = DateTimeOffset.UtcNow;

    public required string ResumoExecutivo { get; init; }

    public required int NotaFinal { get; init; }

    public required string Classificacao { get; init; }

    public required IReadOnlyList<Score> Scores { get; init; }

    public IReadOnlyList<AlteracaoResumo> Alteracoes { get; init; } = Array.Empty<AlteracaoResumo>();

    public IReadOnlyList<string> Destaques { get; init; } = Array.Empty<string>();

    public bool RegressaoDetectada { get; init; }
}
````

### `src/HardwareOptimizer.Core/Reporting/Score.cs`

````csharp
namespace HardwareOptimizer.Core.Reporting;

/// <summary>Domínios pontuados no relatório executivo (do documento, campo "scores").</summary>
public enum Dominio
{
    Hardware = 0,
    Bios = 1,
    Cpu = 2,
    Gpu = 3,
    Ram = 4,
    Windows = 5,
    Estabilidade = 6,
}

/// <summary>
/// Nota de um domínio (0-100), com classificação legível e os critérios que a
/// compuseram, para transparência do cálculo.
/// </summary>
public sealed record Score
{
    public required Dominio Dominio { get; init; }

    /// <summary>Valor de 0 a 100.</summary>
    public required int Valor { get; init; }

    public required string Classificacao { get; init; }

    public IReadOnlyList<string> Criterios { get; init; } = Array.Empty<string>();

    /// <summary>Classifica uma nota 0-100 em faixa legível.</summary>
    public static string Classificar(int valor) => valor switch
    {
        >= 85 => "Excelente",
        >= 70 => "Bom",
        >= 50 => "Regular",
        _ => "Requer atenção",
    };
}
````


## HardwareOptimizer.Agent

### `src/HardwareOptimizer.Agent/HardwareOptimizer.Agent.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Permite que os testes verifiquem constantes/identificadores internos. -->
    <InternalsVisibleTo Include="HardwareOptimizer.Agent.Tests" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.6" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.11" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
    <!-- Acesso ao registro do Windows (usado só sob Windows; em outras plataformas
         os tipos existem mas lançam em tempo de execução). -->
    <PackageReference Include="Microsoft.Win32.Registry" Version="5.0.0" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

### `src/HardwareOptimizer.Agent/Backup/ServicoBackup.cs`

````csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Backup;

/// <summary>Metadados de um backup criado antes de qualquer alteração.</summary>
public sealed record Backup
{
    public required string Id { get; init; }

    public required string Caminho { get; init; }

    public required string Checksum { get; init; }

    public DateTimeOffset CriadoEm { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Backup íntegro e gravado. O executor exige isto antes de aplicar.</summary>
    public bool Confirmado { get; init; }
}

/// <summary>
/// Backup obrigatório e bloqueante. Sem backup confirmado, nenhuma alteração
/// prossegue (regra invariante). No Windows real, complementaria com ponto de
/// restauração e export de serviços/energia/registro; no MVP multiplataforma,
/// persiste um snapshot íntegro do inventário e do contexto.
/// </summary>
public interface IServicoBackup
{
    Task<Resultado<Backup>> CriarBackupAsync(Inventario inventario, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IServicoBackup"/>
public sealed class ServicoBackup : IServicoBackup
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _diretorioBase;
    private readonly ILogger _log;

    public ServicoBackup(string? diretorioBase = null, ILogger? logger = null)
    {
        _diretorioBase = diretorioBase
            ?? Path.Combine(AppContext.BaseDirectory, "data", "backups");
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<Resultado<Backup>> CriarBackupAsync(
        Inventario inventario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        _log.LogInformation("Iniciando backup obrigatório em '{Diretorio}'.", _diretorioBase);

        try
        {
            var id = $"bkp-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            var pasta = Path.Combine(_diretorioBase, id);
            Directory.CreateDirectory(pasta);

            var caminho = Path.Combine(pasta, "inventario.json");
            var conteudo = JsonSerializer.Serialize(inventario, Json);
            await File.WriteAllTextAsync(caminho, conteudo, cancellationToken).ConfigureAwait(false);

            var checksum = Checksum(conteudo);
            await File.WriteAllTextAsync(
                Path.Combine(pasta, "checksum.sha256"), checksum, cancellationToken).ConfigureAwait(false);

            // Confirmação: o arquivo existe e o checksum confere com o conteúdo relido.
            var relido = await File.ReadAllTextAsync(caminho, cancellationToken).ConfigureAwait(false);
            var integro = Checksum(relido) == checksum;

            var backup = new Backup
            {
                Id = id,
                Caminho = caminho,
                Checksum = checksum,
                Confirmado = integro,
            };

            if (integro)
            {
                _log.LogInformation("Backup '{Id}' confirmado em '{Caminho}'.", id, caminho);
                return Resultado<Backup>.Ok(backup);
            }

            _log.LogError("Backup '{Id}': falha de integridade (checksum não confere).", id);
            return Resultado<Backup>.Falhar("Falha de integridade ao confirmar o backup.");
        }
        catch (IOException ex)
        {
            _log.LogError(ex, "Falha de E/S ao criar backup em '{Diretorio}'.", _diretorioBase);
            return Resultado<Backup>.Falhar($"Falha de E/S ao criar backup: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogError(ex, "Sem permissão para criar backup em '{Diretorio}'.", _diretorioBase);
            return Resultado<Backup>.Falhar($"Sem permissão para criar backup: {ex.Message}");
        }
    }

    private static string Checksum(string conteudo)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(conteudo));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
````

### `src/HardwareOptimizer.Agent/Bios/ModuloBios.cs`

````csharp
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Bios;

/// <summary>
/// Orquestra o fluxo de BIOS (fluxo_bios): identifica a versão atual, consulta o
/// fabricante (via <see cref="IProvedorInfoBios"/>), decide de forma conservadora
/// e gera o guia passo a passo. NÃO aplica nada — é orientação ao usuário.
/// </summary>
public sealed class ModuloBios
{
    private readonly IProvedorInfoBios _provedor;
    private readonly AnalisadorBios _analisador = new();
    private readonly GeradorGuiaBios _gerador = new();
    private readonly ILogger _log;

    public ModuloBios(IProvedorInfoBios? provedor = null, ILogger? logger = null)
    {
        _provedor = provedor ?? new BancoCuradoBios();
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<RelatorioBios> AnalisarAsync(
        Inventario inventario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        var identificacao = IdentificacaoBios.DeInventario(inventario);
        _log.LogInformation(
            "BIOS: analisando '{Fabricante} {Modelo}' (versão atual {Versao}, chave '{Chave}').",
            identificacao.Fabricante, identificacao.Modelo,
            identificacao.VersaoAtual ?? "n/d", identificacao.ChaveBusca);

        var info = await _provedor.ObterAsync(identificacao.ChaveBusca, cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            _log.LogWarning(
                "BIOS: nenhuma fonte encontrada para '{Chave}'; recomendação conservadora (manter).",
                identificacao.ChaveBusca);
        }

        var decisao = _analisador.Decidir(identificacao, info);
        _log.LogInformation(
            "BIOS: decisão -> recomenda atualizar={Recomenda} (versão recomendada {Recomendada}, risco {Risco}).",
            decisao.RecomendaAtualizar, decisao.VersaoRecomendada ?? "n/d", decisao.Risco);

        var guia = _gerador.Gerar(identificacao);

        return new RelatorioBios
        {
            Identificacao = identificacao,
            InfoFabricante = info,
            Decisao = decisao,
            Guia = guia,
        };
    }
}
````

### `src/HardwareOptimizer.Agent/Bios/ProvedorBiosComCache.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Bios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Bios;

/// <summary>
/// Decorador que adiciona cache em SQLite a qualquer <see cref="IProvedorInfoBios"/>.
/// Consulta o cache primeiro; em caso de falta, delega ao provedor interno
/// (banco curado ou, futuramente, busca web) e persiste o resultado.
/// </summary>
public sealed class ProvedorBiosComCache : IProvedorInfoBios
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly IProvedorInfoBios _interno;
    private readonly IRepositorioOtimizacao _repositorio;
    private readonly ILogger _log;

    public ProvedorBiosComCache(
        IProvedorInfoBios interno, IRepositorioOtimizacao repositorio, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(interno);
        ArgumentNullException.ThrowIfNull(repositorio);
        _interno = interno;
        _repositorio = repositorio;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<InfoBiosFabricante?> ObterAsync(
        string chaveBusca, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveBusca);

        var cacheJson = await _repositorio.ObterCacheBiosAsync(chaveBusca, cancellationToken).ConfigureAwait(false);
        if (cacheJson is not null)
        {
            var emCache = Desserializar(chaveBusca, cacheJson);
            if (emCache is not null)
            {
                _log.LogDebug("BIOS cache HIT para '{Chave}'.", chaveBusca);
                return emCache;
            }
        }

        _log.LogDebug("BIOS cache MISS para '{Chave}'; consultando provedor interno.", chaveBusca);
        var info = await _interno.ObterAsync(chaveBusca, cancellationToken).ConfigureAwait(false);
        if (info is not null)
        {
            await _repositorio
                .SalvarCacheBiosAsync(chaveBusca, JsonSerializer.Serialize(info, Json), cancellationToken)
                .ConfigureAwait(false);
            _log.LogDebug("BIOS cache atualizado para '{Chave}'.", chaveBusca);
        }

        return info;
    }

    private InfoBiosFabricante? Desserializar(string chaveBusca, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<InfoBiosFabricante>(json, Json);
        }
        catch (JsonException ex)
        {
            // Cache corrompido: ignora e recorre ao provedor interno.
            _log.LogWarning(ex, "BIOS cache corrompido para '{Chave}'; recorrendo ao provedor interno.", chaveBusca);
            return null;
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Collector/ColetorInventario.cs`

````csharp
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Coletor read-only que delega ao leitor da plataforma corrente. Seleciona
/// automaticamente o leitor adequado (Windows/Linux) quando nenhum é informado.
/// </summary>
public sealed class ColetorInventario : IColetorInventario
{
    private readonly ILeitorPlataforma _leitor;
    private readonly ILogger _log;

    public ColetorInventario(ILeitorPlataforma? leitor = null, ILoggerFactory? loggerFactory = null)
    {
        var fabrica = loggerFactory ?? NullLoggerFactory.Instance;
        _log = fabrica.CreateLogger<ColetorInventario>();
        _leitor = leitor ?? CriarLeitorPadrao(fabrica);
    }

    public async Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default)
    {
        _log.LogInformation("Iniciando coleta de inventário (leitor {Plataforma}).", _leitor.Tipo);

        var inventario = await _leitor.LerAsync(cancellationToken).ConfigureAwait(false);

        _log.LogInformation(
            "Coleta concluída: placa '{Fabricante} {Modelo}', CPU '{Cpu}', {Memorias} módulo(s) de memória, {Gpus} GPU(s).",
            inventario.Placa.Fabricante, inventario.Placa.Modelo, inventario.Cpu.Nome,
            inventario.Memoria.Count, inventario.Gpu.Count);

        return inventario;
    }

    private static ILeitorPlataforma CriarLeitorPadrao(ILoggerFactory fabrica) =>
        OperatingSystem.IsWindows()
            ? new LeitorWindows(fabrica.CreateLogger<LeitorWindows>())
            : (ILeitorPlataforma)new LeitorLinux(fabrica.CreateLogger<LeitorLinux>());
}
````

### `src/HardwareOptimizer.Agent/Collector/ILeitorPlataforma.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Leitor de inventário específico de plataforma. Toda implementação é
/// estritamente read-only: jamais modifica o sistema.
/// </summary>
public interface ILeitorPlataforma
{
    SistemaOperacionalTipo Tipo { get; }

    Task<Inventario> LerAsync(CancellationToken cancellationToken = default);
}

/// <summary>Orquestrador do coletor de inventário.</summary>
public interface IColetorInventario
{
    Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default);
}
````

### `src/HardwareOptimizer.Agent/Collector/LeitorLinux.cs`

````csharp
using System.Globalization;
using System.Runtime.InteropServices;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Leitor de inventário para Linux. Lê exclusivamente pseudo-arquivos do sistema
/// (/sys, /proc), sem invocar binários nem modificar nada. Campos indisponíveis
/// (ex.: que exigem root) são deixados nulos, sem falhar a coleta.
/// </summary>
public sealed class LeitorLinux : ILeitorPlataforma
{
    private const string DmiBase = "/sys/class/dmi/id";

    private readonly ILogger _log;

    public LeitorLinux(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Linux;

    public Task<Inventario> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo inventário do Linux a partir de /sys e /proc.");

        var inventario = new Inventario
        {
            Placa = LerPlaca(),
            Cpu = LerCpu(),
            Memoria = LerMemoria(),
            Gpu = Array.Empty<PlacaVideo>(), // Nome legível de GPU exige lspci; fora do MVP Linux.
            SistemaOperacional = LerSistemaOperacional(),
            Rede = LerRede(),
            Identificadores = LerIdentificadores(),
            ColetadoEm = DateTimeOffset.UtcNow,
        };

        if (inventario.Placa.Fabricante == "Desconhecido" || inventario.Cpu.Nome == "Desconhecido")
        {
            _log.LogWarning(
                "Coleta Linux parcial: campos ficaram como 'Desconhecido' "
                + "(arquivos de /sys/class/dmi ausentes ou sem permissão de leitura).");
        }

        return Task.FromResult(inventario);
    }

    private static PlacaMae LerPlaca() => new()
    {
        Fabricante = LerTexto($"{DmiBase}/board_vendor") ?? "Desconhecido",
        Modelo = LerTexto($"{DmiBase}/board_name") ?? "Desconhecido",
        VersaoBios = LerTexto($"{DmiBase}/bios_version"),
        DataBios = NormalizadorData.Normalizar(LerTexto($"{DmiBase}/bios_date")),
        Modo = Directory.Exists("/sys/firmware/efi") ? "UEFI" : "Legacy",
        SecureBoot = LerSecureBoot(),
    };

    private static Processador LerCpu()
    {
        var nome = "Desconhecido";
        var threads = 0;
        var nucleos = new HashSet<string>(StringComparer.Ordinal);
        string? physicalId = null;

        foreach (var linha in LerLinhas("/proc/cpuinfo"))
        {
            var (chave, valor) = SepararChaveValor(linha);
            switch (chave)
            {
                case "model name":
                    nome = valor;
                    break;
                case "processor":
                    threads++;
                    break;
                case "physical id":
                    physicalId = valor;
                    break;
                case "core id":
                    nucleos.Add($"{physicalId}:{valor}");
                    break;
            }
        }

        return new Processador
        {
            Nome = nome,
            Threads = threads > 0 ? threads : null,
            Nucleos = nucleos.Count > 0 ? nucleos.Count : null,
            TempIdleC = LerTemperaturaCpu(),
        };
    }

    private static IReadOnlyList<ModuloMemoria> LerMemoria()
    {
        foreach (var linha in LerLinhas("/proc/meminfo"))
        {
            var (chave, valor) = SepararChaveValor(linha);
            if (chave != "MemTotal")
            {
                continue;
            }

            // Valor no formato "16384000 kB".
            var numero = valor.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (long.TryParse(numero, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            {
                var gb = (int)Math.Round(kb / 1024.0 / 1024.0);
                return new[] { new ModuloMemoria { TamanhoGb = gb } };
            }
        }

        return Array.Empty<ModuloMemoria>();
    }

    private static SistemaOperacionalInfo LerSistemaOperacional() => new()
    {
        Tipo = SistemaOperacionalTipo.Linux,
        Nome = LerOsReleasePrettyName() ?? "Linux",
        Versao = Environment.OSVersion.VersionString,
        Arquitetura = RuntimeInformation.OSArchitecture.ToString(),
    };

    private static IReadOnlyList<InterfaceRede> LerRede()
    {
        const string baseRede = "/sys/class/net";
        if (!Directory.Exists(baseRede))
        {
            return Array.Empty<InterfaceRede>();
        }

        var interfaces = new List<InterfaceRede>();
        foreach (var dir in EnumerarDiretorios(baseRede))
        {
            var nome = Path.GetFileName(dir);
            if (nome == "lo")
            {
                continue;
            }

            interfaces.Add(new InterfaceRede
            {
                Nome = nome,
                EnderecoMac = LerTexto(Path.Combine(dir, "address")),
            });
        }

        return interfaces;
    }

    private static IdentificadoresSensiveis LerIdentificadores() => new()
    {
        NumeroSerie = LerTexto($"{DmiBase}/product_serial"),
        UuidPlaca = LerTexto($"{DmiBase}/product_uuid"),
        NomeMaquina = SeguroOuNulo(() => Environment.MachineName),
        NomeUsuario = SeguroOuNulo(() => Environment.UserName),
        ChaveProdutoWindows = null,
    };

    private static bool? LerSecureBoot()
    {
        // A variável EFI SecureBoot tem um cabeçalho de 4 bytes seguido do valor.
        var arquivos = SeguroOuNulo(() => Directory.Exists("/sys/firmware/efi/efivars")
            ? Directory.GetFiles("/sys/firmware/efi/efivars", "SecureBoot-*")
            : Array.Empty<string>());

        if (arquivos is null || arquivos.Length == 0)
        {
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(arquivos[0]);
            return bytes.Length >= 5 ? bytes[4] == 1 : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static double? LerTemperaturaCpu()
    {
        var texto = LerTexto("/sys/class/thermal/thermal_zone0/temp");
        if (texto is not null &&
            long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mili))
        {
            return Math.Round(mili / 1000.0, 1);
        }

        return null;
    }

    private static string? LerOsReleasePrettyName()
    {
        foreach (var linha in LerLinhas("/etc/os-release"))
        {
            if (linha.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
            {
                return linha["PRETTY_NAME=".Length..].Trim('"');
            }
        }

        return null;
    }

    private static (string Chave, string Valor) SepararChaveValor(string linha)
    {
        var idx = linha.IndexOf(':', StringComparison.Ordinal);
        return idx < 0
            ? (linha.Trim(), string.Empty)
            : (linha[..idx].Trim(), linha[(idx + 1)..].Trim());
    }

    private static string? LerTexto(string caminho)
    {
        try
        {
            return File.Exists(caminho) ? File.ReadAllText(caminho).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> LerLinhas(string caminho)
    {
        string[]? linhas = null;
        try
        {
            if (File.Exists(caminho))
            {
                linhas = File.ReadAllLines(caminho);
            }
        }
        catch (IOException)
        {
            // Ignorado: arquivo indisponível resulta em coleta parcial.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorado: sem permissão resulta em coleta parcial.
        }

        return linhas ?? Array.Empty<string>();
    }

    private static IEnumerable<string> EnumerarDiretorios(string caminho)
    {
        try
        {
            return Directory.EnumerateDirectories(caminho);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static T? SeguroOuNulo<T>(Func<T> acao)
        where T : class
    {
        try
        {
            return acao();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Collector/LeitorWindows.cs`

````csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Leitor de inventário para Windows (plataforma prioritária do MVP). Usa
/// PowerShell + CIM/WMI em modo somente leitura (Get-CimInstance). É defensivo:
/// qualquer falha de uma consulta resulta em coleta parcial, nunca em exceção.
/// A validação real ocorre em máquinas Windows (Fase 1 do roadmap).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorWindows : ILeitorPlataforma
{
    private readonly ILogger _log;

    public LeitorWindows(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<Inventario> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo inventário do Windows via PowerShell/CIM (Get-CimInstance).");

        var inventario = new Inventario
        {
            Placa = LerPlaca(),
            Cpu = LerCpu(),
            Memoria = LerMemoria(),
            Gpu = LerGpu(),
            SistemaOperacional = LerSistemaOperacional(),
            Rede = LerRede(),
            Identificadores = LerIdentificadores(),
            ColetadoEm = DateTimeOffset.UtcNow,
        };

        if (inventario.Placa.Fabricante == "Desconhecido")
        {
            _log.LogWarning(
                "Coleta Windows parcial: consultas CIM/PowerShell não retornaram dados "
                + "(PowerShell ausente, sem permissão ou execução bloqueada).");
        }

        return Task.FromResult(inventario);
    }

    private static PlacaMae LerPlaca()
    {
        var board = PrimeiroItem("Win32_BaseBoard", "Manufacturer,Product,SerialNumber");
        var bios = PrimeiroItem("Win32_BIOS", "SMBIOSBIOSVersion,ReleaseDate");

        return new PlacaMae
        {
            Fabricante = Texto(board, "Manufacturer") ?? "Desconhecido",
            Modelo = Texto(board, "Product") ?? "Desconhecido",
            VersaoBios = Texto(bios, "SMBIOSBIOSVersion"),
            DataBios = NormalizadorData.Normalizar(Texto(bios, "ReleaseDate")),
            Modo = LerTexto("$env:firmware_type") is { Length: > 0 } modo ? modo : null,
            SecureBoot = LerSecureBoot(),
        };
    }

    private static Processador LerCpu()
    {
        var cpu = PrimeiroItem("Win32_Processor", "Name,NumberOfCores,NumberOfLogicalProcessors");
        return new Processador
        {
            Nome = Texto(cpu, "Name") ?? "Desconhecido",
            Nucleos = Inteiro(cpu, "NumberOfCores"),
            Threads = Inteiro(cpu, "NumberOfLogicalProcessors"),
        };
    }

    private static IReadOnlyList<ModuloMemoria> LerMemoria()
    {
        var modulos = new List<ModuloMemoria>();
        foreach (var item in Itens("Win32_PhysicalMemory", "Capacity,Speed,Manufacturer"))
        {
            int? gb = null;
            if (long.TryParse(Texto(item, "Capacity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            {
                gb = (int)Math.Round(bytes / 1024.0 / 1024.0 / 1024.0);
            }

            modulos.Add(new ModuloMemoria
            {
                TamanhoGb = gb,
                VelocidadeMhz = Inteiro(item, "Speed"),
                Fabricante = Texto(item, "Manufacturer"),
            });
        }

        return modulos;
    }

    private static IReadOnlyList<PlacaVideo> LerGpu()
    {
        var gpus = new List<PlacaVideo>();
        foreach (var item in Itens("Win32_VideoController", "Name,DriverVersion"))
        {
            var nome = Texto(item, "Name");
            if (!string.IsNullOrWhiteSpace(nome))
            {
                gpus.Add(new PlacaVideo { Nome = nome, VersaoDriver = Texto(item, "DriverVersion") });
            }
        }

        return gpus;
    }

    private static SistemaOperacionalInfo LerSistemaOperacional()
    {
        var os = PrimeiroItem("Win32_OperatingSystem", "Caption,Version");
        return new SistemaOperacionalInfo
        {
            Tipo = SistemaOperacionalTipo.Windows,
            Nome = Texto(os, "Caption") ?? "Windows",
            Versao = Texto(os, "Version") ?? Environment.OSVersion.VersionString,
            Arquitetura = RuntimeInformation.OSArchitecture.ToString(),
        };
    }

    private static IReadOnlyList<InterfaceRede> LerRede()
    {
        var interfaces = new List<InterfaceRede>();
        foreach (var item in Itens(
            "Win32_NetworkAdapter -Filter 'PhysicalAdapter=True'", "NetConnectionID,MACAddress"))
        {
            var nome = Texto(item, "NetConnectionID");
            if (!string.IsNullOrWhiteSpace(nome))
            {
                interfaces.Add(new InterfaceRede { Nome = nome, EnderecoMac = Texto(item, "MACAddress") });
            }
        }

        return interfaces;
    }

    private static IdentificadoresSensiveis LerIdentificadores()
    {
        var board = PrimeiroItem("Win32_BaseBoard", "SerialNumber");
        var produto = PrimeiroItem("Win32_ComputerSystemProduct", "UUID");
        return new IdentificadoresSensiveis
        {
            NumeroSerie = Texto(board, "SerialNumber"),
            UuidPlaca = Texto(produto, "UUID"),
            NomeMaquina = SeguroOuNulo(() => Environment.MachineName),
            NomeUsuario = SeguroOuNulo(() => Environment.UserName),
            ChaveProdutoWindows = null, // exige leitura adicional; sensível, omitido por padrão.
        };
    }

    private static bool? LerSecureBoot()
    {
        var saida = LerTexto("try { Confirm-SecureBootUEFI } catch { '' }");
        return bool.TryParse(saida, out var valor) ? valor : null;
    }

    // ---- Infraestrutura CIM/PowerShell ----------------------------------------------------

    private static JsonElement? PrimeiroItem(string classe, string propriedades) =>
        Itens(classe, propriedades).FirstOrDefault() is { ValueKind: not JsonValueKind.Undefined } e ? e : null;

    private static IEnumerable<JsonElement> Itens(string classe, string propriedades)
    {
        var saida = LerTexto(
            $"Get-CimInstance -ClassName {classe} | Select-Object {propriedades} | ConvertTo-Json -Compress -Depth 3");
        if (string.IsNullOrWhiteSpace(saida))
        {
            yield break;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(saida);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    yield return item.Clone();
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                yield return doc.RootElement.Clone();
            }
        }
    }

    private static string? Texto(JsonElement? elemento, string propriedade)
    {
        if (elemento is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var texto = prop.GetString();
            return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        }

        return null;
    }

    private static int? Inteiro(JsonElement? elemento, string propriedade)
    {
        if (elemento is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var prop) &&
            prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var valor))
        {
            return valor;
        }

        return null;
    }

    private static string? LerTexto(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{comando}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(psi);
            if (processo is null)
            {
                return null;
            }

            var saida = processo.StandardOutput.ReadToEnd();
            if (!processo.WaitForExit(20_000))
            {
                return null;
            }

            return saida.Trim();
        }
        catch (Win32Exception)
        {
            return null; // PowerShell ausente.
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static T? SeguroOuNulo<T>(Func<T> acao)
        where T : class
    {
        try
        {
            return acao();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Collector/NormalizadorData.cs`

````csharp
using System.Globalization;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Normaliza datas vindas das fontes de inventário para o formato ISO
/// <c>yyyy-MM-dd</c>. Cobre o formato legado do <c>ConvertTo-Json</c> do Windows
/// PowerShell (<c>/Date(ms)/</c>), o CIM DATETIME bruto (<c>yyyyMMddHHmmss…</c>) e
/// o formato do DMI no Linux (<c>MM/dd/yyyy</c>). Pura e determinística.
/// </summary>
internal static class NormalizadorData
{
    /// <summary>Devolve a data em ISO (yyyy-MM-dd) ou o texto original se não reconhecer; nulo se vazio.</summary>
    public static string? Normalizar(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return null;
        }

        var texto = bruto.Trim();

        if (TentarDateJson(texto, out var data)
            || TentarCimDatetime(texto, out data)
            || TentarFormatosComuns(texto, out data))
        {
            return data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return texto; // formato não reconhecido: devolve como veio (não perde informação)
    }

    // /Date(1754611200000)/  ou  /Date(1754611200000+0000)/
    private static bool TentarDateJson(string texto, out DateTimeOffset data)
    {
        data = default;
        var inicio = texto.IndexOf("/Date(", StringComparison.Ordinal);
        if (inicio < 0)
        {
            return false;
        }

        var fim = texto.IndexOf(")/", inicio, StringComparison.Ordinal);
        if (fim <= inicio)
        {
            return false;
        }

        var conteudo = texto[(inicio + 6)..fim];
        var sinal = conteudo.IndexOfAny(new[] { '+', '-' }, 1);
        var milissegundos = sinal > 0 ? conteudo[..sinal] : conteudo;

        if (long.TryParse(milissegundos, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
        {
            data = DateTimeOffset.FromUnixTimeMilliseconds(epoch);
            return true;
        }

        return false;
    }

    // CIM DATETIME: 20250808000000.000000+000  (usa os 8 primeiros dígitos)
    private static bool TentarCimDatetime(string texto, out DateTimeOffset data)
    {
        data = default;
        return texto.Length >= 8
            && texto[..8].All(char.IsDigit)
            && DateTimeOffset.TryParseExact(
                texto[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out data);
    }

    // MM/dd/yyyy (DMI/Linux) e variações ISO.
    private static bool TentarFormatosComuns(string texto, out DateTimeOffset data)
    {
        string[] formatos = { "MM/dd/yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:sszzz" };
        return DateTimeOffset.TryParseExact(
            texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out data);
    }
}
````

### `src/HardwareOptimizer.Agent/Execution/ComandoEstadoSistema.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Comando interno que define um único alvo do <see cref="IEstadoSistema"/> com
/// um valor derivado dos parâmetros. Cobre toggles (valor fixo) e ações
/// parametrizadas (valor vindo de um parâmetro), com rollback por restauração
/// do valor anterior.
/// </summary>
public sealed class ComandoEstadoSistema : IComandoInterno
{
    private readonly IEstadoSistema _estado;
    private readonly Func<IReadOnlyDictionary<string, string>, string> _resolverAlvo;
    private readonly Func<IReadOnlyDictionary<string, string>, string> _resolverValor;

    public ComandoEstadoSistema(
        string id,
        IEstadoSistema estado,
        Func<IReadOnlyDictionary<string, string>, string> resolverAlvo,
        Func<IReadOnlyDictionary<string, string>, string> resolverValor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(resolverAlvo);
        ArgumentNullException.ThrowIfNull(resolverValor);

        Id = id;
        _estado = estado;
        _resolverAlvo = resolverAlvo;
        _resolverValor = resolverValor;
    }

    public string Id { get; }

    public Task<RegistroAlteracao> AplicarAsync(
        string acaoId,
        CategoriaAcao categoria,
        IReadOnlyDictionary<string, string> parametros,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        cancellationToken.ThrowIfCancellationRequested();

        var alvo = _resolverAlvo(parametros);
        var valorNovo = _resolverValor(parametros);
        var valorAnterior = _estado.Ler(alvo);

        _estado.Escrever(alvo, valorNovo);

        var registro = new RegistroAlteracao
        {
            AcaoId = acaoId,
            ComandoId = Id,
            Categoria = categoria,
            Alvo = alvo,
            ValorAnterior = valorAnterior,
            ValorNovo = valorNovo,
        };

        return Task.FromResult(registro);
    }

    public Task ReverterAsync(RegistroAlteracao registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);
        cancellationToken.ThrowIfCancellationRequested();

        _estado.Restaurar(registro.Alvo, registro.ValorAnterior);
        return Task.CompletedTask;
    }
}
````

### `src/HardwareOptimizer.Agent/Execution/ExecutorControlado.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Aplica um perfil aprovado, uma categoria por vez, na ordem do documento.
/// Após cada categoria, valida a estabilidade; se reprovar (ou se uma
/// pré-condição falhar), reverte a categoria inteira pelo registro de
/// alterações. Executa somente comandos do registro — nada fora do catálogo.
/// Cada passo é registrado em log para diagnóstico do ponto exato de falha.
/// </summary>
public sealed class ExecutorControlado
{
    private readonly CatalogoAcoes _catalogo;
    private readonly RegistroComandos _comandos;
    private readonly IVerificadorPreCondicoes _preCondicoes;
    private readonly IValidadorCategoria _validadorCategoria;
    private readonly ILogger _log;

    public ExecutorControlado(
        CatalogoAcoes catalogo,
        RegistroComandos comandos,
        IVerificadorPreCondicoes preCondicoes,
        IValidadorCategoria validadorCategoria,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(comandos);
        ArgumentNullException.ThrowIfNull(preCondicoes);
        ArgumentNullException.ThrowIfNull(validadorCategoria);

        _catalogo = catalogo;
        _comandos = comandos;
        _preCondicoes = preCondicoes;
        _validadorCategoria = validadorCategoria;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<RelatorioExecucao> AplicarPerfilAsync(
        Perfil perfil, ContextoExecucao contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(contexto);

        // Perfil customizado só pode ser aplicado após consentimento registrado.
        if (!perfil.PodeAplicar)
        {
            _log.LogWarning(
                "Execução bloqueada: perfil customizado '{Perfil}' sem consentimento registrado.", perfil.Nome);
            return new RelatorioExecucao
            {
                Sucesso = false,
                PerfilNome = perfil.Nome,
                Mensagens = new[] { "Perfil customizado sem consentimento registrado: execução bloqueada." },
            };
        }

        _log.LogInformation(
            "Iniciando execução do perfil '{Perfil}' ({Qtd} ações, backup={Backup}).",
            perfil.Nome, perfil.Selecoes.Count, contexto.BackupConfirmado);

        var categorias = new List<ResultadoCategoria>();
        var sucessoGeral = true;

        // Agrupa por categoria e ordena pela ordem natural do enum (= categorias_ordem).
        var grupos = perfil.Selecoes
            .Select(selecao => (selecao, acao: _catalogo.Obter(selecao.AcaoId)))
            .Where(par => par.acao is not null)
            .GroupBy(par => par.acao!.Categoria)
            .OrderBy(grupo => grupo.Key);

        foreach (var grupo in grupos)
        {
            var resultado = await AplicarCategoriaAsync(grupo.Key, grupo, contexto, cancellationToken)
                .ConfigureAwait(false);
            categorias.Add(resultado);

            if (resultado.Situacao != SituacaoCategoria.Aplicada)
            {
                sucessoGeral = false;
            }
        }

        _log.LogInformation(
            "Execução do perfil '{Perfil}' finalizada. Sucesso geral={Sucesso}.", perfil.Nome, sucessoGeral);

        return new RelatorioExecucao
        {
            Sucesso = sucessoGeral,
            PerfilNome = perfil.Nome,
            Categorias = categorias,
        };
    }

    private async Task<ResultadoCategoria> AplicarCategoriaAsync(
        CategoriaAcao categoria,
        IEnumerable<(SelecaoAcao Selecao, AcaoOtimizacao? Acao)> itens,
        ContextoExecucao contexto,
        CancellationToken cancellationToken)
    {
        var aplicadas = new List<RegistroAlteracao>();
        var mensagens = new List<string>();
        _log.LogInformation("Categoria {Categoria}: aplicando.", categoria);

        foreach (var (selecao, acao) in itens)
        {
            var pre = _preCondicoes.Verificar(acao!, selecao.Parametros, contexto);
            if (pre.Falha)
            {
                _log.LogWarning(
                    "Categoria {Categoria} BLOQUEADA na ação '{Acao}': {Motivo}",
                    categoria, acao!.Id, pre.MensagemErro);
                mensagens.AddRange(pre.Erros);
                return await ReverterCategoriaAsync(
                    categoria, aplicadas, SituacaoCategoria.Bloqueada, mensagens, cancellationToken)
                    .ConfigureAwait(false);
            }

            var comando = _comandos.Obter(acao!.ComandoInternoId);
            if (comando is null)
            {
                _log.LogError(
                    "Categoria {Categoria}: comando interno '{Comando}' da ação '{Acao}' não está registrado.",
                    categoria, acao.ComandoInternoId, acao.Id);
                mensagens.Add($"Ação '{acao.Id}': comando interno '{acao.ComandoInternoId}' não registrado.");
                return await ReverterCategoriaAsync(
                    categoria, aplicadas, SituacaoCategoria.Bloqueada, mensagens, cancellationToken)
                    .ConfigureAwait(false);
            }

            var registro = await comando
                .AplicarAsync(acao.Id, categoria, selecao.Parametros, cancellationToken)
                .ConfigureAwait(false);
            aplicadas.Add(registro);
            _log.LogDebug(
                "Ação '{Acao}' aplicada: {Alvo} '{Antes}' -> '{Depois}'.",
                acao.Id, registro.Alvo, registro.ValorAnterior ?? "(não definido)", registro.ValorNovo);
        }

        // Validação por categoria (runner de testes). Reprovou -> reverte tudo.
        var validacao = await _validadorCategoria
            .ValidarAsync(categoria, aplicadas, cancellationToken)
            .ConfigureAwait(false);

        if (validacao.Regressao)
        {
            _log.LogWarning(
                "Categoria {Categoria}: REGRESSÃO detectada ({Ferramenta}); revertendo {Qtd} alteração(ões).",
                categoria, validacao.Ferramenta, aplicadas.Count);
            mensagens.Add($"Regressão detectada na categoria {categoria}: revertendo.");
            var revertida = await ReverterCategoriaAsync(
                categoria, aplicadas, SituacaoCategoria.Revertida, mensagens, cancellationToken)
                .ConfigureAwait(false);
            return revertida with { Validacao = validacao };
        }

        _log.LogInformation(
            "Categoria {Categoria}: APLICADA com {Qtd} alteração(ões).", categoria, aplicadas.Count);
        return new ResultadoCategoria
        {
            Categoria = categoria,
            Situacao = SituacaoCategoria.Aplicada,
            Alteracoes = aplicadas,
            Validacao = validacao,
        };
    }

    private async Task<ResultadoCategoria> ReverterCategoriaAsync(
        CategoriaAcao categoria,
        List<RegistroAlteracao> aplicadas,
        SituacaoCategoria situacao,
        List<string> mensagens,
        CancellationToken cancellationToken)
    {
        if (aplicadas.Count > 0)
        {
            _log.LogWarning(
                "Categoria {Categoria}: revertendo {Qtd} alteração(ões) (situação {Situacao}).",
                categoria, aplicadas.Count, situacao);
        }

        var revertidas = new List<RegistroAlteracao>(aplicadas.Count);

        // Reverte na ordem inversa da aplicação.
        for (var i = aplicadas.Count - 1; i >= 0; i--)
        {
            var registro = aplicadas[i];
            var comando = _comandos.Obter(registro.ComandoId);
            if (comando is null)
            {
                _log.LogError(
                    "Sem comando para reverter '{Acao}' ({Comando}); estado pode ficar inconsistente.",
                    registro.AcaoId, registro.ComandoId);
                mensagens.Add($"Sem comando para reverter '{registro.AcaoId}' ({registro.ComandoId}).");
                continue;
            }

            await comando.ReverterAsync(registro, cancellationToken).ConfigureAwait(false);
            _log.LogDebug("Revertido: {Alvo} -> '{Anterior}'.", registro.Alvo, registro.ValorAnterior ?? "(removido)");
            revertidas.Add(registro with { Revertido = true });
        }

        revertidas.Reverse();
        return new ResultadoCategoria
        {
            Categoria = categoria,
            Situacao = situacao,
            Alteracoes = revertidas,
            Mensagens = mensagens,
        };
    }
}
````

### `src/HardwareOptimizer.Agent/Execution/IComandoInterno.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Comando interno determinístico e versionado. É a única coisa que de fato
/// altera o sistema; nunca é gerado pelo LLM. Aplica e reverte uma ação,
/// produzindo o registro auditável correspondente.
/// </summary>
public interface IComandoInterno
{
    /// <summary>Identificador versionado (ex.: "cmd.so.system_responsiveness.v1").</summary>
    string Id { get; }

    Task<RegistroAlteracao> AplicarAsync(
        string acaoId,
        CategoriaAcao categoria,
        IReadOnlyDictionary<string, string> parametros,
        CancellationToken cancellationToken = default);

    Task ReverterAsync(RegistroAlteracao registro, CancellationToken cancellationToken = default);
}
````

### `src/HardwareOptimizer.Agent/Execution/IEstadoSistema.cs`

````csharp
using System.Collections.Concurrent;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Abstração do estado mutável do sistema operacional (chaves de registro,
/// planos de energia, serviços). Permite ler o valor atual, escrever um novo e
/// restaurar o anterior — base do registro antes/depois e do rollback.
/// </summary>
public interface IEstadoSistema
{
    string? Ler(string alvo);

    void Escrever(string alvo, string valor);

    /// <summary>Restaura o valor anterior. Nulo significa "remover/voltar ao não definido".</summary>
    void Restaurar(string alvo, string? valorAnterior);
}

/// <summary>
/// Estado simulado em memória — modo seguro (dry-run) padrão do MVP. Não toca o
/// sistema real, mas reproduz fielmente a semântica de ler/escrever/restaurar,
/// tornando o executor e o rollback totalmente testáveis. Implementações reais
/// (powercfg, registro, sc.exe) substituem esta em Windows elevado.
/// </summary>
public sealed class EstadoSistemaSimulado : IEstadoSistema
{
    private readonly ConcurrentDictionary<string, string> _valores;

    public EstadoSistemaSimulado(IReadOnlyDictionary<string, string>? estadoInicial = null)
    {
        _valores = estadoInicial is null
            ? new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new ConcurrentDictionary<string, string>(estadoInicial, StringComparer.OrdinalIgnoreCase);
    }

    public string? Ler(string alvo) => _valores.TryGetValue(alvo, out var valor) ? valor : null;

    public void Escrever(string alvo, string valor) => _valores[alvo] = valor;

    public void Restaurar(string alvo, string? valorAnterior)
    {
        if (valorAnterior is null)
        {
            _valores.TryRemove(alvo, out _);
        }
        else
        {
            _valores[alvo] = valorAnterior;
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Execution/IValidadorCategoria.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Valida a estabilidade após aplicar uma categoria. É o ponto de extensão para
/// o Runner de Validação (OCCT, Cinebench, Prime95, MemTest86) da Fase 9. Se a
/// validação reprovar, o executor reverte a categoria.
/// </summary>
public interface IValidadorCategoria
{
    Task<ResultadoValidacao> ValidarAsync(
        CategoriaAcao categoria,
        IReadOnlyList<RegistroAlteracao> alteracoes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validador trivial do MVP: considera toda categoria estável. Substituível por
/// uma implementação que dispare testes de estresse reais.
/// </summary>
public sealed class ValidadorCategoriaSempreEstavel : IValidadorCategoria
{
    public Task<ResultadoValidacao> ValidarAsync(
        CategoriaAcao categoria,
        IReadOnlyList<RegistroAlteracao> alteracoes,
        CancellationToken cancellationToken = default)
    {
        var resultado = new ResultadoValidacao
        {
            Categoria = categoria.ToString(),
            Ferramenta = "validação-mvp",
            Regressao = false,
            Estabilidade = "Totalmente validado",
        };

        return Task.FromResult(resultado);
    }
}
````

### `src/HardwareOptimizer.Agent/Execution/PreCondicoes.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>Contexto de execução: estado que as pré-condições consultam.</summary>
public sealed class ContextoExecucao
{
    public required bool BackupConfirmado { get; init; }
}

/// <summary>Verifica as pré-condições obrigatórias de uma ação antes de aplicá-la.</summary>
public interface IVerificadorPreCondicoes
{
    Resultado Verificar(AcaoOtimizacao acao, IReadOnlyDictionary<string, string> parametros, ContextoExecucao contexto);
}

/// <summary>
/// Implementação padrão. Conhece um conjunto fechado de pré-condições e recusa
/// qualquer pré-condição desconhecida (postura conservadora).
/// </summary>
public sealed class VerificadorPreCondicoes : IVerificadorPreCondicoes
{
    public Resultado Verificar(
        AcaoOtimizacao acao, IReadOnlyDictionary<string, string> parametros, ContextoExecucao contexto)
    {
        ArgumentNullException.ThrowIfNull(acao);
        ArgumentNullException.ThrowIfNull(parametros);
        ArgumentNullException.ThrowIfNull(contexto);

        var erros = new List<string>();

        foreach (var preCondicao in acao.PreCondicoes)
        {
            switch (preCondicao)
            {
                case "backup_confirmado":
                    if (!contexto.BackupConfirmado)
                    {
                        erros.Add($"Ação '{acao.Id}': backup não confirmado. Sem backup, não se prossegue.");
                    }

                    break;

                case "servico_consta_na_lista_segura":
                    VerificarServicoNaListaSegura(acao, parametros, erros);
                    break;

                default:
                    erros.Add($"Ação '{acao.Id}': pré-condição desconhecida '{preCondicao}' (bloqueio conservador).");
                    break;
            }
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }

    private static void VerificarServicoNaListaSegura(
        AcaoOtimizacao acao, IReadOnlyDictionary<string, string> parametros, List<string> erros)
    {
        if (acao.ObterParametro("nome_servico") is not ParametroListaBranca lista)
        {
            erros.Add($"Ação '{acao.Id}': parâmetro 'nome_servico' de lista branca ausente.");
            return;
        }

        if (!parametros.TryGetValue("nome_servico", out var nome) ||
            !lista.ValoresSeguros.Contains(nome, StringComparer.OrdinalIgnoreCase))
        {
            erros.Add($"Ação '{acao.Id}': serviço '{(parametros.GetValueOrDefault("nome_servico") ?? "?")}' "
                + "não consta na lista segura.");
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Execution/RegistroAlteracao.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Registro auditável de uma alteração aplicada: guarda o alvo e os valores
/// anterior e novo, permitindo rollback determinístico por categoria.
/// </summary>
public sealed record RegistroAlteracao
{
    public required string AcaoId { get; init; }

    public required string ComandoId { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    /// <summary>Recurso afetado (ex.: chave de registro, plano de energia, serviço).</summary>
    public required string Alvo { get; init; }

    public string? ValorAnterior { get; init; }

    public string? ValorNovo { get; init; }

    public DateTimeOffset AplicadoEm { get; init; } = DateTimeOffset.UtcNow;

    public bool Revertido { get; init; }
}
````

### `src/HardwareOptimizer.Agent/Execution/RegistroComandos.cs`

````csharp
using System.Globalization;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Mapeia cada <c>comando_interno</c> do catálogo à sua implementação
/// determinística versionada. É a fronteira entre a seleção (LLM) e a execução
/// (agente): só IDs aqui registrados podem rodar.
/// </summary>
public sealed class RegistroComandos
{
    private readonly IReadOnlyDictionary<string, IComandoInterno> _comandos;

    public RegistroComandos(IEnumerable<IComandoInterno> comandos)
    {
        ArgumentNullException.ThrowIfNull(comandos);
        var mapa = new Dictionary<string, IComandoInterno>(StringComparer.OrdinalIgnoreCase);
        foreach (var comando in comandos)
        {
            if (!mapa.TryAdd(comando.Id, comando))
            {
                throw new ArgumentException($"Comando interno duplicado: '{comando.Id}'.", nameof(comandos));
            }
        }

        _comandos = mapa;
    }

    public bool Contem(string comandoId) => comandoId is not null && _comandos.ContainsKey(comandoId);

    public IComandoInterno? Obter(string comandoId) =>
        comandoId is not null && _comandos.TryGetValue(comandoId, out var c) ? c : null;

    /// <summary>
    /// Registro padrão para o catálogo embutido, operando sobre um
    /// <see cref="IEstadoSistema"/> (simulado no MVP). Os alvos e valores abaixo
    /// refletem as chaves reais que as implementações Windows manipulariam.
    /// </summary>
    public static RegistroComandos Padrao(IEstadoSistema estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        IComandoInterno Fixo(string id, string alvo, string valor) =>
            new ComandoEstadoSistema(id, estado, _ => alvo, _ => valor);

        IComandoInterno DeParametro(string id, string alvo, string parametro) =>
            new ComandoEstadoSistema(
                id, estado,
                _ => alvo,
                p => p.TryGetValue(parametro, out var v) ? v : throw FaltaParametro(parametro, id));

        return new RegistroComandos(new[]
        {
            Fixo("cmd.pwr.plano_alto_desempenho.v1", "powercfg:plano_ativo", "ALTO_DESEMPENHO"),
            Fixo("cmd.pwr.usb_suspensao_seletiva.v1", "powercfg:usb_suspensao_seletiva", "DESABILITADO"),
            Fixo("cmd.so.efeitos_visuais.v1", "registro:VisualFXSetting", "DESEMPENHO"),
            DeParametro("cmd.so.system_responsiveness.v1", "registro:SystemResponsiveness", "percentual_reserva"),
            DeParametro("cmd.gpu.tdr_delay.v1", "registro:TdrDelay", "tempo_segundos"),
            Fixo("cmd.gpu.hags.v1", "registro:HwSchMode", "2"),
            Fixo("cmd.net.throttling_index.v1", "registro:NetworkThrottlingIndex", "ffffffff"),
            new ComandoEstadoSistema(
                "cmd.srv.desativar_servico.v1",
                estado,
                p => "servico:" + (p.TryGetValue("nome_servico", out var nome)
                    ? nome
                    : throw FaltaParametro("nome_servico", "cmd.srv.desativar_servico.v1")),
                _ => "Disabled"),
        });
    }

    private static InvalidOperationException FaltaParametro(string parametro, string comandoId) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "Parâmetro '{0}' ausente para o comando '{1}'.", parametro, comandoId));
}
````

### `src/HardwareOptimizer.Agent/Execution/RelatorioExecucao.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>Desfecho da aplicação de uma categoria.</summary>
public enum SituacaoCategoria
{
    Aplicada = 0,
    Revertida = 1,
    Bloqueada = 2,
}

/// <summary>Resultado da aplicação de uma categoria, com suas alterações e validação.</summary>
public sealed record ResultadoCategoria
{
    public required CategoriaAcao Categoria { get; init; }

    public required SituacaoCategoria Situacao { get; init; }

    public IReadOnlyList<RegistroAlteracao> Alteracoes { get; init; } = Array.Empty<RegistroAlteracao>();

    public ResultadoValidacao? Validacao { get; init; }

    public IReadOnlyList<string> Mensagens { get; init; } = Array.Empty<string>();
}

/// <summary>Relatório consolidado da execução de um perfil.</summary>
public sealed record RelatorioExecucao
{
    public required bool Sucesso { get; init; }

    public required string PerfilNome { get; init; }

    public IReadOnlyList<ResultadoCategoria> Categorias { get; init; } = Array.Empty<ResultadoCategoria>();

    public IReadOnlyList<string> Mensagens { get; init; } = Array.Empty<string>();

    public IEnumerable<RegistroAlteracao> TodasAlteracoes =>
        Categorias.SelectMany(c => c.Alteracoes);
}
````

### `src/HardwareOptimizer.Agent/Execution/Windows/EstadoSistemaWindows.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Agent.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Execution.Windows;

/// <summary>
/// Implementação real de <see cref="IEstadoSistema"/> para Windows. Interpreta os
/// alvos simbólicos do catálogo (<c>registro:*</c>, <c>powercfg:*</c>,
/// <c>servico:*</c>) e os traduz em operações concretas de registro, plano de
/// energia e serviços — preservando a semântica ler/escrever/restaurar, de modo
/// que o <see cref="ExecutorControlado"/> e o rollback funcionem sem alteração.
///
/// O acesso ao registro e a processos é abstraído (<see cref="IAcessoRegistro"/>,
/// <see cref="IExecutorProcesso"/>), tornando toda a lógica testável fora do
/// Windows com fakes. Os adaptadores reais só são criados sob Windows elevado.
/// </summary>
public sealed class EstadoSistemaWindows : IEstadoSistema
{
    // GUIDs oficiais do Windows.
    internal const string GuidAltoDesempenho = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    internal const string SubgrupoUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
    internal const string ConfigUsbSuspensao = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

    private const string MultimediaSystemProfile =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string GraphicsDrivers = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    private readonly IAcessoRegistro _registro;
    private readonly IExecutorProcesso _processo;
    private readonly ILogger _log;

    public EstadoSistemaWindows(IAcessoRegistro registro, IExecutorProcesso processo, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(processo);

        _registro = registro;
        _processo = processo;
        _log = logger ?? NullLogger.Instance;
    }

    public string? Ler(string alvo) => Mapear(alvo).Ler();

    public void Escrever(string alvo, string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        _log.LogDebug("Windows: aplicando '{Alvo}' = '{Valor}'.", alvo, valor);
        Mapear(alvo).Escrever(valor);
    }

    public void Restaurar(string alvo, string? valorAnterior)
    {
        _log.LogDebug("Windows: restaurando '{Alvo}' = '{Valor}'.", alvo, valorAnterior ?? "(remover)");
        Mapear(alvo).Restaurar(valorAnterior);
    }

    /// <summary>
    /// Seleciona o estado de execução do ambiente: o estado real do Windows quando
    /// estamos sob Windows E a execução real foi explicitamente habilitada
    /// (<c>HWOPT_EXECUCAO_REAL=1</c>); caso contrário, o simulado (dry-run), que é
    /// o padrão seguro do projeto.
    /// </summary>
    public static IEstadoSistema Selecionar(ILogger? logger = null)
    {
        var log = logger ?? NullLogger.Instance;
        if (OperatingSystem.IsWindows() && ExecucaoRealHabilitada())
        {
            log.LogWarning(
                "Execução REAL no Windows habilitada (HWOPT_EXECUCAO_REAL): as ações aprovadas alterarão o sistema.");
            return new EstadoSistemaWindows(new AcessoRegistroWindows(), new ExecutorProcesso(), log);
        }

        log.LogInformation("Execução em modo SIMULADO (dry-run): nenhuma alteração real será feita.");
        return new EstadoSistemaSimulado();
    }

    internal static bool ExecucaoRealHabilitada()
    {
        var valor = Environment.GetEnvironmentVariable("HWOPT_EXECUCAO_REAL");
        return valor is "1" || string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase);
    }

    private IAlvoWindows Mapear(string alvo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alvo);

        var separador = alvo.IndexOf(':', StringComparison.Ordinal);
        if (separador <= 0 || separador == alvo.Length - 1)
        {
            throw NaoMapeado(alvo);
        }

        var tipo = alvo[..separador];
        var chave = alvo[(separador + 1)..];

        return tipo switch
        {
            "registro" => MapearRegistro(chave),
            "powercfg" => MapearPowercfg(chave),
            "servico" => new AlvoServico(_processo, chave, _log),
            _ => throw NaoMapeado(alvo),
        };
    }

    private IAlvoWindows MapearRegistro(string nome) => nome switch
    {
        "VisualFXSetting" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", nome, TraduzirVisualFx),
        "SystemResponsiveness" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, MultimediaSystemProfile, nome, TraduzirNumero),
        "NetworkThrottlingIndex" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, MultimediaSystemProfile, nome, TraduzirNumero),
        "TdrDelay" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, GraphicsDrivers, nome, TraduzirNumero),
        "HwSchMode" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, GraphicsDrivers, nome, TraduzirNumero),
        _ => throw NaoMapeado("registro:" + nome),
    };

    private IAlvoWindows MapearPowercfg(string chave) => chave switch
    {
        "plano_ativo" => new AlvoPlanoEnergia(_processo),
        "usb_suspensao_seletiva" => new AlvoUsbSuspensao(_processo),
        _ => throw NaoMapeado("powercfg:" + chave),
    };

    // Valores numéricos do comando podem vir em decimal ("20") ou hexadecimal
    // ("ffffffff"); a leitura do registro devolve sempre decimal (round-trip).
    internal static uint TraduzirNumero(string valor)
    {
        if (uint.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalValor))
        {
            return decimalValor;
        }

        if (uint.TryParse(valor, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValor))
        {
            return hexValor;
        }

        throw new FormatException($"Valor numérico inválido para o registro: '{valor}'.");
    }

    internal static uint TraduzirVisualFx(string valor) => valor.ToUpperInvariant() switch
    {
        "DESEMPENHO" => 2,  // "Ajustar para obter um melhor desempenho"
        "APARENCIA" => 1,   // "Ajustar para obter uma melhor aparência"
        "AUTOMATICO" or "PADRAO" => 0,
        _ => TraduzirNumero(valor),
    };

    private static InvalidOperationException FalhaProcesso(string comando, ResultadoProcesso resultado) =>
        new($"Comando '{comando}' falhou (código {resultado.CodigoSaida}). {resultado.SaidaErro}".Trim());

    private static NotSupportedException NaoMapeado(string alvo) =>
        new($"Alvo do estado do sistema não mapeado para Windows: '{alvo}'.");

    /// <summary>Estratégia de um alvo concreto (registro, plano, serviço).</summary>
    private interface IAlvoWindows
    {
        string? Ler();
        void Escrever(string valor);
        void Restaurar(string? valorAnterior);
    }

    /// <summary>Valor DWORD do registro, com tradução simbólica → numérica.</summary>
    private sealed class AlvoRegistroDword : IAlvoWindows
    {
        private readonly IAcessoRegistro _registro;
        private readonly ColmeiaRegistro _colmeia;
        private readonly string _subchave;
        private readonly string _nome;
        private readonly Func<string, uint> _traduzir;

        public AlvoRegistroDword(
            IAcessoRegistro registro, ColmeiaRegistro colmeia, string subchave, string nome, Func<string, uint> traduzir)
        {
            _registro = registro;
            _colmeia = colmeia;
            _subchave = subchave;
            _nome = nome;
            _traduzir = traduzir;
        }

        public string? Ler() =>
            _registro.LerDword(_colmeia, _subchave, _nome)?.ToString(CultureInfo.InvariantCulture);

        public void Escrever(string valor) =>
            _registro.EscreverDword(_colmeia, _subchave, _nome, _traduzir(valor));

        public void Restaurar(string? valorAnterior)
        {
            if (valorAnterior is null)
            {
                _registro.RemoverValor(_colmeia, _subchave, _nome);
            }
            else
            {
                _registro.EscreverDword(_colmeia, _subchave, _nome, _traduzir(valorAnterior));
            }
        }
    }

    /// <summary>Plano de energia ativo (powercfg /getactivescheme · /setactive).</summary>
    private sealed class AlvoPlanoEnergia : IAlvoWindows
    {
        private readonly IExecutorProcesso _processo;

        public AlvoPlanoEnergia(IExecutorProcesso processo) => _processo = processo;

        public string? Ler()
        {
            var resultado = _processo.Executar("powercfg", new[] { "/getactivescheme" });
            return ExtrairGuid(resultado.SaidaPadrao);
        }

        public void Escrever(string valor) => Aplicar(
            valor.Equals("ALTO_DESEMPENHO", StringComparison.OrdinalIgnoreCase) ? GuidAltoDesempenho : valor);

        public void Restaurar(string? valorAnterior)
        {
            if (!string.IsNullOrWhiteSpace(valorAnterior))
            {
                Aplicar(valorAnterior);
            }
        }

        private void Aplicar(string guid)
        {
            var resultado = _processo.Executar("powercfg", new[] { "/setactive", guid });
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso("powercfg /setactive", resultado);
            }
        }

        // Locale-independente: o primeiro token no formato GUID "D" é o plano ativo.
        internal static string? ExtrairGuid(string saida)
        {
            var separadores = new[] { ' ', '\t', '\r', '\n', ':', '(', ')' };
            foreach (var token in saida.Split(separadores, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Guid.TryParseExact(token, "D", out var guid))
                {
                    return guid.ToString("D", CultureInfo.InvariantCulture);
                }
            }

            return null;
        }
    }

    /// <summary>Suspensão seletiva de USB (índice 0/1 no esquema atual).</summary>
    private sealed class AlvoUsbSuspensao : IAlvoWindows
    {
        private readonly IExecutorProcesso _processo;

        public AlvoUsbSuspensao(IExecutorProcesso processo) => _processo = processo;

        public string? Ler()
        {
            var resultado = _processo.Executar(
                "powercfg", new[] { "/query", "SCHEME_CURRENT", SubgrupoUsb, ConfigUsbSuspensao });
            return ExtrairIndice(resultado.SaidaPadrao);
        }

        public void Escrever(string valor) => Aplicar(valor.ToUpperInvariant() switch
        {
            "DESABILITADO" => 0u,
            "HABILITADO" => 1u,
            _ => TraduzirNumero(valor),
        });

        public void Restaurar(string? valorAnterior)
        {
            if (!string.IsNullOrWhiteSpace(valorAnterior))
            {
                Aplicar(TraduzirNumero(valorAnterior));
            }
        }

        private void Aplicar(uint indice)
        {
            var texto = indice.ToString(CultureInfo.InvariantCulture);
            Exec("/setacvalueindex", "SCHEME_CURRENT", SubgrupoUsb, ConfigUsbSuspensao, texto);
            Exec("/setdcvalueindex", "SCHEME_CURRENT", SubgrupoUsb, ConfigUsbSuspensao, texto);
            Exec("/setactive", "SCHEME_CURRENT");
        }

        private void Exec(params string[] argumentos)
        {
            var resultado = _processo.Executar("powercfg", argumentos);
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso("powercfg " + string.Join(' ', argumentos), resultado);
            }
        }

        // Pega o primeiro "0x..." da saída (índice CA/AC), independente do idioma.
        internal static string? ExtrairIndice(string saida)
        {
            var inicio = saida.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            if (inicio < 0)
            {
                return null;
            }

            var fim = inicio + 2;
            while (fim < saida.Length && Uri.IsHexDigit(saida[fim]))
            {
                fim++;
            }

            var hex = saida[(inicio + 2)..fim];
            return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var valor)
                ? valor.ToString(CultureInfo.InvariantCulture)
                : null;
        }
    }

    /// <summary>Modo de início de um serviço (sc.exe qc · config · stop).</summary>
    private sealed class AlvoServico : IAlvoWindows
    {
        private readonly IExecutorProcesso _processo;
        private readonly string _servico;
        private readonly ILogger _log;

        public AlvoServico(IExecutorProcesso processo, string servico, ILogger log)
        {
            _processo = processo;
            _servico = servico;
            _log = log;
        }

        public string? Ler()
        {
            var resultado = _processo.Executar("sc", new[] { "qc", _servico });
            return resultado.Sucesso ? InterpretarStartType(resultado.SaidaPadrao) : null;
        }

        public void Escrever(string valor)
        {
            var modo = NormalizarModo(valor);
            var resultado = _processo.Executar("sc", new[] { "config", _servico, "start=", modo });
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso($"sc config {_servico}", resultado);
            }

            if (modo == "disabled")
            {
                // Melhor esforço: para o serviço agora. O rollback restaura o tipo
                // de início (o estado em execução é retomado no próximo boot).
                var parada = _processo.Executar("sc", new[] { "stop", _servico });
                if (!parada.Sucesso)
                {
                    _log.LogDebug(
                        "sc stop {Servico} retornou {Codigo} (o serviço já pode estar parado).",
                        _servico, parada.CodigoSaida);
                }
            }
        }

        public void Restaurar(string? valorAnterior)
        {
            var modo = string.IsNullOrWhiteSpace(valorAnterior) ? "demand" : NormalizarModo(valorAnterior);
            var resultado = _processo.Executar("sc", new[] { "config", _servico, "start=", modo });
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso($"sc config {_servico} (restauração)", resultado);
            }
        }

        // Mapeia a linha "START_TYPE : N XXX_START" para o vocabulário do sc config.
        internal static string InterpretarStartType(string saida)
        {
            foreach (var linha in saida.Split('\n'))
            {
                if (linha.IndexOf("START_TYPE", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var texto = linha.ToUpperInvariant();
                if (texto.Contains("DISABLED", StringComparison.Ordinal)) return "disabled";
                if (texto.Contains("DELAYED", StringComparison.Ordinal)) return "delayed-auto";
                if (texto.Contains("AUTO_START", StringComparison.Ordinal)) return "auto";
                if (texto.Contains("DEMAND_START", StringComparison.Ordinal)) return "demand";
                if (texto.Contains("SYSTEM_START", StringComparison.Ordinal)) return "system";
                if (texto.Contains("BOOT_START", StringComparison.Ordinal)) return "boot";
            }

            return "demand"; // padrão conservador quando não foi possível interpretar
        }

        internal static string NormalizarModo(string valor) => valor.Trim().ToUpperInvariant() switch
        {
            "DISABLED" => "disabled",
            "AUTO" or "AUTOMATIC" or "AUTO_START" => "auto",
            "DELAYED-AUTO" or "DELAYED" => "delayed-auto",
            "DEMAND" or "MANUAL" or "DEMAND_START" => "demand",
            "SYSTEM" or "SYSTEM_START" => "system",
            "BOOT" or "BOOT_START" => "boot",
            _ => "demand",
        };
    }
}
````

### `src/HardwareOptimizer.Agent/Persistence/IRepositorioOtimizacao.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Persistence;

/// <summary>
/// Persistência local (SQLite): inventário, auditoria de consentimento e
/// histórico de execução. A auditoria de consentimento é exigência do documento
/// para rastreabilidade.
/// </summary>
public interface IRepositorioOtimizacao
{
    Task InicializarAsync(CancellationToken cancellationToken = default);

    Task<long> SalvarInventarioAsync(Inventario inventario, CancellationToken cancellationToken = default);

    Task<long> RegistrarConsentimentoAsync(
        RegistroConsentimento registro, CancellationToken cancellationToken = default);

    Task<long> RegistrarExecucaoAsync(
        RelatorioExecucao relatorio, CancellationToken cancellationToken = default);

    Task<long> ContarInventariosAsync(CancellationToken cancellationToken = default);

    Task<long> ContarConsentimentosAsync(CancellationToken cancellationToken = default);

    Task<long> ContarExecucoesAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera o JSON de informação de BIOS cacheado para a chave, ou nulo.</summary>
    Task<string?> ObterCacheBiosAsync(string chaveBusca, CancellationToken cancellationToken = default);

    Task SalvarCacheBiosAsync(string chaveBusca, string dadosJson, CancellationToken cancellationToken = default);
}
````

### `src/HardwareOptimizer.Agent/Persistence/RepositorioSqlite.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Persistence;

/// <summary>Repositório SQLite. Abre uma conexão por operação a partir da connection string.</summary>
public sealed class RepositorioSqlite : IRepositorioOtimizacao
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string _connectionString;
    private readonly ILogger _log;

    public RepositorioSqlite(string connectionString, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _log = logger ?? NullLogger.Instance;
    }

    /// <summary>Cria um repositório apontando para um arquivo de banco local.</summary>
    public static RepositorioSqlite DeArquivo(string caminhoArquivo, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoArquivo);
        var diretorio = Path.GetDirectoryName(Path.GetFullPath(caminhoArquivo));
        if (!string.IsNullOrEmpty(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        return new RepositorioSqlite($"Data Source={caminhoArquivo}", logger);
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        _log.LogDebug("Inicializando esquema do banco SQLite ('{ConnectionString}').", _connectionString);
        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS inventarios (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                coletado_em TEXT NOT NULL,
                dados_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS consentimentos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome_perfil TEXT NOT NULL,
                versao_catalogo TEXT NOT NULL,
                registrado_em TEXT NOT NULL,
                checkboxes_json TEXT NOT NULL,
                valores_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS execucoes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                perfil_nome TEXT NOT NULL,
                sucesso INTEGER NOT NULL,
                executado_em TEXT NOT NULL,
                relatorio_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cache_bios (
                chave_busca TEXT PRIMARY KEY,
                dados_json TEXT NOT NULL,
                atualizado_em TEXT NOT NULL
            );
            """;
        await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> SalvarInventarioAsync(
        Inventario inventario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        _log.LogDebug("Persistindo inventário coletado em {Quando}.", inventario.ColetadoEm);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO inventarios (coletado_em, dados_json)
            VALUES ($coletado, $dados);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$coletado", inventario.ColetadoEm.ToString("O"));
        comando.Parameters.AddWithValue("$dados", JsonSerializer.Serialize(inventario, Json));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<long> RegistrarConsentimentoAsync(
        RegistroConsentimento registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);
        _log.LogInformation(
            "Registrando consentimento (auditoria): perfil '{Perfil}', catálogo {Versao}.",
            registro.NomePerfil, registro.VersaoCatalogo);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO consentimentos
                (nome_perfil, versao_catalogo, registrado_em, checkboxes_json, valores_json)
            VALUES ($perfil, $versao, $em, $checkboxes, $valores);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$perfil", registro.NomePerfil);
        comando.Parameters.AddWithValue("$versao", registro.VersaoCatalogo);
        comando.Parameters.AddWithValue("$em", registro.RegistradoEm.ToString("O"));
        comando.Parameters.AddWithValue("$checkboxes", JsonSerializer.Serialize(registro.CheckboxesMarcados, Json));
        comando.Parameters.AddWithValue("$valores", JsonSerializer.Serialize(registro.ValoresEscolhidos, Json));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<long> RegistrarExecucaoAsync(
        RelatorioExecucao relatorio, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relatorio);
        _log.LogInformation(
            "Registrando execução: perfil '{Perfil}', sucesso={Sucesso}.", relatorio.PerfilNome, relatorio.Sucesso);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO execucoes (perfil_nome, sucesso, executado_em, relatorio_json)
            VALUES ($perfil, $sucesso, $em, $relatorio);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$perfil", relatorio.PerfilNome);
        comando.Parameters.AddWithValue("$sucesso", relatorio.Sucesso ? 1 : 0);
        comando.Parameters.AddWithValue("$em", DateTimeOffset.UtcNow.ToString("O"));
        comando.Parameters.AddWithValue("$relatorio", JsonSerializer.Serialize(relatorio, Json));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<string?> ObterCacheBiosAsync(
        string chaveBusca, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveBusca);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT dados_json FROM cache_bios WHERE chave_busca = $chave;";
        comando.Parameters.AddWithValue("$chave", chaveBusca);
        var resultado = await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return resultado as string;
    }

    public async Task SalvarCacheBiosAsync(
        string chaveBusca, string dadosJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveBusca);
        ArgumentNullException.ThrowIfNull(dadosJson);

        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO cache_bios (chave_busca, dados_json, atualizado_em)
            VALUES ($chave, $dados, $em)
            ON CONFLICT(chave_busca) DO UPDATE SET dados_json = $dados, atualizado_em = $em;
            """;
        comando.Parameters.AddWithValue("$chave", chaveBusca);
        comando.Parameters.AddWithValue("$dados", dadosJson);
        comando.Parameters.AddWithValue("$em", DateTimeOffset.UtcNow.ToString("O"));
        await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<long> ContarInventariosAsync(CancellationToken cancellationToken = default) =>
        ContarAsync("inventarios", cancellationToken);

    public Task<long> ContarConsentimentosAsync(CancellationToken cancellationToken = default) =>
        ContarAsync("consentimentos", cancellationToken);

    public Task<long> ContarExecucoesAsync(CancellationToken cancellationToken = default) =>
        ContarAsync("execucoes", cancellationToken);

    private async Task<long> ContarAsync(string tabela, CancellationToken cancellationToken)
    {
        // 'tabela' provém apenas de chamadas internas com nomes fixos (sem entrada do usuário).
        await using var conexao = await AbrirAsync(cancellationToken).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabela};";
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<SqliteConnection> AbrirAsync(CancellationToken cancellationToken)
    {
        var conexao = new SqliteConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conexao;
    }
}
````

### `src/HardwareOptimizer.Agent/Platform/AcessoRegistroWindows.cs`

````csharp
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace HardwareOptimizer.Agent.Platform;

/// <summary>
/// Implementação real de <see cref="IAcessoRegistro"/> sobre
/// <see cref="Microsoft.Win32.Registry"/>. Só é instanciada sob Windows (ver
/// <see cref="Execution.Windows.EstadoSistemaWindows.Selecionar"/>), por isso a
/// anotação de plataforma.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AcessoRegistroWindows : IAcessoRegistro
{
    public uint? LerDword(ColmeiaRegistro colmeia, string subchave, string nome)
    {
        using var chave = BaseDe(colmeia).OpenSubKey(subchave, writable: false);
        var valor = chave?.GetValue(nome);

        // Um DWORD volta como int (boxed). 0xFFFFFFFF chega como -1; converte sem
        // perda para uint mantendo a semântica de 32 bits.
        return valor is null
            ? null
            : unchecked((uint)Convert.ToInt64(valor, CultureInfo.InvariantCulture));
    }

    public void EscreverDword(ColmeiaRegistro colmeia, string subchave, string nome, uint valor)
    {
        using var chave = BaseDe(colmeia).CreateSubKey(subchave, writable: true);
        chave.SetValue(nome, unchecked((int)valor), RegistryValueKind.DWord);
    }

    public void RemoverValor(ColmeiaRegistro colmeia, string subchave, string nome)
    {
        using var chave = BaseDe(colmeia).OpenSubKey(subchave, writable: true);
        chave?.DeleteValue(nome, throwOnMissingValue: false);
    }

    private static RegistryKey BaseDe(ColmeiaRegistro colmeia) => colmeia switch
    {
        ColmeiaRegistro.LocalMachine => Registry.LocalMachine,
        ColmeiaRegistro.CurrentUser => Registry.CurrentUser,
        _ => throw new ArgumentOutOfRangeException(nameof(colmeia), colmeia, "Colmeia não suportada."),
    };
}
````

### `src/HardwareOptimizer.Agent/Platform/ExecutorProcesso.cs`

````csharp
using System.Diagnostics;

namespace HardwareOptimizer.Agent.Platform;

/// <summary>
/// Implementação real de <see cref="IExecutorProcesso"/> sobre
/// <see cref="Process"/>. Captura stdout/stderr sem risco de deadlock (leituras
/// assíncronas + espera com tempo limite) — os utilitários alvo (powercfg,
/// sc.exe) produzem saída pequena e terminam rápido.
/// </summary>
public sealed class ExecutorProcesso : IExecutorProcesso
{
    private readonly TimeSpan _tempoLimite;

    public ExecutorProcesso(TimeSpan? tempoLimite = null) =>
        _tempoLimite = tempoLimite ?? TimeSpan.FromSeconds(30);

    public ResultadoProcesso Executar(string arquivo, IReadOnlyList<string> argumentos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivo);
        ArgumentNullException.ThrowIfNull(argumentos);

        var inicio = new ProcessStartInfo
        {
            FileName = arquivo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argumento in argumentos)
        {
            inicio.ArgumentList.Add(argumento);
        }

        using var processo = Process.Start(inicio)
            ?? throw new InvalidOperationException($"Não foi possível iniciar o processo '{arquivo}'.");

        // Lê de forma assíncrona para não bloquear caso ambos os fluxos encham.
        var leituraSaida = processo.StandardOutput.ReadToEndAsync();
        var leituraErro = processo.StandardError.ReadToEndAsync();

        if (!processo.WaitForExit((int)_tempoLimite.TotalMilliseconds))
        {
            try
            {
                if (!processo.HasExited)
                {
                    processo.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // O processo terminou entre a verificação e o Kill — nada a fazer.
            }

            throw new TimeoutException($"Processo '{arquivo}' excedeu o tempo limite de {_tempoLimite}.");
        }

        // Garante que as leituras assíncronas concluíram após a saída do processo.
        processo.WaitForExit();
        return new ResultadoProcesso(
            processo.ExitCode,
            leituraSaida.GetAwaiter().GetResult(),
            leituraErro.GetAwaiter().GetResult());
    }
}
````

### `src/HardwareOptimizer.Agent/Platform/IAcessoRegistro.cs`

````csharp
namespace HardwareOptimizer.Agent.Platform;

/// <summary>Colmeia (hive) do registro do Windows.</summary>
public enum ColmeiaRegistro
{
    /// <summary>HKEY_LOCAL_MACHINE (configurações do sistema; exige elevação).</summary>
    LocalMachine,

    /// <summary>HKEY_CURRENT_USER (configurações do usuário atual).</summary>
    CurrentUser,
}

/// <summary>
/// Porta para o registro do Windows, restrita a valores DWORD (suficiente para o
/// catálogo atual). Abstraída para que a lógica de <see cref="Execution.Windows.EstadoSistemaWindows"/>
/// seja testável em qualquer plataforma com um fake, sem tocar o registro real.
/// </summary>
public interface IAcessoRegistro
{
    /// <summary>Lê um valor DWORD; nulo se a chave ou o valor não existir.</summary>
    uint? LerDword(ColmeiaRegistro colmeia, string subchave, string nome);

    /// <summary>Escreve um valor DWORD, criando a subchave se necessário.</summary>
    void EscreverDword(ColmeiaRegistro colmeia, string subchave, string nome, uint valor);

    /// <summary>Remove um valor (sem erro se ausente) — usado no rollback para "não definido".</summary>
    void RemoverValor(ColmeiaRegistro colmeia, string subchave, string nome);
}
````

### `src/HardwareOptimizer.Agent/Platform/IExecutorProcesso.cs`

````csharp
namespace HardwareOptimizer.Agent.Platform;

/// <summary>
/// Resultado da execução de um processo externo (powercfg, sc.exe).
/// </summary>
public sealed record ResultadoProcesso(int CodigoSaida, string SaidaPadrao, string SaidaErro)
{
    public bool Sucesso => CodigoSaida == 0;
}

/// <summary>
/// Porta para executar utilitários do sistema (powercfg, sc.exe). Síncrona para
/// casar com o contrato síncrono de <see cref="Execution.IEstadoSistema"/>;
/// abstraída para permitir fakes nos testes (sem tocar o sistema real).
/// </summary>
public interface IExecutorProcesso
{
    ResultadoProcesso Executar(string arquivo, IReadOnlyList<string> argumentos);
}
````

### `src/HardwareOptimizer.Agent/Sensors/FonteSensoresLhm.cs`

````csharp
using System.ComponentModel;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Contracts;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Implementação real de <see cref="IFonteSensoresLhm"/> sobre
/// LibreHardwareMonitorLib. Abre o <see cref="Computer"/> uma vez (carrega o
/// driver de kernel assinado — atentar a Secure Boot/elevação) e, a cada leitura,
/// atualiza o hardware e projeta os sensores suportados no contrato do domínio.
/// Defensiva: falhas de driver/permissão viram leitura parcial/vazia, nunca
/// exceção que derrube o serviço de sensores.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FonteSensoresLhm : IFonteSensoresLhm, IDisposable
{
    private readonly Computer _computer;
    private readonly ILogger _log;
    private bool _aberto;

    public FonteSensoresLhm(ILogger? logger = null)
    {
        _log = logger ?? NullLogger.Instance;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
        };
    }

    public IReadOnlyList<Sensor> Ler()
    {
        var sensores = new List<Sensor>();
        try
        {
            if (!_aberto)
            {
                _computer.Open();
                _aberto = true;
            }

            foreach (var hardware in _computer.Hardware)
            {
                ColetarHardware(hardware, sensores);
            }
        }
        catch (Exception ex) when (
            ex is Win32Exception or DllNotFoundException or UnauthorizedAccessException
               or InvalidOperationException or IOException or BadImageFormatException)
        {
            _log.LogWarning(ex, "Falha ao ler sensores via LibreHardwareMonitor (driver/elevação?).");
        }

        return sensores;
    }

    private static void ColetarHardware(IHardware hardware, List<Sensor> destino)
    {
        hardware.Update();

        foreach (var sub in hardware.SubHardware)
        {
            ColetarHardware(sub, destino);
        }

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not { } valor || MapearTipo(sensor.SensorType) is not { } tipo)
            {
                continue;
            }

            destino.Add(new Sensor
            {
                Nome = $"{hardware.Name} / {sensor.Name}",
                Tipo = tipo,
                Valor = Math.Round((double)valor, 2),
                Unidade = UnidadeDe(tipo),
            });
        }
    }

    private static TipoSensor? MapearTipo(SensorType tipo) => tipo switch
    {
        SensorType.Temperature => TipoSensor.Temperatura,
        SensorType.Clock => TipoSensor.Clock,
        SensorType.Voltage => TipoSensor.Voltagem,
        SensorType.Fan => TipoSensor.Fan,
        SensorType.Power => TipoSensor.Potencia,
        _ => null,
    };

    private static string UnidadeDe(TipoSensor tipo) => tipo switch
    {
        TipoSensor.Temperatura => "°C",
        TipoSensor.Clock => "MHz",
        TipoSensor.Voltagem => "V",
        TipoSensor.Fan => "RPM",
        TipoSensor.Potencia => "W",
        _ => string.Empty,
    };

    public void Dispose()
    {
        if (!_aberto)
        {
            return;
        }

        try
        {
            _computer.Close();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _log.LogDebug(ex, "Falha ao fechar o LibreHardwareMonitor.");
        }
        finally
        {
            _aberto = false;
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Sensors/IFonteSensoresLhm.cs`

````csharp
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Fonte de leituras de sensores via LibreHardwareMonitor. Abstrai a biblioteca
/// (e o driver de kernel) por trás de uma única chamada, tornando
/// <see cref="LeitorSensoresLhm"/> testável fora do Windows com um fake.
/// A implementação real é defensiva: nunca lança, devolve o que conseguiu ler.
/// </summary>
public interface IFonteSensoresLhm
{
    IReadOnlyList<Sensor> Ler();
}
````

### `src/HardwareOptimizer.Agent/Sensors/ILeitorSensores.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores específico de plataforma. Read-only: nunca modifica o
/// sistema. No Windows, a implementação de produção usa um driver de kernel
/// assinado (LibreHardwareMonitor) — atentar a Secure Boot.
/// </summary>
public interface ILeitorSensores
{
    SistemaOperacionalTipo Tipo { get; }

    Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default);
}
````

### `src/HardwareOptimizer.Agent/Sensors/LeitorSensoresComposto.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Encadeia leitores de sensores e devolve a primeira leitura **com dados**,
/// permitindo degradação graciosa: no Windows tenta o LibreHardwareMonitor (rico,
/// requer driver/elevação) e, se vier vazio, recai sobre o WMI (temperatura, sem
/// elevação). Read-only, como todos os leitores.
/// </summary>
public sealed class LeitorSensoresComposto : ILeitorSensores
{
    private readonly IReadOnlyList<ILeitorSensores> _leitores;
    private readonly ILogger _log;

    public LeitorSensoresComposto(IReadOnlyList<ILeitorSensores> leitores, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(leitores);
        if (leitores.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um leitor.", nameof(leitores));
        }

        _leitores = leitores;
        _log = logger ?? NullLogger.Instance;
    }

    public SistemaOperacionalTipo Tipo => _leitores[0].Tipo;

    public async Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        foreach (var leitor in _leitores)
        {
            var leitura = await leitor.LerAsync(cancellationToken).ConfigureAwait(false);
            if (leitura.Sensores.Count > 0)
            {
                return leitura;
            }

            _log.LogDebug("Leitor {Leitor} sem dados; tentando o próximo.", leitor.GetType().Name);
        }

        _log.LogWarning("Nenhum leitor de sensores retornou dados.");
        return new LeituraSensores();
    }
}
````

### `src/HardwareOptimizer.Agent/Sensors/LeitorSensoresLhm.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores baseado em LibreHardwareMonitor (clock, voltagem, fan,
/// consumo e temperatura por componente). Opera sobre a <see cref="IFonteSensoresLhm"/>,
/// o que mantém esta lógica (filtragem e empacotamento) testável em qualquer
/// plataforma; a fonte real só roda sob Windows com o driver carregado.
/// </summary>
public sealed class LeitorSensoresLhm : ILeitorSensores
{
    private readonly IFonteSensoresLhm _fonte;
    private readonly ILogger _log;

    public LeitorSensoresLhm(IFonteSensoresLhm fonte, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        _fonte = fonte;
        _log = logger ?? NullLogger.Instance;
    }

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo sensores via LibreHardwareMonitor.");

        // Descarta valores não finitos (NaN/Infinito) e leituras indisponíveis:
        // temperatura/clock em 0 indicam sensor não lido (típico da CPU sem o
        // driver/elevação — leituras de MSR exigem Ring0). Tensão, fan e potência
        // em 0 são válidos e permanecem.
        var validos = _fonte.Ler()
            .Where(s => double.IsFinite(s.Valor) && !LeituraIndisponivel(s))
            .ToList();

        if (validos.Count == 0)
        {
            _log.LogWarning(
                "LibreHardwareMonitor não retornou sensores (driver ausente ou sem elevação?).");
        }
        else
        {
            _log.LogDebug("LibreHardwareMonitor: {Qtd} sensor(es) válido(s).", validos.Count);
        }

        return Task.FromResult(new LeituraSensores { Sensores = validos });
    }

    // Temperatura/clock ≤ 0 = sensor indisponível (não lido). Outros tipos podem
    // legitimamente valer 0 (ex.: fan parado, consumo ocioso).
    private static bool LeituraIndisponivel(Sensor sensor) =>
        sensor.Tipo is TipoSensor.Temperatura or TipoSensor.Clock && sensor.Valor <= 0;
}
````

### `src/HardwareOptimizer.Agent/Sensors/LeitorSensoresLinux.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores para Linux. Lê os pseudo-arquivos de /sys/class/hwmon
/// (temperatura, fan, voltagem, consumo) e a frequência atual da CPU em
/// /sys/devices/system/cpu. Os caminhos-base são injetáveis para teste.
/// </summary>
public sealed class LeitorSensoresLinux : ILeitorSensores
{
    private readonly string _baseHwmon;
    private readonly string _baseCpu;
    private readonly ILogger _log;

    public LeitorSensoresLinux(string? baseHwmon = null, string? baseCpu = null, ILogger? logger = null)
    {
        _baseHwmon = baseHwmon ?? "/sys/class/hwmon";
        _baseCpu = baseCpu ?? "/sys/devices/system/cpu";
        _log = logger ?? NullLogger.Instance;
    }

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Linux;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sensores = new List<Sensor>();
        LerHwmon(sensores);
        LerClockCpu(sensores);

        if (sensores.Count == 0)
        {
            _log.LogWarning("Nenhum sensor legível em '{Base}' (driver/permissão ausente?).", _baseHwmon);
        }

        return Task.FromResult(new LeituraSensores { Sensores = sensores });
    }

    private void LerHwmon(List<Sensor> sensores)
    {
        foreach (var dir in EnumerarDiretorios(_baseHwmon))
        {
            var chip = LerTexto(Path.Combine(dir, "name")) ?? Path.GetFileName(dir);

            foreach (var arquivo in EnumerarArquivos(dir))
            {
                var nome = Path.GetFileName(arquivo);
                if (!nome.EndsWith("_input", StringComparison.Ordinal))
                {
                    continue;
                }

                var chave = nome[..^"_input".Length];
                var (prefixo, indice) = SepararPrefixoIndice(chave);
                var mapeamento = Mapear(prefixo);
                if (mapeamento is null)
                {
                    continue;
                }

                var bruto = LerNumero(arquivo);
                if (bruto is not { } valorBruto)
                {
                    continue;
                }

                var (tipo, unidade, fator) = mapeamento.Value;
                var rotulo = LerTexto(Path.Combine(dir, $"{prefixo}{indice}_label"));

                sensores.Add(new Sensor
                {
                    Nome = rotulo ?? $"{chip} {prefixo}{indice}",
                    Tipo = tipo,
                    Valor = Math.Round(valorBruto * fator, 2),
                    Unidade = unidade,
                });
            }
        }
    }

    private void LerClockCpu(List<Sensor> sensores)
    {
        var raizCpu = Path.Combine(_baseCpu);
        double? maiorKHz = null;
        foreach (var dir in EnumerarDiretorios(raizCpu))
        {
            var nome = Path.GetFileName(dir);
            if (!nome.StartsWith("cpu", StringComparison.Ordinal)
                || !nome[3..].All(char.IsDigit) || nome.Length == 3)
            {
                continue;
            }

            var freq = LerNumero(Path.Combine(dir, "cpufreq", "scaling_cur_freq"));
            if (freq is { } khz && (maiorKHz is null || khz > maiorKHz))
            {
                maiorKHz = khz;
            }
        }

        if (maiorKHz is { } maior)
        {
            sensores.Add(new Sensor
            {
                Nome = "CPU (clock atual máx.)",
                Tipo = TipoSensor.Clock,
                Valor = Math.Round(maior / 1000.0, 0),
                Unidade = "MHz",
            });
        }
    }

    private static (TipoSensor Tipo, string Unidade, double Fator)? Mapear(string prefixo) => prefixo switch
    {
        "temp" => (TipoSensor.Temperatura, "°C", 0.001),
        "fan" => (TipoSensor.Fan, "RPM", 1.0),
        "in" => (TipoSensor.Voltagem, "V", 0.001),
        "power" => (TipoSensor.Potencia, "W", 0.000001),
        _ => null,
    };

    private static (string Prefixo, string Indice) SepararPrefixoIndice(string chave)
    {
        var i = 0;
        while (i < chave.Length && !char.IsDigit(chave[i]))
        {
            i++;
        }

        return (chave[..i], chave[i..]);
    }

    private double? LerNumero(string caminho)
    {
        var texto = LerTexto(caminho);
        return texto is not null
            && double.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private string? LerTexto(string caminho)
    {
        try
        {
            return File.Exists(caminho) ? File.ReadAllText(caminho).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerarDiretorios(string caminho)
    {
        try
        {
            return Directory.Exists(caminho) ? Directory.EnumerateDirectories(caminho) : Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerarArquivos(string caminho)
    {
        try
        {
            return Directory.EnumerateFiles(caminho);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Sensors/LeitorSensoresWindows.cs`

````csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Leitor de sensores para Windows. Lê a temperatura via WMI
/// (MSAcpi_ThermalZoneTemperature) por PowerShell, em modo somente leitura. Para
/// dados ricos (clock, voltagem, fan, consumo por componente), a implementação
/// de produção usa LibreHardwareMonitorLib (driver de kernel assinado; atentar a
/// Secure Boot). Defensivo: falhas resultam em leitura vazia, nunca em exceção.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorSensoresWindows : ILeitorSensores
{
    private readonly ILogger _log;

    public LeitorSensoresWindows(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo temperatura via WMI (MSAcpi_ThermalZoneTemperature).");

        var sensores = new List<Sensor>();
        var indice = 1;
        foreach (var decimosKelvin in LerTemperaturas())
        {
            var celsius = Math.Round((decimosKelvin / 10.0) - 273.15, 1);
            sensores.Add(new Sensor
            {
                Nome = $"Zona térmica {indice++}",
                Tipo = TipoSensor.Temperatura,
                Valor = celsius,
                Unidade = "°C",
            });
        }

        if (sensores.Count == 0)
        {
            _log.LogWarning("Nenhuma zona térmica WMI legível (use LibreHardwareMonitor para dados completos).");
        }

        return Task.FromResult(new LeituraSensores { Sensores = sensores });
    }

    private IEnumerable<double> LerTemperaturas()
    {
        var saida = ExecutarPowerShell(
            "Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature "
            + "| Select-Object CurrentTemperature | ConvertTo-Json -Compress");
        if (string.IsNullOrWhiteSpace(saida))
        {
            yield break;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(saida);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (ExtrairTemperatura(item) is { } valor)
                    {
                        yield return valor;
                    }
                }
            }
            else if (ExtrairTemperatura(doc.RootElement) is { } unica)
            {
                yield return unica;
            }
        }
    }

    private static double? ExtrairTemperatura(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty("CurrentTemperature", out var prop)
        && prop.ValueKind == JsonValueKind.Number
        && prop.TryGetDouble(out var valor)
            ? valor
            : null;

    private string? ExecutarPowerShell(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{comando}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(psi);
            if (processo is null)
            {
                return null;
            }

            var saida = processo.StandardOutput.ReadToEnd();
            return processo.WaitForExit(20_000) ? saida.Trim() : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
````

### `src/HardwareOptimizer.Agent/Sensors/ServicoSensores.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Serviço de sensores: delega ao leitor da plataforma corrente, escolhido
/// automaticamente quando nenhum é informado. Leitura em tempo real.
/// </summary>
public sealed class ServicoSensores
{
    private readonly ILeitorSensores _leitor;
    private readonly ILogger _log;

    public ServicoSensores(ILeitorSensores? leitor = null, ILoggerFactory? loggerFactory = null)
    {
        var fabrica = loggerFactory ?? NullLoggerFactory.Instance;
        _log = fabrica.CreateLogger<ServicoSensores>();
        _leitor = leitor ?? CriarLeitorPadrao(fabrica);
    }

    public async Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        _log.LogInformation("Lendo sensores (plataforma {Plataforma}).", _leitor.Tipo);

        var leitura = await _leitor.LerAsync(cancellationToken).ConfigureAwait(false);

        _log.LogInformation(
            "Sensores: {Qtd} leitura(s); temperatura máx {Temp}.",
            leitura.Sensores.Count,
            leitura.TemperaturaMaxC?.ToString("0.0", CultureInfo.InvariantCulture) ?? "n/d");

        return leitura;
    }

    private static ILeitorSensores CriarLeitorPadrao(ILoggerFactory fabrica)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new LeitorSensoresLinux(logger: fabrica.CreateLogger<LeitorSensoresLinux>());
        }

        // Produção no Windows: LibreHardwareMonitor (rico) com fallback para WMI
        // (temperatura, sem elevação) quando o driver não está disponível.
        var lhm = new LeitorSensoresLhm(
            new FonteSensoresLhm(fabrica.CreateLogger<FonteSensoresLhm>()),
            fabrica.CreateLogger<LeitorSensoresLhm>());
        var wmi = new LeitorSensoresWindows(fabrica.CreateLogger<LeitorSensoresWindows>());

        return new LeitorSensoresComposto(
            new ILeitorSensores[] { lhm, wmi },
            fabrica.CreateLogger<LeitorSensoresComposto>());
    }
}
````

### `src/HardwareOptimizer.Agent/Validation/AnalisadorRegressao.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Decide se houve regressão a partir das métricas medidas (e de um baseline
/// opcional para a comparação antes/depois). Sinais inequívocos — WHEA, erros
/// de memória, artefatos, TDR, BSOD, superaquecimento — reprovam a categoria.
/// Produz o contrato <see cref="ResultadoValidacao"/>.
/// </summary>
public sealed class AnalisadorRegressao
{
    public ResultadoValidacao Analisar(
        CategoriaAcao categoria,
        string ferramenta,
        MedicaoEstresse atual,
        MedicaoEstresse? baseline,
        LimiaresValidacao limiares)
    {
        ArgumentNullException.ThrowIfNull(atual);
        ArgumentNullException.ThrowIfNull(limiares);

        var erros = new List<string>();

        if (atual.ErrosWhea > 0)
        {
            erros.Add($"WHEA: {atual.ErrosWhea}");
        }

        if (atual.ErrosMemoria > 0)
        {
            erros.Add($"Erros de memória: {atual.ErrosMemoria}");
        }

        if (atual.Artefatos)
        {
            erros.Add("Artefatos gráficos detectados");
        }

        if (atual.DriverTimeout)
        {
            erros.Add("Driver timeout (TDR)");
        }

        if (atual.TelaAzul)
        {
            erros.Add("Tela azul (BSOD)");
        }

        if (atual.TempMaxC is { } temp && temp > limiares.TempMaxAceitavelC)
        {
            erros.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Temperatura {temp}°C acima do limite {limiares.TempMaxAceitavelC}°C"));
        }

        if (baseline?.Pontuacao is { } baseScore && atual.Pontuacao is { } score
            && score < baseScore * (1 - limiares.MargemQuedaPontuacao))
        {
            erros.Add(string.Create(
                CultureInfo.InvariantCulture, $"Queda de pontuação: {score} < {baseScore}"));
        }

        var regressao = erros.Count > 0;
        return new ResultadoValidacao
        {
            Categoria = categoria.ToString(),
            Ferramenta = ferramenta,
            Antes = baseline is null ? null : ParaMedicaoTeste(baseline),
            Depois = ParaMedicaoTeste(atual),
            Regressao = regressao,
            Erros = erros,
            Estabilidade = regressao ? "Reprovado" : "Totalmente validado",
        };
    }

    private static MedicaoTeste ParaMedicaoTeste(MedicaoEstresse m) => new()
    {
        Score = m.Pontuacao,
        TempMaxC = m.TempMaxC,
        ClockMhz = m.ClockMhz,
        ConsumoW = m.ConsumoW,
    };
}
````

### `src/HardwareOptimizer.Agent/Validation/IFerramentaEstresse.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Ferramenta de estresse que produz a saída textual a ser parseada. As
/// implementações reais invocam OCCT/Prime95/MemTest86; a simulada é usada no
/// MVP e nos testes.
/// </summary>
public interface IFerramentaEstresse
{
    string Nome { get; }

    Task<string> ExecutarAsync(CategoriaAcao categoria, TimeSpan duracao, CancellationToken cancellationToken = default);
}

/// <summary>Mapeia a categoria à ferramenta de estresse recomendada (do documento).</summary>
public static class SeletorFerramenta
{
    public static string Recomendada(CategoriaAcao categoria) => categoria switch
    {
        CategoriaAcao.Cpu => "OCCT/Prime95",
        CategoriaAcao.Memoria => "MemTest86/OCCT Memory",
        CategoriaAcao.Gpu => "OCCT GPU/VRAM",
        _ => "OCCT",
    };
}

/// <summary>
/// Ferramenta simulada: devolve uma saída pré-definida (sem invocar binários).
/// Os helpers geram saídas saudáveis ou com regressão para o MVP e os testes.
/// </summary>
public sealed class FerramentaEstresseSimulada : IFerramentaEstresse
{
    private readonly string _saida;

    public FerramentaEstresseSimulada(string nome, string saida)
    {
        Nome = nome;
        _saida = saida;
    }

    public string Nome { get; }

    public Task<string> ExecutarAsync(
        CategoriaAcao categoria, TimeSpan duracao, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_saida);
    }

    public static FerramentaEstresseSimulada Saudavel(string nome = "OCCT") =>
        new(nome,
            "Tool: " + nome + "\nWHEA errors: 0\nMemory errors: 0\nMax temperature: 78 C\n"
            + "Clock: 4600 MHz\nPower: 88 W\nScore: 11850\nArtifacts: no\nDriver timeout: no\n"
            + "BSOD: no\nStability: PASS");

    public static FerramentaEstresseSimulada ComRegressao(string motivo = "whea", string nome = "OCCT") =>
        new(nome, "Tool: " + nome + "\n" + motivo.Trim().ToLowerInvariant() switch
        {
            "bsod" => "WHEA errors: 0\nBSOD: yes\nStability: FAIL",
            "temp" => "WHEA errors: 0\nMax temperature: 99 C\nStability: WARN",
            "artefatos" => "WHEA errors: 0\nArtifacts: yes\nDriver timeout: yes\nStability: FAIL",
            "memoria" => "Memory errors: 7\nStability: FAIL",
            _ => "WHEA errors: 3\nMax temperature: 92 C\nStability: FAIL",
        });
}
````

### `src/HardwareOptimizer.Agent/Validation/MedicaoEstresse.cs`

````csharp
namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Métricas extraídas da saída de uma ferramenta de estresse (OCCT, Prime95,
/// Cinebench, MemTest86). Inclui os sinais de falha que indicam regressão.
/// </summary>
public sealed record MedicaoEstresse
{
    public double? TempMaxC { get; init; }

    public double? ClockMhz { get; init; }

    public double? ConsumoW { get; init; }

    public double? Pontuacao { get; init; }

    public int ErrosWhea { get; init; }

    public int ErrosMemoria { get; init; }

    public bool Artefatos { get; init; }

    public bool DriverTimeout { get; init; }

    public bool TelaAzul { get; init; }

    /// <summary>Há falha crítica inequívoca (WHEA, memória, artefatos, TDR ou BSOD)?</summary>
    public bool TemFalhaCritica =>
        ErrosWhea > 0 || ErrosMemoria > 0 || Artefatos || DriverTimeout || TelaAzul;
}

/// <summary>Limiares que definem o que conta como regressão.</summary>
public sealed record LimiaresValidacao
{
    /// <summary>Acima desta temperatura máxima, considera-se regressão térmica.</summary>
    public double TempMaxAceitavelC { get; init; } = 95;

    /// <summary>Queda relativa de pontuação tolerada antes/depois (ex.: 0,05 = 5%).</summary>
    public double MargemQuedaPontuacao { get; init; } = 0.05;

    public static LimiaresValidacao Padrao { get; } = new();
}
````

### `src/HardwareOptimizer.Agent/Validation/ParserEstresse.cs`

````csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Parser tolerante da saída de ferramentas de estresse. Lê linhas no formato
/// "chave: valor" e mapeia para <see cref="MedicaoEstresse"/>, normalizando a
/// chave e extraindo número/booleano do valor. Parsers específicos por
/// ferramenta podem especializar esta convenção.
/// </summary>
public sealed partial class ParserEstresse
{
    public MedicaoEstresse Parse(string saida)
    {
        ArgumentNullException.ThrowIfNull(saida);

        double? tempMax = null, clock = null, consumo = null, pontuacao = null;
        var whea = 0;
        var memoria = 0;
        bool artefatos = false, driverTimeout = false, telaAzul = false;

        foreach (var linha in saida.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = linha.IndexOf(':', StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            var chave = Normalizar(linha[..idx]);
            var valor = linha[(idx + 1)..].Trim();

            switch (chave)
            {
                case "wheaerrors" or "whea":
                    whea = Inteiro(valor);
                    break;
                case "memoryerrors" or "memerrors" or "errosdememoria":
                    memoria = Inteiro(valor);
                    break;
                case "maxtemperature" or "maxtemp" or "temperatura" or "temperaturamaxima" or "temp":
                    tempMax = Numero(valor);
                    break;
                case "clock" or "frequencia":
                    clock = Numero(valor);
                    break;
                case "power" or "consumo":
                    consumo = Numero(valor);
                    break;
                case "score" or "pontuacao":
                    pontuacao = Numero(valor);
                    break;
                case "artifacts" or "artefatos":
                    artefatos = Booleano(valor);
                    break;
                case "drivertimeout" or "tdr":
                    driverTimeout = Booleano(valor);
                    break;
                case "bsod" or "telaazul" or "bluescreen":
                    telaAzul = Booleano(valor);
                    break;
                default:
                    break;
            }
        }

        return new MedicaoEstresse
        {
            TempMaxC = tempMax,
            ClockMhz = clock,
            ConsumoW = consumo,
            Pontuacao = pontuacao,
            ErrosWhea = whea,
            ErrosMemoria = memoria,
            Artefatos = artefatos,
            DriverTimeout = driverTimeout,
            TelaAzul = telaAzul,
        };
    }

    private static string Normalizar(string chave) =>
        new string(chave.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static double? Numero(string valor)
    {
        var m = NumeroRegex().Match(valor);
        return m.Success
            && double.TryParse(
                m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    private static int Inteiro(string valor) => (int)(Numero(valor) ?? 0);

    private static bool Booleano(string valor)
    {
        var t = valor.Trim().ToLowerInvariant();
        return t is "yes" or "sim" or "true" or "1" or "detected" || (int.TryParse(t, out var n) && n > 0);
    }

    [GeneratedRegex(@"-?\d+(?:[.,]\d+)?")]
    private static partial Regex NumeroRegex();
}
````

### `src/HardwareOptimizer.Agent/Validation/RunnerValidacao.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Validation;

/// <summary>
/// Runner de validação real: após cada categoria, executa a ferramenta de
/// estresse, parseia a saída e analisa regressão. Implementa
/// <see cref="IValidadorCategoria"/>, de modo que o <c>ExecutorControlado</c>
/// reverte automaticamente a categoria quando <see cref="ResultadoValidacao.Regressao"/>
/// é verdadeiro — fechando o ciclo validar → comparar → reverter.
/// </summary>
public sealed class RunnerValidacao : IValidadorCategoria
{
    private readonly IFerramentaEstresse _ferramenta;
    private readonly ParserEstresse _parser = new();
    private readonly AnalisadorRegressao _analisador = new();
    private readonly LimiaresValidacao _limiares;
    private readonly MedicaoEstresse? _baseline;
    private readonly TimeSpan _duracao;
    private readonly ILogger _log;

    public RunnerValidacao(
        IFerramentaEstresse ferramenta,
        LimiaresValidacao? limiares = null,
        MedicaoEstresse? baseline = null,
        TimeSpan? duracao = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(ferramenta);
        _ferramenta = ferramenta;
        _limiares = limiares ?? LimiaresValidacao.Padrao;
        _baseline = baseline;
        _duracao = duracao ?? TimeSpan.FromMinutes(1);
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<ResultadoValidacao> ValidarAsync(
        CategoriaAcao categoria,
        IReadOnlyList<RegistroAlteracao> alteracoes,
        CancellationToken cancellationToken = default)
    {
        _log.LogInformation(
            "Validação {Categoria}: executando {Ferramenta} (recomendada: {Recomendada}).",
            categoria, _ferramenta.Nome, SeletorFerramenta.Recomendada(categoria));

        var saida = await _ferramenta.ExecutarAsync(categoria, _duracao, cancellationToken).ConfigureAwait(false);
        var medicao = _parser.Parse(saida);
        var resultado = _analisador.Analisar(categoria, _ferramenta.Nome, medicao, _baseline, _limiares);

        if (resultado.Regressao)
        {
            _log.LogWarning(
                "Validação {Categoria}: REPROVADO — {Erros}", categoria, string.Join("; ", resultado.Erros));
        }
        else
        {
            _log.LogInformation("Validação {Categoria}: {Estabilidade}.", categoria, resultado.Estabilidade);
        }

        return resultado;
    }
}
````


## HardwareOptimizer.Cerebro

### `src/HardwareOptimizer.Cerebro/HardwareOptimizer.Cerebro.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Anthropic" Version="12.27.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

### `src/HardwareOptimizer.Cerebro/CerebroLlm.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Cérebro baseado em LLM. Monta os prompts a partir do inventário sanitizado,
/// chama o <see cref="IClienteLlm"/> e passa a resposta pelo guard, que valida
/// tudo contra o catálogo. Antes de enviar, recusa qualquer inventário que ainda
/// contenha dados pessoais (defesa de privacidade em profundidade).
/// </summary>
public sealed class CerebroLlm : ICerebro
{
    private readonly IClienteLlm _cliente;
    private readonly ConstrutorPrompt _construtor = new();
    private readonly LeitorRespostaCerebro _guard = new();
    private readonly ILogger _log;

    public CerebroLlm(IClienteLlm cliente, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        _cliente = cliente;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<MatrizDecisao> ProporAsync(
        Inventario inventarioSanitizado, CatalogoAcoes catalogo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventarioSanitizado);
        ArgumentNullException.ThrowIfNull(catalogo);

        GarantirSanitizado(inventarioSanitizado);

        var sistema = _construtor.MontarSistema(catalogo);
        var usuario = _construtor.MontarUsuario(inventarioSanitizado, catalogo);

        _log.LogInformation("Cérebro (nuvem/{Modelo}): solicitando proposta.", _cliente.Modelo);
        var resposta = await _cliente.ResponderAsync(sistema, usuario, cancellationToken).ConfigureAwait(false);

        var matriz = _guard.Ler(resposta, catalogo, OrigemDecisao.Nuvem, _cliente.Modelo);
        _log.LogInformation(
            "Cérebro: {Itens} ação(ões) válidas após o guard; {Avisos} aviso(s).",
            matriz.Itens.Count, matriz.Avisos.Count);

        return matriz;
    }

    /// <summary>
    /// Recusa enviar à nuvem se o inventário ainda tiver PII (nomes, chave de
    /// produto). Após o pipeline de sanitização, esses campos são nulos.
    /// </summary>
    private static void GarantirSanitizado(Inventario inventario)
    {
        if (inventario.Identificadores is { } id
            && (NaoVazio(id.NomeUsuario) || NaoVazio(id.NomeMaquina) || NaoVazio(id.ChaveProdutoWindows)))
        {
            throw new InvalidOperationException(
                "Envio recusado: o inventário ainda contém dados pessoais não sanitizados. "
                + "Passe pelo pipeline de sanitização antes de enviar ao cérebro na nuvem.");
        }
    }

    private static bool NaoVazio(string? valor) => !string.IsNullOrWhiteSpace(valor);
}
````

### `src/HardwareOptimizer.Cerebro/CerebroLocal.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Cérebro local determinístico (opção "modelo local" do documento e padrão do
/// MVP). Não envia nada à nuvem: seleciona ações de baixo risco aplicáveis ao
/// inventário, com os valores padrão seguros. Sempre produz uma matriz válida.
/// </summary>
public sealed class CerebroLocal : ICerebro
{
    public Task<MatrizDecisao> ProporAsync(
        Inventario inventarioSanitizado, CatalogoAcoes catalogo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventarioSanitizado);
        ArgumentNullException.ThrowIfNull(catalogo);
        cancellationToken.ThrowIfCancellationRequested();

        var temGpu = inventarioSanitizado.Gpu.Count > 0;
        var itens = new List<ItemDecisao>();
        var prioridade = 1;

        foreach (var acao in catalogo.Todas
            .Where(a => a.Risco <= NivelRisco.Baixo)
            .Where(a => temGpu || a.Categoria != CategoriaAcao.Gpu)
            .OrderBy(a => (int)a.Risco)
            .ThenBy(a => a.Categoria)
            .ThenBy(a => a.Id, StringComparer.Ordinal))
        {
            var parametros = acao.Parametros.ToDictionary(
                p => p.Nome, p => p.ValorPadraoSeguro, StringComparer.OrdinalIgnoreCase);

            itens.Add(new ItemDecisao
            {
                AcaoId = acao.Id,
                Prioridade = prioridade++,
                Categoria = acao.Categoria,
                Risco = acao.Risco,
                GanhoEsperado = EstimarGanho(acao.Risco),
                Justificativa = acao.Descricao,
                Parametros = parametros,
            });
        }

        var matriz = new MatrizDecisao
        {
            Origem = OrigemDecisao.Local,
            Modelo = null,
            Itens = itens,
        };

        return Task.FromResult(matriz);
    }

    private static string EstimarGanho(NivelRisco risco) => risco switch
    {
        NivelRisco.Nenhum or NivelRisco.MuitoBaixo => "Baixo",
        _ => "Médio",
    };
}
````

### `src/HardwareOptimizer.Cerebro/ClienteLlmAnthropic.cs`

````csharp
using System.Text;
using Anthropic;
using Anthropic.Models.Messages;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Implementação de <see cref="IClienteLlm"/> sobre o SDK oficial da Anthropic.
/// O modelo e a chave de API vêm de configuração/ambiente — nada é fixado no
/// código. Usa pensamento adaptativo, recomendado para tarefas de raciocínio.
/// </summary>
public sealed class ClienteLlmAnthropic : IClienteLlm
{
    private readonly AnthropicClient _client;
    private readonly int _maxTokens;

    /// <param name="modelo">ID do modelo Claude a usar (ex.: vindo de variável de ambiente).</param>
    /// <param name="apiKey">Chave de API. Se nula, o SDK lê de ANTHROPIC_API_KEY.</param>
    public ClienteLlmAnthropic(string modelo, string? apiKey = null, int maxTokens = 8000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        Modelo = modelo;
        _maxTokens = maxTokens;
        _client = apiKey is null
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };
    }

    public string Modelo { get; }

    public async Task<string> ResponderAsync(
        string promptSistema, string promptUsuario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promptSistema);
        ArgumentNullException.ThrowIfNull(promptUsuario);
        cancellationToken.ThrowIfCancellationRequested();

        var parametros = new MessageCreateParams
        {
            Model = Modelo,
            MaxTokens = _maxTokens,
            System = promptSistema,
            Thinking = new ThinkingConfigAdaptive(),
            Messages = [new() { Role = Role.User, Content = promptUsuario }],
        };

        var resposta = await _client.Messages.Create(parametros).ConfigureAwait(false);

        var sb = new StringBuilder();
        foreach (var bloco in resposta.Content.Select(b => b.Value).OfType<TextBlock>())
        {
            sb.Append(bloco.Text);
        }

        return sb.ToString();
    }
}
````

### `src/HardwareOptimizer.Cerebro/ConstrutorPrompt.cs`

````csharp
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Monta os prompts do cérebro. O system prompt fixa as regras invariantes (só
/// IDs do catálogo, JSON estrito, ordem da filosofia); o user prompt traz o
/// inventário <b>sanitizado</b> e um resumo do catálogo com IDs e limites.
/// </summary>
public sealed class ConstrutorPrompt
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string MontarSistema(CatalogoAcoes catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return
            "Você é o cérebro de um sistema de otimização de hardware. Sua função é "
            + "SELECIONAR e PRIORIZAR ações de um catálogo fechado — você NUNCA inventa ações, "
            + "comandos ou parâmetros fora do catálogo fornecido.\n\n"
            + "Regras invariantes:\n"
            + "1. Use APENAS os IDs de ação presentes no catálogo do usuário.\n"
            + "2. Para cada ação escolhida, defina os parâmetros dentro da faixa segura indicada.\n"
            + "3. Priorize segundo a ordem: ESTABILIDADE > SEGURANÇA > EFICIÊNCIA > DESEMPENHO.\n"
            + "4. Busque o maior desempenho SUSTENTÁVEL e validado, não o maior possível.\n"
            + "5. Justifique cada escolha com base nas evidências do inventário.\n\n"
            + "Responda EXCLUSIVAMENTE com um JSON neste formato, sem texto adicional:\n"
            + "{\"acoes\":[{\"id\":\"<ID_DO_CATALOGO>\",\"prioridade\":1,"
            + "\"justificativa\":\"<motivo>\",\"parametros\":{\"<nome>\":\"<valor>\"}}]}\n"
            + $"(Catálogo versão {catalogo.Versao}.)";
    }

    public string MontarUsuario(Inventario inventarioSanitizado, CatalogoAcoes catalogo)
    {
        ArgumentNullException.ThrowIfNull(inventarioSanitizado);
        ArgumentNullException.ThrowIfNull(catalogo);

        var sb = new StringBuilder();
        sb.AppendLine("# Inventário (sanitizado)");
        sb.AppendLine(JsonSerializer.Serialize(inventarioSanitizado, Json));
        sb.AppendLine();
        sb.AppendLine("# Catálogo de ações disponíveis");
        foreach (var acao in catalogo.Todas.OrderBy(a => a.Categoria).ThenBy(a => a.Id, StringComparer.Ordinal))
        {
            sb.Append("- ").Append(acao.Id)
                .Append(" [").Append(acao.Categoria).Append("] risco=").Append(acao.Risco)
                .Append(": ").Append(acao.Titulo);

            foreach (var parametro in acao.Parametros)
            {
                sb.Append(" | param ").Append(parametro.Nome).Append('=').Append(DescreverParametro(parametro));
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Selecione e priorize as ações adequadas a este equipamento. Responda só com o JSON.");
        return sb.ToString();
    }

    private static string DescreverParametro(Parametro parametro) => parametro switch
    {
        ParametroNumerico n => string.Create(
            CultureInfo.InvariantCulture,
            $"faixa_segura [{n.FaixaSegura.Minimo}..{n.FaixaSegura.Maximo}]{n.Unidade} (padrão {n.PadraoSeguro})"),
        ParametroListaBranca l => "um de {" + string.Join(", ", l.ValoresSeguros) + "}",
        _ => "(sem detalhe)",
    };
}
````

### `src/HardwareOptimizer.Cerebro/ICerebro.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// O cérebro: propõe uma matriz de decisão a partir do inventário
/// <b>já sanitizado</b> e do catálogo. Implementações: local (offline) e LLM.
/// O cérebro NUNCA gera comandos — apenas seleciona e prioriza IDs do catálogo.
/// </summary>
public interface ICerebro
{
    Task<MatrizDecisao> ProporAsync(
        Inventario inventarioSanitizado,
        CatalogoAcoes catalogo,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstração mínima de um modelo de linguagem: recebe prompt de sistema + de
/// usuário e devolve texto. Mantém o cérebro independente de provedor e
/// permite testar com um cliente falso.
/// </summary>
public interface IClienteLlm
{
    /// <summary>Identificação do modelo, para registrar na matriz.</summary>
    string Modelo { get; }

    Task<string> ResponderAsync(
        string promptSistema, string promptUsuario, CancellationToken cancellationToken = default);
}
````

### `src/HardwareOptimizer.Cerebro/LeitorRespostaCerebro.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Guard determinístico da resposta do cérebro. Converte o texto/JSON do LLM em
/// uma <see cref="MatrizDecisao"/> válida: descarta qualquer ação que não exista
/// no catálogo e força cada parâmetro à faixa segura (usando o padrão seguro
/// quando o valor proposto é inválido). É o que mantém a regra invariante mesmo
/// se o modelo alucinar — a saída do LLM é tratada como NÃO confiável.
/// </summary>
public sealed class LeitorRespostaCerebro
{
    public MatrizDecisao Ler(
        string respostaLlm, CatalogoAcoes catalogo, OrigemDecisao origem, string? modelo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        var avisos = new List<string>();
        var json = ExtrairJson(respostaLlm);
        if (json is null)
        {
            avisos.Add("Resposta do cérebro não continha JSON interpretável; matriz vazia.");
            return Vazia(origem, modelo, avisos);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            avisos.Add("JSON inválido na resposta do cérebro: " + ex.Message);
            return Vazia(origem, modelo, avisos);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("acoes", out var acoes)
                || acoes.ValueKind != JsonValueKind.Array)
            {
                avisos.Add("Resposta sem a lista 'acoes'; matriz vazia.");
                return Vazia(origem, modelo, avisos);
            }

            var itens = new List<ItemDecisao>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var elemento in acoes.EnumerateArray())
            {
                var item = Interpretar(elemento, catalogo, vistos, avisos);
                if (item is not null)
                {
                    itens.Add(item);
                }
            }

            // Reordena por prioridade declarada e, em empate, por menor risco; renumera.
            var ordenados = itens
                .OrderBy(i => i.Prioridade)
                .ThenBy(i => (int)i.Risco)
                .Select((item, indice) => item with { Prioridade = indice + 1 })
                .ToList();

            return new MatrizDecisao
            {
                Origem = origem,
                Modelo = modelo,
                Itens = ordenados,
                Avisos = avisos,
            };
        }
    }

    private static ItemDecisao? Interpretar(
        JsonElement elemento, CatalogoAcoes catalogo, HashSet<string> vistos, List<string> avisos)
    {
        if (elemento.ValueKind != JsonValueKind.Object
            || !elemento.TryGetProperty("id", out var idProp)
            || idProp.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var id = idProp.GetString()!;
        var acao = catalogo.Obter(id);
        if (acao is null)
        {
            avisos.Add($"Ação '{id}' ignorada: não consta no catálogo whitelisted.");
            return null;
        }

        if (!vistos.Add(id))
        {
            return null; // duplicada: mantém a primeira ocorrência.
        }

        var prioridade = elemento.TryGetProperty("prioridade", out var p)
            && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var valorP)
            ? valorP
            : 999;

        var justificativa = elemento.TryGetProperty("justificativa", out var j)
            && j.ValueKind == JsonValueKind.String
            ? j.GetString()!
            : acao.Descricao;

        var parametrosBrutos = LerParametrosBrutos(elemento);
        var parametros = ForcarFaixaSegura(acao, parametrosBrutos, avisos);

        return new ItemDecisao
        {
            AcaoId = acao.Id,
            Prioridade = prioridade,
            Categoria = acao.Categoria,
            Risco = acao.Risco,
            GanhoEsperado = null,
            Justificativa = justificativa,
            Parametros = parametros,
        };
    }

    private static Dictionary<string, string> LerParametrosBrutos(JsonElement elemento)
    {
        var brutos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (elemento.TryGetProperty("parametros", out var par) && par.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in par.EnumerateObject())
            {
                brutos[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => string.Empty,
                };
            }
        }

        return brutos;
    }

    /// <summary>
    /// Para cada parâmetro declarado pela ação, usa o valor proposto somente se
    /// ele for aceito sob o perfil seguro; caso contrário, aplica o padrão seguro.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ForcarFaixaSegura(
        AcaoOtimizacao acao, IReadOnlyDictionary<string, string> propostos, List<string> avisos)
    {
        var finais = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parametro in acao.Parametros)
        {
            if (propostos.TryGetValue(parametro.Nome, out var valor))
            {
                var resultado = parametro.Validar(valor, TipoPerfil.Seguro);
                if (resultado.Situacao == SituacaoParametro.Aceito)
                {
                    finais[parametro.Nome] = valor;
                    continue;
                }

                avisos.Add(
                    $"Ação '{acao.Id}': valor '{valor}' do parâmetro '{parametro.Nome}' "
                    + $"rejeitado pelo guard; usando padrão seguro '{parametro.ValorPadraoSeguro}'.");
            }

            finais[parametro.Nome] = parametro.ValorPadraoSeguro;
        }

        return finais;
    }

    private static MatrizDecisao Vazia(OrigemDecisao origem, string? modelo, IReadOnlyList<string> avisos) =>
        new()
        {
            Origem = origem,
            Modelo = modelo,
            Itens = Array.Empty<ItemDecisao>(),
            Avisos = avisos,
        };

    /// <summary>Extrai o primeiro objeto JSON do texto, tolerando cercas de markdown.</summary>
    private static string? ExtrairJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{', StringComparison.Ordinal);
        var fim = texto.LastIndexOf('}');
        return inicio >= 0 && fim > inicio ? texto[inicio..(fim + 1)] : null;
    }
}
````

### `src/HardwareOptimizer.Cerebro/MatrizDecisao.cs`

````csharp
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Cerebro;

/// <summary>Origem da proposta: modelo local (offline) ou LLM na nuvem.</summary>
public enum OrigemDecisao
{
    Local = 0,
    Nuvem = 1,
}

/// <summary>
/// Um item priorizado da matriz de decisão. Referencia sempre um ID de ação do
/// catálogo; os parâmetros já passaram pelo guard e estão dentro da faixa segura.
/// </summary>
public sealed record ItemDecisao
{
    public required string AcaoId { get; init; }

    /// <summary>1 = mais prioritário.</summary>
    public required int Prioridade { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    public required NivelRisco Risco { get; init; }

    public string? GanhoEsperado { get; init; }

    public required string Justificativa { get; init; }

    public IReadOnlyDictionary<string, string> Parametros { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Matriz de decisão produzida pelo cérebro: lista priorizada de ações do
/// catálogo, com a origem (local/nuvem) e avisos do guard (ex.: itens descartados
/// por não constarem no catálogo). É o contrato "proposta" do documento.
/// </summary>
public sealed record MatrizDecisao
{
    public required OrigemDecisao Origem { get; init; }

    /// <summary>Modelo usado (quando origem é nuvem); nulo para local.</summary>
    public string? Modelo { get; init; }

    public required IReadOnlyList<ItemDecisao> Itens { get; init; }

    /// <summary>Avisos do guard: itens fora do catálogo, parâmetros corrigidos, etc.</summary>
    public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();

    public IEnumerable<string> AcaoIds => Itens.Select(i => i.AcaoId);
}
````

### `src/HardwareOptimizer.Cerebro/Visao/ClienteVisaoAnthropic.cs`

````csharp
using System.Text;
using Anthropic;
using Anthropic.Models.Messages;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Implementação multimodal de <see cref="IClienteVisao"/> sobre o SDK oficial
/// da Anthropic: envia a imagem (base64) + os prompts e devolve o texto. Modelo
/// e chave vêm de configuração/ambiente — nada é fixado no código.
/// </summary>
public sealed class ClienteVisaoAnthropic : IClienteVisao
{
    private readonly AnthropicClient _client;
    private readonly int _maxTokens;

    public ClienteVisaoAnthropic(string modelo, string? apiKey = null, int maxTokens = 2000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        Modelo = modelo;
        _maxTokens = maxTokens;
        _client = apiKey is null
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };
    }

    public string Modelo { get; }

    public async Task<string> AnalisarAsync(
        ImagemEntrada imagem, string promptSistema, string promptUsuario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagem);
        ArgumentNullException.ThrowIfNull(promptSistema);
        ArgumentNullException.ThrowIfNull(promptUsuario);
        cancellationToken.ThrowIfCancellationRequested();

        var parametros = new MessageCreateParams
        {
            Model = Modelo,
            MaxTokens = _maxTokens,
            System = promptSistema,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource
                            {
                                Data = imagem.Base64,
                                MediaType = imagem.MediaType,
                            },
                        },
                        new TextBlockParam { Text = promptUsuario },
                    },
                },
            ],
        };

        var resposta = await _client.Messages.Create(parametros).ConfigureAwait(false);

        var sb = new StringBuilder();
        foreach (var bloco in resposta.Content.Select(b => b.Value).OfType<TextBlock>())
        {
            sb.Append(bloco.Text);
        }

        return sb.ToString();
    }
}
````

### `src/HardwareOptimizer.Cerebro/Visao/ConferenciaVisual.cs`

````csharp
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Cruza a leitura visual com o inventário coletado. Implementa a regra do
/// documento: nunca confiar cegamente na leitura visual; se a confiança for
/// baixa, pedir nova foto; caso contrário, validar contra os dados coletados.
/// </summary>
public sealed class ConferenciaVisual
{
    public ResultadoConferencia Conferir(LeituraVisual leitura, Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(inventario);

        if (leitura.Confianca == NivelConfianca.Baixa)
        {
            return PedirNovaFoto("Confiança baixa na leitura visual.");
        }

        return leitura.TipoTela switch
        {
            TipoTela.BiosUefi => ConferirBios(leitura, inventario.Placa),
            TipoTela.EtiquetaPlaca => ConferirEtiqueta(leitura, inventario.Placa),
            TipoTela.MensagemErro or TipoTela.Benchmark =>
                Inconclusivo("Leitura aceita; não há campo equivalente no inventário para cruzar."),
            _ => PedirNovaFoto("Tela não identificada com clareza."),
        };
    }

    private static ResultadoConferencia ConferirBios(LeituraVisual leitura, PlacaMae placa)
    {
        var versaoLida = leitura.Campo("versao");
        if (string.IsNullOrWhiteSpace(versaoLida) || string.IsNullOrWhiteSpace(placa.VersaoBios))
        {
            return Inconclusivo("Sem versão de BIOS suficiente para comparar leitura e inventário.");
        }

        return VersaoBios.Comparar(versaoLida, placa.VersaoBios) == 0
            ? Confere($"Versão de BIOS confere com o inventário ({placa.VersaoBios}).")
            : Diverge($"Versão lida '{versaoLida}' difere da coletada '{placa.VersaoBios}'.");
    }

    private static ResultadoConferencia ConferirEtiqueta(LeituraVisual leitura, PlacaMae placa)
    {
        var fabricanteLido = NormalizadorFabricante.Normalizar(leitura.Campo("fabricante"));
        var fabricanteInv = NormalizadorFabricante.Normalizar(placa.Fabricante);
        var modeloLido = (leitura.Campo("modelo") ?? string.Empty).Trim();

        var fabricanteOk = string.Equals(fabricanteLido, fabricanteInv, StringComparison.OrdinalIgnoreCase);
        var modeloOk = !string.IsNullOrWhiteSpace(modeloLido)
            && placa.Modelo.Contains(modeloLido, StringComparison.OrdinalIgnoreCase);

        return fabricanteOk && modeloOk
            ? Confere($"Etiqueta confere com o inventário ({fabricanteInv} {placa.Modelo}).")
            : Diverge($"Etiqueta '{fabricanteLido} {modeloLido}' difere do inventário "
                + $"'{fabricanteInv} {placa.Modelo}'.");
    }

    private static ResultadoConferencia Confere(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Confere, Mensagem = mensagem };

    private static ResultadoConferencia Diverge(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Diverge, Mensagem = mensagem };

    private static ResultadoConferencia Inconclusivo(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Inconclusivo, Mensagem = mensagem };

    private static ResultadoConferencia PedirNovaFoto(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Inconclusivo, Mensagem = mensagem, PedirNovaFoto = true };
}
````

### `src/HardwareOptimizer.Cerebro/Visao/ConstrutorPromptVisao.cs`

````csharp
namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Monta os prompts direcionados do módulo de visão. O system prompt exige JSON
/// estrito com nível de confiança; o user prompt foca a pergunta no caso de uso.
/// </summary>
public sealed class ConstrutorPromptVisao
{
    public string MontarSistema() =>
        "Você lê fotos de telas e etiquetas de hardware. Extraia apenas o que está visível; "
        + "NUNCA invente valores. Se não tiver certeza, use confiança \"baixa\".\n\n"
        + "Responda EXCLUSIVAMENTE com um JSON neste formato, sem texto adicional:\n"
        + "{\"tipoTela\":\"biosUefi|etiquetaPlaca|mensagemErro|benchmark|desconhecida\","
        + "\"campos\":{\"<nome>\":\"<valor lido>\"},"
        + "\"confianca\":\"alta|media|baixa\","
        + "\"proximoPasso\":\"<o que o usuário deve fazer a seguir>\"}";

    public string MontarUsuario(CasoUsoVisao caso) => caso switch
    {
        CasoUsoVisao.LerVersaoBios =>
            "Esta é uma tela de BIOS/UEFI. Identifique o fabricante e a placa e leia a VERSÃO da BIOS. "
            + "Use os campos 'fabricante', 'modelo' e 'versao'.",
        CasoUsoVisao.LerEtiquetaPlaca =>
            "Esta é a etiqueta de uma placa-mãe. Leia o fabricante e o modelo. "
            + "Use os campos 'fabricante' e 'modelo'.",
        CasoUsoVisao.LerMensagemErro =>
            "Esta é uma mensagem de erro ou tela azul. Leia o código de parada e a mensagem principal. "
            + "Use os campos 'codigo' e 'mensagem'.",
        CasoUsoVisao.LerBenchmark =>
            "Esta é uma tela de benchmark/estresse (ex.: OCCT, Cinebench). Leia temperatura, clock, "
            + "consumo e pontuação quando visíveis. Use campos como 'temperatura', 'clock', 'consumo', 'pontuacao'.",
        _ =>
            "Que tela é esta? Identifique o tipo e leia os campos relevantes visíveis.",
    };
}
````

### `src/HardwareOptimizer.Cerebro/Visao/IClienteVisao.cs`

````csharp
namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Cliente multimodal: recebe uma imagem + prompts e devolve texto/JSON.
/// Abstrai o provedor (SDK Anthropic) e permite testar com um cliente falso.
/// </summary>
public interface IClienteVisao
{
    string Modelo { get; }

    Task<string> AnalisarAsync(
        ImagemEntrada imagem,
        string promptSistema,
        string promptUsuario,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validação/pré-processamento da imagem antes do envio. Mantém o passo do
/// pipeline; o redimensionamento real (via biblioteca de imagem) é um próximo
/// passo, mas o tipo e o tamanho já são checados aqui.
/// </summary>
public sealed class PreProcessadorImagem
{
    /// <summary>Limite de aviso de tamanho (~4 MB de base64).</summary>
    private const int LimiteBase64 = 4 * 1024 * 1024;

    private static readonly HashSet<string> Suportados =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp", "image/gif" };

    public IReadOnlyList<string> Validar(ImagemEntrada imagem)
    {
        ArgumentNullException.ThrowIfNull(imagem);

        if (string.IsNullOrWhiteSpace(imagem.Base64))
        {
            throw new ArgumentException("Imagem vazia.", nameof(imagem));
        }

        if (!Suportados.Contains(imagem.MediaType))
        {
            throw new NotSupportedException($"Tipo de imagem não suportado: {imagem.MediaType}");
        }

        var avisos = new List<string>();
        if (imagem.Base64.Length > LimiteBase64)
        {
            avisos.Add("Imagem grande; considere redimensionar antes do envio para reduzir custo/tempo.");
        }

        return avisos;
    }
}
````

### `src/HardwareOptimizer.Cerebro/Visao/LeitorRespostaVisao.cs`

````csharp
using System.Text.Json;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Converte a resposta do modelo multimodal em uma <see cref="LeituraVisual"/>.
/// Defensivo: JSON ausente/ inválido vira leitura "desconhecida" com confiança
/// baixa e pedido de nova foto — nunca lança nem confia cegamente.
/// </summary>
public sealed class LeitorRespostaVisao
{
    public LeituraVisual Ler(string respostaModelo, string? modelo)
    {
        var json = ExtrairJson(respostaModelo);
        if (json is null)
        {
            return Indefinida(modelo, respostaModelo);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            return new LeituraVisual
            {
                TipoTela = MapearTipo(Texto(raiz, "tipoTela")),
                Campos = LerCampos(raiz),
                Confianca = MapearConfianca(Texto(raiz, "confianca")),
                ProximoPasso = Texto(raiz, "proximoPasso"),
                TextoBruto = respostaModelo,
                Modelo = modelo,
            };
        }
        catch (JsonException)
        {
            return Indefinida(modelo, respostaModelo);
        }
    }

    private static IReadOnlyDictionary<string, string> LerCampos(JsonElement raiz)
    {
        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raiz.TryGetProperty("campos", out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                var valor = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null,
                };

                if (!string.IsNullOrWhiteSpace(valor))
                {
                    campos[prop.Name] = valor;
                }
            }
        }

        return campos;
    }

    private static TipoTela MapearTipo(string? valor) => Normalizar(valor) switch
    {
        "biosuefi" or "bios" or "uefi" => TipoTela.BiosUefi,
        "etiquetaplaca" or "etiqueta" => TipoTela.EtiquetaPlaca,
        "mensagemerro" or "erro" or "telaazul" => TipoTela.MensagemErro,
        "benchmark" or "estresse" => TipoTela.Benchmark,
        _ => TipoTela.Desconhecida,
    };

    private static NivelConfianca MapearConfianca(string? valor) => Normalizar(valor) switch
    {
        "alta" => NivelConfianca.Alta,
        "media" => NivelConfianca.Media,
        _ => NivelConfianca.Baixa,
    };

    private static string Normalizar(string? valor) =>
        new string((valor ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? Texto(JsonElement raiz, string propriedade) =>
        raiz.TryGetProperty(propriedade, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static LeituraVisual Indefinida(string? modelo, string? bruto) => new()
    {
        TipoTela = TipoTela.Desconhecida,
        Confianca = NivelConfianca.Baixa,
        ProximoPasso = "Não foi possível interpretar a imagem; envie outra foto, mais nítida.",
        TextoBruto = bruto,
        Modelo = modelo,
    };

    private static string? ExtrairJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{', StringComparison.Ordinal);
        var fim = texto.LastIndexOf('}');
        return inicio >= 0 && fim > inicio ? texto[inicio..(fim + 1)] : null;
    }
}
````

### `src/HardwareOptimizer.Cerebro/Visao/ModuloVisao.cs`

````csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Orquestra o fluxo_visao: pré-processa a imagem, monta o prompt direcionado,
/// chama o modelo multimodal e estrutura a leitura. A confirmação contra o
/// inventário fica em <see cref="ConferenciaVisual"/> (a regra do documento de
/// nunca confiar cegamente na leitura visual).
/// </summary>
public sealed class ModuloVisao
{
    private readonly IClienteVisao _cliente;
    private readonly ConstrutorPromptVisao _prompt = new();
    private readonly LeitorRespostaVisao _leitor = new();
    private readonly PreProcessadorImagem _pre = new();
    private readonly ILogger _log;

    public ModuloVisao(IClienteVisao cliente, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        _cliente = cliente;
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<LeituraVisual> InterpretarAsync(
        ImagemEntrada imagem, CasoUsoVisao caso, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagem);

        foreach (var aviso in _pre.Validar(imagem))
        {
            _log.LogWarning("Visão: {Aviso}", aviso);
        }

        _log.LogInformation(
            "Visão: analisando imagem '{Descricao}' ({MediaType}) para o caso {Caso}.",
            imagem.Descricao ?? "(sem nome)", imagem.MediaType, caso);

        var sistema = _prompt.MontarSistema();
        var usuario = _prompt.MontarUsuario(caso);
        var resposta = await _cliente.AnalisarAsync(imagem, sistema, usuario, cancellationToken).ConfigureAwait(false);

        var leitura = _leitor.Ler(resposta, _cliente.Modelo);
        _log.LogInformation(
            "Visão: tela={Tipo}, confiança={Confianca}, {Campos} campo(s) lido(s).",
            leitura.TipoTela, leitura.Confianca, leitura.Campos.Count);

        return leitura;
    }
}
````

### `src/HardwareOptimizer.Cerebro/Visao/Visao.cs`

````csharp
namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>Tipo de tela/imagem identificada (entradas do fluxo_visao).</summary>
public enum TipoTela
{
    Desconhecida = 0,
    BiosUefi = 1,
    EtiquetaPlaca = 2,
    MensagemErro = 3,
    Benchmark = 4,
}

/// <summary>Nível de confiança da leitura visual.</summary>
public enum NivelConfianca
{
    Baixa = 0,
    Media = 1,
    Alta = 2,
}

/// <summary>Caso de uso que direciona o prompt enviado ao modelo multimodal.</summary>
public enum CasoUsoVisao
{
    Identificar = 0,
    LerVersaoBios = 1,
    LerEtiquetaPlaca = 2,
    LerMensagemErro = 3,
    LerBenchmark = 4,
}

/// <summary>Desfecho do cruzamento da leitura visual com o inventário coletado.</summary>
public enum SituacaoConferencia
{
    Confere = 0,
    Diverge = 1,
    Inconclusivo = 2,
}

/// <summary>Imagem de entrada já em base64, pronta para o modelo multimodal.</summary>
public sealed record ImagemEntrada
{
    public required string Base64 { get; init; }

    /// <summary>image/png, image/jpeg, image/webp ou image/gif.</summary>
    public required string MediaType { get; init; }

    public string? Descricao { get; init; }

    public static ImagemEntrada DeArquivo(string caminho)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        var bytes = File.ReadAllBytes(caminho);
        return new ImagemEntrada
        {
            Base64 = Convert.ToBase64String(bytes),
            MediaType = InferirMediaType(caminho),
            Descricao = Path.GetFileName(caminho),
        };
    }

    private static string InferirMediaType(string caminho) =>
        Path.GetExtension(caminho).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png",
        };
}

/// <summary>Leitura estruturada de uma imagem: tipo de tela, campos lidos e confiança.</summary>
public sealed record LeituraVisual
{
    public required TipoTela TipoTela { get; init; }

    public IReadOnlyDictionary<string, string> Campos { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public required NivelConfianca Confianca { get; init; }

    public string? ProximoPasso { get; init; }

    public string? TextoBruto { get; init; }

    public string? Modelo { get; init; }

    public string? Campo(string nome) => Campos.TryGetValue(nome, out var v) ? v : null;
}

/// <summary>Resultado do cruzamento da leitura visual com o inventário.</summary>
public sealed record ResultadoConferencia
{
    public required SituacaoConferencia Situacao { get; init; }

    public required string Mensagem { get; init; }

    /// <summary>Verdadeiro quando a confiança é baixa ou a leitura é inconclusiva.</summary>
    public bool PedirNovaFoto { get; init; }
}
````


## HardwareOptimizer.Ipc

### `src/HardwareOptimizer.Ipc/HardwareOptimizer.Ipc.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
    <ProjectReference Include="..\HardwareOptimizer.Agent\HardwareOptimizer.Agent.csproj" />
    <ProjectReference Include="..\HardwareOptimizer.Cerebro\HardwareOptimizer.Cerebro.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

### `src/HardwareOptimizer.Ipc/ClienteNamedPipe.cs`

````csharp
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace HardwareOptimizer.Ipc;

/// <summary>
/// Cliente IPC sobre named pipe (usado pela UI). Abre conexão, envia uma
/// requisição e lê a resposta. No resultado, <see cref="RespostaIpc.Resultado"/>
/// chega como <see cref="JsonElement"/> para a camada de apresentação ler.
/// </summary>
public sealed class ClienteNamedPipe
{
    private readonly string _nomePipe;
    private readonly string _servidor;

    public ClienteNamedPipe(string nomePipe, string servidor = ".")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomePipe);
        _nomePipe = nomePipe;
        _servidor = servidor;
    }

    public async Task<RespostaIpc> ChamarAsync(
        RequisicaoIpc requisicao, CancellationToken cancellationToken = default, int timeoutMs = 5000)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        using var cliente = new NamedPipeClientStream(
            _servidor, _nomePipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await cliente.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);

        using var leitor = new StreamReader(cliente, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
        await using var escritor = new StreamWriter(cliente, new UTF8Encoding(false)) { AutoFlush = true };

        await escritor.WriteLineAsync(JsonSerializer.Serialize(requisicao, ProtocoloIpc.Json)).ConfigureAwait(false);

        var linha = await leitor.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return linha is null
            ? RespostaIpc.Falha(requisicao.Id, "Sem resposta do servidor.")
            : JsonSerializer.Deserialize<RespostaIpc>(linha, ProtocoloIpc.Json)
                ?? RespostaIpc.Falha(requisicao.Id, "Resposta vazia.");
    }

    /// <summary>Atalho para chamar um método sem parâmetros.</summary>
    public Task<RespostaIpc> ChamarAsync(string metodo, CancellationToken cancellationToken = default) =>
        ChamarAsync(new RequisicaoIpc { Metodo = metodo }, cancellationToken);
}
````

### `src/HardwareOptimizer.Ipc/IRoteadorIpc.cs`

````csharp
namespace HardwareOptimizer.Ipc;

/// <summary>
/// Abstração do roteador do agente. Permite que a UI (e os testes) dependam do
/// contrato em vez da implementação concreta, e que a UI fale com o agente em
/// processo (<see cref="RoteadorIpc"/>) ou remoto (via named pipe) de forma
/// intercambiável.
/// </summary>
public interface IRoteadorIpc
{
    Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default);
}
````

### `src/HardwareOptimizer.Ipc/ProtocoloIpc.cs`

````csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Ipc;

/// <summary>Requisição IPC: método + parâmetros opcionais (JSON livre).</summary>
public sealed record RequisicaoIpc
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required string Metodo { get; init; }

    public JsonElement? Parametros { get; init; }
}

/// <summary>Resposta IPC. O resultado é serializado apenas no transporte.</summary>
public sealed record RespostaIpc
{
    public required string Id { get; init; }

    public required bool Sucesso { get; init; }

    public object? Resultado { get; init; }

    public string? Erro { get; init; }

    public static RespostaIpc Ok(string id, object? resultado) =>
        new() { Id = id, Sucesso = true, Resultado = resultado };

    public static RespostaIpc Falha(string id, string erro) =>
        new() { Id = id, Sucesso = false, Erro = erro };
}

/// <summary>Resumo de uma ação do catálogo, próprio para serialização/UI.</summary>
public sealed record AcaoResumoDto
{
    public required string Id { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    public required string Titulo { get; init; }

    public required NivelRisco Risco { get; init; }

    public bool RequerReinicio { get; init; }

    public IReadOnlyList<string> PreCondicoes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ParametroResumoDto> Parametros { get; init; } = Array.Empty<ParametroResumoDto>();

    public static AcaoResumoDto De(AcaoOtimizacao acao) => new()
    {
        Id = acao.Id,
        Categoria = acao.Categoria,
        Titulo = acao.Titulo,
        Risco = acao.Risco,
        RequerReinicio = acao.RequerReinicio,
        PreCondicoes = acao.PreCondicoes,
        Parametros = acao.Parametros.Select(ParametroResumoDto.De).ToList(),
    };
}

/// <summary>Resumo de um parâmetro (numérico ou lista branca) para a UI.</summary>
public sealed record ParametroResumoDto
{
    public required string Nome { get; init; }

    public required string Tipo { get; init; }

    public string? Detalhe { get; init; }

    public static ParametroResumoDto De(Parametro parametro) => parametro switch
    {
        ParametroNumerico n => new ParametroResumoDto
        {
            Nome = n.Nome,
            Tipo = "numerico",
            Detalhe = $"seguro {n.FaixaSegura}, permitido {n.FaixaPermitida}, "
                + $"limite_absoluto {n.LimiteAbsoluto}, padrão {n.PadraoSeguro}{n.Unidade}",
        },
        ParametroListaBranca l => new ParametroResumoDto
        {
            Nome = l.Nome,
            Tipo = "lista_branca",
            Detalhe = string.Join(", ", l.ValoresSeguros),
        },
        _ => new ParametroResumoDto { Nome = parametro.Nome, Tipo = "desconhecido" },
    };
}

/// <summary>Opções de serialização compartilhadas pelo protocolo IPC.</summary>
public static class ProtocoloIpc
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
````

### `src/HardwareOptimizer.Ipc/RoteadorIpc.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Execution.Windows;
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Ipc;

/// <summary>
/// Dispatcher do protocolo IPC: traduz uma <see cref="RequisicaoIpc"/> em chamada
/// aos módulos do agente e devolve uma <see cref="RespostaIpc"/>. Lógica pura de
/// roteamento (sem transporte), portanto totalmente testável.
/// </summary>
public sealed class RoteadorIpc : IRoteadorIpc
{
    private readonly CatalogoAcoes _catalogo;
    private readonly IColetorInventario _coletor;
    private readonly ServicoSensores _sensores;
    private readonly ICerebro _cerebro;
    private readonly ILogger _log;

    public RoteadorIpc(
        CatalogoAcoes? catalogo = null,
        IColetorInventario? coletor = null,
        ServicoSensores? sensores = null,
        ICerebro? cerebro = null,
        ILogger? logger = null)
    {
        _catalogo = catalogo ?? CatalogoPadrao.Criar();
        _coletor = coletor ?? new ColetorInventario();
        _sensores = sensores ?? new ServicoSensores();
        _cerebro = cerebro ?? new CerebroLocal();
        _log = logger ?? NullLogger.Instance;
    }

    public async Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        _log.LogInformation("IPC: método '{Metodo}' (id {Id}).", requisicao.Metodo, requisicao.Id);

        try
        {
            return requisicao.Metodo.ToLowerInvariant() switch
            {
                "ping" => RespostaIpc.Ok(requisicao.Id, "pong"),
                "coletar" => RespostaIpc.Ok(requisicao.Id, await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false)),
                "sensores" => RespostaIpc.Ok(requisicao.Id, await _sensores.LerAsync(cancellationToken).ConfigureAwait(false)),
                "catalogo" => RespostaIpc.Ok(requisicao.Id, ListarCatalogo()),
                "proposta" => RespostaIpc.Ok(requisicao.Id, await ProporAsync(cancellationToken).ConfigureAwait(false)),
                "relatorio" => RespostaIpc.Ok(requisicao.Id, await RelatorioAsync(cancellationToken).ConfigureAwait(false)),
                "aprovar" => await AprovarAsync(requisicao, cancellationToken).ConfigureAwait(false),
                _ => RespostaIpc.Falha(requisicao.Id, $"Método desconhecido: {requisicao.Metodo}"),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            _log.LogError(ex, "IPC: falha no método '{Metodo}'.", requisicao.Metodo);
            return RespostaIpc.Falha(requisicao.Id, ex.Message);
        }
    }

    private IReadOnlyList<AcaoResumoDto> ListarCatalogo() =>
        _catalogo.Todas
            .OrderBy(a => a.Categoria)
            .ThenBy(a => a.Id, StringComparer.Ordinal)
            .Select(AcaoResumoDto.De)
            .ToList();

    private async Task<MatrizDecisao> ProporAsync(CancellationToken cancellationToken)
    {
        var inventario = await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false);
        var sanitizado = new Sanitizador().Sanitizar(inventario).InventarioSeguro;
        return await _cerebro.ProporAsync(sanitizado, _catalogo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RelatorioExecutivo> RelatorioAsync(CancellationToken cancellationToken)
    {
        var inventario = await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false);
        return new GeradorRelatorio().Gerar(
            inventario,
            Array.Empty<ResultadoValidacao>(),
            Array.Empty<AlteracaoResumo>(),
            new HashSet<Dominio>());
    }

    /// <summary>
    /// Fluxo de aprovação explícita por ação: a UI envia os IDs aprovados; o
    /// agente monta o perfil seguro, faz backup obrigatório e executa por
    /// categoria com validação (e rollback automático em regressão).
    /// </summary>
    private async Task<RespostaIpc> AprovarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken)
    {
        var acoes = LerAcoes(requisicao.Parametros);
        if (acoes.Count == 0)
        {
            return RespostaIpc.Falha(requisicao.Id, "Nenhuma ação aprovada (parâmetro 'acoes' vazio).");
        }

        var construcao = new ConstrutorPerfil(_catalogo).CriarPerfilSeguro(LerNomePerfil(requisicao.Parametros), acoes);
        if (!construcao.Sucesso)
        {
            return RespostaIpc.Falha(requisicao.Id, "Perfil inválido: " + string.Join(" | ", construcao.Bloqueios));
        }

        var inventario = await _coletor.ColetarAsync(cancellationToken).ConfigureAwait(false);
        var backup = await new ServicoBackup().CriarBackupAsync(inventario, cancellationToken).ConfigureAwait(false);

        var estado = EstadoSistemaWindows.Selecionar(_log);
        var executor = new ExecutorControlado(
            _catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel()));

        var relatorio = await executor
            .AplicarPerfilAsync(construcao.Perfil!, new ContextoExecucao { BackupConfirmado = backup.Sucesso }, cancellationToken)
            .ConfigureAwait(false);

        return RespostaIpc.Ok(requisicao.Id, relatorio);
    }

    private static IReadOnlyList<string> LerAcoes(JsonElement? parametros)
    {
        var acoes = new List<string>();
        if (parametros is { } p && p.ValueKind == JsonValueKind.Object
            && p.TryGetProperty("acoes", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var elemento in arr.EnumerateArray())
            {
                if (elemento.ValueKind == JsonValueKind.String && elemento.GetString() is { } id)
                {
                    acoes.Add(id);
                }
            }
        }

        return acoes;
    }

    private static string LerNomePerfil(JsonElement? parametros) =>
        parametros is { } p && p.ValueKind == JsonValueKind.Object
        && p.TryGetProperty("nomePerfil", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!
            : "perfil-ipc";
}
````

### `src/HardwareOptimizer.Ipc/ServidorNamedPipe.cs`

````csharp
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Ipc;

/// <summary>
/// Servidor IPC sobre named pipe. Atende uma conexão por vez (suficiente para
/// uma UI local): lê uma requisição JSON por linha, roteia e responde. Usa modo
/// Byte para funcionar também em Linux/macOS.
/// </summary>
public sealed class ServidorNamedPipe
{
    private readonly string _nomePipe;
    private readonly RoteadorIpc _roteador;
    private readonly ILogger _log;

    public ServidorNamedPipe(string nomePipe, RoteadorIpc? roteador = null, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomePipe);
        _nomePipe = nomePipe;
        _roteador = roteador ?? new RoteadorIpc();
        _log = logger ?? NullLogger.Instance;
    }

    public async Task ServirAsync(CancellationToken cancellationToken = default)
    {
        _log.LogInformation("IPC: servidor escutando no pipe '{Pipe}'.", _nomePipe);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var servidor = new NamedPipeServerStream(
                _nomePipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await servidor.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await AtenderAsync(servidor, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AtenderAsync(Stream fluxo, CancellationToken cancellationToken)
    {
        using var leitor = new StreamReader(fluxo, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
        await using var escritor = new StreamWriter(fluxo, new UTF8Encoding(false)) { AutoFlush = true };

        var linha = await leitor.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(linha))
        {
            return;
        }

        RespostaIpc resposta;
        try
        {
            var requisicao = JsonSerializer.Deserialize<RequisicaoIpc>(linha, ProtocoloIpc.Json);
            resposta = requisicao is null
                ? RespostaIpc.Falha("?", "Requisição vazia.")
                : await _roteador.TratarAsync(requisicao, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            resposta = RespostaIpc.Falha("?", "Requisição inválida: " + ex.Message);
        }

        await escritor.WriteLineAsync(JsonSerializer.Serialize(resposta, ProtocoloIpc.Json)).ConfigureAwait(false);
    }
}
````


## HardwareOptimizer.App

### `src/HardwareOptimizer.App/HardwareOptimizer.App.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    <!-- O compilador de XAML do Avalonia gera código que pode emitir avisos;
         não tratamos avisos como erro NESTE projeto (o restante da solução trata). -->
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Desktop" Version="12.0.4" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.4" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\HardwareOptimizer.Ipc\HardwareOptimizer.Ipc.csproj" />
  </ItemGroup>

</Project>
````

### `src/HardwareOptimizer.App/App.axaml`

````xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="HardwareOptimizer.App.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
````

### `src/HardwareOptimizer.App/App.axaml.cs`

````csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.App.Views;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // App em processo: a UI fala com o agente pelo roteador local
            // (a mesma API exposta por named pipe quando UI e agente são separados).
            var roteador = new RoteadorIpc();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(roteador),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
````

### `src/HardwareOptimizer.App/Program.cs`

````csharp
using Avalonia;

namespace HardwareOptimizer.App;

internal static class Program
{
    // Ponto de entrada da UI desktop (Avalonia).
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
````

### `src/HardwareOptimizer.App/ViewModels/ItemMatrizViewModel.cs`

````csharp
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.App.ViewModels;

/// <summary>Item da matriz de decisão exibido na UI, com seleção para aprovação.</summary>
public partial class ItemMatrizViewModel : ObservableObject
{
    private readonly ItemDecisao _item;

    public ItemMatrizViewModel(ItemDecisao item)
    {
        _item = item;
        // Pré-seleciona apenas as ações de risco muito baixo (postura conservadora).
        _selecionado = item.Risco <= NivelRisco.MuitoBaixo;
    }

    [ObservableProperty]
    private bool _selecionado;

    public string AcaoId => _item.AcaoId;

    public string Descricao => $"{_item.Prioridade}. {_item.AcaoId} — {_item.Justificativa}";

    public string Risco => $"risco {_item.Risco}";
}
````

### `src/HardwareOptimizer.App/ViewModels/MainWindowViewModel.cs`

````csharp
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

/// <summary>
/// ViewModel principal. Orquestra a UI consumindo o agente pelo contrato
/// <see cref="IRoteadorIpc"/> — testável com um roteador falso. Cada ação
/// corresponde a um método do protocolo IPC.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public MainWindowViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
        Sensores = new ObservableCollection<string>();
        Matriz = new ObservableCollection<ItemMatrizViewModel>();
    }

    [ObservableProperty]
    private bool _ocupado;

    [ObservableProperty]
    private string _status = "Pronto.";

    [ObservableProperty]
    private string _inventarioResumo = "(inventário não coletado)";

    [ObservableProperty]
    private string _resultadoAprovacao = string.Empty;

    public ObservableCollection<string> Sensores { get; }

    public ObservableCollection<ItemMatrizViewModel> Matriz { get; }

    [RelayCommand]
    private Task Coletar() => ExecutarAsync(new RequisicaoIpc { Metodo = "coletar" }, resposta =>
    {
        if (resposta.Resultado is Inventario inv)
        {
            InventarioResumo =
                $"{inv.Placa.Fabricante} {inv.Placa.Modelo} • {inv.Cpu.Nome} • {inv.SistemaOperacional.Nome}";
        }
    });

    [RelayCommand]
    private Task LerSensores() => ExecutarAsync(new RequisicaoIpc { Metodo = "sensores" }, resposta =>
    {
        Sensores.Clear();
        if (resposta.Resultado is LeituraSensores leitura)
        {
            foreach (var sensor in leitura.Sensores)
            {
                Sensores.Add($"{sensor.Tipo} — {sensor.Nome}: {sensor.Valor} {sensor.Unidade}");
            }
        }

        if (Sensores.Count == 0)
        {
            Sensores.Add("(nenhum sensor legível nesta máquina)");
        }
    });

    [RelayCommand]
    private Task Propor() => ExecutarAsync(new RequisicaoIpc { Metodo = "proposta" }, resposta =>
    {
        Matriz.Clear();
        if (resposta.Resultado is MatrizDecisao matriz)
        {
            foreach (var item in matriz.Itens)
            {
                Matriz.Add(new ItemMatrizViewModel(item));
            }
        }
    });

    [RelayCommand]
    private async Task Aprovar()
    {
        var selecionadas = Matriz.Where(i => i.Selecionado).Select(i => i.AcaoId).ToArray();
        if (selecionadas.Length == 0)
        {
            Status = "Selecione ao menos uma ação para aprovar.";
            return;
        }

        var requisicao = new RequisicaoIpc
        {
            Metodo = "aprovar",
            Parametros = JsonSerializer.SerializeToElement(new { acoes = selecionadas }),
        };

        await ExecutarAsync(requisicao, resposta =>
        {
            ResultadoAprovacao = resposta.Sucesso
                ? (resposta.Resultado is RelatorioExecucao r
                    ? $"Aplicado: sucesso={r.Sucesso}, {r.Categorias.Count} categoria(s)."
                    : "Aplicado.")
                : "Falha: " + resposta.Erro;
        });
    }

    private async Task ExecutarAsync(RequisicaoIpc requisicao, Action<RespostaIpc> aoConcluir)
    {
        Ocupado = true;
        Status = $"Executando '{requisicao.Metodo}'…";
        try
        {
            var resposta = await _agente.TratarAsync(requisicao);
            aoConcluir(resposta);
            Status = resposta.Sucesso ? $"'{requisicao.Metodo}' concluído." : $"'{requisicao.Metodo}' falhou: {resposta.Erro}";
        }
        catch (Exception ex)
        {
            Status = "Erro: " + ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }
}
````

### `src/HardwareOptimizer.App/Views/MainWindow.axaml`

````xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:HardwareOptimizer.App.ViewModels"
        x:Class="HardwareOptimizer.App.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Width="860" Height="620"
        Title="Agente de Otimização de Hardware">

  <DockPanel Margin="12">

    <TextBlock DockPanel.Dock="Top"
               Text="Agente de Otimização e Confiabilidade de Hardware"
               FontSize="18" FontWeight="Bold" Margin="0,0,0,8" />

    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,8">
      <Button Content="Coletar inventário" Command="{Binding ColetarCommand}" IsEnabled="{Binding !Ocupado}" />
      <Button Content="Ler sensores" Command="{Binding LerSensoresCommand}" IsEnabled="{Binding !Ocupado}" />
      <Button Content="Propor ações" Command="{Binding ProporCommand}" IsEnabled="{Binding !Ocupado}" />
      <Button Content="Aprovar selecionadas" Command="{Binding AprovarCommand}" IsEnabled="{Binding !Ocupado}" />
    </StackPanel>

    <TextBlock DockPanel.Dock="Bottom" Text="{Binding Status}" Margin="0,8,0,0" Foreground="Gray" />

    <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,*">
      <StackPanel Grid.Row="0" Grid.Column="0" Grid.ColumnSpan="2" Margin="0,0,0,8">
        <TextBlock Text="Inventário" FontWeight="Bold" />
        <TextBlock Text="{Binding InventarioResumo}" TextWrapping="Wrap" />
      </StackPanel>

      <DockPanel Grid.Row="1" Grid.Column="0" Margin="0,0,8,0">
        <TextBlock DockPanel.Dock="Top" Text="Sensores" FontWeight="Bold" />
        <ListBox ItemsSource="{Binding Sensores}" />
      </DockPanel>

      <DockPanel Grid.Row="1" Grid.Column="1">
        <TextBlock DockPanel.Dock="Top" Text="Matriz de decisão (marque para aprovar)" FontWeight="Bold" />
        <TextBlock DockPanel.Dock="Bottom" Text="{Binding ResultadoAprovacao}" TextWrapping="Wrap" Foreground="#2E7D32" />
        <ListBox ItemsSource="{Binding Matriz}">
          <ListBox.ItemTemplate>
            <DataTemplate x:DataType="vm:ItemMatrizViewModel">
              <CheckBox IsChecked="{Binding Selecionado}">
                <StackPanel>
                  <TextBlock Text="{Binding Descricao}" TextWrapping="Wrap" />
                  <TextBlock Text="{Binding Risco}" Foreground="Gray" FontSize="11" />
                </StackPanel>
              </CheckBox>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>
      </DockPanel>
    </Grid>

  </DockPanel>
</Window>
````

### `src/HardwareOptimizer.App/Views/MainWindow.axaml.cs`

````csharp
using Avalonia.Controls;

namespace HardwareOptimizer.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
````


## HardwareOptimizer.Cli

### `src/HardwareOptimizer.Cli/HardwareOptimizer.Cli.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
    <ProjectReference Include="..\HardwareOptimizer.Agent\HardwareOptimizer.Agent.csproj" />
    <ProjectReference Include="..\HardwareOptimizer.Cerebro\HardwareOptimizer.Cerebro.csproj" />
    <ProjectReference Include="..\HardwareOptimizer.Ipc\HardwareOptimizer.Ipc.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.1" />
  </ItemGroup>

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

### `src/HardwareOptimizer.Cli/Apresentacao.cs`

````csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HardwareOptimizer.Cli;

/// <summary>Helpers de saída no console para os fluxos da CLI.</summary>
internal static class Apresentacao
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Titulo(string texto)
    {
        Console.WriteLine();
        Console.WriteLine("== " + texto + " ==");
    }

    public static void Item(string rotulo, string? valor) =>
        Console.WriteLine($"  - {rotulo}: {valor ?? "(n/d)"}");

    public static void Linha(string texto = "") => Console.WriteLine(texto);

    public static void ImprimirJson<T>(T objeto) =>
        Console.WriteLine(JsonSerializer.Serialize(objeto, Json));
}
````

### `src/HardwareOptimizer.Cli/ArquivoLoggerProvider.cs`

````csharp
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Cli;

/// <summary>
/// Provider de log em arquivo (append, thread-safe). Cada linha registra
/// timestamp, nível, categoria (classe) e mensagem — formato pensado para
/// análise posterior do ponto exato de falha. Não há provider de arquivo
/// embutido no Microsoft.Extensions.Logging, por isso este mínimo.
/// </summary>
public sealed class ArquivoLoggerProvider : ILoggerProvider
{
    private static readonly Encoding Utf8SemBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _caminho;
    private readonly LogLevel _minimo;
    private readonly object _trava = new();

    public ArquivoLoggerProvider(string caminho, LogLevel minimo = LogLevel.Debug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        _caminho = caminho;
        _minimo = minimo;

        var diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }
    }

    public ILogger CreateLogger(string categoryName) => new ArquivoLogger(categoryName, this, _minimo);

    public void Dispose()
    {
        // Sem recursos persistentes: cada escrita abre/fecha o arquivo.
    }

    private void Anexar(string linha)
    {
        lock (_trava)
        {
            File.AppendAllText(_caminho, linha + Environment.NewLine, Utf8SemBom);
        }
    }

    private sealed class ArquivoLogger : ILogger
    {
        private readonly string _categoria;
        private readonly ArquivoLoggerProvider _provider;
        private readonly LogLevel _minimo;

        public ArquivoLogger(string categoria, ArquivoLoggerProvider provider, LogLevel minimo)
        {
            _categoria = categoria;
            _provider = provider;
            _minimo = minimo;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimo && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var idx = _categoria.LastIndexOf('.');
            var categoriaCurta = idx >= 0 ? _categoria[(idx + 1)..] : _categoria;

            var sb = new StringBuilder(160);
            sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append(" [").Append(Nivel(logLevel)).Append("] ");
            sb.Append(categoriaCurta).Append(" - ");
            sb.Append(formatter(state, exception));

            if (exception is not null)
            {
                sb.Append(" | EXCEÇÃO ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            }

            _provider.Anexar(sb.ToString());
        }

        private static string Nivel(LogLevel nivel) => nivel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "?????",
        };
    }
}
````

### `src/HardwareOptimizer.Cli/Program.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Bios;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Execution.Windows;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Ipc;
using HardwareOptimizer.Cerebro.Visao;
using HardwareOptimizer.Cli;
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal static class Program
{
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    private static async Task<int> Main(string[] args)
    {
        var comando = args.Length > 0 ? args[0].ToLowerInvariant() : "ajuda";

        // Log persistente em arquivo, para análise posterior do ponto exato de falha.
        var caminhoLog = Path.Combine(
            AppContext.BaseDirectory, "data", "logs", $"otimizador-{DateTime.Now:yyyyMMdd}.log");
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new ArquivoLoggerProvider(caminhoLog, LogLevel.Debug));
        });
        _loggerFactory = loggerFactory;

        var log = loggerFactory.CreateLogger("Program");
        log.LogInformation("=== Início: comando '{Comando}' ===", comando);
        // Caminho do log vai para stderr para não poluir a saída JSON em stdout.
        Console.Error.WriteLine($"[log] Registro do processo em: {caminhoLog}");

        try
        {
            switch (comando)
            {
                case "coletar":
                    await ComandoColetar();
                    return 0;
                case "sanitizar":
                    await ComandoSanitizar();
                    return 0;
                case "catalogo":
                    ComandoCatalogo();
                    return 0;
                case "relatorio":
                    await ComandoRelatorio();
                    return 0;
                case "sensores":
                    await ComandoSensores();
                    return 0;
                case "servir":
                    await ComandoServir(args);
                    return 0;
                case "ipc-demo":
                    await ComandoIpcDemo();
                    return 0;
                case "bios":
                    await ComandoBios();
                    return 0;
                case "proposta":
                    await ComandoProposta();
                    return 0;
                case "visao":
                    await ComandoVisao(args);
                    return 0;
                case "demo":
                    await ComandoDemo();
                    return 0;
                case "aplicar":
                    return await ComandoAplicar(args);
                default:
                    ImprimirAjuda();
                    return 0;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            log.LogError(ex, "Falha ao executar o comando '{Comando}'.", comando);
            Console.Error.WriteLine("Erro: " + ex.Message);
            return 1;
        }
        finally
        {
            log.LogInformation("=== Fim: comando '{Comando}' ===", comando);
        }
    }

    private static ILogger Log<T>() => _loggerFactory.CreateLogger<T>();

    private static void ImprimirAjuda()
    {
        Apresentacao.Linha("Agente de Otimização e Confiabilidade de Hardware — CLI (MVP)");
        Apresentacao.Linha();
        Apresentacao.Linha("Uso: hwopt <comando>");
        Apresentacao.Linha();
        Apresentacao.Linha("Comandos:");
        Apresentacao.Linha("  coletar     Coleta o inventário (read-only) e imprime em JSON.");
        Apresentacao.Linha("  sanitizar   Coleta e mostra a versão segura para nuvem + relatório de privacidade.");
        Apresentacao.Linha("  catalogo    Lista o catálogo de ações whitelisted e seus limites.");
        Apresentacao.Linha("  relatorio   Gera o relatório executivo e a nota 0-100 do equipamento.");
        Apresentacao.Linha("  sensores    Lê os sensores (temperatura, clock, voltagem, fan, consumo) em tempo real.");
        Apresentacao.Linha("  servir      Hospeda o servidor IPC (named pipe) para a UI. Ctrl+C encerra.");
        Apresentacao.Linha("  ipc-demo    Demonstra o IPC (servidor + cliente no mesmo processo).");
        Apresentacao.Linha("  bios        Identifica a BIOS, verifica com o fabricante e gera o guia (não aplica).");
        Apresentacao.Linha("  proposta    Cérebro propõe a matriz de decisão a partir do inventário sanitizado.");
        Apresentacao.Linha("  visao <img> Interpreta uma foto (BIOS/etiqueta/erro/benchmark) e cruza com o inventário.");
        Apresentacao.Linha("  demo        Executa o fluxo completo ponta a ponta (modo simulação seguro).");
        Apresentacao.Linha("  aplicar [ids...]  Aplica um perfil seguro (coleta→backup→executa→valida→rollback).");
        Apresentacao.Linha("              Sem ids, usa a proposta do cérebro. Simulação por padrão;");
        Apresentacao.Linha("              HWOPT_EXECUCAO_REAL=1 em terminal Administrador aplica de verdade.");
    }

    private static async Task ComandoColetar()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        Apresentacao.ImprimirJson(inventario);
    }

    private static async Task ComandoSanitizar()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var resultado = new Sanitizador(logger: Log<Sanitizador>()).Sanitizar(inventario);

        Apresentacao.Titulo("Inventário seguro para nuvem");
        Apresentacao.ImprimirJson(resultado.InventarioSeguro);

        Apresentacao.Titulo("Relatório de sanitização (o que foi tratado antes de sair da máquina)");
        if (resultado.CamposAlterados.Count == 0)
        {
            Apresentacao.Linha("  (nenhum campo sensível encontrado)");
        }

        foreach (var campo in resultado.CamposAlterados)
        {
            Apresentacao.Item(campo.Campo, campo.Acao.ToString());
        }
    }

    private static void ComandoCatalogo()
    {
        var catalogo = CatalogoPadrao.Criar();
        Apresentacao.Linha($"Catálogo whitelisted (versão {catalogo.Versao}) — {catalogo.Todas.Count} ações");

        foreach (var acao in catalogo.Todas.OrderBy(a => a.Categoria).ThenBy(a => a.Id))
        {
            Apresentacao.Titulo($"{acao.Id}  [{acao.Categoria}]  risco={acao.Risco}");
            Apresentacao.Item("Título", acao.Titulo);
            Apresentacao.Item("Reinício", acao.RequerReinicio ? "sim" : "não");
            Apresentacao.Item("Pré-condições", string.Join(", ", acao.PreCondicoes));

            foreach (var parametro in acao.Parametros)
            {
                if (parametro is ParametroNumerico n)
                {
                    Apresentacao.Item(
                        $"param {n.Nome}",
                        $"seguro {n.FaixaSegura}, permitido {n.FaixaPermitida}, limite_absoluto {n.LimiteAbsoluto}, padrão {n.PadraoSeguro}{n.Unidade}");
                }
                else if (parametro is ParametroListaBranca l)
                {
                    Apresentacao.Item($"param {l.Nome}", "lista segura: " + string.Join(", ", l.ValoresSeguros));
                }
            }
        }
    }

    private static async Task ComandoRelatorio()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var relatorio = GerarRelatorioExecutivo(inventario, execucao: null);

        Apresentacao.Titulo("Relatório executivo (diagnóstico do equipamento)");
        ImprimirRelatorioExecutivo(relatorio);
    }

    private static RelatorioExecutivo GerarRelatorioExecutivo(Inventario inventario, RelatorioExecucao? execucao)
    {
        var validacoes = new List<ResultadoValidacao>();
        var alteracoes = new List<AlteracaoResumo>();
        var dominiosOtimizados = new HashSet<Dominio>();

        if (execucao is not null)
        {
            foreach (var categoria in execucao.Categorias)
            {
                if (categoria.Validacao is not null)
                {
                    validacoes.Add(categoria.Validacao);
                }

                if (categoria.Situacao == SituacaoCategoria.Aplicada)
                {
                    dominiosOtimizados.Add(MapearDominio(categoria.Categoria));
                }
            }

            foreach (var alteracao in execucao.TodasAlteracoes)
            {
                alteracoes.Add(new AlteracaoResumo(alteracao.Alvo, alteracao.ValorAnterior, alteracao.ValorNovo));
            }
        }

        return new GeradorRelatorio().Gerar(inventario, validacoes, alteracoes, dominiosOtimizados);
    }

    private static Dominio MapearDominio(CategoriaAcao categoria) => categoria switch
    {
        CategoriaAcao.Cpu => Dominio.Cpu,
        CategoriaAcao.Memoria => Dominio.Ram,
        CategoriaAcao.Gpu => Dominio.Gpu,
        _ => Dominio.Windows,
    };

    private static void ImprimirRelatorioExecutivo(RelatorioExecutivo relatorio)
    {
        Apresentacao.Item("Nota final", $"{relatorio.NotaFinal}/100 ({relatorio.Classificacao})");
        foreach (var score in relatorio.Scores.OrderBy(s => s.Dominio))
        {
            Apresentacao.Item(score.Dominio.ToString(), $"{score.Valor}/100 ({score.Classificacao})");
        }

        if (relatorio.Alteracoes.Count > 0)
        {
            Apresentacao.Linha("  Alterações:");
            foreach (var alteracao in relatorio.Alteracoes)
            {
                Apresentacao.Linha(
                    $"      {alteracao.Alvo}: {alteracao.Antes ?? "(não definido)"} -> {alteracao.Depois}");
            }
        }
    }

    private static async Task ComandoServir(string[] args)
    {
        var nome = args.Length > 1 ? args[1] : "hwopt-agente";
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Apresentacao.Linha($"Servidor IPC no pipe '{nome}'. Ctrl+C para encerrar.");
        var servidor = new ServidorNamedPipe(nome, new RoteadorIpc(logger: Log<RoteadorIpc>()), Log<ServidorNamedPipe>());

        try
        {
            await servidor.ServirAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // encerramento solicitado
        }
    }

    private static async Task ComandoIpcDemo()
    {
        var nome = "hwopt-demo-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var servidor = new ServidorNamedPipe(nome, new RoteadorIpc(logger: Log<RoteadorIpc>()), Log<ServidorNamedPipe>());
        var tarefa = servidor.ServirAsync(cts.Token);

        var cliente = new ClienteNamedPipe(nome);
        Apresentacao.Titulo("IPC demo (servidor + cliente em processo)");

        foreach (var metodo in new[] { "ping", "catalogo", "coletar", "sensores", "proposta", "relatorio" })
        {
            var resposta = await cliente.ChamarAsync(metodo, cts.Token);
            Apresentacao.Item(metodo, resposta.Sucesso ? "OK" : "ERRO: " + resposta.Erro);
        }

        // Fluxo de aprovação explícita por ação (a UI envia os IDs aprovados).
        var aprovacao = await cliente.ChamarAsync(
            new RequisicaoIpc
            {
                Metodo = "aprovar",
                Parametros = JsonSerializer.SerializeToElement(
                    new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO", "SO_EFEITOS_VISUAIS_DESEMPENHO" } }),
            },
            cts.Token);
        Apresentacao.Item("aprovar (2 ações)", aprovacao.Sucesso ? "executado e validado" : "ERRO: " + aprovacao.Erro);

        await cts.CancelAsync();
        try
        {
            await tarefa;
        }
        catch (OperationCanceledException)
        {
            // encerramento esperado
        }
    }

    private static async Task ComandoSensores()
    {
        var leitura = await new ServicoSensores(loggerFactory: _loggerFactory).LerAsync();

        Apresentacao.Titulo("Sensores (tempo real)");
        if (leitura.Sensores.Count == 0)
        {
            Apresentacao.Linha("  (nenhum sensor legível nesta máquina — driver/permissão ausente)");
            return;
        }

        foreach (var sensor in leitura.Sensores.OrderBy(s => s.Tipo).ThenBy(s => s.Nome, StringComparer.Ordinal))
        {
            Apresentacao.Item($"{sensor.Tipo} — {sensor.Nome}", $"{sensor.Valor} {sensor.Unidade}");
        }
    }

    private static async Task ComandoBios()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var repositorio = RepositorioSqlite.DeArquivo(
            Path.Combine(AppContext.BaseDirectory, "data", "otimizador.db"),
            Log<RepositorioSqlite>());
        await repositorio.InicializarAsync();

        // Banco curado com cache em SQLite (a busca web entraria como provedor interno futuro).
        var provedor = new ProvedorBiosComCache(
            new BancoCuradoBios(), repositorio, Log<ProvedorBiosComCache>());
        var relatorio = await new ModuloBios(provedor, Log<ModuloBios>()).AnalisarAsync(inventario);

        ImprimirRelatorioBios(relatorio);
    }

    private static void ImprimirRelatorioBios(RelatorioBios relatorio)
    {
        var id = relatorio.Identificacao;
        Apresentacao.Titulo("BIOS — Identificação");
        Apresentacao.Item("Fabricante", $"{id.Fabricante} (bruto: {id.FabricanteBruto})");
        Apresentacao.Item("Modelo", id.Modelo);
        Apresentacao.Item("Versão atual", id.VersaoAtual);
        Apresentacao.Item("Modo", id.Modo);
        Apresentacao.Item("Secure Boot", id.SecureBoot?.ToString());
        Apresentacao.Item("Fonte encontrada", relatorio.FonteEncontrada ? "sim (banco curado)" : "não");

        var decisao = relatorio.Decisao;
        Apresentacao.Titulo("BIOS — Decisão conservadora");
        Apresentacao.Item("Recomenda atualizar", decisao.RecomendaAtualizar ? "sim" : "não");
        Apresentacao.Item("Versão recomendada", decisao.VersaoRecomendada);
        Apresentacao.Item("Ganho", decisao.Ganho.ToString());
        Apresentacao.Item("Risco", decisao.Risco.ToString());
        Apresentacao.Item("Justificativa", decisao.Justificativa);
        Apresentacao.Item("Fonte", decisao.Fonte);

        var guia = relatorio.Guia;
        Apresentacao.Titulo("BIOS — Guia passo a passo");
        Apresentacao.Item("Tecla de setup", guia.TeclaSetup);
        Apresentacao.Item("Utilitário", guia.Utilitario);
        foreach (var passo in guia.Passos)
        {
            Apresentacao.Linha("   - " + passo);
        }

        Apresentacao.Linha("  Avisos:");
        foreach (var aviso in guia.Avisos)
        {
            Apresentacao.Linha("   ! " + aviso);
        }
    }

    private static async Task ComandoProposta()
    {
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var sanitizacao = new Sanitizador(logger: Log<Sanitizador>()).Sanitizar(inventario);

        var matriz = await CriarCerebro().ProporAsync(sanitizacao.InventarioSeguro, CatalogoPadrao.Criar());
        ImprimirMatriz(matriz);
    }

    /// <summary>
    /// Usa o cérebro LLM quando HWOPT_LLM_MODELO e ANTHROPIC_API_KEY estão
    /// definidos no ambiente; caso contrário, usa o cérebro local (offline).
    /// O modelo nunca é fixado no código — vem da configuração.
    /// </summary>
    private static ICerebro CriarCerebro()
    {
        var modelo = Environment.GetEnvironmentVariable("HWOPT_LLM_MODELO");
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (!string.IsNullOrWhiteSpace(modelo) && !string.IsNullOrWhiteSpace(apiKey))
        {
            return new CerebroLlm(new ClienteLlmAnthropic(modelo, apiKey), Log<CerebroLlm>());
        }

        return new CerebroLocal();
    }

    private static async Task ComandoVisao(string[] args)
    {
        var modelo = Environment.GetEnvironmentVariable("HWOPT_LLM_MODELO");
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(modelo) || string.IsNullOrWhiteSpace(apiKey))
        {
            Apresentacao.Linha(
                "O módulo de visão exige um modelo multimodal: defina ANTHROPIC_API_KEY e HWOPT_LLM_MODELO.");
            return;
        }

        if (args.Length < 2)
        {
            Apresentacao.Linha("Uso: hwopt visao <arquivo-de-imagem> [bios|etiqueta|erro|benchmark]");
            return;
        }

        var caminho = args[1];
        if (!File.Exists(caminho))
        {
            Apresentacao.Linha($"Arquivo não encontrado: {caminho}");
            return;
        }

        var caso = MapearCaso(args.Length > 2 ? args[2] : null);
        var imagem = ImagemEntrada.DeArquivo(caminho);

        var modulo = new ModuloVisao(new ClienteVisaoAnthropic(modelo, apiKey), Log<ModuloVisao>());
        var leitura = await modulo.InterpretarAsync(imagem, caso);

        Apresentacao.Titulo("Leitura visual");
        Apresentacao.Item("Tela", leitura.TipoTela.ToString());
        Apresentacao.Item("Confiança", leitura.Confianca.ToString());
        foreach (var campo in leitura.Campos)
        {
            Apresentacao.Item("  " + campo.Key, campo.Value);
        }

        Apresentacao.Item("Próximo passo", leitura.ProximoPasso);

        // Regra do documento: validar a leitura visual contra os dados coletados.
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        var conferencia = new ConferenciaVisual().Conferir(leitura, inventario);

        Apresentacao.Titulo("Conferência com o inventário");
        Apresentacao.Item("Situação", conferencia.Situacao.ToString());
        Apresentacao.Item("Detalhe", conferencia.Mensagem);
        if (conferencia.PedirNovaFoto)
        {
            Apresentacao.Linha("   ! Recomenda-se enviar uma nova foto, mais nítida.");
        }
    }

    private static CasoUsoVisao MapearCaso(string? arg) => (arg ?? string.Empty).ToLowerInvariant() switch
    {
        "bios" => CasoUsoVisao.LerVersaoBios,
        "etiqueta" => CasoUsoVisao.LerEtiquetaPlaca,
        "erro" => CasoUsoVisao.LerMensagemErro,
        "benchmark" => CasoUsoVisao.LerBenchmark,
        _ => CasoUsoVisao.Identificar,
    };

    private static void ImprimirMatriz(MatrizDecisao matriz)
    {
        var origem = matriz.Modelo is null ? matriz.Origem.ToString() : $"{matriz.Origem}/{matriz.Modelo}";
        Apresentacao.Item("Origem", origem);
        foreach (var item in matriz.Itens)
        {
            var parametros = item.Parametros.Count == 0
                ? string.Empty
                : " [" + string.Join(", ", item.Parametros.Select(p => $"{p.Key}={p.Value}")) + "]";
            Apresentacao.Item(
                $"{item.Prioridade}. {item.AcaoId}",
                $"risco {item.Risco}, ganho {item.GanhoEsperado}{parametros}");
        }

        foreach (var aviso in matriz.Avisos)
        {
            Apresentacao.Linha("   ! " + aviso);
        }
    }

    private static async Task ComandoDemo()
    {
        var catalogo = CatalogoPadrao.Criar();
        var caminhoBanco = Path.Combine(AppContext.BaseDirectory, "data", "otimizador.db");
        var repositorio = RepositorioSqlite.DeArquivo(caminhoBanco, Log<RepositorioSqlite>());
        await repositorio.InicializarAsync();

        // Passo 1 — Coleta read-only.
        Apresentacao.Titulo("Passo 1 — Coleta de inventário (read-only)");
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        Apresentacao.Item("Placa", $"{inventario.Placa.Fabricante} {inventario.Placa.Modelo}");
        Apresentacao.Item("CPU", inventario.Cpu.Nome);
        Apresentacao.Item("SO", $"{inventario.SistemaOperacional.Nome} ({inventario.SistemaOperacional.Tipo})");
        await repositorio.SalvarInventarioAsync(inventario);

        var sensores = await new ServicoSensores(loggerFactory: _loggerFactory).LerAsync();
        Apresentacao.Item(
            "Sensores",
            $"{sensores.Sensores.Count} leitura(s)"
            + (sensores.TemperaturaMaxC is { } tmax ? $", temperatura máx {tmax} °C" : string.Empty));

        // Passo 2 — Sanitização (privacidade).
        Apresentacao.Titulo("Passo 2 — Sanitização (privacidade)");
        var sanitizacao = new Sanitizador(logger: Log<Sanitizador>()).Sanitizar(inventario);
        Apresentacao.Item("Campos tratados", sanitizacao.CamposAlterados.Count.ToString());
        foreach (var campo in sanitizacao.CamposAlterados)
        {
            Apresentacao.Item(campo.Campo, campo.Acao.ToString());
        }

        // Passo 3 — Cérebro propõe (matriz de decisão; somente IDs do catálogo).
        Apresentacao.Titulo("Passo 3 — Cérebro propõe a matriz de decisão (inventário sanitizado)");
        var cerebro = CriarCerebro();
        var matriz = await cerebro.ProporAsync(sanitizacao.InventarioSeguro, catalogo);
        ImprimirMatriz(matriz);

        // Passo 4 — Perfil seguro a partir da matriz.
        var construtor = new ConstrutorPerfil(catalogo, Log<ConstrutorPerfil>());
        var perfilSeguro = construtor
            .CriarPerfilSeguro("perfil-seguro-demo", matriz.AcaoIds)
            .Perfil!;

        // Passo 5 — Backup obrigatório (bloqueante).
        Apresentacao.Titulo("Passo 4 — Backup obrigatório (bloqueante)");
        var backup = await new ServicoBackup(logger: Log<ServicoBackup>()).CriarBackupAsync(inventario);
        Apresentacao.Item("Backup confirmado", backup.Sucesso ? "sim" : "não");

        // Passo 6 — Execução controlada por categoria (modo simulação).
        Apresentacao.Titulo("Passo 5 — Execução controlada por categoria (modo simulação seguro)");
        var estado = new EstadoSistemaSimulado(new Dictionary<string, string>
        {
            ["registro:SystemResponsiveness"] = "20",
            ["powercfg:plano_ativo"] = "EQUILIBRADO",
        });
        var executor = new ExecutorControlado(
            catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel(), logger: Log<RunnerValidacao>()),
            Log<ExecutorControlado>());

        var contexto = new ContextoExecucao { BackupConfirmado = backup.Sucesso };
        var relatorio = await executor.AplicarPerfilAsync(perfilSeguro, contexto);
        ImprimirRelatorio(relatorio);
        await repositorio.RegistrarExecucaoAsync(relatorio);

        // Passo 5b — Validação detecta regressão simulada e reverte automaticamente.
        Apresentacao.Titulo("Passo 5b — Validação detecta regressão e reverte automaticamente");
        var estadoRegressao = new EstadoSistemaSimulado(new Dictionary<string, string>
        {
            ["registro:SystemResponsiveness"] = "20",
        });
        var executorRegressao = new ExecutorControlado(
            catalogo,
            RegistroComandos.Padrao(estadoRegressao),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.ComRegressao("whea"), logger: Log<RunnerValidacao>()),
            Log<ExecutorControlado>());
        var perfilRegressao = construtor.CriarPerfilSeguro("teste-regressao", new[] { "SO_SYSTEM_RESPONSIVENESS" }).Perfil!;
        var relRegressao = await executorRegressao.AplicarPerfilAsync(perfilRegressao, contexto);
        var categoriaRegressao = relRegressao.Categorias.Single();
        Apresentacao.Item("Categoria", categoriaRegressao.Categoria.ToString());
        Apresentacao.Item("Validação", categoriaRegressao.Validacao?.Estabilidade);
        Apresentacao.Item("Situação", categoriaRegressao.Situacao.ToString());
        Apresentacao.Item("Estado após rollback", estadoRegressao.Ler("registro:SystemResponsiveness") ?? "(restaurado)");

        // Passo 7 — Demonstração do perfil customizado e do consentimento.
        Apresentacao.Titulo("Passo 6 — Perfil customizado: bloqueio rígido por limite absoluto");
        var bloqueado = construtor.CriarPerfilCustomizado("custom-arriscado", "usuario", new[]
        {
            new SelecaoAcao
            {
                AcaoId = "SO_SYSTEM_RESPONSIVENESS",
                Parametros = new Dictionary<string, string> { ["percentual_reserva"] = "25" },
            },
        });
        Apresentacao.Item("Salvou?", bloqueado.Sucesso ? "sim" : "não (bloqueado)");
        foreach (var motivo in bloqueado.Bloqueios)
        {
            Apresentacao.Item("Bloqueio", motivo);
        }

        Apresentacao.Titulo("Passo 7 — Perfil customizado: risco assumido + consentimento");
        var customizado = construtor.CriarPerfilCustomizado("custom-demo", "usuario", new[]
        {
            new SelecaoAcao
            {
                AcaoId = "SO_SYSTEM_RESPONSIVENESS",
                Parametros = new Dictionary<string, string> { ["percentual_reserva"] = "5" },
            },
        });
        Apresentacao.Item("Válido?", customizado.Sucesso ? "sim" : "não");
        Apresentacao.Item("Exige consentimento?", customizado.ExigeConsentimento ? "sim" : "não");
        foreach (var risco in customizado.RiscosAssumidos)
        {
            Apresentacao.Item("Risco assumido", $"{risco.AcaoId}.{risco.Parametro} = {risco.Valor}");
        }

        await ProcessarConsentimento(customizado.Perfil!, estado, catalogo, repositorio);

        // Passo 8 — Auditoria.
        Apresentacao.Titulo("Passo 8 — Auditoria persistida (SQLite)");
        Apresentacao.Item("Inventários", (await repositorio.ContarInventariosAsync()).ToString());
        Apresentacao.Item("Consentimentos", (await repositorio.ContarConsentimentosAsync()).ToString());
        Apresentacao.Item("Execuções", (await repositorio.ContarExecucoesAsync()).ToString());
        Apresentacao.Linha();
        Apresentacao.Linha($"Banco: {caminhoBanco}");

        // Passo 9 — Relatório executivo e nota final.
        Apresentacao.Titulo("Passo 9 — Relatório executivo e nota final");
        var relatorioExecutivo = GerarRelatorioExecutivo(inventario, relatorio);
        ImprimirRelatorioExecutivo(relatorioExecutivo);
    }

    private static async Task<int> ComandoAplicar(string[] args)
    {
        var catalogo = CatalogoPadrao.Criar();
        var caminhoBanco = Path.Combine(AppContext.BaseDirectory, "data", "otimizador.db");
        var repositorio = RepositorioSqlite.DeArquivo(caminhoBanco, Log<RepositorioSqlite>());
        await repositorio.InicializarAsync();

        // Estado real (Windows + HWOPT_EXECUCAO_REAL) ou simulado (padrão seguro do projeto).
        var estado = EstadoSistemaWindows.Selecionar(_loggerFactory.CreateLogger("Aplicar"));
        var real = estado is EstadoSistemaWindows;

        Apresentacao.Titulo("Aplicar otimizações — " + (real ? "EXECUÇÃO REAL no Windows" : "SIMULAÇÃO (dry-run)"));
        if (real)
        {
            Apresentacao.Linha("  ATENÇÃO: as ações alterarão o sistema (registro, plano de energia, serviços).");
            Apresentacao.Linha("  Obs.: neste MVP a validação de estresse usa runner simulado (sem teste de carga real).");
        }
        else
        {
            Apresentacao.Linha("  Modo seguro: nada será alterado no sistema.");
            Apresentacao.Linha("  Para aplicar de verdade: terminal Administrador + HWOPT_EXECUCAO_REAL=1 (Windows).");
        }

        // 1) Inventário (read-only) + auditoria.
        var inventario = await new ColetorInventario(loggerFactory: _loggerFactory).ColetarAsync();
        await repositorio.SalvarInventarioAsync(inventario);
        Apresentacao.Item("Equipamento", $"{inventario.Placa.Fabricante} {inventario.Placa.Modelo} · {inventario.Cpu.Nome}");

        // 2) Seleção: IDs informados no comando ou a proposta do cérebro.
        var idsInformados = args.Skip(1).Where(a => !a.StartsWith('-')).ToList();
        List<string> ids;
        if (idsInformados.Count > 0)
        {
            ids = idsInformados;
            Apresentacao.Item("Ações (informadas)", string.Join(", ", ids));
        }
        else
        {
            var sanitizado = new Sanitizador(logger: Log<Sanitizador>()).Sanitizar(inventario).InventarioSeguro;
            var matriz = await CriarCerebro().ProporAsync(sanitizado, catalogo);
            ids = matriz.AcaoIds.ToList();
            Apresentacao.Item("Ações (propostas pelo cérebro)", ids.Count > 0 ? string.Join(", ", ids) : "(nenhuma)");
        }

        if (ids.Count == 0)
        {
            Apresentacao.Linha("Nada a aplicar.");
            return 0;
        }

        // 3) Perfil seguro (valores na faixa segura; sem risco assumido).
        var construcao = new ConstrutorPerfil(catalogo, Log<ConstrutorPerfil>())
            .CriarPerfilSeguro("perfil-aplicar", ids);
        if (!construcao.Sucesso)
        {
            Apresentacao.Titulo("Perfil inválido — nada aplicado");
            foreach (var bloqueio in construcao.Bloqueios)
            {
                Apresentacao.Item("Bloqueio", bloqueio);
            }

            return 1;
        }

        // 4) Backup obrigatório (bloqueante).
        var backup = await new ServicoBackup(logger: Log<ServicoBackup>()).CriarBackupAsync(inventario);
        Apresentacao.Item("Backup", backup.Sucesso ? "confirmado" : "FALHOU");
        if (!backup.Sucesso)
        {
            Apresentacao.Linha("Sem backup confirmado, nada é aplicado (regra de segurança).");
            return 1;
        }

        // 5) Execução controlada por categoria, com rollback automático em regressão.
        var executor = new ExecutorControlado(
            catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel(), logger: Log<RunnerValidacao>()),
            Log<ExecutorControlado>());

        var relatorio = await executor.AplicarPerfilAsync(
            construcao.Perfil!, new ContextoExecucao { BackupConfirmado = backup.Sucesso });

        Apresentacao.Titulo("Resultado da execução");
        ImprimirRelatorio(relatorio);
        await repositorio.RegistrarExecucaoAsync(relatorio);

        Apresentacao.Linha();
        Apresentacao.Item("Modo", real ? "execução real" : "simulação");
        Apresentacao.Item("Banco (auditoria)", caminhoBanco);
        return relatorio.Sucesso ? 0 : 1;
    }

    private static async Task ProcessarConsentimento(
        Perfil perfil, EstadoSistemaSimulado estado, CatalogoAcoes catalogo, IRepositorioOtimizacao repositorio)
    {
        var avaliador = new AvaliadorConsentimento(logger: Log<AvaliadorConsentimento>());
        var termo = avaliador.Termo;

        Apresentacao.Linha();
        Apresentacao.Linha("  >> " + termo.Titulo);
        foreach (var paragrafo in termo.CorpoAviso)
        {
            Apresentacao.Linha("     " + paragrafo);
        }

        // Simula o usuário marcando os dois checkboxes e confirmando.
        var resposta = new RespostaConsentimento(
            termo.Checkboxes.Select(c => c.Id), confirmacaoFinal: true);
        var consentimento = avaliador.Avaliar(resposta, perfil, catalogo.Versao);

        if (consentimento.Falha)
        {
            Apresentacao.Item("Consentimento", "recusado — " + consentimento.MensagemErro);
            return;
        }

        await repositorio.RegistrarConsentimentoAsync(consentimento.ValorObrigatorio);
        Apresentacao.Item("Consentimento", "registrado (auditoria gravada)");

        // Com consentimento registrado, o perfil pode ser aplicado.
        var perfilConsentido = perfil with { ConsentimentoRegistrado = true };
        var executor = new ExecutorControlado(
            catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel(), logger: Log<RunnerValidacao>()),
            Log<ExecutorControlado>());

        var relatorio = await executor.AplicarPerfilAsync(
            perfilConsentido, new ContextoExecucao { BackupConfirmado = true });
        await repositorio.RegistrarExecucaoAsync(relatorio);
        Apresentacao.Item("Aplicado", relatorio.Sucesso ? "sim" : "não");
        Apresentacao.Item("SystemResponsiveness agora", estado.Ler("registro:SystemResponsiveness"));
    }

    private static void ImprimirRelatorio(RelatorioExecucao relatorio)
    {
        Apresentacao.Item("Perfil", relatorio.PerfilNome);
        Apresentacao.Item("Sucesso geral", relatorio.Sucesso ? "sim" : "não");
        foreach (var categoria in relatorio.Categorias)
        {
            Apresentacao.Item(categoria.Categoria.ToString(), categoria.Situacao.ToString());
            foreach (var alteracao in categoria.Alteracoes)
            {
                Apresentacao.Linha(
                    $"      {alteracao.Alvo}: {alteracao.ValorAnterior ?? "(não definido)"} -> {alteracao.ValorNovo}");
            }
        }
    }
}
````


## HardwareOptimizer.Core.Tests

### `tests/HardwareOptimizer.Core.Tests/HardwareOptimizer.Core.Tests.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
  </ItemGroup>

</Project>
````

### `tests/HardwareOptimizer.Core.Tests/AnalisadorBiosTests.cs`

````csharp
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class AnalisadorBiosTests
{
    private static IdentificacaoBios Identificacao(string versaoAtual) => new()
    {
        FabricanteBruto = "ASUSTeK",
        Fabricante = "ASUS",
        Modelo = "ROG STRIX B550-F",
        VersaoAtual = versaoAtual,
        ChaveBusca = "asus|rog strix b550-f",
    };

    private static InfoBiosFabricante Info(string versao, GanhoEstimado ganho) => new()
    {
        Fabricante = "ASUS",
        Modelo = "ROG STRIX B550-F",
        VersaoMaisRecente = versao,
        Fonte = "https://www.asus.com/support/",
        Ganho = ganho,
        Motivo = "Estabilidade de memória.",
    };

    [Fact]
    public void Sem_info_do_fabricante_nao_recomenda_e_risco_medio()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("2806"), info: null);

        Assert.False(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Medio, decisao.Risco);
    }

    [Fact]
    public void Versao_atual_igual_ou_superior_nao_recomenda()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("3405"), Info("3405", GanhoEstimado.Medio));

        Assert.False(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Nenhum, decisao.Risco);
    }

    [Fact]
    public void Versao_mais_nova_sem_ganho_real_nao_recomenda()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("2806"), Info("3405", GanhoEstimado.Nenhum));

        Assert.False(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Medio, decisao.Risco);
    }

    [Fact]
    public void Versao_mais_nova_com_ganho_recomenda_com_risco_medio()
    {
        var decisao = new AnalisadorBios().Decidir(Identificacao("2806"), Info("3405", GanhoEstimado.Medio));

        Assert.True(decisao.RecomendaAtualizar);
        Assert.Equal(NivelRisco.Medio, decisao.Risco); // flash de BIOS nunca é risco baixo
        Assert.Equal("3405", decisao.VersaoRecomendada);
        Assert.Equal("2806", decisao.VersaoAtual);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/CalculadoraScoreTests.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Reporting;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class CalculadoraScoreTests
{
    private static Inventario InventarioBom() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806", Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6, Threads = 12, TempIdleC = 38 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 }, new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 } },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060", TempIdleC = 41, VersaoDriver = "551.23" } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
    };

    private static Inventario InventarioFraco() => new()
    {
        Placa = new PlacaMae { Fabricante = "?", Modelo = "?", Modo = "Legacy", SecureBoot = false },
        Cpu = new Processador { Nome = "CPU", Nucleos = 2 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 4 } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    private static readonly IReadOnlyList<ResultadoValidacao> SemTestes = Array.Empty<ResultadoValidacao>();

    private static HashSet<Dominio> Nenhum() => new();

    [Fact]
    public void Todas_as_notas_ficam_entre_0_e_100()
    {
        var calc = new CalculadoraScore();

        foreach (var inv in new[] { InventarioBom(), InventarioFraco() })
        {
            var resultado = calc.Calcular(inv, SemTestes, Nenhum());
            Assert.All(resultado.Scores, s => Assert.InRange(s.Valor, 0, 100));
            Assert.InRange(resultado.NotaFinal, 0, 100);
        }
    }

    [Fact]
    public void Os_sete_dominios_sao_pontuados()
    {
        var resultado = new CalculadoraScore().Calcular(InventarioBom(), SemTestes, Nenhum());

        Assert.Equal(7, resultado.Scores.Count);
        foreach (var dominio in Enum.GetValues<Dominio>())
        {
            Assert.NotNull(resultado.Obter(dominio));
        }
    }

    [Fact]
    public void Bios_uefi_com_secureboot_pontua_mais_que_legacy()
    {
        var calc = new CalculadoraScore();
        var bom = calc.Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Bios)!;
        var fraco = calc.Calcular(InventarioFraco(), SemTestes, Nenhum()).Obter(Dominio.Bios)!;

        Assert.True(bom.Valor > fraco.Valor, $"{bom.Valor} deveria ser > {fraco.Valor}");
    }

    [Fact]
    public void Ram_maior_e_mais_rapida_pontua_mais()
    {
        var calc = new CalculadoraScore();
        var bom = calc.Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Ram)!;
        var fraco = calc.Calcular(InventarioFraco(), SemTestes, Nenhum()).Obter(Dominio.Ram)!;

        Assert.True(bom.Valor > fraco.Valor);
    }

    [Fact]
    public void Estabilidade_sem_testes_eh_neutra()
    {
        var score = new CalculadoraScore().Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Estabilidade)!;
        Assert.Equal(70, score.Valor);
    }

    [Fact]
    public void Estabilidade_com_regressao_despenca()
    {
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Cpu", Ferramenta = "x", Regressao = true, Estabilidade = "Reprovado" },
        };

        var score = new CalculadoraScore().Calcular(InventarioBom(), validacoes, Nenhum()).Obter(Dominio.Estabilidade)!;
        Assert.Equal(30, score.Valor);
    }

    [Fact]
    public void Estabilidade_totalmente_validada_eh_maxima()
    {
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Cpu", Ferramenta = "x", Regressao = false, Estabilidade = "Totalmente validado" },
        };

        var score = new CalculadoraScore().Calcular(InventarioBom(), validacoes, Nenhum()).Obter(Dominio.Estabilidade)!;
        Assert.Equal(100, score.Valor);
    }

    [Fact]
    public void Otimizacoes_de_windows_aplicadas_elevam_a_nota_do_dominio()
    {
        var calc = new CalculadoraScore();
        var sem = calc.Calcular(InventarioBom(), SemTestes, Nenhum()).Obter(Dominio.Windows)!;
        var com = calc.Calcular(InventarioBom(), SemTestes, new HashSet<Dominio> { Dominio.Windows }).Obter(Dominio.Windows)!;

        Assert.True(com.Valor > sem.Valor);
    }

    [Fact]
    public void Hardware_eh_a_media_dos_componentes()
    {
        var resultado = new CalculadoraScore().Calcular(InventarioBom(), SemTestes, Nenhum());
        var esperado = (int)Math.Round(
            (resultado.Obter(Dominio.Cpu)!.Valor
            + resultado.Obter(Dominio.Gpu)!.Valor
            + resultado.Obter(Dominio.Ram)!.Valor
            + resultado.Obter(Dominio.Bios)!.Valor) / 4.0);

        Assert.Equal(esperado, resultado.Obter(Dominio.Hardware)!.Valor);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/CatalogoTests.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class CatalogoTests
{
    [Fact]
    public void CatalogoPadrao_eh_internamente_coerente()
    {
        var catalogo = CatalogoPadrao.Criar();

        var coerencia = catalogo.VerificarCoerencia();

        Assert.True(coerencia.Sucesso, coerencia.MensagemErro);
    }

    [Fact]
    public void CatalogoPadrao_contem_acoes_do_documento()
    {
        var catalogo = CatalogoPadrao.Criar();

        Assert.True(catalogo.Contem("PWR_PLANO_ALTO_DESEMPENHO"));
        Assert.True(catalogo.Contem("SRV_DESATIVAR_SERVICO"));
    }

    [Fact]
    public void Construtor_rejeita_ids_duplicados()
    {
        var acao = new AcaoOtimizacao
        {
            Id = "DUP",
            Categoria = Common.CategoriaAcao.Rede,
            Titulo = "x",
            Descricao = "x",
            ComandoInternoId = "cmd.x",
            Reversao = "x",
            Risco = Common.NivelRisco.Nenhum,
        };

        Assert.Throws<ArgumentException>(() => new CatalogoAcoes("v1", new[] { acao, acao }));
    }

    [Fact]
    public void Parametro_incoerente_eh_detectado()
    {
        // Faixa segura fora da permitida: deve falhar na verificação de coerência.
        var parametro = new ParametroNumerico(
            nome: "x",
            descricao: "x",
            faixaSegura: new FaixaNumerica(0, 100),
            faixaPermitida: new FaixaNumerica(0, 50),
            limiteAbsoluto: 50,
            padraoSeguro: 10);

        Assert.True(parametro.VerificarCoerencia().Falha);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/ConsentimentoTests.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ConsentimentoTests
{
    private static Perfil PerfilCustomizado() => new()
    {
        Nome = "perfil_teste",
        Tipo = TipoPerfil.Customizado,
        Autor = "usuario",
        Selecoes = new[]
        {
            new SelecaoAcao
            {
                AcaoId = "SO_SYSTEM_RESPONSIVENESS",
                Parametros = new Dictionary<string, string> { ["percentual_reserva"] = "5" },
            },
        },
    };

    [Fact]
    public void Go_nao_habilita_sem_os_dois_checkboxes()
    {
        var avaliador = new AvaliadorConsentimento();

        Assert.False(avaliador.PodeHabilitarConfirmacao(new[] { TermoConsentimento.IdAceiteRiscos }));
        Assert.False(avaliador.PodeHabilitarConfirmacao(Array.Empty<string>()));
    }

    [Fact]
    public void Go_habilita_com_os_dois_checkboxes()
    {
        var avaliador = new AvaliadorConsentimento();

        var pode = avaliador.PodeHabilitarConfirmacao(
            new[] { TermoConsentimento.IdAceiteRiscos, TermoConsentimento.IdDesejoProsseguir });

        Assert.True(pode);
    }

    [Fact]
    public void Termo_sem_obrigatorios_nao_habilita_confirmacao()
    {
        var termo = new TermoConsentimento("Aviso", new[] { "corpo" }, Array.Empty<Checkbox>());
        var avaliador = new AvaliadorConsentimento(termo);

        Assert.False(avaliador.PodeHabilitarConfirmacao(Array.Empty<string>()));
    }

    [Fact]
    public void Avaliar_gera_registro_de_auditoria_quando_completo()
    {
        var avaliador = new AvaliadorConsentimento();
        var resposta = new RespostaConsentimento(
            new[] { TermoConsentimento.IdAceiteRiscos, TermoConsentimento.IdDesejoProsseguir },
            confirmacaoFinal: true);

        var r = avaliador.Avaliar(resposta, PerfilCustomizado(), "cat-v1");

        Assert.True(r.Sucesso);
        Assert.Equal("perfil_teste", r.ValorObrigatorio.NomePerfil);
        Assert.Equal("cat-v1", r.ValorObrigatorio.VersaoCatalogo);
        Assert.Contains("SO_SYSTEM_RESPONSIVENESS.percentual_reserva = 5", r.ValorObrigatorio.ValoresEscolhidos);
    }

    [Fact]
    public void Avaliar_falha_sem_confirmacao_final()
    {
        var avaliador = new AvaliadorConsentimento();
        var resposta = new RespostaConsentimento(
            new[] { TermoConsentimento.IdAceiteRiscos, TermoConsentimento.IdDesejoProsseguir },
            confirmacaoFinal: false);

        Assert.True(avaliador.Avaliar(resposta, PerfilCustomizado(), "cat-v1").Falha);
    }

    [Fact]
    public void Avaliar_falha_com_apenas_um_checkbox()
    {
        var avaliador = new AvaliadorConsentimento();
        var resposta = new RespostaConsentimento(
            new[] { TermoConsentimento.IdAceiteRiscos }, confirmacaoFinal: true);

        Assert.True(avaliador.Avaliar(resposta, PerfilCustomizado(), "cat-v1").Falha);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/ConstrutorPerfilTests.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ConstrutorPerfilTests
{
    private static ConstrutorPerfil Construtor() => new(CatalogoPadrao.Criar());

    private static SelecaoAcao Selecao(string id, params (string Nome, string Valor)[] pares) => new()
    {
        AcaoId = id,
        Parametros = pares.ToDictionary(p => p.Nome, p => p.Valor, StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public void Perfil_seguro_usa_valor_padrao_e_nao_exige_consentimento()
    {
        var r = Construtor().CriarPerfilSeguro(
            "padrao", new[] { "PWR_PLANO_ALTO_DESEMPENHO", "SO_SYSTEM_RESPONSIVENESS" });

        Assert.True(r.Sucesso);
        Assert.False(r.ExigeConsentimento);
        Assert.NotNull(r.Perfil);

        var selecao = r.Perfil!.Selecoes.Single(s => s.AcaoId == "SO_SYSTEM_RESPONSIVENESS");
        Assert.Equal("20", selecao.Parametros["percentual_reserva"]);
    }

    [Fact]
    public void Perfil_seguro_com_acao_inexistente_eh_bloqueado()
    {
        var r = Construtor().CriarPerfilSeguro("x", new[] { "NAO_EXISTE" });

        Assert.False(r.Sucesso);
        Assert.Null(r.Perfil);
        Assert.NotEmpty(r.Bloqueios);
    }

    [Fact]
    public void Perfil_customizado_com_risco_assumido_exige_consentimento()
    {
        var r = Construtor().CriarPerfilCustomizado(
            "meu_perfil", "usuario", new[] { Selecao("SO_SYSTEM_RESPONSIVENESS", ("percentual_reserva", "5")) });

        Assert.True(r.Sucesso);
        Assert.True(r.ExigeConsentimento);
        Assert.Single(r.RiscosAssumidos);
        Assert.False(r.Perfil!.ConsentimentoRegistrado);
        Assert.False(r.Perfil.PodeAplicar); // customizado não aplica sem consentimento.
    }

    [Fact]
    public void Perfil_customizado_acima_do_limite_absoluto_eh_bloqueado_sem_perfil()
    {
        var r = Construtor().CriarPerfilCustomizado(
            "agressivo", "usuario", new[] { Selecao("SO_SYSTEM_RESPONSIVENESS", ("percentual_reserva", "25")) });

        Assert.False(r.Sucesso);
        Assert.Null(r.Perfil);
        Assert.NotEmpty(r.Bloqueios);
    }

    [Fact]
    public void Perfil_customizado_dentro_da_faixa_segura_ainda_exige_consentimento()
    {
        // Mesmo só com valores seguros, qualquer perfil customizado exige consentimento ao salvar.
        var r = Construtor().CriarPerfilCustomizado(
            "conservador", "usuario", new[] { Selecao("SO_SYSTEM_RESPONSIVENESS", ("percentual_reserva", "20")) });

        Assert.True(r.Sucesso);
        Assert.True(r.ExigeConsentimento);
        Assert.Empty(r.RiscosAssumidos);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/FaixaNumericaTests.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class FaixaNumericaTests
{
    [Fact]
    public void Construtor_rejeita_maximo_menor_que_minimo()
    {
        Assert.Throws<ArgumentException>(() => new FaixaNumerica(10, 5));
    }

    [Theory]
    [InlineData(0, 10, 5, true)]
    [InlineData(0, 10, 0, true)] // inclui o mínimo
    [InlineData(0, 10, 10, true)] // inclui o máximo
    [InlineData(0, 10, -1, false)]
    [InlineData(0, 10, 11, false)]
    public void Contem_respeita_intervalo_fechado(double min, double max, double valor, bool esperado)
    {
        Assert.Equal(esperado, new FaixaNumerica(min, max).Contem(valor));
    }

    [Fact]
    public void EstaContidaEm_detecta_continencia()
    {
        var interna = new FaixaNumerica(10, 20);
        var externa = new FaixaNumerica(0, 20);

        Assert.True(interna.EstaContidaEm(externa));
        Assert.False(externa.EstaContidaEm(interna));
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/GeradorGuiaBiosTests.cs`

````csharp
using HardwareOptimizer.Core.Bios;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class GeradorGuiaBiosTests
{
    private static IdentificacaoBios Identificacao(string fabricante) => new()
    {
        FabricanteBruto = fabricante,
        Fabricante = fabricante,
        Modelo = "Placa X",
        ChaveBusca = $"{fabricante.ToLowerInvariant()}|placa x",
    };

    [Theory]
    [InlineData("ASUS", "EZ Flash")]
    [InlineData("MSI", "M-Flash")]
    [InlineData("Gigabyte", "Q-Flash")]
    [InlineData("ASRock", "Instant Flash")]
    public void Guia_usa_o_utilitario_do_fabricante(string fabricante, string utilitarioEsperado)
    {
        var guia = new GeradorGuiaBios().Gerar(Identificacao(fabricante));

        Assert.Contains(utilitarioEsperado, guia.Utilitario, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guia_sempre_traz_avisos_de_seguranca_e_ajustes()
    {
        var guia = new GeradorGuiaBios().Gerar(Identificacao("ASUS"));

        Assert.NotEmpty(guia.Passos);
        Assert.NotEmpty(guia.Avisos);
        Assert.NotEmpty(guia.AjustesRecomendados);
        Assert.Contains(guia.Avisos, a => a.Contains("brick", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Fabricante_desconhecido_gera_guia_generico()
    {
        var guia = new GeradorGuiaBios().Gerar(Identificacao("FabricanteX"));

        Assert.NotEmpty(guia.Utilitario);
        Assert.NotEmpty(guia.TeclaSetup);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/GeradorRelatorioTests.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Reporting;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class GeradorRelatorioTests
{
    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806", Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6, TempIdleC = 38 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
    };

    [Fact]
    public void Relatorio_consolida_notas_alteracoes_e_destaques()
    {
        var alteracoes = new[]
        {
            new AlteracaoResumo("registro:SystemResponsiveness", "20", "10"),
        };
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Windows", Ferramenta = "x", Regressao = false, Estabilidade = "Totalmente validado" },
        };

        var relatorio = new GeradorRelatorio().Gerar(
            Inventario(), validacoes, alteracoes, new HashSet<Dominio> { Dominio.Windows });

        Assert.Equal(7, relatorio.Scores.Count);
        Assert.InRange(relatorio.NotaFinal, 0, 100);
        Assert.False(relatorio.RegressaoDetectada);
        Assert.Single(relatorio.Alteracoes);
        Assert.NotEmpty(relatorio.Destaques);
        Assert.NotEmpty(relatorio.ResumoExecutivo);
    }

    [Fact]
    public void Regressao_eh_refletida_no_relatorio()
    {
        var validacoes = new[]
        {
            new ResultadoValidacao { Categoria = "Cpu", Ferramenta = "x", Regressao = true, Estabilidade = "Reprovado" },
        };

        var relatorio = new GeradorRelatorio().Gerar(
            Inventario(), validacoes, Array.Empty<AlteracaoResumo>(), new HashSet<Dominio>());

        Assert.True(relatorio.RegressaoDetectada);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/GlobalUsings.cs`

````csharp
global using Xunit;
````

### `tests/HardwareOptimizer.Core.Tests/NormalizadorFabricanteTests.cs`

````csharp
using HardwareOptimizer.Core.Bios;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class NormalizadorFabricanteTests
{
    [Theory]
    [InlineData("ASUSTeK Computer Inc.", "ASUS")]
    [InlineData("ASUS", "ASUS")]
    [InlineData("Micro-Star International Co., Ltd.", "MSI")]
    [InlineData("Gigabyte Technology Co., Ltd.", "Gigabyte")]
    [InlineData("ASRock", "ASRock")]
    [InlineData("Hewlett-Packard", "HP")]
    public void Normalizar_padroniza_nomes_sujos(string bruto, string esperado)
    {
        Assert.Equal(esperado, NormalizadorFabricante.Normalizar(bruto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_trata_vazio(string? bruto)
    {
        Assert.Equal("Desconhecido", NormalizadorFabricante.Normalizar(bruto));
    }

    [Fact]
    public void GerarChaveBusca_normaliza_fabricante_e_colapsa_espacos()
    {
        var chave = NormalizadorFabricante.GerarChaveBusca("ASUSTeK Computer Inc.", "  ROG  STRIX   B550-F  ");
        Assert.Equal("asus|rog strix b550-f", chave);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/ResultadoTests.cs`

````csharp
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ResultadoTests
{
    [Fact]
    public void Ok_indica_sucesso_sem_erros()
    {
        var r = Resultado.Ok();
        Assert.True(r.Sucesso);
        Assert.False(r.Falha);
        Assert.Empty(r.Erros);
    }

    [Fact]
    public void Falhar_acumula_erros()
    {
        var r = Resultado.Falhar("a", "b");
        Assert.True(r.Falha);
        Assert.Equal(2, r.Erros.Count);
        Assert.Contains("a", r.MensagemErro, StringComparison.Ordinal);
    }

    [Fact]
    public void Falhar_sem_mensagem_gera_erro_padrao()
    {
        var r = Resultado.Falhar();
        Assert.True(r.Falha);
        Assert.NotEmpty(r.Erros);
    }

    [Fact]
    public void ResultadoGenerico_ok_carrega_valor()
    {
        var r = Resultado<int>.Ok(42);
        Assert.True(r.Sucesso);
        Assert.Equal(42, r.ValorObrigatorio);
    }

    [Fact]
    public void ResultadoGenerico_falha_lanca_ao_acessar_valor_obrigatorio()
    {
        var r = Resultado<string>.Falhar("erro");
        Assert.True(r.Falha);
        Assert.Throws<InvalidOperationException>(() => r.ValorObrigatorio);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/SanitizadorTests.cs`

````csharp
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class SanitizadorTests
{
    private static Inventario InventarioComSegredos() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = "2806" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
        Rede = new[]
        {
            new InterfaceRede { Nome = "eth0", EnderecoMac = "AA:BB:CC:DD:EE:FF" },
        },
        Identificadores = new IdentificadoresSensiveis
        {
            NumeroSerie = "SN-12345",
            UuidPlaca = "uuid-abcdef",
            NomeMaquina = "PC-DO-MICHEL",
            NomeUsuario = "michel",
            ChaveProdutoWindows = "XXXXX-YYYYY-ZZZZZ",
        },
    };

    [Fact]
    public void Sanitizar_hasheia_correlacionaveis_e_remove_pii()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());
        var ident = resultado.InventarioSeguro.Identificadores;

        Assert.NotNull(ident);

        // Correlacionáveis: preservados apenas como hash (o valor bruto não vaza).
        Assert.StartsWith("sha256:", ident!.NumeroSerie);
        Assert.StartsWith("sha256:", ident.UuidPlaca);
        Assert.DoesNotContain("SN-12345", ident.NumeroSerie!, StringComparison.Ordinal);

        // PII: removida.
        Assert.Null(ident.NomeMaquina);
        Assert.Null(ident.NomeUsuario);
        Assert.Null(ident.ChaveProdutoWindows);
    }

    [Fact]
    public void Sanitizar_preserva_dados_de_baixo_risco()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());

        Assert.Equal("ROG STRIX B550-F", resultado.InventarioSeguro.Placa.Modelo);
        Assert.Equal("2806", resultado.InventarioSeguro.Placa.VersaoBios);
        Assert.Equal("Ryzen 5 5600X", resultado.InventarioSeguro.Cpu.Nome);
    }

    [Fact]
    public void Sanitizar_hasheia_mac()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());

        var mac = resultado.InventarioSeguro.Rede[0].EnderecoMac;
        Assert.NotNull(mac);
        Assert.StartsWith("sha256:", mac);
        Assert.DoesNotContain("AA:BB", mac, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Relatorio_classifica_remocao_e_hash_corretamente()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());

        var porCampo = resultado.CamposAlterados.ToDictionary(c => c.Campo, c => c.Acao);

        Assert.Equal(AcaoSanitizacao.Hasheado, porCampo["identificadores.numero_serie"]);
        Assert.Equal(AcaoSanitizacao.Hasheado, porCampo["identificadores.uuid_placa"]);
        Assert.Equal(AcaoSanitizacao.Removido, porCampo["identificadores.nome_usuario"]);
        Assert.Equal(AcaoSanitizacao.Removido, porCampo["identificadores.nome_maquina"]);
        Assert.Equal(AcaoSanitizacao.Removido, porCampo["identificadores.chave_produto_windows"]);
        Assert.Equal(AcaoSanitizacao.Hasheado, porCampo["rede[0].endereco_mac"]);
    }

    [Fact]
    public void Hash_eh_deterministico_para_o_mesmo_sal()
    {
        var a = new Sanitizador("sal-fixo");
        var b = new Sanitizador("sal-fixo");

        Assert.Equal(a.Hashear("valor"), b.Hashear("valor"));
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/ValidadorAcaoTests.cs`

````csharp
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ValidadorAcaoTests
{
    private static ValidadorAcao Validador() => new(CatalogoPadrao.Criar());

    private static Dictionary<string, string> Params(params (string Nome, string Valor)[] pares) =>
        pares.ToDictionary(p => p.Nome, p => p.Valor, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Acao_fora_do_catalogo_eh_recusada()
    {
        var r = Validador().Validar("ACAO_INEXISTENTE", Params(), TipoPerfil.Seguro);

        Assert.False(r.AcaoConhecida);
        Assert.False(r.Aplicavel);
    }

    [Fact]
    public void Valor_acima_do_limite_absoluto_eh_bloqueio_rigido()
    {
        // SO_SYSTEM_RESPONSIVENESS: limite absoluto = 20.
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "25")), TipoPerfil.Customizado);

        Assert.True(r.TemBloqueioRigido);
        Assert.False(r.Aplicavel);
        Assert.Contains(r.Parametros, p => p.Situacao == SituacaoParametro.BloqueioLimiteAbsoluto);
    }

    [Fact]
    public void Valor_na_faixa_segura_eh_aceito_sem_consentimento()
    {
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "20")), TipoPerfil.Seguro);

        Assert.True(r.Aplicavel);
        Assert.False(r.ExigeConsentimento);
    }

    [Fact]
    public void Valor_fora_da_segura_dentro_da_permitida_eh_risco_assumido()
    {
        // faixa segura [10,20], permitida [0,20]; 5 é risco assumido no perfil customizado.
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "5")), TipoPerfil.Customizado);

        Assert.True(r.Aplicavel);
        Assert.True(r.ExigeConsentimento);
        Assert.Contains(r.Parametros, p => p.Situacao == SituacaoParametro.RiscoAssumido);
    }

    [Fact]
    public void Perfil_seguro_recusa_valor_fora_da_faixa_segura()
    {
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "5")), TipoPerfil.Seguro);

        Assert.False(r.Aplicavel);
        Assert.Contains(r.Parametros, p => p.Situacao == SituacaoParametro.Rejeitado);
    }

    [Fact]
    public void Lista_branca_aceita_valor_da_lista_e_recusa_fora()
    {
        var aceito = Validador().Validar(
            "SRV_DESATIVAR_SERVICO", Params(("nome_servico", "DiagTrack")), TipoPerfil.Seguro);
        var recusado = Validador().Validar(
            "SRV_DESATIVAR_SERVICO", Params(("nome_servico", "ServicoCritico")), TipoPerfil.Customizado);

        Assert.True(aceito.Aplicavel);
        Assert.False(recusado.Aplicavel);
    }

    [Fact]
    public void Parametro_faltante_ou_desconhecido_gera_erro()
    {
        var faltante = Validador().Validar("SO_SYSTEM_RESPONSIVENESS", Params(), TipoPerfil.Seguro);
        var desconhecido = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("inexistente", "1")), TipoPerfil.Seguro);

        Assert.False(faltante.Aplicavel);
        Assert.False(desconhecido.Aplicavel);
        Assert.NotEmpty(desconhecido.Erros);
    }

    [Fact]
    public void Acao_sem_parametros_eh_aplicavel()
    {
        var r = Validador().Validar("PWR_PLANO_ALTO_DESEMPENHO", Params(), TipoPerfil.Seguro);

        Assert.True(r.Aplicavel);
        Assert.False(r.ExigeConsentimento);
    }
}
````

### `tests/HardwareOptimizer.Core.Tests/VersaoBiosTests.cs`

````csharp
using HardwareOptimizer.Core.Bios;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class VersaoBiosTests
{
    [Theory]
    [InlineData("2806", "3405", -1)] // numérico puro
    [InlineData("3405", "2806", 1)]
    [InlineData("2806", "2806", 0)]
    [InlineData("F10", "F12", -1)] // prefixo igual, número maior
    [InlineData("P3.60", "P3.70", -1)] // pontuado
    [InlineData("0805", "805", 0)] // zeros à esquerda
    public void Comparar_respeita_ordem_de_versao(string a, string b, int sinalEsperado)
    {
        var resultado = VersaoBios.Comparar(a, b);
        Assert.Equal(sinalEsperado, Math.Sign(resultado));
    }

    [Fact]
    public void EhMaisRecente_detecta_versao_mais_nova()
    {
        Assert.True(VersaoBios.EhMaisRecente("2806", "3405"));
        Assert.False(VersaoBios.EhMaisRecente("3405", "2806"));
        Assert.False(VersaoBios.EhMaisRecente("2806", "2806"));
    }
}
````


## HardwareOptimizer.Agent.Tests

### `tests/HardwareOptimizer.Agent.Tests/HardwareOptimizer.Agent.Tests.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Agent\HardwareOptimizer.Agent.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Cli\HardwareOptimizer.Cli.csproj" />
  </ItemGroup>

</Project>
````

### `tests/HardwareOptimizer.Agent.Tests/ArquivoLoggerProviderTests.cs`

````csharp
using HardwareOptimizer.Cli;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ArquivoLoggerProviderTests : IDisposable
{
    private readonly string _arquivo;

    public ArquivoLoggerProviderTests()
    {
        _arquivo = Path.Combine(Path.GetTempPath(), "hwopt-log-" + Guid.NewGuid().ToString("N") + ".log");
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_arquivo);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    [Fact]
    public void Logger_escreve_linha_com_nivel_categoria_e_mensagem()
    {
        using (var provider = new ArquivoLoggerProvider(_arquivo, LogLevel.Debug))
        {
            var logger = provider.CreateLogger("HardwareOptimizer.Agent.Execution.ExecutorControlado");
            logger.LogWarning("Categoria {Categoria} BLOQUEADA: {Motivo}", "CPU", "backup");
        }

        var conteudo = File.ReadAllText(_arquivo);
        Assert.Contains("WARN", conteudo, StringComparison.Ordinal);
        Assert.Contains("ExecutorControlado", conteudo, StringComparison.Ordinal); // categoria curta (sem namespace)
        Assert.Contains("Categoria CPU BLOQUEADA: backup", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_inclui_detalhe_da_excecao()
    {
        using (var provider = new ArquivoLoggerProvider(_arquivo, LogLevel.Debug))
        {
            var logger = provider.CreateLogger("X");
            logger.LogError(new InvalidOperationException("falha simulada"), "erro ao processar");
        }

        var conteudo = File.ReadAllText(_arquivo);
        Assert.Contains("InvalidOperationException", conteudo, StringComparison.Ordinal);
        Assert.Contains("falha simulada", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_respeita_nivel_minimo()
    {
        using (var provider = new ArquivoLoggerProvider(_arquivo, LogLevel.Warning))
        {
            var logger = provider.CreateLogger("X");
            logger.LogDebug("abaixo do minimo");
            logger.LogError("acima do minimo");
        }

        var conteudo = File.Exists(_arquivo) ? File.ReadAllText(_arquivo) : string.Empty;
        Assert.DoesNotContain("abaixo do minimo", conteudo, StringComparison.Ordinal);
        Assert.Contains("acima do minimo", conteudo, StringComparison.Ordinal);
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/ColetorInventarioTests.cs`

````csharp
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ColetorInventarioTests
{
    [Fact]
    public async Task Coletor_delega_ao_leitor_informado()
    {
        var esperado = new Inventario
        {
            Placa = new PlacaMae { Fabricante = "ACME", Modelo = "X1" },
            Cpu = new Processador { Nome = "CPU Teste" },
            SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Linux },
        };

        var coletor = new ColetorInventario(new LeitorFake(esperado));

        var obtido = await coletor.ColetarAsync();

        Assert.Same(esperado, obtido);
    }

    [Fact]
    public async Task LeitorLinux_le_dados_reais_quando_em_linux()
    {
        if (!OperatingSystem.IsLinux())
        {
            return; // Teste específico de plataforma.
        }

        var inventario = await new LeitorLinux().LerAsync();

        Assert.Equal(SistemaOperacionalTipo.Linux, inventario.SistemaOperacional.Tipo);
        Assert.False(string.IsNullOrWhiteSpace(inventario.Cpu.Nome));
        Assert.NotEqual("Desconhecido", inventario.Cpu.Nome);
    }

    private sealed class LeitorFake : ILeitorPlataforma
    {
        private readonly Inventario _inventario;

        public LeitorFake(Inventario inventario) => _inventario = inventario;

        public SistemaOperacionalTipo Tipo => _inventario.SistemaOperacional.Tipo;

        public Task<Inventario> LerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_inventario);
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/ComandoEstadoSistemaTests.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ComandoEstadoSistemaTests
{
    private static Dictionary<string, string> Sem() => new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task Aplicar_grava_valor_e_registra_anterior_existente()
    {
        var estado = new EstadoSistemaSimulado(new Dictionary<string, string> { ["alvo"] = "antigo" });
        var comando = new ComandoEstadoSistema("cmd.x", estado, _ => "alvo", _ => "novo");

        var registro = await comando.AplicarAsync("ACAO", CategoriaAcao.Rede, Sem());

        Assert.Equal("antigo", registro.ValorAnterior);
        Assert.Equal("novo", registro.ValorNovo);
        Assert.Equal("novo", estado.Ler("alvo"));
    }

    [Fact]
    public async Task Reverter_valor_preexistente_restaura_o_anterior()
    {
        var estado = new EstadoSistemaSimulado(new Dictionary<string, string> { ["alvo"] = "antigo" });
        var comando = new ComandoEstadoSistema("cmd.x", estado, _ => "alvo", _ => "novo");

        var registro = await comando.AplicarAsync("ACAO", CategoriaAcao.Rede, Sem());
        await comando.ReverterAsync(registro);

        Assert.Equal("antigo", estado.Ler("alvo"));
    }

    [Fact]
    public async Task Reverter_valor_novo_remove_a_chave()
    {
        var estado = new EstadoSistemaSimulado();
        var comando = new ComandoEstadoSistema("cmd.x", estado, _ => "alvo", _ => "novo");

        var registro = await comando.AplicarAsync("ACAO", CategoriaAcao.Rede, Sem());
        Assert.Equal("novo", estado.Ler("alvo"));

        await comando.ReverterAsync(registro);
        Assert.Null(estado.Ler("alvo")); // não existia antes -> volta a não definido
    }

    [Fact]
    public async Task Resolver_valor_usa_parametro_informado()
    {
        var estado = new EstadoSistemaSimulado();
        var comando = new ComandoEstadoSistema(
            "cmd.x", estado, _ => "alvo", p => p["valor"]);

        var registro = await comando.AplicarAsync(
            "ACAO", CategoriaAcao.Cpu, new Dictionary<string, string> { ["valor"] = "123" });

        Assert.Equal("123", registro.ValorNovo);
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/EstadoSistemaWindowsTests.cs`

````csharp
using System.Globalization;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Execution.Windows;
using HardwareOptimizer.Agent.Platform;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Cobre a implementação real do Windows com fakes de registro e processo — toda
/// a lógica de tradução, parsing e round-trip de rollback roda fora do Windows.
/// </summary>
public sealed class EstadoSistemaWindowsTests
{
    // ---- Registro --------------------------------------------------------

    [Fact]
    public void Registro_escreve_traduz_decimal_e_le_de_volta()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:SystemResponsiveness", "10");

        Assert.Equal(10u, registro.Valor(ColmeiaRegistro.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness"));
        Assert.Equal("10", estado.Ler("registro:SystemResponsiveness"));
    }

    [Fact]
    public void Registro_visualfx_traduz_simbolico_para_dword()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:VisualFXSetting", "DESEMPENHO");

        Assert.Equal(2u, registro.Valor(ColmeiaRegistro.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting"));
    }

    [Fact]
    public void Registro_network_throttling_aceita_hexadecimal_e_round_trip()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:NetworkThrottlingIndex", "ffffffff");

        // 0xFFFFFFFF preservado e relido em decimal.
        Assert.Equal(uint.MaxValue, registro.Valor(ColmeiaRegistro.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex"));
        Assert.Equal(uint.MaxValue.ToString(CultureInfo.InvariantCulture),
            estado.Ler("registro:NetworkThrottlingIndex"));
    }

    [Fact]
    public void Registro_le_nulo_quando_valor_ausente()
    {
        var estado = new EstadoSistemaWindows(new RegistroFake(), new ProcessoFake());
        Assert.Null(estado.Ler("registro:TdrDelay"));
    }

    [Fact]
    public void Registro_restaura_valor_anterior_ou_remove_quando_nulo()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:TdrDelay", "8");
        estado.Restaurar("registro:TdrDelay", "2");
        Assert.Equal(2u, registro.Valor(ColmeiaRegistro.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay"));

        estado.Restaurar("registro:TdrDelay", null);
        Assert.Null(estado.Ler("registro:TdrDelay"));
    }

    // ---- Plano de energia ------------------------------------------------

    [Fact]
    public void Plano_le_guid_ativo_e_aplica_alto_desempenho()
    {
        const string guidAtual = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var processo = new ProcessoFake();
        processo.AoChamar("powercfg /getactivescheme",
            new ResultadoProcesso(0, $"Power Scheme GUID: {guidAtual}  (Balanced)", ""));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Equal(guidAtual, estado.Ler("powercfg:plano_ativo"));

        estado.Escrever("powercfg:plano_ativo", "ALTO_DESEMPENHO");
        Assert.Contains($"powercfg /setactive {EstadoSistemaWindows.GuidAltoDesempenho}", processo.Chamados);

        // Rollback volta ao plano anterior capturado pela leitura.
        estado.Restaurar("powercfg:plano_ativo", guidAtual);
        Assert.Contains($"powercfg /setactive {guidAtual}", processo.Chamados);
    }

    // ---- Suspensão seletiva de USB ---------------------------------------

    [Fact]
    public void Usb_desabilita_define_indices_ac_dc_e_ativa()
    {
        var processo = new ProcessoFake();
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        estado.Escrever("powercfg:usb_suspensao_seletiva", "DESABILITADO");

        Assert.Contains(
            $"powercfg /setacvalueindex SCHEME_CURRENT {EstadoSistemaWindows.SubgrupoUsb} {EstadoSistemaWindows.ConfigUsbSuspensao} 0",
            processo.Chamados);
        Assert.Contains(
            $"powercfg /setdcvalueindex SCHEME_CURRENT {EstadoSistemaWindows.SubgrupoUsb} {EstadoSistemaWindows.ConfigUsbSuspensao} 0",
            processo.Chamados);
        Assert.Contains("powercfg /setactive SCHEME_CURRENT", processo.Chamados);
    }

    [Fact]
    public void Usb_le_indice_atual_do_primeiro_hex()
    {
        var processo = new ProcessoFake();
        processo.AoChamar(
            $"powercfg /query SCHEME_CURRENT {EstadoSistemaWindows.SubgrupoUsb} {EstadoSistemaWindows.ConfigUsbSuspensao}",
            new ResultadoProcesso(0,
                "  Current AC Power Setting Index: 0x00000001\n  Current DC Power Setting Index: 0x00000001", ""));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Equal("1", estado.Ler("powercfg:usb_suspensao_seletiva"));
    }

    // ---- Serviços --------------------------------------------------------

    [Fact]
    public void Servico_desabilita_configura_e_para()
    {
        var processo = new ProcessoFake();
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        estado.Escrever("servico:DiagTrack", "Disabled");

        Assert.Contains("sc config DiagTrack start= disabled", processo.Chamados);
        Assert.Contains("sc stop DiagTrack", processo.Chamados);
    }

    [Fact]
    public void Servico_le_start_type_e_restaura_modo_anterior()
    {
        var processo = new ProcessoFake();
        processo.AoChamar("sc qc DiagTrack",
            new ResultadoProcesso(0, "        START_TYPE         : 2   AUTO_START", ""));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Equal("auto", estado.Ler("servico:DiagTrack"));

        estado.Restaurar("servico:DiagTrack", "auto");
        Assert.Contains("sc config DiagTrack start= auto", processo.Chamados);
    }

    [Fact]
    public void Escrever_falha_de_processo_lanca()
    {
        var processo = new ProcessoFake();
        processo.AoChamar("sc config Fax start= disabled",
            new ResultadoProcesso(5, "", "Acesso negado."));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Throws<InvalidOperationException>(() => estado.Escrever("servico:Fax", "Disabled"));
    }

    // ---- Mapeamento ------------------------------------------------------

    [Theory]
    [InlineData("registro:Inexistente")]
    [InlineData("powercfg:plano_inexistente")]
    [InlineData("desconhecido:x")]
    [InlineData("sem_separador")]
    public void Alvo_nao_mapeado_lanca(string alvo)
    {
        var estado = new EstadoSistemaWindows(new RegistroFake(), new ProcessoFake());
        Assert.Throws<NotSupportedException>(() => estado.Ler(alvo));
    }

    // ---- Seleção do ambiente --------------------------------------------

    [Fact]
    public void Selecionar_retorna_simulado_sem_flag_de_execucao_real()
    {
        var original = Environment.GetEnvironmentVariable("HWOPT_EXECUCAO_REAL");
        try
        {
            Environment.SetEnvironmentVariable("HWOPT_EXECUCAO_REAL", null);
            Assert.IsType<EstadoSistemaSimulado>(EstadoSistemaWindows.Selecionar());
        }
        finally
        {
            Environment.SetEnvironmentVariable("HWOPT_EXECUCAO_REAL", original);
        }
    }

    // ---- Integração com o executor (round-trip de rollback) --------------

    [Fact]
    public async Task Comando_do_catalogo_aplica_e_reverte_sobre_o_estado_real()
    {
        // O estado real do Windows pluga no mesmo RegistroComandos/rollback do MVP.
        var registro = new RegistroFake();
        const string subchave = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        registro.EscreverDword(ColmeiaRegistro.LocalMachine, subchave, "SystemResponsiveness", 20);

        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());
        var comando = RegistroComandos.Padrao(estado).Obter("cmd.so.system_responsiveness.v1")!;

        var alteracao = await comando.AplicarAsync(
            "SO_SYSTEM_RESPONSIVENESS", CategoriaAcao.SistemaOperacional,
            new Dictionary<string, string> { ["percentual_reserva"] = "10" });

        Assert.Equal("20", alteracao.ValorAnterior);
        Assert.Equal(10u, registro.Valor(ColmeiaRegistro.LocalMachine, subchave, "SystemResponsiveness"));

        await comando.ReverterAsync(alteracao);
        Assert.Equal(20u, registro.Valor(ColmeiaRegistro.LocalMachine, subchave, "SystemResponsiveness"));
    }

    // ---- Fakes -----------------------------------------------------------

    private sealed class RegistroFake : IAcessoRegistro
    {
        private readonly Dictionary<string, uint> _valores = new(StringComparer.OrdinalIgnoreCase);

        private static string Chave(ColmeiaRegistro colmeia, string subchave, string nome) =>
            $"{colmeia}|{subchave}|{nome}";

        public uint? Valor(ColmeiaRegistro colmeia, string subchave, string nome) =>
            _valores.TryGetValue(Chave(colmeia, subchave, nome), out var v) ? v : null;

        public uint? LerDword(ColmeiaRegistro colmeia, string subchave, string nome) =>
            Valor(colmeia, subchave, nome);

        public void EscreverDword(ColmeiaRegistro colmeia, string subchave, string nome, uint valor) =>
            _valores[Chave(colmeia, subchave, nome)] = valor;

        public void RemoverValor(ColmeiaRegistro colmeia, string subchave, string nome) =>
            _valores.Remove(Chave(colmeia, subchave, nome));
    }

    private sealed class ProcessoFake : IExecutorProcesso
    {
        private readonly Dictionary<string, ResultadoProcesso> _respostas = new(StringComparer.Ordinal);

        public List<string> Chamados { get; } = new();

        public void AoChamar(string comando, ResultadoProcesso resposta) => _respostas[comando] = resposta;

        public ResultadoProcesso Executar(string arquivo, IReadOnlyList<string> argumentos)
        {
            var comando = arquivo + " " + string.Join(' ', argumentos);
            Chamados.Add(comando);
            return _respostas.TryGetValue(comando, out var resposta)
                ? resposta
                : new ResultadoProcesso(0, string.Empty, string.Empty);
        }
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/ExecutorControladoTests.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ExecutorControladoTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private static (ExecutorControlado Executor, EstadoSistemaSimulado Estado) Montar(
        IValidadorCategoria? validador = null)
    {
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            Catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            validador ?? new ValidadorCategoriaSempreEstavel());
        return (executor, estado);
    }

    private static ContextoExecucao ComBackup() => new() { BackupConfirmado = true };

    [Fact]
    public async Task Perfil_seguro_com_backup_aplica_e_grava_estado()
    {
        var (executor, estado) = Montar();
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS", "PWR_PLANO_ALTO_DESEMPENHO" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        Assert.True(relatorio.Sucesso);
        Assert.All(relatorio.Categorias, c => Assert.Equal(SituacaoCategoria.Aplicada, c.Situacao));
        Assert.Equal("20", estado.Ler("registro:SystemResponsiveness"));
        Assert.Equal("ALTO_DESEMPENHO", estado.Ler("powercfg:plano_ativo"));
    }

    [Fact]
    public async Task Sem_backup_confirmado_categoria_eh_bloqueada()
    {
        var (executor, estado) = Montar();
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, new ContextoExecucao { BackupConfirmado = false });

        Assert.False(relatorio.Sucesso);
        Assert.Equal(SituacaoCategoria.Bloqueada, relatorio.Categorias.Single().Situacao);
        Assert.Null(estado.Ler("registro:SystemResponsiveness")); // nada foi gravado.
    }

    [Fact]
    public async Task Regressao_reverte_categoria_e_restaura_estado()
    {
        var (executor, estado) = Montar(new ValidadorComRegressao(CategoriaAcao.SistemaOperacional));
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        Assert.False(relatorio.Sucesso);
        Assert.Equal(SituacaoCategoria.Revertida, relatorio.Categorias.Single().Situacao);
        Assert.Null(estado.Ler("registro:SystemResponsiveness")); // rollback restaurou o estado.
    }

    [Fact]
    public async Task Perfil_customizado_sem_consentimento_eh_bloqueado()
    {
        var (executor, _) = Montar();
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilCustomizado(
                "custom", "usuario",
                new[] { new SelecaoAcao { AcaoId = "SO_SYSTEM_RESPONSIVENESS", Parametros = Par("percentual_reserva", "5") } })
            .Perfil!;

        Assert.False(perfil.ConsentimentoRegistrado);

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        Assert.False(relatorio.Sucesso);
        Assert.Empty(relatorio.Categorias);
    }

    [Fact]
    public async Task Categorias_sao_aplicadas_na_ordem_do_documento()
    {
        var (executor, _) = Montar();
        // Seleção fora de ordem: Rede, GPU, Serviços, Sistema Operacional.
        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[]
            {
                "NET_THROTTLING_DESABILITAR", "GPU_HAGS", "SRV_DESATIVAR_SERVICO", "PWR_PLANO_ALTO_DESEMPENHO",
            })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        var ordem = relatorio.Categorias.Select(c => c.Categoria).ToArray();
        Assert.Equal(
            new[] { CategoriaAcao.Gpu, CategoriaAcao.SistemaOperacional, CategoriaAcao.Servicos, CategoriaAcao.Rede },
            ordem);
    }

    [Fact]
    public async Task Registro_de_alteracao_guarda_valor_anterior_e_novo()
    {
        var estadoInicial = new Dictionary<string, string> { ["registro:SystemResponsiveness"] = "20" };
        var estado = new EstadoSistemaSimulado(estadoInicial);
        var executor = new ExecutorControlado(
            Catalogo, RegistroComandos.Padrao(estado), new VerificadorPreCondicoes(), new ValidadorCategoriaSempreEstavel());

        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilCustomizado(
                "custom", "usuario",
                new[] { new SelecaoAcao { AcaoId = "SO_SYSTEM_RESPONSIVENESS", Parametros = Par("percentual_reserva", "10") } })
            .Perfil! with
            { ConsentimentoRegistrado = true };

        var relatorio = await executor.AplicarPerfilAsync(perfil, ComBackup());

        var alteracao = relatorio.TodasAlteracoes.Single();
        Assert.Equal("20", alteracao.ValorAnterior);
        Assert.Equal("10", alteracao.ValorNovo);
    }

    private static Dictionary<string, string> Par(string nome, string valor) =>
        new(StringComparer.OrdinalIgnoreCase) { [nome] = valor };

    private sealed class ValidadorComRegressao : IValidadorCategoria
    {
        private readonly CategoriaAcao _categoriaComRegressao;

        public ValidadorComRegressao(CategoriaAcao categoria) => _categoriaComRegressao = categoria;

        public Task<ResultadoValidacao> ValidarAsync(
            CategoriaAcao categoria,
            IReadOnlyList<RegistroAlteracao> alteracoes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoValidacao
            {
                Categoria = categoria.ToString(),
                Ferramenta = "fake",
                Regressao = categoria == _categoriaComRegressao,
                Estabilidade = categoria == _categoriaComRegressao ? "Reprovado" : "OK",
            });
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/FluxoCompletoTests.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using HardwareOptimizer.Core.Profiles;
using HardwareOptimizer.Core.Reporting;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Teste de integração que percorre TODOS os processos em sequência, como o
/// fluxo real: coleta -> sanitização -> perfil seguro -> backup -> execução
/// controlada -> relatório/score -> persistência.
/// </summary>
public sealed class FluxoCompletoTests : IDisposable
{
    private const string Serial = "SN-SEGREDO-123";
    private const string Uuid = "uuid-SEGREDO-abc";
    private const string Maquina = "PC-DO-USUARIO";
    private const string Usuario = "michel";
    private const string Mac = "AA:BB:CC:DD:EE:FF";

    private readonly string _dir;

    public FluxoCompletoTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    private static Inventario InventarioRico() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = "2806", Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6, Threads = 12, TempIdleC = 38 },
        Memoria = new[] { new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 }, new ModuloMemoria { TamanhoGb = 16, VelocidadeMhz = 3200 } },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060", TempIdleC = 41, VersaoDriver = "551.23" } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
        Rede = new[] { new InterfaceRede { Nome = "Ethernet", EnderecoMac = Mac } },
        Identificadores = new IdentificadoresSensiveis
        {
            NumeroSerie = Serial,
            UuidPlaca = Uuid,
            NomeMaquina = Maquina,
            NomeUsuario = Usuario,
            ChaveProdutoWindows = "AAAAA-BBBBB-CCCCC",
        },
    };

    [Fact]
    public async Task Fluxo_ponta_a_ponta_executa_todos_os_processos()
    {
        var catalogo = CatalogoPadrao.Criar();

        // 1) Coleta (via leitor injetado).
        var inventario = await new ColetorInventario(new LeitorFixo(InventarioRico())).ColetarAsync();

        // 2) Sanitização — nenhum segredo bruto pode sobrar no payload de nuvem.
        var sanitizacao = new Sanitizador("sal-fixo").Sanitizar(inventario);
        var jsonSeguro = JsonSerializer.Serialize(sanitizacao.InventarioSeguro);
        foreach (var segredo in new[] { Serial, Uuid, Maquina, Usuario, Mac })
        {
            Assert.DoesNotContain(segredo, jsonSeguro, StringComparison.OrdinalIgnoreCase);
        }

        // 3) Persistência do inventário + 7) auditoria.
        var repositorio = RepositorioSqlite.DeArquivo(Path.Combine(_dir, "fluxo.db"));
        await repositorio.InicializarAsync();
        await repositorio.SalvarInventarioAsync(inventario);

        // 4) Perfil seguro com TODAS as ações do catálogo.
        var construcao = new ConstrutorPerfil(catalogo)
            .CriarPerfilSeguro("e2e", catalogo.Todas.Select(a => a.Id));
        Assert.True(construcao.Sucesso, string.Join(" | ", construcao.Bloqueios));
        Assert.False(construcao.ExigeConsentimento); // perfil seguro não exige consentimento

        // 5) Backup obrigatório (bloqueante).
        var backup = await new ServicoBackup(Path.Combine(_dir, "backups")).CriarBackupAsync(inventario);
        Assert.True(backup.Sucesso);

        // 6) Execução controlada por categoria.
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            catalogo, RegistroComandos.Padrao(estado), new VerificadorPreCondicoes(), new ValidadorCategoriaSempreEstavel());
        var execucao = await executor.AplicarPerfilAsync(
            construcao.Perfil!, new ContextoExecucao { BackupConfirmado = backup.Sucesso });

        Assert.True(execucao.Sucesso);
        Assert.All(execucao.Categorias, c => Assert.Equal(SituacaoCategoria.Aplicada, c.Situacao));
        Assert.Equal("ALTO_DESEMPENHO", estado.Ler("powercfg:plano_ativo"));
        await repositorio.RegistrarExecucaoAsync(execucao);

        // 7) Relatório executivo e nota final.
        var validacoes = execucao.Categorias.Where(c => c.Validacao is not null).Select(c => c.Validacao!).ToList();
        var alteracoes = execucao.TodasAlteracoes.Select(a => new AlteracaoResumo(a.Alvo, a.ValorAnterior, a.ValorNovo)).ToList();
        var dominios = new HashSet<Dominio> { Dominio.Windows, Dominio.Gpu };
        var relatorio = new GeradorRelatorio().Gerar(inventario, validacoes, alteracoes, dominios);

        Assert.InRange(relatorio.NotaFinal, 0, 100);
        Assert.Equal(7, relatorio.Scores.Count);
        Assert.False(relatorio.RegressaoDetectada);

        // Auditoria persistida.
        Assert.Equal(1, await repositorio.ContarInventariosAsync());
        Assert.Equal(1, await repositorio.ContarExecucoesAsync());
    }

    private sealed class LeitorFixo : ILeitorPlataforma
    {
        private readonly Inventario _inventario;

        public LeitorFixo(Inventario inventario) => _inventario = inventario;

        public SistemaOperacionalTipo Tipo => _inventario.SistemaOperacional.Tipo;

        public Task<Inventario> LerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_inventario);
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/GlobalUsings.cs`

````csharp
global using Xunit;
````

### `tests/HardwareOptimizer.Agent.Tests/ModuloBiosTests.cs`

````csharp
using HardwareOptimizer.Agent.Bios;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ModuloBiosTests : IDisposable
{
    private readonly string _dir;

    public ModuloBiosTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-bios-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    private static Inventario Inventario(string fabricante, string modelo, string? versao) => new()
    {
        Placa = new PlacaMae { Fabricante = fabricante, Modelo = modelo, VersaoBios = versao, Modo = "UEFI", SecureBoot = true },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public async Task Placa_curada_desatualizada_recomenda_atualizacao()
    {
        var relatorio = await new ModuloBios().AnalisarAsync(
            Inventario("ASUSTeK Computer Inc.", "ROG STRIX B550-F", "2806"));

        Assert.True(relatorio.FonteEncontrada);
        Assert.True(relatorio.Decisao.RecomendaAtualizar);
        Assert.Equal("3405", relatorio.Decisao.VersaoRecomendada);
        Assert.Equal("ASUS", relatorio.Identificacao.Fabricante);
        Assert.Contains("EZ Flash", relatorio.Guia.Utilitario, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Placa_desconhecida_nao_encontra_fonte_e_nao_recomenda()
    {
        var relatorio = await new ModuloBios().AnalisarAsync(
            Inventario("Fabricante Genérico", "Placa Z", "1.0"));

        Assert.False(relatorio.FonteEncontrada);
        Assert.False(relatorio.Decisao.RecomendaAtualizar);
    }

    [Fact]
    public async Task Provedor_com_cache_consulta_interno_apenas_uma_vez()
    {
        var repo = RepositorioSqlite.DeArquivo(Path.Combine(_dir, "bios.db"));
        await repo.InicializarAsync();

        var interno = new ProvedorContador();
        var comCache = new ProvedorBiosComCache(interno, repo);

        var primeira = await comCache.ObterAsync("asus|rog strix b550-f");
        var segunda = await comCache.ObterAsync("asus|rog strix b550-f");

        Assert.NotNull(primeira);
        Assert.NotNull(segunda);
        Assert.Equal("3405", segunda!.VersaoMaisRecente);
        Assert.Equal(1, interno.Chamadas); // segunda veio do cache
    }

    private sealed class ProvedorContador : IProvedorInfoBios
    {
        private readonly BancoCuradoBios _curado = new();

        public int Chamadas { get; private set; }

        public Task<InfoBiosFabricante?> ObterAsync(string chaveBusca, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return _curado.ObterAsync(chaveBusca, cancellationToken);
        }
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/NormalizadorDataTests.cs`

````csharp
using HardwareOptimizer.Agent.Collector;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Normalização de datas das fontes de inventário (bug real visto no Windows:
/// a data da BIOS vinha como "/Date(ms)/" do ConvertTo-Json).
/// </summary>
public sealed class NormalizadorDataTests
{
    [Theory]
    [InlineData("/Date(1754611200000)/", "2025-08-08")]      // ConvertTo-Json (PS 5.1)
    [InlineData("/Date(1754611200000+0000)/", "2025-08-08")] // com offset
    [InlineData("20250808000000.000000+000", "2025-08-08")]  // CIM DATETIME bruto
    [InlineData("08/08/2025", "2025-08-08")]                 // DMI/Linux (MM/dd/yyyy)
    [InlineData("2025-08-08", "2025-08-08")]                 // já ISO
    public void Normaliza_formatos_conhecidos_para_iso(string entrada, string esperado)
    {
        Assert.Equal(esperado, NormalizadorData.Normalizar(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vazio_ou_nulo_retorna_nulo(string? entrada)
    {
        Assert.Null(NormalizadorData.Normalizar(entrada));
    }

    [Fact]
    public void Formato_desconhecido_e_preservado()
    {
        // Não reconhece, mas não perde a informação original.
        Assert.Equal("AMI 5041", NormalizadorData.Normalizar("  AMI 5041  "));
    }

    [Fact]
    public void Date_json_invalido_e_preservado()
    {
        Assert.Equal("/Date(abc)/", NormalizadorData.Normalizar("/Date(abc)/"));
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/PersistenciaTests.cs`

````csharp
using HardwareOptimizer.Agent.Backup;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Persistence;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class PersistenciaTests : IDisposable
{
    private readonly string _dir;

    public PersistenciaTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Limpeza best-effort.
        }
    }

    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    [Fact]
    public async Task Repositorio_persiste_inventario_consentimento_e_execucao()
    {
        var repo = RepositorioSqlite.DeArquivo(Path.Combine(_dir, "otimizador.db"));
        await repo.InicializarAsync();

        await repo.SalvarInventarioAsync(Inventario());
        await repo.RegistrarConsentimentoAsync(new RegistroConsentimento
        {
            NomePerfil = "custom",
            VersaoCatalogo = "v1",
            CheckboxesMarcados = new[] { "aceite_riscos", "desejo_prosseguir" },
            ValoresEscolhidos = new[] { "SO_SYSTEM_RESPONSIVENESS.percentual_reserva = 5" },
        });
        await repo.RegistrarExecucaoAsync(new RelatorioExecucao { Sucesso = true, PerfilNome = "custom" });

        Assert.Equal(1, await repo.ContarInventariosAsync());
        Assert.Equal(1, await repo.ContarConsentimentosAsync());
        Assert.Equal(1, await repo.ContarExecucoesAsync());
    }

    [Fact]
    public async Task Backup_eh_confirmado_e_gravado_em_disco()
    {
        var servico = new ServicoBackup(Path.Combine(_dir, "backups"));

        var resultado = await servico.CriarBackupAsync(Inventario());

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.ValorObrigatorio.Confirmado);
        Assert.True(File.Exists(resultado.ValorObrigatorio.Caminho));
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/RegistroComandosTests.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class RegistroComandosTests
{
    [Fact]
    public void Todo_comando_interno_do_catalogo_esta_registrado()
    {
        var catalogo = CatalogoPadrao.Criar();
        var registro = RegistroComandos.Padrao(new EstadoSistemaSimulado());

        var faltando = catalogo.Todas
            .Select(a => a.ComandoInternoId)
            .Where(id => !registro.Contem(id))
            .Distinct()
            .ToList();

        Assert.True(faltando.Count == 0, "Comandos internos sem implementação registrada: " + string.Join(", ", faltando));
    }

    [Fact]
    public void Registro_rejeita_ids_duplicados()
    {
        var estado = new EstadoSistemaSimulado();
        var c1 = new ComandoEstadoSistema("dup", estado, _ => "a", _ => "v");
        var c2 = new ComandoEstadoSistema("dup", estado, _ => "b", _ => "w");

        Assert.Throws<ArgumentException>(() => new RegistroComandos(new IComandoInterno[] { c1, c2 }));
    }

    [Fact]
    public async Task Comando_com_parametro_ausente_lanca_ao_aplicar()
    {
        var registro = RegistroComandos.Padrao(new EstadoSistemaSimulado());
        var comando = registro.Obter("cmd.so.system_responsiveness.v1")!;

        // O comando depende do parâmetro 'percentual_reserva'; sem ele, deve lançar.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => comando.AplicarAsync("ACAO", CategoriaAcao.SistemaOperacional, new Dictionary<string, string>()));
    }

    [Fact]
    public void Obter_id_inexistente_retorna_nulo()
    {
        var registro = RegistroComandos.Padrao(new EstadoSistemaSimulado());
        Assert.Null(registro.Obter("cmd.inexistente"));
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/SensoresLhmTests.cs`

````csharp
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Cobre a lógica de sensores baseada em LibreHardwareMonitor com fakes — a fonte
/// real (driver) não roda aqui, mas filtragem, fallback e empacotamento sim.
/// </summary>
public sealed class SensoresLhmTests
{
    private static Sensor Temp(string nome, double c) =>
        new() { Nome = nome, Tipo = TipoSensor.Temperatura, Valor = c, Unidade = "°C" };

    // ---- LeitorSensoresLhm ----------------------------------------------

    [Fact]
    public async Task Lhm_descarta_valores_nao_finitos()
    {
        var fonte = new FonteFake(new[]
        {
            Temp("CPU", 55),
            new Sensor { Nome = "Clock", Tipo = TipoSensor.Clock, Valor = double.NaN, Unidade = "MHz" },
            new Sensor { Nome = "Volt", Tipo = TipoSensor.Voltagem, Valor = double.PositiveInfinity, Unidade = "V" },
            new Sensor { Nome = "Fan", Tipo = TipoSensor.Fan, Valor = 1200, Unidade = "RPM" },
        });
        var leitor = new LeitorSensoresLhm(fonte);

        var leitura = await leitor.LerAsync();

        Assert.Equal(2, leitura.Sensores.Count); // só os finitos
        Assert.Equal(55, leitura.TemperaturaMaxC);
        Assert.DoesNotContain(leitura.Sensores, s => double.IsNaN(s.Valor) || double.IsInfinity(s.Valor));
    }

    [Fact]
    public async Task Lhm_descarta_temperatura_e_clock_zerados_mas_mantem_volt_fan_potencia()
    {
        // Cenário real visto no Windows sem elevação: CPU reporta 0 °C / 0 MHz
        // (MSR não lido), enquanto tensão/fan/potência em 0 são válidos.
        var fonte = new FonteFake(new[]
        {
            new Sensor { Nome = "GPU", Tipo = TipoSensor.Temperatura, Valor = 37, Unidade = "°C" },
            new Sensor { Nome = "CPU Tctl", Tipo = TipoSensor.Temperatura, Valor = 0, Unidade = "°C" },
            new Sensor { Nome = "Core #1", Tipo = TipoSensor.Clock, Valor = 0, Unidade = "MHz" },
            new Sensor { Nome = "VID", Tipo = TipoSensor.Voltagem, Valor = 0, Unidade = "V" },
            new Sensor { Nome = "Fan", Tipo = TipoSensor.Fan, Valor = 0, Unidade = "RPM" },
            new Sensor { Nome = "Pkg", Tipo = TipoSensor.Potencia, Valor = 0, Unidade = "W" },
        });

        var leitura = await new LeitorSensoresLhm(fonte).LerAsync();

        Assert.Equal(37, leitura.TemperaturaMaxC); // GPU mantida; CPU 0 °C descartada
        Assert.DoesNotContain(leitura.Sensores, s => s.Tipo == TipoSensor.Clock); // 0 MHz descartado
        Assert.Contains(leitura.Sensores, s => s.Tipo == TipoSensor.Voltagem && s.Valor == 0);
        Assert.Contains(leitura.Sensores, s => s.Tipo == TipoSensor.Fan && s.Valor == 0);
        Assert.Contains(leitura.Sensores, s => s.Tipo == TipoSensor.Potencia && s.Valor == 0);
    }

    [Fact]
    public async Task Lhm_sem_sensores_retorna_leitura_vazia()
    {
        var leitor = new LeitorSensoresLhm(new FonteFake(Array.Empty<Sensor>()));

        var leitura = await leitor.LerAsync();

        Assert.Empty(leitura.Sensores);
        Assert.Null(leitura.TemperaturaMaxC);
    }

    [Fact]
    public void Lhm_reporta_plataforma_windows()
    {
        var leitor = new LeitorSensoresLhm(new FonteFake(Array.Empty<Sensor>()));
        Assert.Equal(SistemaOperacionalTipo.Windows, leitor.Tipo);
    }

    // ---- LeitorSensoresComposto -----------------------------------------

    [Fact]
    public async Task Composto_cai_para_o_proximo_quando_o_primeiro_vem_vazio()
    {
        var vazio = new LeitorFake(new LeituraSensores());
        var comDados = new LeitorFake(new LeituraSensores { Sensores = new[] { Temp("CPU", 60) } });
        var composto = new LeitorSensoresComposto(new ILeitorSensores[] { vazio, comDados });

        var leitura = await composto.LerAsync();

        Assert.Single(leitura.Sensores);
        Assert.Equal(60, leitura.TemperaturaMaxC);
        Assert.Equal(1, vazio.Chamadas);
        Assert.Equal(1, comDados.Chamadas);
    }

    [Fact]
    public async Task Composto_para_no_primeiro_com_dados_sem_chamar_o_resto()
    {
        var comDados = new LeitorFake(new LeituraSensores { Sensores = new[] { Temp("CPU", 50) } });
        var segundo = new LeitorFake(new LeituraSensores { Sensores = new[] { Temp("GPU", 70) } });
        var composto = new LeitorSensoresComposto(new ILeitorSensores[] { comDados, segundo });

        var leitura = await composto.LerAsync();

        Assert.Equal(50, leitura.TemperaturaMaxC); // o primeiro venceu
        Assert.Equal(1, comDados.Chamadas);
        Assert.Equal(0, segundo.Chamadas); // curto-circuito
    }

    [Fact]
    public async Task Composto_todos_vazios_retorna_vazio()
    {
        var composto = new LeitorSensoresComposto(new ILeitorSensores[]
        {
            new LeitorFake(new LeituraSensores()),
            new LeitorFake(new LeituraSensores()),
        });

        var leitura = await composto.LerAsync();

        Assert.Empty(leitura.Sensores);
    }

    [Fact]
    public void Composto_rejeita_lista_vazia()
    {
        Assert.Throws<ArgumentException>(() =>
            new LeitorSensoresComposto(Array.Empty<ILeitorSensores>()));
    }

    // ---- Fakes -----------------------------------------------------------

    private sealed class FonteFake : IFonteSensoresLhm
    {
        private readonly IReadOnlyList<Sensor> _sensores;
        public FonteFake(IReadOnlyList<Sensor> sensores) => _sensores = sensores;
        public IReadOnlyList<Sensor> Ler() => _sensores;
    }

    private sealed class LeitorFake : ILeitorSensores
    {
        private readonly LeituraSensores _leitura;
        public LeitorFake(LeituraSensores leitura) => _leitura = leitura;
        public int Chamadas { get; private set; }
        public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

        public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(_leitura);
        }
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/SensoresTests.cs`

````csharp
using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class SensoresTests : IDisposable
{
    private readonly string _dir;

    public SensoresTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-sensores-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    private void Escrever(string caminhoRelativo, string conteudo)
    {
        var completo = Path.Combine(_dir, caminhoRelativo);
        Directory.CreateDirectory(Path.GetDirectoryName(completo)!);
        File.WriteAllText(completo, conteudo);
    }

    [Fact]
    public async Task LeitorLinux_le_hwmon_e_clock_de_arquivos_fabricados()
    {
        Escrever("hwmon/hwmon0/name", "coretemp");
        Escrever("hwmon/hwmon0/temp1_input", "45000");   // 45 °C
        Escrever("hwmon/hwmon0/temp1_label", "Core 0");
        Escrever("hwmon/hwmon0/fan1_input", "1200");      // 1200 RPM
        Escrever("hwmon/hwmon0/in1_input", "1200");       // 1.2 V
        Escrever("hwmon/hwmon0/power1_input", "15000000"); // 15 W
        Escrever("cpu/cpu0/cpufreq/scaling_cur_freq", "3600000"); // 3600 MHz
        Escrever("cpu/cpu1/cpufreq/scaling_cur_freq", "4000000"); // 4000 MHz

        var leitor = new LeitorSensoresLinux(
            baseHwmon: Path.Combine(_dir, "hwmon"), baseCpu: Path.Combine(_dir, "cpu"));
        var leitura = await leitor.LerAsync();

        var temp = leitura.PorTipo(TipoSensor.Temperatura).Single();
        Assert.Equal("Core 0", temp.Nome);
        Assert.Equal(45, temp.Valor);
        Assert.Equal("°C", temp.Unidade);

        Assert.Equal(1200, leitura.PorTipo(TipoSensor.Fan).Single().Valor);
        Assert.Equal(1.2, leitura.PorTipo(TipoSensor.Voltagem).Single().Valor);
        Assert.Equal(15, leitura.PorTipo(TipoSensor.Potencia).Single().Valor);
        Assert.Equal(4000, leitura.PorTipo(TipoSensor.Clock).Single().Valor); // maior entre cpu0/cpu1
    }

    [Fact]
    public async Task LeitorLinux_sem_hwmon_retorna_leitura_vazia_sem_lancar()
    {
        var leitor = new LeitorSensoresLinux(
            baseHwmon: Path.Combine(_dir, "inexistente"), baseCpu: Path.Combine(_dir, "inexistente"));

        var leitura = await leitor.LerAsync();

        Assert.Empty(leitura.Sensores);
    }

    [Fact]
    public void TemperaturaMaxC_retorna_a_maior_temperatura()
    {
        var leitura = new LeituraSensores
        {
            Sensores = new[]
            {
                new Sensor { Nome = "a", Tipo = TipoSensor.Temperatura, Valor = 45, Unidade = "°C" },
                new Sensor { Nome = "b", Tipo = TipoSensor.Temperatura, Valor = 71, Unidade = "°C" },
                new Sensor { Nome = "fan", Tipo = TipoSensor.Fan, Valor = 1500, Unidade = "RPM" },
            },
        };

        Assert.Equal(71, leitura.TemperaturaMaxC);
    }

    [Fact]
    public async Task Servico_delega_ao_leitor_informado()
    {
        var esperada = new LeituraSensores
        {
            Sensores = new[] { new Sensor { Nome = "x", Tipo = TipoSensor.Temperatura, Valor = 50, Unidade = "°C" } },
        };

        var leitura = await new ServicoSensores(new LeitorFake(esperada)).LerAsync();

        Assert.Same(esperada, leitura);
    }

    [Fact]
    public async Task Servico_real_no_linux_nao_lanca()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var leitura = await new ServicoSensores().LerAsync();
        Assert.NotNull(leitura);
    }

    private sealed class LeitorFake : ILeitorSensores
    {
        private readonly LeituraSensores _leitura;

        public LeitorFake(LeituraSensores leitura) => _leitura = leitura;

        public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Linux;

        public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_leitura);
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/ValidacaoTests.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Validation;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ValidacaoTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    // ---- ParserEstresse ------------------------------------------------------

    [Fact]
    public void Parser_le_saida_saudavel()
    {
        var medicao = new ParserEstresse().Parse(
            "WHEA errors: 0\nMax temperature: 78 C\nScore: 11850\nArtifacts: no\nBSOD: no");

        Assert.Equal(0, medicao.ErrosWhea);
        Assert.Equal(78, medicao.TempMaxC);
        Assert.Equal(11850, medicao.Pontuacao);
        Assert.False(medicao.TemFalhaCritica);
    }

    [Fact]
    public void Parser_detecta_sinais_de_falha()
    {
        var medicao = new ParserEstresse().Parse("WHEA errors: 3\nArtifacts: yes\nBSOD: yes\nMemory errors: 2");

        Assert.Equal(3, medicao.ErrosWhea);
        Assert.Equal(2, medicao.ErrosMemoria);
        Assert.True(medicao.Artefatos);
        Assert.True(medicao.TelaAzul);
        Assert.True(medicao.TemFalhaCritica);
    }

    // ---- AnalisadorRegressao -------------------------------------------------

    [Fact]
    public void Analisador_reprova_com_whea()
    {
        var medicao = new MedicaoEstresse { ErrosWhea = 3 };
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "OCCT", medicao, null, LimiaresValidacao.Padrao);

        Assert.True(r.Regressao);
        Assert.Equal("Reprovado", r.Estabilidade);
    }

    [Fact]
    public void Analisador_reprova_por_superaquecimento()
    {
        var medicao = new MedicaoEstresse { TempMaxC = 99 };
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "OCCT", medicao, null, LimiaresValidacao.Padrao);

        Assert.True(r.Regressao);
    }

    [Fact]
    public void Analisador_aprova_quando_saudavel()
    {
        var medicao = new MedicaoEstresse { TempMaxC = 78, Pontuacao = 11850 };
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "OCCT", medicao, null, LimiaresValidacao.Padrao);

        Assert.False(r.Regressao);
        Assert.Equal("Totalmente validado", r.Estabilidade);
    }

    [Fact]
    public void Analisador_detecta_queda_de_pontuacao_vs_baseline()
    {
        var baseline = new MedicaoEstresse { Pontuacao = 12000 };
        var atual = new MedicaoEstresse { Pontuacao = 9000 }; // queda > 5%
        var r = new AnalisadorRegressao().Analisar(CategoriaAcao.Cpu, "Cinebench", atual, baseline, LimiaresValidacao.Padrao);

        Assert.True(r.Regressao);
        Assert.NotNull(r.Antes);
    }

    // ---- RunnerValidacao -----------------------------------------------------

    [Fact]
    public async Task Runner_aprova_com_ferramenta_saudavel()
    {
        var r = await new RunnerValidacao(FerramentaEstresseSimulada.Saudavel())
            .ValidarAsync(CategoriaAcao.Cpu, Array.Empty<RegistroAlteracao>());

        Assert.False(r.Regressao);
    }

    [Fact]
    public async Task Runner_reprova_com_ferramenta_em_regressao()
    {
        var r = await new RunnerValidacao(FerramentaEstresseSimulada.ComRegressao("whea"))
            .ValidarAsync(CategoriaAcao.Cpu, Array.Empty<RegistroAlteracao>());

        Assert.True(r.Regressao);
    }

    // ---- Integração: regressão simulada reverte automaticamente --------------

    [Fact]
    public async Task Executor_com_runner_reverte_categoria_em_regressao()
    {
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            Catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.ComRegressao("bsod")));

        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, new ContextoExecucao { BackupConfirmado = true });

        Assert.False(relatorio.Sucesso);
        Assert.Equal(SituacaoCategoria.Revertida, relatorio.Categorias.Single().Situacao);
        Assert.Null(estado.Ler("registro:SystemResponsiveness")); // rollback automático restaurou o estado
    }

    [Fact]
    public async Task Executor_com_runner_saudavel_aplica_categoria()
    {
        var estado = new EstadoSistemaSimulado();
        var executor = new ExecutorControlado(
            Catalogo,
            RegistroComandos.Padrao(estado),
            new VerificadorPreCondicoes(),
            new RunnerValidacao(FerramentaEstresseSimulada.Saudavel()));

        var perfil = new ConstrutorPerfil(Catalogo)
            .CriarPerfilSeguro("seguro", new[] { "SO_SYSTEM_RESPONSIVENESS" })
            .Perfil!;

        var relatorio = await executor.AplicarPerfilAsync(perfil, new ContextoExecucao { BackupConfirmado = true });

        Assert.True(relatorio.Sucesso);
        Assert.Equal("20", estado.Ler("registro:SystemResponsiveness"));
    }
}
````

### `tests/HardwareOptimizer.Agent.Tests/VerificadorPreCondicoesTests.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class VerificadorPreCondicoesTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private static AcaoOtimizacao Acao(string id) =>
        Catalogo.Obter(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Backup_nao_confirmado_bloqueia()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("PWR_PLANO_ALTO_DESEMPENHO"),
            new Dictionary<string, string>(),
            new ContextoExecucao { BackupConfirmado = false });

        Assert.True(resultado.Falha);
    }

    [Fact]
    public void Backup_confirmado_aprova()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("PWR_PLANO_ALTO_DESEMPENHO"),
            new Dictionary<string, string>(),
            new ContextoExecucao { BackupConfirmado = true });

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void Servico_fora_da_lista_segura_bloqueia()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("SRV_DESATIVAR_SERVICO"),
            new Dictionary<string, string> { ["nome_servico"] = "ServicoCritico" },
            new ContextoExecucao { BackupConfirmado = true });

        Assert.True(resultado.Falha);
    }

    [Fact]
    public void Servico_na_lista_segura_aprova()
    {
        var resultado = new VerificadorPreCondicoes().Verificar(
            Acao("SRV_DESATIVAR_SERVICO"),
            new Dictionary<string, string> { ["nome_servico"] = "DiagTrack" },
            new ContextoExecucao { BackupConfirmado = true });

        Assert.True(resultado.Sucesso);
    }
}
````


## HardwareOptimizer.Cerebro.Tests

### `tests/HardwareOptimizer.Cerebro.Tests/HardwareOptimizer.Cerebro.Tests.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Cerebro\HardwareOptimizer.Cerebro.csproj" />
  </ItemGroup>

</Project>
````

### `tests/HardwareOptimizer.Cerebro.Tests/CerebroTests.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Cerebro.Tests;

public sealed class CerebroTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private const string Usuario = "michel-secreto";

    private static Inventario Sanitizado(bool comGpu = true) => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        Gpu = comGpu ? new[] { new PlacaVideo { Nome = "RTX 3060" } } : Array.Empty<PlacaVideo>(),
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
        Identificadores = null, // sanitizado
    };

    private static Inventario ComPii() => Sanitizado() with
    {
        Identificadores = new IdentificadoresSensiveis { NomeUsuario = Usuario },
    };

    // ---- CerebroLocal --------------------------------------------------------

    [Fact]
    public async Task Local_propoe_apenas_acoes_do_catalogo_de_baixo_risco()
    {
        var matriz = await new CerebroLocal().ProporAsync(Sanitizado(), Catalogo);

        Assert.NotEmpty(matriz.Itens);
        Assert.Equal(OrigemDecisao.Local, matriz.Origem);
        Assert.All(matriz.Itens, i => Assert.True(Catalogo.Contem(i.AcaoId)));
        Assert.All(matriz.Itens, i => Assert.True(i.Risco <= NivelRisco.Baixo));
    }

    [Fact]
    public async Task Local_so_propoe_gpu_quando_ha_placa_de_video()
    {
        var com = await new CerebroLocal().ProporAsync(Sanitizado(comGpu: true), Catalogo);
        var sem = await new CerebroLocal().ProporAsync(Sanitizado(comGpu: false), Catalogo);

        Assert.Contains(com.Itens, i => i.AcaoId == "GPU_HAGS");
        Assert.DoesNotContain(sem.Itens, i => i.AcaoId == "GPU_HAGS");
    }

    [Fact]
    public async Task Matriz_serializa_para_json_valido()
    {
        var matriz = await new CerebroLocal().ProporAsync(Sanitizado(), Catalogo);

        var json = JsonSerializer.Serialize(matriz);
        using var doc = JsonDocument.Parse(json); // não lança => JSON válido

        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    // ---- CerebroLlm ----------------------------------------------------------

    [Fact]
    public async Task Llm_constroi_matriz_a_partir_da_resposta()
    {
        var cliente = new ClienteFake(
            "{\"acoes\":[{\"id\":\"PWR_PLANO_ALTO_DESEMPENHO\",\"prioridade\":1,\"justificativa\":\"ok\"}]}");

        var matriz = await new CerebroLlm(cliente).ProporAsync(Sanitizado(), Catalogo);

        Assert.Equal(OrigemDecisao.Nuvem, matriz.Origem);
        Assert.Equal("fake-1", matriz.Modelo);
        Assert.Single(matriz.Itens);
    }

    [Fact]
    public async Task Llm_filtra_acao_alucinada_pelo_guard()
    {
        var cliente = new ClienteFake(
            "{\"acoes\":[{\"id\":\"NAO_EXISTE\",\"prioridade\":1},{\"id\":\"PWR_PLANO_ALTO_DESEMPENHO\",\"prioridade\":2}]}");

        var matriz = await new CerebroLlm(cliente).ProporAsync(Sanitizado(), Catalogo);

        Assert.Single(matriz.Itens);
        Assert.Equal("PWR_PLANO_ALTO_DESEMPENHO", matriz.Itens[0].AcaoId);
    }

    [Fact]
    public async Task Llm_recusa_inventario_com_pii()
    {
        var cliente = new ClienteFake("{\"acoes\":[]}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CerebroLlm(cliente).ProporAsync(ComPii(), Catalogo));
    }

    // ---- ConstrutorPrompt ----------------------------------------------------

    [Fact]
    public void Prompt_sistema_fixa_a_regra_do_catalogo_fechado()
    {
        var sistema = new ConstrutorPrompt().MontarSistema(Catalogo);

        Assert.Contains("APENAS", sistema, StringComparison.Ordinal);
        Assert.Contains("acoes", sistema, StringComparison.Ordinal); // formato JSON exigido
    }

    [Fact]
    public void Prompt_usuario_lista_ids_do_catalogo_e_nao_vaza_segredo()
    {
        var prompt = new ConstrutorPrompt().MontarUsuario(Sanitizado(), Catalogo);

        Assert.Contains("PWR_PLANO_ALTO_DESEMPENHO", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(Usuario, prompt, StringComparison.Ordinal); // inventário sanitizado
    }

    private sealed class ClienteFake : IClienteLlm
    {
        private readonly string _resposta;

        public ClienteFake(string resposta) => _resposta = resposta;

        public string Modelo => "fake-1";

        public Task<string> ResponderAsync(
            string promptSistema, string promptUsuario, CancellationToken cancellationToken = default) =>
            Task.FromResult(_resposta);
    }
}
````

### `tests/HardwareOptimizer.Cerebro.Tests/GlobalUsings.cs`

````csharp
global using Xunit;
````

### `tests/HardwareOptimizer.Cerebro.Tests/GuardRespostaTests.cs`

````csharp
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Catalog;
using Xunit;

namespace HardwareOptimizer.Cerebro.Tests;

public sealed class GuardRespostaTests
{
    private static readonly CatalogoAcoes Catalogo = CatalogoPadrao.Criar();

    private static MatrizDecisao Ler(string resposta) =>
        new LeitorRespostaCerebro().Ler(resposta, Catalogo, OrigemDecisao.Nuvem, "modelo-teste");

    [Fact]
    public void Aceita_acoes_validas_do_catalogo()
    {
        const string json = """
            {"acoes":[
              {"id":"PWR_PLANO_ALTO_DESEMPENHO","prioridade":1,"justificativa":"energia"},
              {"id":"SO_SYSTEM_RESPONSIVENESS","prioridade":2,"parametros":{"percentual_reserva":"20"}}
            ]}
            """;

        var matriz = Ler(json);

        Assert.Equal(2, matriz.Itens.Count);
        Assert.Contains(matriz.Itens, i => i.AcaoId == "PWR_PLANO_ALTO_DESEMPENHO");
        Assert.Equal("20", matriz.Itens.Single(i => i.AcaoId == "SO_SYSTEM_RESPONSIVENESS").Parametros["percentual_reserva"]);
    }

    [Fact]
    public void Descarta_acao_fora_do_catalogo()
    {
        const string json = """
            {"acoes":[
              {"id":"ACAO_INVENTADA","prioridade":1,"justificativa":"alucinação"},
              {"id":"PWR_PLANO_ALTO_DESEMPENHO","prioridade":2}
            ]}
            """;

        var matriz = Ler(json);

        Assert.Single(matriz.Itens);
        Assert.Equal("PWR_PLANO_ALTO_DESEMPENHO", matriz.Itens[0].AcaoId);
        Assert.Contains(matriz.Avisos, a => a.Contains("ACAO_INVENTADA", StringComparison.Ordinal));
    }

    [Fact]
    public void Forca_parametro_acima_do_limite_para_o_padrao_seguro()
    {
        // 25 ultrapassa o limite absoluto (20) -> guard usa o padrão seguro (20).
        const string json = """
            {"acoes":[{"id":"SO_SYSTEM_RESPONSIVENESS","prioridade":1,"parametros":{"percentual_reserva":"25"}}]}
            """;

        var matriz = Ler(json);

        Assert.Equal("20", matriz.Itens.Single().Parametros["percentual_reserva"]);
        Assert.Contains(matriz.Avisos, a => a.Contains("padrão seguro", StringComparison.Ordinal));
    }

    [Fact]
    public void Forca_parametro_fora_da_faixa_segura_para_o_padrao()
    {
        // 5 está dentro da permitida mas fora da segura -> no perfil seguro, vira o padrão (20).
        const string json = """
            {"acoes":[{"id":"SO_SYSTEM_RESPONSIVENESS","prioridade":1,"parametros":{"percentual_reserva":"5"}}]}
            """;

        var matriz = Ler(json);

        Assert.Equal("20", matriz.Itens.Single().Parametros["percentual_reserva"]);
    }

    [Fact]
    public void Json_malformado_gera_matriz_vazia_sem_lancar()
    {
        var matriz = Ler("isto não é json");

        Assert.Empty(matriz.Itens);
        Assert.NotEmpty(matriz.Avisos);
    }

    [Fact]
    public void Tolera_cercas_de_markdown()
    {
        const string resposta = "```json\n{\"acoes\":[{\"id\":\"PWR_PLANO_ALTO_DESEMPENHO\",\"prioridade\":1}]}\n```";

        var matriz = Ler(resposta);

        Assert.Single(matriz.Itens);
    }

    [Fact]
    public void Renumera_prioridade_por_ordem()
    {
        const string json = """
            {"acoes":[
              {"id":"NET_THROTTLING_DESABILITAR","prioridade":9},
              {"id":"PWR_PLANO_ALTO_DESEMPENHO","prioridade":3}
            ]}
            """;

        var matriz = Ler(json);

        Assert.Equal("PWR_PLANO_ALTO_DESEMPENHO", matriz.Itens[0].AcaoId);
        Assert.Equal(1, matriz.Itens[0].Prioridade);
        Assert.Equal(2, matriz.Itens[1].Prioridade);
    }
}
````

### `tests/HardwareOptimizer.Cerebro.Tests/VisaoTests.cs`

````csharp
using HardwareOptimizer.Cerebro.Visao;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Cerebro.Tests;

public sealed class VisaoTests
{
    private static ImagemEntrada Imagem(string mediaType = "image/png") =>
        new() { Base64 = "ZmFrZQ==", MediaType = mediaType, Descricao = "teste" };

    private static Inventario Inventario(string versaoBios = "2806") => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = versaoBios },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
    };

    // ---- LeitorRespostaVisao -------------------------------------------------

    [Fact]
    public void Parser_le_tipo_campos_e_confianca()
    {
        const string json =
            "{\"tipoTela\":\"biosUefi\",\"campos\":{\"versao\":\"3405\",\"fabricante\":\"ASUS\"},"
            + "\"confianca\":\"alta\",\"proximoPasso\":\"confirmar\"}";

        var leitura = new LeitorRespostaVisao().Ler(json, "m");

        Assert.Equal(TipoTela.BiosUefi, leitura.TipoTela);
        Assert.Equal("3405", leitura.Campo("versao"));
        Assert.Equal(NivelConfianca.Alta, leitura.Confianca);
    }

    [Fact]
    public void Parser_default_confianca_baixa_quando_ausente()
    {
        var leitura = new LeitorRespostaVisao().Ler("{\"tipoTela\":\"benchmark\",\"campos\":{}}", "m");
        Assert.Equal(NivelConfianca.Baixa, leitura.Confianca);
    }

    [Fact]
    public void Parser_resposta_invalida_vira_desconhecida_e_pede_nova_foto()
    {
        var leitura = new LeitorRespostaVisao().Ler("isto não é json", "m");

        Assert.Equal(TipoTela.Desconhecida, leitura.TipoTela);
        Assert.Equal(NivelConfianca.Baixa, leitura.Confianca);
        Assert.False(string.IsNullOrWhiteSpace(leitura.ProximoPasso));
    }

    // ---- ModuloVisao ---------------------------------------------------------

    [Fact]
    public async Task Modulo_interpreta_imagem_via_cliente()
    {
        var cliente = new ClienteVisaoFake(
            "{\"tipoTela\":\"biosUefi\",\"campos\":{\"versao\":\"3405\"},\"confianca\":\"alta\",\"proximoPasso\":\"x\"}");

        var leitura = await new ModuloVisao(cliente).InterpretarAsync(Imagem(), CasoUsoVisao.LerVersaoBios);

        Assert.Equal(TipoTela.BiosUefi, leitura.TipoTela);
        Assert.Equal("3405", leitura.Campo("versao"));
        Assert.Equal("fake-visao", leitura.Modelo);
    }

    [Fact]
    public async Task Modulo_rejeita_tipo_de_imagem_nao_suportado()
    {
        var cliente = new ClienteVisaoFake("{}");

        await Assert.ThrowsAsync<NotSupportedException>(
            () => new ModuloVisao(cliente).InterpretarAsync(Imagem("image/bmp"), CasoUsoVisao.Identificar));
    }

    // ---- ConferenciaVisual ---------------------------------------------------

    [Fact]
    public void Conferencia_bios_confere_quando_versao_bate()
    {
        var leitura = new LeituraVisual
        {
            TipoTela = TipoTela.BiosUefi,
            Confianca = NivelConfianca.Alta,
            Campos = new Dictionary<string, string> { ["versao"] = "2806" },
        };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario("2806"));

        Assert.Equal(SituacaoConferencia.Confere, resultado.Situacao);
        Assert.False(resultado.PedirNovaFoto);
    }

    [Fact]
    public void Conferencia_bios_diverge_quando_versao_difere()
    {
        var leitura = new LeituraVisual
        {
            TipoTela = TipoTela.BiosUefi,
            Confianca = NivelConfianca.Alta,
            Campos = new Dictionary<string, string> { ["versao"] = "3405" },
        };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario("2806"));

        Assert.Equal(SituacaoConferencia.Diverge, resultado.Situacao);
    }

    [Fact]
    public void Conferencia_confianca_baixa_pede_nova_foto()
    {
        var leitura = new LeituraVisual { TipoTela = TipoTela.BiosUefi, Confianca = NivelConfianca.Baixa };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario());

        Assert.True(resultado.PedirNovaFoto);
        Assert.Equal(SituacaoConferencia.Inconclusivo, resultado.Situacao);
    }

    [Fact]
    public void Conferencia_etiqueta_confere_com_fabricante_sujo()
    {
        var leitura = new LeituraVisual
        {
            TipoTela = TipoTela.EtiquetaPlaca,
            Confianca = NivelConfianca.Alta,
            Campos = new Dictionary<string, string>
            {
                ["fabricante"] = "ASUSTeK Computer Inc.",
                ["modelo"] = "ROG STRIX B550-F",
            },
        };

        var resultado = new ConferenciaVisual().Conferir(leitura, Inventario());

        Assert.Equal(SituacaoConferencia.Confere, resultado.Situacao);
    }

    // ---- ConstrutorPromptVisao -----------------------------------------------

    [Fact]
    public void Prompt_sistema_exige_json_com_confianca()
    {
        var sistema = new ConstrutorPromptVisao().MontarSistema();
        Assert.Contains("confianca", sistema, StringComparison.Ordinal);
        Assert.Contains("tipoTela", sistema, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_usuario_foca_no_caso_de_uso()
    {
        var prompt = new ConstrutorPromptVisao().MontarUsuario(CasoUsoVisao.LerVersaoBios);
        Assert.Contains("BIOS", prompt, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ClienteVisaoFake : IClienteVisao
    {
        private readonly string _resposta;

        public ClienteVisaoFake(string resposta) => _resposta = resposta;

        public string Modelo => "fake-visao";

        public Task<string> AnalisarAsync(
            ImagemEntrada imagem, string promptSistema, string promptUsuario, CancellationToken cancellationToken = default) =>
            Task.FromResult(_resposta);
    }
}
````


## HardwareOptimizer.Ipc.Tests

### `tests/HardwareOptimizer.Ipc.Tests/HardwareOptimizer.Ipc.Tests.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Agent\HardwareOptimizer.Agent.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Ipc\HardwareOptimizer.Ipc.csproj" />
  </ItemGroup>

</Project>
````

### `tests/HardwareOptimizer.Ipc.Tests/GlobalUsings.cs`

````csharp
global using Xunit;
````

### `tests/HardwareOptimizer.Ipc.Tests/IpcTests.cs`

````csharp
using System.Text.Json;
using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.Ipc.Tests;

public sealed class IpcTests
{
    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F", VersaoBios = "2806" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X", Nucleos = 6 },
        Gpu = new[] { new PlacaVideo { Nome = "RTX 3060" } },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Arquitetura = "X64" },
    };

    private static RoteadorIpc Roteador() => new(coletor: new ColetorFake(Inventario()));

    private static RequisicaoIpc Req(string metodo, object? parametros = null) => new()
    {
        Metodo = metodo,
        Parametros = parametros is null ? null : JsonSerializer.SerializeToElement(parametros),
    };

    // ---- RoteadorIpc (unitário) ----------------------------------------------

    [Fact]
    public async Task Ping_responde_pong()
    {
        var r = await Roteador().TratarAsync(Req("ping"));
        Assert.True(r.Sucesso);
        Assert.Equal("pong", r.Resultado);
    }

    [Fact]
    public async Task Metodo_desconhecido_falha()
    {
        var r = await Roteador().TratarAsync(Req("inexistente"));
        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task Catalogo_retorna_lista_de_acoes()
    {
        var r = await Roteador().TratarAsync(Req("catalogo"));

        Assert.True(r.Sucesso);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<AcaoResumoDto>>(r.Resultado);
        Assert.NotEmpty(lista);
        Assert.Contains(lista, a => a.Id == "PWR_PLANO_ALTO_DESEMPENHO");
    }

    [Fact]
    public async Task Coletar_retorna_o_inventario()
    {
        var r = await Roteador().TratarAsync(Req("coletar"));

        Assert.True(r.Sucesso);
        var inv = Assert.IsType<Inventario>(r.Resultado);
        Assert.Equal("ASUS", inv.Placa.Fabricante);
    }

    [Fact]
    public async Task Proposta_retorna_matriz_de_decisao()
    {
        var r = await Roteador().TratarAsync(Req("proposta"));

        Assert.True(r.Sucesso);
        var matriz = Assert.IsType<Cerebro.MatrizDecisao>(r.Resultado);
        Assert.NotEmpty(matriz.Itens);
    }

    [Fact]
    public async Task Aprovar_acoes_executa_e_retorna_relatorio()
    {
        var r = await Roteador().TratarAsync(Req("aprovar", new { acoes = new[] { "PWR_PLANO_ALTO_DESEMPENHO" } }));

        Assert.True(r.Sucesso);
        var relatorio = Assert.IsType<Agent.Execution.RelatorioExecucao>(r.Resultado);
        Assert.True(relatorio.Sucesso);
    }

    [Fact]
    public async Task Aprovar_sem_acoes_falha()
    {
        var r = await Roteador().TratarAsync(Req("aprovar", new { acoes = Array.Empty<string>() }));
        Assert.False(r.Sucesso);
    }

    // ---- Loopback real de named pipe -----------------------------------------

    [Fact]
    public async Task NamedPipe_loopback_responde_requisicoes()
    {
        var nome = "hwopt-test-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var servidor = new ServidorNamedPipe(nome, Roteador());
        var tarefaServidor = servidor.ServirAsync(cts.Token);

        var cliente = new ClienteNamedPipe(nome);
        var ping = await cliente.ChamarAsync("ping", cts.Token);
        var catalogo = await cliente.ChamarAsync("catalogo", cts.Token);

        Assert.True(ping.Sucesso);
        Assert.True(catalogo.Sucesso);

        await cts.CancelAsync();
        try
        {
            await tarefaServidor;
        }
        catch (OperationCanceledException)
        {
            // encerramento esperado
        }
    }

    private sealed class ColetorFake : IColetorInventario
    {
        private readonly Inventario _inventario;

        public ColetorFake(Inventario inventario) => _inventario = inventario;

        public Task<Inventario> ColetarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_inventario);
    }
}
````


## HardwareOptimizer.App.Tests

### `tests/HardwareOptimizer.App.Tests/HardwareOptimizer.App.Tests.csproj`

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HardwareOptimizer.App\HardwareOptimizer.App.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Ipc\HardwareOptimizer.Ipc.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Core\HardwareOptimizer.Core.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Agent\HardwareOptimizer.Agent.csproj" />
    <ProjectReference Include="..\..\src\HardwareOptimizer.Cerebro\HardwareOptimizer.Cerebro.csproj" />
  </ItemGroup>

</Project>
````

### `tests/HardwareOptimizer.App.Tests/GlobalUsings.cs`

````csharp
global using Xunit;
````

### `tests/HardwareOptimizer.App.Tests/MainWindowViewModelTests.cs`

````csharp
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Cerebro;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.App.Tests;

public sealed class MainWindowViewModelTests
{
    private static Inventario Inventario() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "B550-F" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows, Nome = "Windows 11" },
    };

    private static MatrizDecisao Matriz(NivelRisco risco) => new()
    {
        Origem = OrigemDecisao.Local,
        Itens = new[]
        {
            new ItemDecisao
            {
                AcaoId = "PWR_PLANO_ALTO_DESEMPENHO",
                Prioridade = 1,
                Categoria = CategoriaAcao.SistemaOperacional,
                Risco = risco,
                Justificativa = "energia",
            },
        },
    };

    [Fact]
    public async Task Coletar_atualiza_resumo_e_desocupa()
    {
        var vm = new MainWindowViewModel(new RoteadorFake(_ => RespostaIpc.Ok("1", Inventario())));

        await vm.ColetarCommand.ExecuteAsync(null);

        Assert.Contains("ASUS", vm.InventarioResumo, StringComparison.Ordinal);
        Assert.False(vm.Ocupado);
    }

    [Fact]
    public async Task Propor_preenche_a_matriz()
    {
        var vm = new MainWindowViewModel(new RoteadorFake(_ => RespostaIpc.Ok("1", Matriz(NivelRisco.MuitoBaixo))));

        await vm.ProporCommand.ExecuteAsync(null);

        Assert.Single(vm.Matriz);
        Assert.True(vm.Matriz[0].Selecionado); // risco muito baixo é pré-selecionado
    }

    [Fact]
    public async Task Aprovar_sem_selecao_avisa()
    {
        var vm = new MainWindowViewModel(new RoteadorFake(_ => RespostaIpc.Ok("1", Matriz(NivelRisco.Medio))));
        await vm.ProporCommand.ExecuteAsync(null);
        Assert.False(vm.Matriz[0].Selecionado); // risco médio não é pré-selecionado

        await vm.AprovarCommand.ExecuteAsync(null);

        Assert.Contains("Selecione", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aprovar_com_selecao_chama_o_metodo_aprovar()
    {
        string? metodoChamado = null;
        var vm = new MainWindowViewModel(new RoteadorFake(req =>
        {
            metodoChamado = req.Metodo;
            return req.Metodo == "proposta"
                ? RespostaIpc.Ok("1", Matriz(NivelRisco.MuitoBaixo))
                : RespostaIpc.Ok("2", new RelatorioExecucao { Sucesso = true, PerfilNome = "x" });
        }));

        await vm.ProporCommand.ExecuteAsync(null);
        await vm.AprovarCommand.ExecuteAsync(null);

        Assert.Equal("aprovar", metodoChamado);
        Assert.Contains("Aplicado", vm.ResultadoAprovacao, StringComparison.Ordinal);
    }

    private sealed class RoteadorFake : IRoteadorIpc
    {
        private readonly Func<RequisicaoIpc, RespostaIpc> _responder;

        public RoteadorFake(Func<RequisicaoIpc, RespostaIpc> responder) => _responder = responder;

        public Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default) =>
            Task.FromResult(_responder(requisicao));
    }
}
````


## Apêndice — Schemas (contratos JSON)

### `schemas/inventario.schema.json`

````json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://hardwareoptimizer/contracts/inventario.schema.json",
  "title": "Inventario",
  "description": "Inventário normalizado do equipamento (contrato 'inventario'). O bloco 'identificadores' é sensível e é removido pela camada de privacidade antes de qualquer envio à nuvem.",
  "type": "object",
  "required": ["placa", "cpu", "sistemaOperacional"],
  "additionalProperties": false,
  "properties": {
    "placa": {
      "type": "object",
      "required": ["fabricante", "modelo"],
      "additionalProperties": false,
      "properties": {
        "fabricante": { "type": "string" },
        "modelo": { "type": "string" },
        "versaoBios": { "type": ["string", "null"] },
        "dataBios": { "type": ["string", "null"] },
        "modo": { "type": ["string", "null"], "enum": ["UEFI", "Legacy", null] },
        "secureBoot": { "type": ["boolean", "null"] }
      }
    },
    "cpu": {
      "type": "object",
      "required": ["nome"],
      "additionalProperties": false,
      "properties": {
        "nome": { "type": "string" },
        "nucleos": { "type": ["integer", "null"], "minimum": 1 },
        "threads": { "type": ["integer", "null"], "minimum": 1 },
        "tempIdleC": { "type": ["number", "null"] }
      }
    },
    "memoria": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "tamanhoGb": { "type": ["integer", "null"], "minimum": 0 },
          "velocidadeMhz": { "type": ["integer", "null"], "minimum": 0 },
          "fabricante": { "type": ["string", "null"] }
        }
      }
    },
    "gpu": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["nome"],
        "additionalProperties": false,
        "properties": {
          "nome": { "type": "string" },
          "tempIdleC": { "type": ["number", "null"] },
          "versaoDriver": { "type": ["string", "null"] }
        }
      }
    },
    "sistemaOperacional": {
      "type": "object",
      "required": ["tipo"],
      "additionalProperties": false,
      "properties": {
        "tipo": { "type": "string", "enum": ["desconhecido", "windows", "linux"] },
        "nome": { "type": ["string", "null"] },
        "versao": { "type": ["string", "null"] },
        "arquitetura": { "type": ["string", "null"] }
      }
    },
    "rede": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["nome"],
        "additionalProperties": false,
        "properties": {
          "nome": { "type": "string" },
          "tipo": { "type": ["string", "null"] },
          "enderecoMac": { "type": ["string", "null"] }
        }
      }
    },
    "identificadores": {
      "type": ["object", "null"],
      "description": "Campos sensíveis (campos_sensiveis). Nulo após a sanitização.",
      "additionalProperties": false,
      "properties": {
        "numeroSerie": { "type": ["string", "null"] },
        "uuidPlaca": { "type": ["string", "null"] },
        "nomeMaquina": { "type": ["string", "null"] },
        "nomeUsuario": { "type": ["string", "null"] },
        "chaveProdutoWindows": { "type": ["string", "null"] }
      }
    },
    "coletadoEm": { "type": "string", "format": "date-time" }
  }
}
````

### `schemas/recomendacao.schema.json`

````json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://hardwareoptimizer/contracts/recomendacao.schema.json",
  "title": "Recomendacao",
  "description": "Recomendação proposta pelo cérebro (contrato 'recomendacao'). 'acaoId' referencia uma ação do catálogo whitelisted; o LLM nunca gera o comando interno.",
  "type": "object",
  "required": ["categoria", "acao", "justificativa"],
  "additionalProperties": false,
  "properties": {
    "categoria": { "type": "string" },
    "acaoId": {
      "type": ["string", "null"],
      "description": "ID de uma ação existente no catálogo. Obrigatório quando a recomendação é executável."
    },
    "valorAtual": { "type": ["string", "null"] },
    "valorRecomendado": { "type": ["string", "null"] },
    "acao": { "type": "string" },
    "justificativa": { "type": "string" },
    "risco": {
      "type": "string",
      "enum": ["nenhum", "muitoBaixo", "baixo", "medio", "alto"]
    },
    "ganhoEsperado": { "type": ["string", "null"] },
    "fonte": {
      "type": ["string", "null"],
      "description": "Fonte sempre visível; obrigatória para recomendações de BIOS."
    },
    "passosUsuario": {
      "type": "array",
      "items": { "type": "string" }
    }
  }
}
````

### `schemas/resultado_validacao.schema.json`

````json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://hardwareoptimizer/contracts/resultado_validacao.schema.json",
  "title": "ResultadoValidacao",
  "description": "Resultado de um teste de estresse comparando antes/depois (contrato 'resultado_validacao').",
  "type": "object",
  "required": ["categoria", "ferramenta", "estabilidade"],
  "additionalProperties": false,
  "properties": {
    "categoria": { "type": "string" },
    "ferramenta": { "type": "string" },
    "antes": { "$ref": "#/$defs/medicao" },
    "depois": { "$ref": "#/$defs/medicao" },
    "regressao": { "type": "boolean" },
    "erros": {
      "type": "array",
      "items": { "type": "string" }
    },
    "estabilidade": { "type": "string" }
  },
  "$defs": {
    "medicao": {
      "type": ["object", "null"],
      "additionalProperties": false,
      "properties": {
        "score": { "type": ["number", "null"] },
        "tempMaxC": { "type": ["number", "null"] },
        "clockMhz": { "type": ["number", "null"] },
        "consumoW": { "type": ["number", "null"] }
      }
    }
  }
}
````

