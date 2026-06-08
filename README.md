# Agente de Otimização e Confiabilidade de Hardware

Sistema que **diagnostica, recomenda e aplica** otimizações de desempenho em
computadores e notebooks com base em evidências coletadas do próprio
equipamento. O cérebro (LLM) **propõe**, o agente local **executa apenas com
aprovação explícita**, e a BIOS é sempre ajustada **manualmente** pelo usuário
com orientação do sistema.

> Implementação em **C# / .NET 8**. MVP prioriza **Windows 11**; o coletor já
> roda também em **Linux** (usado como ambiente de validação contínua).

---

## Filosofia operacional

Ordem de prioridade inegociável: **ESTABILIDADE → SEGURANÇA → EFICIÊNCIA → DESEMPENHO.**
"Buscar o maior desempenho **sustentável e validado**, nunca o maior desempenho possível."

As regras invariantes do documento de arquitetura não são apenas texto: elas
estão **codificadas e cobertas por testes**. Em particular:

| Regra invariante | Onde é garantida | Teste |
| --- | --- | --- |
| O LLM nunca gera comandos; só escolhe IDs de um catálogo fechado | `ValidadorAcao`, `RegistroComandos` | `ValidadorAcaoTests` |
| Nenhum valor pode ultrapassar o `limite_absoluto` (bloqueio rígido) | `ParametroNumerico.Validar` | `ValidadorAcaoTests`, `ConstrutorPerfilTests` |
| Perfil seguro por padrão (usa sempre a `faixa_segura`) | `ConstrutorPerfil.CriarPerfilSeguro` | `ConstrutorPerfilTests` |
| Perfil customizado exige consentimento (aviso + 2 checkboxes + confirmação) | `AvaliadorConsentimento` | `ConsentimentoTests` |
| Sem backup confirmado, nada prossegue | `VerificadorPreCondicoes` | `ExecutorControladoTests` |
| Uma categoria por vez, com rollback por categoria | `ExecutorControlado` | `ExecutorControladoTests` |
| Inventário é sanitizado antes de ir à nuvem | `Sanitizador` | `SanitizadorTests` |

---

## 📦 Instalação e 📖 manual de uso

- **[Guia de Instalação](docs/INSTALACAO.md)** — binário pronto, código-fonte, Docker e publicação.
- **[Manual de Orientações](docs/MANUAL.md)** — passo a passo de cada comando e o fluxo seguro de otimização.
- **[Documentação completa](docs/README.md)** — índice técnico: arquitetura, segurança, catálogo, contratos, IPC, glossário, testes e FAQ.

Início rápido (do código-fonte):
```bash
dotnet build HardwareOptimizer.sln -c Release && dotnet test HardwareOptimizer.sln -c Release
dotnet run --project src/HardwareOptimizer.Cli -- demo      # fluxo completo (simulação segura)
dotnet run --project src/HardwareOptimizer.App              # interface gráfica
```

---

## Arquitetura

Três planos, como no documento:

```
┌──────────────┐     IPC      ┌─────────────────┐    JSON     ┌──────────────┐
│      UI      │ ───────────▶ │  Agente Local   │ ──────────▶ │   Cérebro    │
│ (Avalonia*)  │ ◀─────────── │  (.NET 8, este  │ ◀────────── │ (local ou    │
│              │              │   repositório)  │             │  LLM/Anthropic)│
└──────────────┘              └─────────────────┘             └──────────────┘
        * A UI (Avalonia) entra em fase posterior do roadmap.
```

Este repositório entrega o **Agente Local**, o **núcleo de domínio**
compartilhado (onde vivem as garantias de segurança) e o **Cérebro** (local ou
via LLM).

### Estrutura da solução

```
HardwareOptimizer.sln
├── src/
│   ├── HardwareOptimizer.Core/      Domínio puro (sem efeitos colaterais)
│   │   ├── Common/                  Resultado<T>, enums, categorias
│   │   ├── Contracts/               Inventario, Recomendacao, ResultadoValidacao
│   │   ├── Catalog/                 Catálogo whitelisted + validador de limites
│   │   ├── Profiles/                Perfil seguro / customizado + validação ao salvar
│   │   ├── Consent/                 Termo, checkboxes, auditoria de consentimento
│   │   ├── Privacy/                 Pipeline de sanitização
│   │   ├── Bios/                    Normalização, comparação de versão, decisão e guia
│   │   └── Reporting/               Notas 0-100 por domínio + relatório executivo
│   ├── HardwareOptimizer.Agent/     Agente local (efeitos colaterais isolados)
│   │   ├── Collector/               Coletor read-only (Linux real + Windows/CIM)
│   │   ├── Sensors/                 Sensores em tempo real (Linux /sys/hwmon + Windows WMI)
│   │   ├── Backup/                  Backup obrigatório e bloqueante
│   │   ├── Execution/               Executor controlado, comandos, estado, rollback
│   │   ├── Validation/              Runner de estresse: parser + análise de regressão
│   │   ├── Bios/                    Orquestrador do fluxo de BIOS + cache do fabricante
│   │   └── Persistence/             Repositório SQLite (inventário, auditoria, cache BIOS)
│   ├── HardwareOptimizer.Cerebro/   Plano Cérebro: matriz de decisão, guard, local + LLM
│   │   ├── ICerebro / MatrizDecisao   Contrato e proposta priorizada (só IDs do catálogo)
│   │   ├── ConstrutorPrompt           System/user prompt a partir do inventário sanitizado
│   │   ├── LeitorRespostaCerebro      Guard: valida a saída do LLM contra o catálogo
│   │   ├── CerebroLocal / CerebroLlm  Offline (padrão) e via LLM
│   │   ├── ClienteLlmAnthropic        Adapter do SDK oficial da Anthropic
│   │   └── Visao/                     Leitura de fotos + confiança + conferência com inventário
│   ├── HardwareOptimizer.Ipc/       Camada IPC: protocolo, roteador, servidor/cliente named pipe
│   ├── HardwareOptimizer.App/       UI desktop Avalonia (MVVM) consumindo o IPC
│   └── HardwareOptimizer.Cli/       Demonstração ponta a ponta (orquestra todos os planos)
├── tests/
│   ├── HardwareOptimizer.Core.Tests/    Regras invariantes do domínio
│   ├── HardwareOptimizer.Agent.Tests/   Executor, coletor, persistência, backup, sensores, validação
│   ├── HardwareOptimizer.Cerebro.Tests/ Guard, matriz, cérebro local/LLM, visão, privacidade
│   ├── HardwareOptimizer.Ipc.Tests/     Roteador + loopback real de named pipe
│   └── HardwareOptimizer.App.Tests/     ViewModels da UI (com roteador falso)
├── scripts/publish.sh               Publicação self-contained multiplataforma
├── Dockerfile                       Imagem de distribuição (Linux)
├── docs/INSTALACAO.md · docs/MANUAL.md  Instalação e manual de uso
├── schemas/                         JSON Schemas dos contratos (draft 2020-12)
└── docs/arquitetura_otimizador.json Documento de arquitetura de referência
```

---

## O catálogo de ações whitelisted

O coração do sistema. Um **conjunto fechado, auditado e parametrizado** de ações.
O LLM só pode **selecionar IDs e definir parâmetros dentro das faixas**; o agente
determinístico executa o `comando_interno` versionado. Cada parâmetro numérico
tem três níveis de controle:

- **`faixa_segura`** — padrão recomendado; o perfil seguro só usa esta faixa.
- **`faixa_permitida`** — mais ampla; o perfil customizado pode usar, assumindo o risco.
- **`limite_absoluto`** — teto técnico que **nenhum** perfil ultrapassa (bloqueio rígido).

```
              faixa_segura            faixa_permitida           limite_absoluto
   ───────────[==========]──────────[==================]──────────────|──────────▶
              ↑ aceito             ↑ risco assumido     ↑ rejeitado    ↑ bloqueio
                                     (consentimento)                     rígido
```

Veja o catálogo completo com `dotnet run --project src/HardwareOptimizer.Cli -- catalogo`.

---

## Como rodar

Pré-requisito: **.NET 8 SDK**.

```bash
# Restaurar, compilar (warnings tratados como erros) e testar
dotnet build HardwareOptimizer.sln -c Release
dotnet test  HardwareOptimizer.sln -c Release

# Coletar o inventário desta máquina (read-only) em JSON
dotnet run --project src/HardwareOptimizer.Cli -- coletar

# Ver o inventário sanitizado (o que sairia para a nuvem) + relatório de privacidade
dotnet run --project src/HardwareOptimizer.Cli -- sanitizar

# Listar o catálogo de ações e seus limites
dotnet run --project src/HardwareOptimizer.Cli -- catalogo

# Relatório executivo e nota 0-100 do equipamento
dotnet run --project src/HardwareOptimizer.Cli -- relatorio

# Sensores em tempo real (temperatura, clock, voltagem, fan, consumo)
dotnet run --project src/HardwareOptimizer.Cli -- sensores

# IPC: demonstra o servidor + cliente (named pipe) no mesmo processo
dotnet run --project src/HardwareOptimizer.Cli -- ipc-demo
# IPC: hospeda o servidor para a UI (named pipe; Ctrl+C encerra)
dotnet run --project src/HardwareOptimizer.Cli -- servir

# Identificar a BIOS, verificar com o fabricante e gerar o guia (sem aplicar)
dotnet run --project src/HardwareOptimizer.Cli -- bios

# Cérebro: matriz de decisão a partir do inventário sanitizado
dotnet run --project src/HardwareOptimizer.Cli -- proposta

# Visão: interpretar uma foto e cruzar com o inventário (exige LLM configurado)
dotnet run --project src/HardwareOptimizer.Cli -- visao foto.png bios

# Fluxo completo ponta a ponta (modo simulação seguro)
dotnet run --project src/HardwareOptimizer.Cli -- demo

# Aplicar um perfil seguro (simulação por padrão; sem ids usa a proposta do cérebro)
dotnet run --project src/HardwareOptimizer.Cli -- aplicar SO_EFEITOS_VISUAIS_DESEMPENHO PWR_PLANO_ALTO_DESEMPENHO
# Aplicar DE VERDADE no Windows (terminal Administrador):
#   $env:HWOPT_EXECUCAO_REAL = "1"; HardwareOptimizer.Cli.exe aplicar
```

O comando `demo` exercita, em sequência: coleta → sanitização → proposta do
cérebro → perfil seguro → backup bloqueante → execução por categoria →
**bloqueio rígido** de um valor acima do limite absoluto → **risco assumido**
com fluxo de consentimento e auditoria → persistência em SQLite.

O comando `aplicar` executa esse mesmo fluxo de forma objetiva (coleta →
seleção → perfil seguro → backup → execução com rollback → auditoria), em
**simulação por padrão**; só altera o sistema com `HWOPT_EXECUCAO_REAL=1` em
terminal Administrador no Windows.

---

## Cérebro / LLM (matriz de decisão)

O cérebro **seleciona e prioriza IDs do catálogo** a partir do inventário
**sanitizado** — nunca gera comandos. Há duas implementações por trás de
`ICerebro`:

- **`CerebroLocal`** — offline e determinístico, **padrão do MVP** (opção
  "modelo local" do documento). Não envia nada à nuvem.
- **`CerebroLlm`** — usa um LLM via o **SDK oficial da Anthropic**
  (`ClienteLlmAnthropic`).

A saída do LLM é tratada como **não confiável**: o guard `LeitorRespostaCerebro`
descarta qualquer ação que não exista no catálogo e força cada parâmetro à faixa
segura (usando o padrão seguro quando o valor proposto é inválido). Assim, a
regra invariante "o LLM só escolhe do catálogo" vale mesmo se o modelo alucinar.

**Privacidade:** o cérebro só recebe o inventário sanitizado; `CerebroLlm`
recusa o envio se ainda houver dados pessoais (nomes, chave de produto).

**Configuração (opcional, para usar o LLM):** defina as variáveis de ambiente
`ANTHROPIC_API_KEY` e `HWOPT_LLM_MODELO` (o ID do modelo Claude desejado — um
modelo Opus atual é recomendado). Sem elas, a CLI usa o cérebro local. O ID do
modelo **não é fixado no código** — vem da configuração.

### Visão (fluxo_visao)

O módulo de visão (`Cerebro/Visao`) interpreta fotos — tela de BIOS/UEFI,
etiqueta da placa, mensagem de erro/tela azul, benchmark — com um modelo
multimodal, devolvendo **leitura estruturada + nível de confiança + próximo
passo**. A regra do documento é aplicada: nunca confiar cegamente na leitura;
`ConferenciaVisual` **cruza com o inventário coletado** (ex.: versão de BIOS lida
× coletada) e, se a confiança for baixa, **pede uma nova foto**. Exige LLM
configurado (mesmas variáveis acima).

---

## Logs e diagnóstico

Todo processo é instrumentado com `Microsoft.Extensions.Logging` (`ILogger`),
para localizar o **ponto exato** de uma falha. Cada componente recebe um logger
opcional (padrão `NullLogger`, então testes e bibliotecas não escrevem nada por
conta própria); a CLI conecta um provider de arquivo e grava em:

```
<saída>/data/logs/otimizador-AAAAMMDD.log
```

O caminho do arquivo é impresso em **stderr** a cada execução (não polui a saída
JSON em stdout). Cada linha traz `timestamp [nível] Classe - mensagem`, de modo
que a categoria identifica a classe onde o evento ocorreu. Exemplo real:

```
2026-06-07 17:01:31 [WARN ] LeitorLinux - Coleta Linux parcial: campos ficaram como 'Desconhecido' ...
2026-06-07 17:01:31 [INFO ] ExecutorControlado - Categoria SistemaOperacional: APLICADA com 4 alteração(ões).
2026-06-07 17:01:31 [WARN ] ConstrutorPerfil - Perfil 'custom-arriscado' NÃO salvo: 1 bloqueio(s) -> ... 25 > limite absoluto 20.
```

Níveis: `INFO` para marcos do processo, `DEBUG` para detalhe (cada alteração
antes/depois), `WARN` para bloqueios/regressões/coleta parcial e `ERROR` para
exceções (backup, E/S, persistência), sempre com o tipo e a mensagem da exceção.

---

## Mapa do roadmap

Entrega incremental, conforme o `roadmap_desenvolvimento` do documento.

| Fase | Tema | Estado nesta entrega |
| --- | --- | --- |
| 0 | Fundação e setup | ✅ Solução .NET, CI, contratos + schemas, limites de segurança |
| 1 | Coletor read-only | ✅ Linux (real) + Windows/CIM (estruturado); orquestrador multiplataforma |
| 2 | Sensores | ✅ Leitura em tempo real (Linux `/sys/class/hwmon` real + clock; Windows **LibreHardwareMonitor** — clock/voltagem/fan/consumo/temperatura — com fallback automático para WMI) |
| 3 | UI e IPC | ✅ IPC (named pipe) + **UI Avalonia (MVVM)** com inventário, sensores, matriz e aprovação por ação |
| 4 | Cérebro / LLM | ✅ Matriz de decisão + guard contra alucinação + cérebro local e LLM (SDK Anthropic); sanitização aplicada antes do envio |
| 5 | Módulo BIOS | ✅ Identificação, normalização, banco curado + cache SQLite, decisão conservadora e guia por fabricante |
| 6 | Visão | ✅ Pipeline (leitura estruturada + confiança), conferência com o inventário e cliente multimodal (SDK Anthropic) |
| 7 | Backup obrigatório | ✅ Serviço bloqueante com verificação de integridade |
| 8 | Executor controlado | ✅ Catálogo, validador, perfis, consentimento, rollback por categoria + **execução real no Windows** (registro/powercfg/sc.exe, opt-in `HWOPT_EXECUCAO_REAL`) |
| 9 | Validação e testes | ✅ Runner de estresse (parser + análise: WHEA/memória/artefatos/TDR/BSOD/temperatura/queda de score) ligado ao rollback automático |
| 10 | Relatório e score | ✅ Notas 0-100 por domínio + nota final ponderada + relatório executivo |
| 11 | Hardening e distribuição | ◐ Publish self-contained multiplataforma + Docker + workflow de release + documentação; assinatura de código (EV) é passo operacional |

Legenda: ✅ entregue · ◐ parcial/estrutural · ⏳ planejado.

---

## Decisões de design

- **Modo simulação (dry-run) é o padrão.** Os comandos internos operam sobre um
  `IEstadoSistema` abstrato; a implementação `EstadoSistemaSimulado` reproduz a
  semântica ler/escrever/restaurar sem tocar o sistema real, tornando executor e
  rollback totalmente testáveis. A **execução real no Windows**
  (`EstadoSistemaWindows`) implementa a mesma interface — traduzindo os alvos do
  catálogo em registro, `powercfg` e `sc.exe` — e é ativada por opt-in explícito
  (`HWOPT_EXECUCAO_REAL=1`, Windows elevado), sem alterar o executor. O acesso ao
  registro e a processos é isolado por portas (`IAcessoRegistro`,
  `IExecutorProcesso`), mantendo a lógica testável fora do Windows.
- **Domínio modelado em português**, alinhado ao público do projeto. Os schemas
  refletem a serialização real (camelCase); os nomes do documento original
  (inglês) permanecem como referência semântica.
- **`Resultado<T>` em vez de exceções** para o fluxo de validação, mantendo
  erros legíveis para a UI e a auditoria.
- **Warnings tratados como erros** + analisadores .NET ligados (`Directory.Build.props`).
- **Segurança em camadas (defense in depth):** o validador recusa valores fora
  dos limites e, no momento da execução, o `VerificadorPreCondicoes` revalida
  pré-condições (backup, lista branca de serviços) de forma independente.

---

## Pontos de atenção (do documento)

- Verificação com o fabricante é a parte menos confiável: prever banco curado +
  fetch oficial + fonte sempre visível.
- Leitura visual deve ser sempre validada contra dados coletados.
- Atualização de BIOS é arriscada: postura conservadora; alertar sobre perda de
  energia e garantia.
- Ferramenta usa driver de sensor: assinatura de código é essencial.
- Exigir elevação (UAC/root) com princípio de menor privilégio.

---

## Contribuição e políticas

- **[CONTRIBUTING.md](CONTRIBUTING.md)** — fluxo de branch, commits, checklist e como estender.
- **[CHANGELOG.md](CHANGELOG.md)** — histórico de versões por fase do roadmap.
- **[SECURITY.md](SECURITY.md)** — política de segurança e reporte de vulnerabilidades.
- **[Glossário](docs/GLOSSARIO.md)** — termos do domínio para pessoas e agentes de IA.

Templates de [issue](.github/ISSUE_TEMPLATE/) e
[pull request](.github/PULL_REQUEST_TEMPLATE.md) padronizam as contribuições.
