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

## Arquitetura

Três planos, como no documento:

```
┌──────────────┐     IPC      ┌─────────────────┐    JSON     ┌──────────────┐
│      UI      │ ───────────▶ │  Agente Local   │ ──────────▶ │   Cérebro    │
│ (Avalonia*)  │ ◀─────────── │  (.NET 8, este  │ ◀────────── │ (LLM multi-  │
│              │              │   repositório)  │             │  modal*)     │
└──────────────┘              └─────────────────┘             └──────────────┘
        * UI e LLM real entram em fases posteriores do roadmap.
```

Este repositório entrega o **Agente Local** e o **núcleo de domínio**
compartilhado, que é onde vivem as garantias de segurança do sistema.

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
│   │   ├── Backup/                  Backup obrigatório e bloqueante
│   │   ├── Execution/               Executor controlado, comandos, estado, rollback
│   │   ├── Bios/                    Orquestrador do fluxo de BIOS + cache do fabricante
│   │   └── Persistence/             Repositório SQLite (inventário, auditoria, cache BIOS)
│   └── HardwareOptimizer.Cli/       Demonstração ponta a ponta + cérebro simulado
├── tests/
│   ├── HardwareOptimizer.Core.Tests/    Regras invariantes do domínio
│   └── HardwareOptimizer.Agent.Tests/   Executor, coletor, persistência, backup
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

# Identificar a BIOS, verificar com o fabricante e gerar o guia (sem aplicar)
dotnet run --project src/HardwareOptimizer.Cli -- bios

# Fluxo completo ponta a ponta (modo simulação seguro)
dotnet run --project src/HardwareOptimizer.Cli -- demo
```

O comando `demo` exercita, em sequência: coleta → sanitização → proposta do
cérebro → perfil seguro → backup bloqueante → execução por categoria →
**bloqueio rígido** de um valor acima do limite absoluto → **risco assumido**
com fluxo de consentimento e auditoria → persistência em SQLite.

---

## Mapa do roadmap

Entrega incremental, conforme o `roadmap_desenvolvimento` do documento.

| Fase | Tema | Estado nesta entrega |
| --- | --- | --- |
| 0 | Fundação e setup | ✅ Solução .NET, CI, contratos + schemas, limites de segurança |
| 1 | Coletor read-only | ✅ Linux (real) + Windows/CIM (estruturado); orquestrador multiplataforma |
| 2 | Sensores | ⏳ Interface prevista; integração LibreHardwareMonitor pendente |
| 3 | UI e IPC | ⏳ CLI no lugar da UI Avalonia; IPC pendente |
| 4 | Cérebro / LLM | ◐ `CerebroSimulado` determinístico + **pipeline de sanitização pronto** |
| 5 | Módulo BIOS | ✅ Identificação, normalização, banco curado + cache SQLite, decisão conservadora e guia por fabricante |
| 6 | Visão | ⏳ Fluxo documentado |
| 7 | Backup obrigatório | ✅ Serviço bloqueante com verificação de integridade |
| 8 | Executor controlado | ✅ Catálogo, validador, perfis, consentimento, rollback por categoria |
| 9 | Validação e testes | ◐ Hook `IValidadorCategoria` + rollback em regressão; runners reais pendentes |
| 10 | Relatório e score | ✅ Notas 0-100 por domínio + nota final ponderada + relatório executivo |
| 11 | Hardening e distribuição | ⏳ Assinatura de código e instalador pendentes |

Legenda: ✅ entregue · ◐ parcial/estrutural · ⏳ planejado.

---

## Decisões de design

- **Modo simulação (dry-run) é o padrão.** Os comandos internos operam sobre um
  `IEstadoSistema` abstrato; a implementação `EstadoSistemaSimulado` reproduz a
  semântica ler/escrever/restaurar sem tocar o sistema real, tornando executor e
  rollback totalmente testáveis. Implementações reais (powercfg, registro,
  `sc.exe`) sob Windows elevado substituem essa peça sem alterar o executor.
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
