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
┌──────────────┐     IPC      ┌─────────────────┐    JSON     ┌────────────────┐
│      UI      │ ───────────▶ │  Agente Local   │ ──────────▶ │     Cérebro    │
│  (Avalonia)  │ ◀─────────── │   (.NET 8)      │ ◀────────── │ (local ou LLM) │
└──────────────┘              └─────────────────┘             └────────────────┘
```

- **UI** — interface desktop (Avalonia/MVVM). Exibe inventário, sensores e a
  matriz de decisão; coleta a **aprovação explícita** do usuário.
- **Agente Local** — processo .NET 8 que coleta inventário, lê sensores, faz
  backup, executa mudanças aprovadas e roda a validação. Hospeda o IPC.
- **Cérebro** — seleciona e prioriza ações do catálogo (offline ou via LLM).
  **Nunca gera comandos** — só escolhe IDs do catálogo.

---

## Projetos e dependências

```
HardwareOptimizer.Core      (domínio puro, sem efeitos colaterais, sem deps externas)
        ▲        ▲       ▲
        │        │       │
   Agent│   Cerebro│     │
        │        │       │
HardwareOptimizer.Agent   HardwareOptimizer.Cerebro   (efeitos colaterais / LLM)
        ▲        ▲         ▲
        └────────┴────┬────┘
                      │
            HardwareOptimizer.Ipc          (composição: protocolo + roteador + transporte)
                 ▲          ▲
                 │          │
   HardwareOptimizer.App   HardwareOptimizer.Cli    (UI desktop / linha de comando)
```

| Projeto | Papel | Depende de |
| --- | --- | --- |
| `HardwareOptimizer.Core` | Domínio puro: contratos, catálogo, validação, perfis, consentimento, privacidade, BIOS, score. | (só BCL) |
| `HardwareOptimizer.Agent` | Efeitos colaterais: coletor, sensores, backup, executor, validação, persistência SQLite. | Core |
| `HardwareOptimizer.Cerebro` | Cérebro: matriz de decisão, guard, local/LLM, visão. | Core, SDK Anthropic |
| `HardwareOptimizer.Ipc` | Protocolo, roteador (dispatcher) e transporte named pipe. | Core, Agent, Cerebro |
| `HardwareOptimizer.App` | UI desktop Avalonia (MVVM). | Ipc |
| `HardwareOptimizer.Cli` | Linha de comando (orquestra tudo). | Core, Agent, Cerebro, Ipc |

### Mapa de módulos (resumo)
- **Core:** `Common` (Resultado, enums), `Contracts`, `Catalog`, `Profiles`,
  `Consent`, `Privacy`, `Bios`, `Reporting`.
- **Agent:** `Collector`, `Sensors`, `Backup`, `Execution`, `Validation`,
  `Bios`, `Persistence`.
- **Cerebro:** raiz (matriz, guard, local/LLM, cliente Anthropic) + `Visao`.
- **Ipc:** protocolo, `RoteadorIpc`, `ServidorNamedPipe`, `ClienteNamedPipe`.

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
| **Modo simulação (dry-run) padrão** | O executor opera sobre `IEstadoSistema` abstrato; o simulado torna executor e rollback totalmente testáveis sem tocar o sistema real. |
| **Domínio em português** | Alinhado ao público; schemas refletem a serialização (camelCase). |
| **Warnings tratados como erros** | Qualidade reforçada pelo compilador (exceto o XAML gerado da UI). |
| **`ILogger` opcional (default `NullLogger`)** | Logging sem acoplar bibliotecas a um provider; a CLI conecta o destino em arquivo. |
| **IPC desacoplado por `IRoteadorIpc`** | A UI fala com o agente em processo ou remoto (named pipe) de forma intercambiável; ViewModels testáveis. |

Para o detalhamento das regras invariantes, veja [SEGURANCA.md](SEGURANCA.md).
