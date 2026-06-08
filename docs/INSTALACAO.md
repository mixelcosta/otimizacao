# Guia de Instalação — Agente de Otimização de Hardware

Este guia mostra, passo a passo, como instalar e executar o software em
**Windows, Linux e macOS**. Escolha **uma** das opções (A, B, C ou D).

> ⚠️ O agente lê hardware e pode aplicar otimizações no sistema. Use uma conta
> com privilégios (administrador no Windows, root/sudo no Linux) **apenas quando
> for aplicar mudanças**. Diagnóstico e leitura funcionam sem elevação.

---

## Sumário

- [Instalador Windows (.exe)](#instalador-windows-exe--mais-fácil-no-windows)
- [Opção A — Binário pronto (sem instalar nada)](#opção-a--binário-pronto-recomendado)
- [Opção B — Compilar do código-fonte](#opção-b--compilar-do-código-fonte)
- [Opção C — Docker (Linux)](#opção-c--docker-linux)
- [Opção D — Publicar você mesmo](#opção-d--publicar-você-mesmo)
- [Interface gráfica (Avalonia)](#interface-gráfica-desktop-avalonia)
- [Configuração opcional do LLM](#configuração-opcional-do-llm-cérebro-na-nuvem-e-visão)
- [Onde ficam dados, logs e backups](#onde-ficam-dados-logs-e-backups)
- [Notas de distribuição e segurança](#notas-de-distribuição-e-segurança)
- [Solução de problemas](#solução-de-problemas)

---

## Instalador Windows (.exe) — mais fácil no Windows

A forma mais simples no Windows: um instalador único que coloca a **interface** e
a **CLI** (ambas *self-contained*, sem precisar de .NET), cria **atalhos** no Menu
Iniciar e na Área de Trabalho e um **desinstalador**.

1. Baixe `OtimizacaoHardware-Setup-<versão>.exe` na página de **Releases** (ou
   gere localmente — veja abaixo).
2. Dê duplo clique e siga o assistente (requer **administrador**, pois instala em
   *Arquivos de Programas*). Opcionalmente, marque "adicionar a CLI ao PATH".
3. Abra **Agente de Otimização de Hardware** pelo Menu Iniciar ou pela Área de
   Trabalho.

> Diagnóstico e leitura não exigem privilégios; para **aplicar de verdade** as
> otimizações, veja [Execução real no Windows](#execução-real-no-windows-opt-in).

**Gerar o instalador localmente** (precisa do Inno Setup):
```powershell
winget install JRSoftware.InnoSetup     # uma vez
scripts\gerar-instalador.ps1            # publica os binários e compila o .exe
# -> artifacts\installer\OtimizacaoHardware-Setup-0.1.0.exe (~58 MB)
```

> O instalador ainda **não é assinado** (Authenticode); o SmartScreen pode exibir
> um aviso ("Mais informações" → "Executar assim mesmo"). A assinatura de código
> (EV) é um passo operacional de distribuição.

---

## Opção A — Binário pronto (recomendado)

O binário é **self-contained**: não precisa ter o .NET instalado.

1. Baixe o pacote da sua plataforma na página de **Releases** do repositório
   (ou gere com a [Opção D](#opção-d--publicar-você-mesmo)):
   - `hwopt-cli-win-x64.zip` (Windows)
   - `hwopt-cli-linux-x64.zip` (Linux)
   - `hwopt-cli-osx-x64.zip` (macOS)
2. Extraia o `.zip` em uma pasta de sua preferência.
3. Execute:

   **Windows (PowerShell):**
   ```powershell
   cd hwopt-cli-win-x64
   .\HardwareOptimizer.Cli.exe ajuda
   ```

   **Linux / macOS:**
   ```bash
   cd hwopt-cli-linux-x64
   chmod +x ./HardwareOptimizer.Cli
   ./HardwareOptimizer.Cli ajuda
   ```

4. Para **aplicar** otimizações (não só diagnosticar), execute como
   administrador/root:
   - Windows: abra o PowerShell **como Administrador**.
   - Linux: use `sudo ./HardwareOptimizer.Cli ...`.

---

## Opção B — Compilar do código-fonte

### Pré-requisitos
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- **Git**

### Passos
```bash
git clone https://github.com/mixelcosta/otimizacao.git
cd otimizacao

# Compilar (warnings tratados como erros) e testar
dotnet build HardwareOptimizer.sln -c Release
dotnet test  HardwareOptimizer.sln -c Release

# Executar a CLI
dotnet run --project src/HardwareOptimizer.Cli -- ajuda
```

---

## Opção C — Docker (Linux)

```bash
# Construir a imagem
docker build -t hwopt .

# Diagnóstico (monte /sys somente leitura para ler sensores/hardware)
docker run --rm -v /sys:/sys:ro hwopt coletar
docker run --rm -v /sys:/sys:ro hwopt sensores

# Fluxo de demonstração ponta a ponta (modo simulação seguro)
docker run --rm hwopt demo
```

> O contêiner é adequado para **diagnóstico**. Aplicar otimizações reais no host
> a partir de um contêiner não é o cenário previsto.

---

## Opção D — Publicar você mesmo

Gera os binários self-contained (não exigem .NET instalado na máquina alvo).

**Windows (PowerShell):**
```powershell
# CLI para win-x64 (padrão)
scripts\publish.ps1

# Vários RIDs
scripts\publish.ps1 -Rids win-x64,linux-x64

# Incluindo a UI desktop
scripts\publish.ps1 -ComUI
```

**Linux / macOS (bash):**
```bash
scripts/publish.sh                 # linux-x64, win-x64, osx-x64
scripts/publish.sh linux-x64       # apenas uma plataforma
COM_UI=1 scripts/publish.sh linux-x64
```

Os artefatos ficam em `artifacts/` (ex.: `artifacts/hwopt-cli-win-x64/HardwareOptimizer.Cli.exe`,
~76 MB, com o runtime .NET embutido).

---

## Interface gráfica (desktop, Avalonia)

A UI consome o agente e exibe inventário, sensores e a matriz de decisão, com
**aprovação por seleção de ações**.

```bash
# Do código-fonte
dotnet run --project src/HardwareOptimizer.App

# Ou publique a UI (Opção D, COM_UI=1) e execute o binário gerado
```

> A UI é uma aplicação **desktop** e exige ambiente gráfico (Windows, macOS ou
> Linux com servidor X/Wayland).

---

## Configuração opcional do LLM (cérebro na nuvem e visão)

Por padrão, o **cérebro local** (offline) é usado e nenhum dado sai da máquina.
Para habilitar o cérebro via LLM e o módulo de **visão** (leitura de fotos),
defina duas variáveis de ambiente:

| Variável            | Para que serve                                        |
| ------------------- | ----------------------------------------------------- |
| `ANTHROPIC_API_KEY` | Sua chave da API da Anthropic.                        |
| `HWOPT_LLM_MODELO`  | O ID de um modelo Claude com visão (um Opus atual).   |

**Windows (PowerShell):**
```powershell
$env:ANTHROPIC_API_KEY = "sua-chave"
$env:HWOPT_LLM_MODELO  = "id-do-modelo"
.\HardwareOptimizer.Cli.exe proposta
```

**Linux / macOS:**
```bash
export ANTHROPIC_API_KEY="sua-chave"
export HWOPT_LLM_MODELO="id-do-modelo"
./HardwareOptimizer.Cli proposta
```

> Antes de qualquer envio à nuvem, o inventário passa pelo **pipeline de
> sanitização** (remove/hasheia identificadores). Sem as variáveis acima, tudo
> roda localmente.

---

## Execução real no Windows (opt-in)

A aplicação de otimizações roda em **modo simulação por padrão**, inclusive no
Windows. Para que as ações aprovadas alterem de fato o sistema:

| Variável              | Para que serve                                                    |
| --------------------- | ----------------------------------------------------------------- |
| `HWOPT_EXECUCAO_REAL` | `1`/`true` ativa a execução real (registro, powercfg, serviços).  |

**Requisitos:** Windows + **terminal como Administrador** (UAC). As mudanças em
`HKLM`, planos de energia e serviços exigem elevação.

```powershell
$env:HWOPT_EXECUCAO_REAL = "1"   # PowerShell elevado
.\HardwareOptimizer.Cli.exe servir
```

Sem a variável — ou em Linux/macOS — o agente usa o estado simulado (dry-run).
O **backup obrigatório** e o **rollback automático** valem nos dois modos.

---

## Onde ficam dados, logs e backups

Relativos à pasta do executável (ou ao diretório de saída do `dotnet run`):

| Caminho                          | Conteúdo                                         |
| -------------------------------- | ------------------------------------------------ |
| `data/otimizador.db`             | Banco SQLite (inventário, auditoria, cache BIOS) |
| `data/backups/`                  | Backups obrigatórios criados antes de aplicar    |
| `data/logs/otimizador-AAAAMMDD.log` | Registro do processo (diagnóstico)            |

O caminho do log também é impresso em **stderr** a cada execução.

---

## Notas de distribuição e segurança

- **Assinatura de código:** para distribuição ampla, assine os binários
  (idealmente com certificado **EV**). Sem assinatura, o **SmartScreen**
  (Windows) e antivírus podem alertar — especialmente porque a ferramenta mexe
  no sistema e, em produção, usa driver de sensor.
- **Elevação (UAC/root):** exigida apenas para **aplicar** mudanças; siga o
  princípio do menor privilégio.
- **Driver de sensores (Windows):** a leitura rica de sensores em produção usa
  LibreHardwareMonitor (driver de kernel **assinado**); atente ao **Secure
  Boot**.
- **BIOS:** o software **nunca** altera a BIOS — apenas identifica, verifica com
  o fabricante e orienta. A atualização é manual e por sua conta.

---

## Solução de problemas

| Sintoma                                            | Causa provável / solução                                            |
| -------------------------------------------------- | ------------------------------------------------------------------- |
| `command not found` / `Permission denied` no Linux | Rode `chmod +x ./HardwareOptimizer.Cli`.                            |
| `sensores` diz "nenhum sensor legível"             | A máquina/contêiner não expõe `/sys/class/hwmon` ou falta permissão. |
| Coleta vem "Desconhecido" no Linux                 | Arquivos de `/sys/class/dmi` exigem permissão; rode com `sudo`.      |
| `visao`/`proposta` pedem configuração              | Defina `ANTHROPIC_API_KEY` e `HWOPT_LLM_MODELO`.                     |
| Alerta de antivírus/SmartScreen                    | Binário não assinado; assine para distribuição (ver acima).         |

Para o passo a passo de **uso**, veja o [Manual de Orientações](MANUAL.md).
