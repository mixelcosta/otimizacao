# Referência do Catálogo de Ações

O catálogo é um **conjunto fechado, auditado e parametrizado** de ações de
otimização. O cérebro (LLM) só pode **selecionar IDs** e definir parâmetros
**dentro das faixas**; o agente determinístico executa o `comando_interno`.
Nenhuma ação fora do catálogo é executada.

Versão do catálogo embutido: **`2024.06-mvp`** (`CatalogoPadrao`).
Veja também: `dotnet run --project src/HardwareOptimizer.Cli -- catalogo`.

## Sumário
- [Estrutura de uma ação](#estrutura-de-uma-ação)
- [Ações do catálogo padrão](#ações-do-catálogo-padrão)
- [Parâmetros numéricos (limites)](#parâmetros-numéricos-limites)
- [Lista branca de serviços](#lista-branca-de-serviços)
- [Como o validador decide](#como-o-validador-decide)

---

## Estrutura de uma ação

| Campo | Descrição |
| --- | --- |
| `id` | Identificador único (ex.: `PWR_PLANO_ALTO_DESEMPENHO`). |
| `categoria` | `Cpu`, `Memoria`, `Gpu`, `SistemaOperacional`, `Drivers`, `Servicos`, `Rede`. |
| `titulo` / `descricao` | Texto legível. |
| `parametros` | Lista de parâmetros (numérico com faixas, ou lista branca). |
| `comando_interno` | Implementação determinística versionada (nunca vem do LLM). |
| `reversao` | Ação inversa para rollback. |
| `risco` | `Nenhum`, `MuitoBaixo`, `Baixo`, `Medio`, `Alto`. |
| `requer_aprovacao` / `requer_reinicio` | Flags. |
| `pre_condicoes` | Checagens obrigatórias (ex.: `backup_confirmado`). |

---

## Ações do catálogo padrão

| ID | Categoria | Risco | Reinício | Pré-condições | Comando interno |
| --- | --- | --- | --- | --- | --- |
| `PWR_PLANO_ALTO_DESEMPENHO` | SistemaOperacional | MuitoBaixo | não | backup | `cmd.pwr.plano_alto_desempenho.v1` |
| `PWR_USB_SUSPENSAO_SELETIVA` | SistemaOperacional | MuitoBaixo | não | backup | `cmd.pwr.usb_suspensao_seletiva.v1` |
| `SO_EFEITOS_VISUAIS_DESEMPENHO` | SistemaOperacional | Nenhum | não | backup | `cmd.so.efeitos_visuais.v1` |
| `SO_SYSTEM_RESPONSIVENESS` | SistemaOperacional | Baixo | não | backup | `cmd.so.system_responsiveness.v1` |
| `GPU_TDR_DELAY` | Gpu | Medio | **sim** | backup | `cmd.gpu.tdr_delay.v1` |
| `GPU_HAGS` | Gpu | Baixo | **sim** | backup | `cmd.gpu.hags.v1` |
| `SRV_DESATIVAR_SERVICO` | Servicos | Medio | não | backup + lista segura | `cmd.srv.desativar_servico.v1` |
| `NET_THROTTLING_DESABILITAR` | Rede | Baixo | **sim** | backup | `cmd.net.throttling_index.v1` |

> Ordem de execução por categoria: **CPU → Memória → GPU → Sistema Operacional →
> Drivers → Serviços → Rede** (ordem natural do enum `CategoriaAcao`).

---

## Parâmetros numéricos (limites)

| Ação | Parâmetro | faixa_segura | faixa_permitida | limite_absoluto | padrão | unidade |
| --- | --- | --- | --- | --- | --- | --- |
| `SO_SYSTEM_RESPONSIVENESS` | `percentual_reserva` | [10, 20] | [0, 20] | 20 | 20 | % |
| `GPU_TDR_DELAY` | `tempo_segundos` | [2, 8] | [2, 60] | 60 | 2 | s |

- **`SO_SYSTEM_RESPONSIVENESS`** — reserva de CPU para tarefas de segundo plano
  (registro `SystemResponsiveness`). Reduzir prioriza o primeiro plano.
- **`GPU_TDR_DELAY`** — tempo antes do Windows reiniciar o driver de vídeo
  travado (registro `TdrDelay`). Valores altos mascaram instabilidade real.

---

## Lista branca de serviços

`SRV_DESATIVAR_SERVICO` só aceita nomes desta lista (parâmetro `nome_servico`;
padrão `DiagTrack`):

```
SysMain · DiagTrack · Fax · RetailDemo · MapsBroker · XblGameSave · XboxNetApiSvc
```

A pré-condição `servico_consta_na_lista_segura` revalida o nome no momento da
execução (defesa em profundidade).

---

## Execução real no Windows

Cada `comando_interno` opera sobre um alvo simbólico (`registro:*`, `powercfg:*`,
`servico:*`) de um `IEstadoSistema`. O **estado simulado** (padrão) guarda os
valores em memória; o **estado real do Windows** (`EstadoSistemaWindows`, ativado
por `HWOPT_EXECUCAO_REAL=1` sob Windows elevado) traduz cada alvo na operação
concreta abaixo, com `Ler`/`Escrever`/`Restaurar` preservando o rollback:

| Alvo simbólico | Operação real no Windows |
| --- | --- |
| `registro:VisualFXSetting` | `HKCU\…\Explorer\VisualEffects\VisualFXSetting` (DWORD; `DESEMPENHO`→2) |
| `registro:SystemResponsiveness` | `HKLM\…\Multimedia\SystemProfile\SystemResponsiveness` (DWORD) |
| `registro:NetworkThrottlingIndex` | `HKLM\…\Multimedia\SystemProfile\NetworkThrottlingIndex` (DWORD; `ffffffff`) |
| `registro:TdrDelay` | `HKLM\…\GraphicsDrivers\TdrDelay` (DWORD) |
| `registro:HwSchMode` | `HKLM\…\GraphicsDrivers\HwSchMode` (DWORD; 2 = HAGS on) |
| `powercfg:plano_ativo` | `powercfg /getactivescheme` · `/setactive <GUID alto desempenho>` |
| `powercfg:usb_suspensao_seletiva` | `powercfg /set{ac,dc}valueindex … 0` + `/setactive` |
| `servico:<nome>` | `sc qc` (lê) · `sc config <nome> start= disabled` + `sc stop` |

O acesso ao registro e a processos é isolado por portas (`IAcessoRegistro`,
`IExecutorProcesso`), o que mantém a lógica testável fora do Windows. Detalhes em
[ARQUITETURA.md](ARQUITETURA.md) e [INSTALACAO.md](INSTALACAO.md#execução-real-no-windows-opt-in).

---

## Como o validador decide

Para um valor proposto de parâmetro numérico, `ParametroNumerico.Validar`
aplica, nesta ordem:

1. **> `limite_absoluto`** → `BloqueioLimiteAbsoluto` (rígido, qualquer perfil).
2. **Fora da `faixa_permitida`** → `Rejeitado`.
3. **Perfil seguro e fora da `faixa_segura`** → `Rejeitado`.
4. **Fora da `faixa_segura`, dentro da permitida** → `RiscoAssumido` (exige
   consentimento; só perfil customizado chega aqui).
5. Caso contrário → `Aceito`.

A saída do LLM passa pelo guard `LeitorRespostaCerebro`, que **descarta ações
fora do catálogo** e **força cada parâmetro à faixa segura** (usa o padrão
seguro quando o valor proposto é inválido).

Para estender o catálogo, veja [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md).
