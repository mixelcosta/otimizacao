# API IPC

Protocolo de comunicação entre a UI e o agente. É a fronteira pública do
"Agente Local".

## Transporte
- **Named pipe** (cross-platform; modo Byte para funcionar em Linux/macOS).
- **Uma requisição JSON por linha**; a resposta também é uma linha JSON.
- Atende uma conexão por vez (suficiente para uma UI local).
- Nome do pipe padrão (CLI `servir`): `hwopt-agente`.

```bash
hwopt servir            # hospeda o agente no pipe padrão
hwopt servir meu-pipe   # nome customizado
hwopt ipc-demo          # exemplo cliente + servidor no mesmo processo
```

## Envelope

**Requisição** (`RequisicaoIpc`):
```json
{ "id": "opcional", "metodo": "coletar", "parametros": null }
```

**Resposta** (`RespostaIpc`):
```json
{ "id": "...", "sucesso": true, "resultado": { /* objeto do método */ }, "erro": null }
```
Em caso de erro: `"sucesso": false`, `"erro": "mensagem"`, `"resultado": null`.

Método desconhecido retorna `sucesso: false`.

---

## Referência de Métodos

### Utilitários

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `ping` | — | `"pong"` | |
| `coletar` | — | [Inventario](CONTRATOS.md#inventario) | |
| `sensores` | — | [LeituraSensores](CONTRATOS.md#leiturasensores) | |
| `catalogo` | — | lista `AcaoResumoDto[]` | ver abaixo |
| `proposta` | — | [MatrizDecisao](CONTRATOS.md#matrizdecisao) | cérebro, inventário sanitizado |
| `relatorio` | — | [RelatorioExecutivo](CONTRATOS.md#relatorioexecutivo) | |
| `aprovar` | `{ acoes: string[], nomePerfil?: string }` | `RelatorioExecucao` | ver fluxo abaixo |

### Licença

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `obterstatuslicenca` | — | [StatusLicencaDto](#statuslicencadto) | |

### Informações do sistema

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `obtersaudediscos` | — | `SaudeDisco[]` | **Windows** |

### Otimizador Windows — Startup

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `obterentradasstartup` | — | `InicializacaoEntrada[]` | **Windows** |
| `ativarstartup` | `{ nome: string }` | `true` | **Windows** |
| `desativarstartup` | `{ nome: string }` | `true` | **Windows** |

### Otimizador Windows — Serviços

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `obterservicos` | — | `ServicoWindows[]` | **Windows** |
| `iniciarservico` | `{ nome: string }` | `true` | **Windows**, eleva UAC |
| `pararservico` | `{ nome: string }` | `true` | **Windows**, eleva UAC |
| `alterarmododeinicio` | `{ nome: string, modo: string }` | `true` | **Windows**, eleva UAC. `modo`: `"Automático"`, `"Manual"`, `"Desativado"`, `"Automático (Atraso na Inicialização)"` |

### Otimizador Windows — Programas

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `desinstalarprogramas` | `{ programas: [{ nome, uninstallString?, quietUninstallString? }] }` | `int` (count iniciados) | **Windows** |

### Otimizador Windows — Recursos Opcionais

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `obterfeatures` | — | `InfoFeatureWindows[]` | **Windows**. Retorna catálogo curado com estado atual de cada recurso. |
| `habilitarfeature` | `{ nome: string }` | `true` | **Windows**, eleva UAC. `nome` deve estar no catálogo curado. |
| `desabilitarfeature` | `{ nome: string }` | `true` | **Windows**, eleva UAC. `nome` deve estar no catálogo curado. |

**Catálogo curado de features suportadas:**

| `nome` | `nomeExibicao` |
| --- | --- |
| `Microsoft-Windows-Subsystem-Linux` | WSL — Subsistema Linux |
| `Microsoft-Hyper-V-All` | Hyper-V |
| `NetFx3` | .NET Framework 3.5 |
| `Containers-DisposableClientVM` | Windows Sandbox |
| `TelnetClient` | Cliente Telnet |

**Formato de `InfoFeatureWindows`:**

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `nome` | string | ID DISM da feature |
| `nomeExibicao` | string | Nome amigável para exibição |
| `descricao` | string | Descrição da finalidade |
| `estado` | `"Enabled"` \| `"Disabled"` \| `"EnablePending"` \| `"Desconhecido"` | Estado lido via PowerShell |
| `habilitada` | bool | `true` quando `estado == "Enabled"` |

### Drivers (Premium)

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `obterdrivers` | — | `InfoDriver[]` | **Windows** |
| `exportarbackupdrivers` | — | `string` (pasta de destino) | **Windows**, `pnputil /export-driver` |
| `instalardriver` | `{ urlDownload: string }` | `string` (saída pnputil ou "Instalador iniciado.") | **Windows**. `.inf`/`.cab` → pnputil; `.exe` → executa diretamente |

### IA — Upgrade (Premium)

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `analise_upgrade` | — | `string` (análise inicial) | requer `ANTHROPIC_API_KEY` |
| `chat_upgrade` | `{ mensagem: string, historico?: MensagemChat[] }` | `string` (resposta IA) | requer `ANTHROPIC_API_KEY` |

### IA — Guia BIOS (Premium)

| Método | Parâmetros | Resultado | Obs |
| --- | --- | --- | --- |
| `chat_bios` | `{ pergunta: string }` | `string` (resposta IA) | requer `ANTHROPIC_API_KEY` |
| `analisarbiosfoto` | `{ imagemBase64: string, mediaType: string }` | `string` (leitura formatada) | multimodal; `mediaType`: `image/png`, `image/jpeg`, `image/webp`. requer `ANTHROPIC_API_KEY` |

> **Rotas marcadas Windows** retornam `{ sucesso: false, erro: "Requer Windows." }` em outros sistemas operacionais.

---

## Fluxo de aprovação (`aprovar`)

A UI envia **apenas os IDs aprovados**. O agente:
1. monta o **perfil seguro** com esses IDs (recusa se algum for inválido);
2. faz **backup obrigatório**;
3. **executa por categoria** com validação e rollback automático.

O resultado é o relatório de execução (`sucesso`, `categorias[]` com `situacao`
Aplicada/Revertida/Bloqueada e as alterações antes/depois).

---

## Formatos de resultado

### `catalogo` — item (`AcaoResumoDto`)

| Campo | Tipo |
| --- | --- |
| `id` | string |
| `categoria` | string (enum) |
| `titulo` | string |
| `risco` | string (enum) |
| `requerReinicio` | bool |
| `preCondicoes` | string[] |
| `parametros` | `{ nome, tipo, detalhe }[]` |

### `StatusLicencaDto`

```json
{
  "tipo": "Gratuita",
  "moduloUpgrade": false,
  "contadorVidaUtil": false,
  "gerenciadorDrivers": false,
  "guiaBiosIa": false
}
```

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `tipo` | `"Gratuita"` \| `"Premium"` | Plano atual |
| `moduloUpgrade` | bool | Acesso ao módulo UPGRADE |
| `contadorVidaUtil` | bool | Acesso ao Contador de Vida Útil |
| `gerenciadorDrivers` | bool | Acesso ao Gerenciador de Drivers |
| `guiaBiosIa` | bool | Acesso ao Guia BIOS IA |

### `instalardriver` — parâmetros e resultado

```jsonc
// Requisição
{ "metodo": "instalardriver", "parametros": { "urlDownload": "https://.../driver.inf" } }

// Resposta (sucesso .inf)
{ "sucesso": true, "resultado": "Microsoft PnP Utility\n\nAdding driver package:  driver.inf\n..." }

// Resposta (sucesso .exe)
{ "sucesso": true, "resultado": "Instalador iniciado." }

// Resposta (falha)
{ "sucesso": false, "erro": "pnputil saiu com código 5." }
```

### `analisarbiosfoto` — parâmetros e resultado

```jsonc
// Requisição
{
  "metodo": "analisarbiosfoto",
  "parametros": {
    "imagemBase64": "<base64 da imagem>",
    "mediaType": "image/jpeg"
  }
}

// Resposta (sucesso)
{
  "sucesso": true,
  "resultado": "Imagem identificada: BIOS/UEFI\nFabricante: ASUS\nModelo: ROG STRIX B550-F\nVersão BIOS: 2806\n\n→ Acesse AI Tweaker > X.M.P. para ativar o perfil XMP."
}

// Resposta (confiança baixa)
{ "sucesso": true, "resultado": "Não foi possível identificar a tela de BIOS. Envie uma foto mais nítida." }
```

---

## Exemplos

```jsonc
// → ping
{ "metodo": "ping" }
// ← pong
{ "id": "…", "sucesso": true, "resultado": "pong" }
```

```jsonc
// → aprovação
{ "metodo": "aprovar", "parametros": { "acoes": ["PWR_PLANO_ALTO_DESEMPENHO"] } }
// ← relatório
{ "id": "…", "sucesso": true, "resultado": { "sucesso": true, "categorias": [/* … */] } }
```

```jsonc
// → status de licença
{ "metodo": "obterstatuslicenca" }
// ← dto
{ "id": "…", "sucesso": true, "resultado": { "tipo": "Premium", "moduloUpgrade": true, "contadorVidaUtil": true, "gerenciadorDrivers": true, "guiaBiosIa": true } }
```

```jsonc
// → instalar driver
{ "metodo": "instalardriver", "parametros": { "urlDownload": "https://cdn.example.com/driver.inf" } }
// ← resultado pnputil
{ "id": "…", "sucesso": true, "resultado": "Microsoft PnP Utility…" }
```

---

## Cliente (C#)

```csharp
var cliente = new ClienteNamedPipe("hwopt-agente");
var resposta = await cliente.ChamarAsync("coletar");
// Em named pipe, resposta.Resultado chega como JsonElement.
```

Em processo (UI desktop), use `IRoteadorIpc`/`RoteadorIpc` diretamente — o
resultado é o objeto real (sem JSON). É a mesma API, transporte diferente.
