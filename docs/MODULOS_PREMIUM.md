# Módulos Premium — Otimize Builder

Documentação dos módulos pagos do produto. O acesso a cada módulo é controlado
por `IServicoLicenca` e verificado antes de exibir o conteúdo na UI
(overlay de bloqueio) e antes de aceitar rotas IPC correspondentes.

## Sumário
- [Modelo de Licenciamento](#modelo-de-licenciamento)
- [Módulo UPGRADE](#módulo-upgrade)
- [Contador de Vida Útil](#contador-de-vida-útil)
- [Gerenciador de Drivers](#gerenciador-de-drivers)
- [Guia BIOS IA](#guia-bios-ia)

---

## Modelo de Licenciamento

### Tipos e funcionalidades

| `TipoLicenca` | `FuncionalidadePremium` | Descrição |
| --- | --- | --- |
| `Gratuita` | — | Acesso ao Dashboard, Otimizador Windows e IA Copiloto |
| `Premium` | `ModuloUpgrade` | Módulo UPGRADE completo |
| `Premium` | `ContadorVidaUtil` | Contador de Vida Útil com S.M.A.R.T. |
| `Premium` | `GerenciadorDrivers` | Gerenciador de Drivers WHQL + instalação silenciosa |
| `Premium` | `GuiaBiosIa` | Guia BIOS IA com chat e análise de foto |

### Implementação (`Features.Licensing`)

```
HardwareOptimizer.Features.Licensing/
├── IServicoLicenca.cs          — interface: TipoAtual, TemAcesso(), AtivarAsync(), DesativarAsync()
├── ServicoLicencaLocal.cs      — implementação: persiste em %AppData%\OtimizeBuilder\license.dat (DPAPI)
├── FuncionalidadePremium.cs    — enum: ModuloUpgrade=0, ContadorVidaUtil=1, GerenciadorDrivers=2, GuiaBiosIa=3
└── TipoLicenca.cs              — enum: Gratuita=0, Premium=1
```

**`ServicoLicencaLocal`** é decorado com `[SupportedOSPlatform("windows")]` porque
usa DPAPI (`System.Security.Cryptography.ProtectedData`) para criptografar o
arquivo de licença em repouso.

### IPC: `obterstatuslicenca`

```jsonc
// Resposta com plano gratuito
{
  "tipo": "Gratuita",
  "moduloUpgrade": false,
  "contadorVidaUtil": false,
  "gerenciadorDrivers": false,
  "guiaBiosIa": false
}

// Resposta com plano Premium
{
  "tipo": "Premium",
  "moduloUpgrade": true,
  "contadorVidaUtil": true,
  "gerenciadorDrivers": true,
  "guiaBiosIa": true
}
```

`IServicoLicenca` é injetado em `RoteadorIpc` como parâmetro opcional — quando
`null`, todas as funcionalidades são tratadas como Gratuita (útil em testes e no
modo CLI sem licença configurada).

---

## Módulo UPGRADE

**Vista:** `UpgradeView.axaml` / `UpgradeViewModel.cs`

### O que faz

Analisa o hardware atual do usuário e sugere upgrades priorizando custo-benefício,
eliminando gargalos identificados pela IA.

### Subcomponentes (`Features.Upgrade`)

| Classe | Responsabilidade |
| --- | --- |
| `ValidadorCompatibilidade` | Verifica compatibilidade entre peça atual e candidata a substituição (socket, DDR, PCIe, TDP) |
| `CalculadoraGargalo` | Score de balanceamento CPU/GPU; identifica o componente limitante |
| `AgenteUpgrade` | Cliente LLM (`AgenteUpgrade.ResponderAsync`) para chat e análise inicial de upgrade |

### Rotas IPC

| Método | Descrição |
| --- | --- |
| `analise_upgrade` | Análise inicial: IA lê o inventário e sugere os upgrades mais impactantes |
| `chat_upgrade` | Chat conversacional sobre upgrade; mantém histórico (`MensagemChat[]`) |

### Segurança

O módulo UPGRADE é **somente leitura** — nunca aplica nenhuma alteração. Serve
exclusivamente para orientação e links de afiliados.

---

## Contador de Vida Útil

**Vista:** `VidaUtilView.axaml` / `VidaUtilViewModel.cs`

### O que faz

Lê atributos S.M.A.R.T. de cada disco, cruza com banco de dados de TBW (Terabytes
Written) por modelo, e estima a vida útil restante.

### Subcomponentes

| Projeto | Classe | Responsabilidade |
| --- | --- | --- |
| `Agent/Smart` | `LeitorSmart` | Lê atributos S.M.A.R.T. via WMI (Windows) |
| `Features.LifeCounter` | `CalculadoraVidaUtil` | Calcula `PorcentagemVidaRestante` usando TBW fabricante vs. escrito |
| `Features.LifeCounter` | `tbw_database.json` | Banco curado de TBW máximo por modelo (Samsung, WD, Seagate, Crucial, Kingston…) |

### Atributos S.M.A.R.T. lidos

| ID | Nome | Uso |
| --- | --- | --- |
| `0xF1` | Total LBAs Written | Cálculo de TBW acumulado |
| `0x09` | Power-On Hours | Horas de uso do disco |
| `0xBB` | Uncorrectable Errors | Indicador de falha iminente |
| `0xC5` | Current Pending Sectors | Setores com problemas pendentes |

### Rota IPC

| Método | Resultado | Plataforma |
| --- | --- | --- |
| `obtersaudediscos` | `SaudeDisco[]` (modelo, TBW escrito, horas, vida restante %, nível) | **Windows** |

### Segurança

`LeitorSmart` é **somente leitura** — nunca emite comandos SMART de escrita.

---

## Gerenciador de Drivers

**Vista:** `DriversView.axaml` / `DriversViewModel.cs`

### O que faz

Lista todos os dispositivos detectados por hardware ID (HWID), compara com o
catálogo WHQL, e permite backup e instalação silenciosa de drivers atualizados.

### Subcomponentes

| Projeto | Classe | Responsabilidade |
| --- | --- | --- |
| `Agent/Drivers` | `ColetorHwid` | Varre `Win32_PnPEntity` via WMI; filtra Display, Rede, Áudio, Chipset, USB, Armazenamento |
| `Features.Drivers` | `IRepositorioDriversWhql` | Interface do catálogo WHQL (versão, URL, certificação) |
| `Features.Drivers` | `AtualizadorDrivers` | `ExportarBackupAsync` (pnputil /export-driver) e `InstalarAsync` |

### Rotas IPC

| Método | Parâmetros | Resultado |
| --- | --- | --- |
| `obterdrivers` | — | `InfoDriver[]` |
| `exportarbackupdrivers` | — | `string` (pasta do snapshot) |
| `instalardriver` | `{ urlDownload }` | `string` (saída pnputil ou "Instalador iniciado.") |

### Fluxo de instalação silenciosa

```
URL fornecida
    ↓ HttpClient.GetByteArrayAsync (timeout 5 min)
    ↓ salva em %TEMP%\OtimizeBuilder\Drivers\<guid>\<arquivo>
    ↓ detecta extensão:
        .inf / .cab → pnputil /add-driver "<caminho>" /install
        .exe        → Process.Start (UseShellExecute=true)
        outro       → Falha: "Formato não suportado"
```

### Segurança e rollback

- O backup obrigatório (`exportarbackupdrivers`) cria um snapshot em
  `%LocalAppData%\OtimizeBuilder\DriverBackups\<timestamp>` antes de qualquer
  instalação.
- Rollback manual: `pnputil /delete-driver <oem-inf>` + restaurar snapshot.
- `pnputil /add-driver` requer que o aplicativo esteja rodando como administrador
  (solicitado via manifesto do app ou UAC na inicialização).

---

## Guia BIOS IA

**Vista:** `BiosGuideView.axaml` / `BiosGuideViewModel.cs`

### O que faz

Dois modos integrados na mesma tela:

1. **Stepper XMP/EXPO** — guia passo a passo específico por fabricante de
   placa-mãe para ativar XMP (DDR4) ou EXPO (DDR5).
2. **Chat com IA** — pergunta livre ao Claude sobre BIOS (contexto da
   configuração do usuário injetado no system prompt).
3. **Análise de foto** — o usuário fotografa a tela de BIOS e a IA identifica
   fabricante, modelo, versão e sugere o próximo passo.

### Rotas IPC

| Método | Parâmetros | Resultado |
| --- | --- | --- |
| `chat_bios` | `{ pergunta: string }` | `string` (resposta IA contextualizada) |
| `analisarbiosfoto` | `{ imagemBase64: string, mediaType: string }` | `string` (leitura formatada) |

### Fluxo de análise de foto

```
Usuário clica "📷 Carregar foto do BIOS"
    ↓ BiosGuideView.OnCarregarFotoClick
    ↓ StorageProvider.OpenFilePickerAsync (PNG/JPG/JPEG/WEBP)
    ↓ BiosGuideViewModel.AnalisarFotoAsync(caminho)
        ↓ File.ReadAllBytes → Convert.ToBase64String
        ↓ IPC: analisarbiosfoto { imagemBase64, mediaType }
            ↓ RoteadorIpc → ModuloVisao.InterpretarAsync(imagem, LerVersaoBios)
                ↓ ClienteVisaoAnthropic (multimodal Claude)
                ↓ LeituraVisual { TipoTela, Campos, Confianca, ProximoPasso }
            ↓ FormatarLeituraBios → string formatada
    ↓ BiosGuideViewModel.ResultadoFoto = texto
    ↓ BiosGuideView exibe card com resultado
```

### Resultado formatado (`analisarbiosfoto`)

```
Imagem identificada: BIOS/UEFI
Fabricante: ASUS
Modelo: ROG STRIX B550-F
Versão BIOS: 2806

→ Acesse AI Tweaker > X.M.P. / D.O.C.P. para ativar o perfil de memória.
```

Quando `TipoTela == Desconhecida` ou `Confianca == Baixa`, retorna somente o
`ProximoPasso` (ex.: "Envie uma foto mais nítida").

### Stepper XMP/EXPO (`GeradorGuiaXmpExpo`)

- Gera passos específicos por fabricante: ASUS → "AI Tweaker", MSI → "OC",
  Gigabyte → "Tweaker", ASRock → "OC Tweaker".
- Identifica DDR4 (XMP) vs DDR5 (EXPO) pela velocidade da RAM (`VelocidadeMhz ≥ 4800`).
- Exibe aviso (`AvisoXmp`) quando necessário (ex.: sem suporte ou placa genérica).
- **Nunca aplica** nenhuma configuração — guia exclusivamente o usuário.

### Variáveis de ambiente requeridas

| Variável | Uso | Padrão |
| --- | --- | --- |
| `ANTHROPIC_API_KEY` | Autenticação com a API Anthropic | (sem chave = falha com mensagem clara) |
| `CLAUDE_MODEL` | Modelo multimodal para visão e chat | `claude-sonnet-4-6` |
