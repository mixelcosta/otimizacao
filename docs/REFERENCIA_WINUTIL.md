# Referência Técnica — Chris Titus Tech WinUtil

Documento de engenharia reversa do **WinUtil** (`ChrisTitusTech/winutil`, executado via
`irm https://christitus.com/win | iex`) com o objetivo de avaliar quais padrões
de arquitetura são reaproveitáveis no **Otimize Builder**. Não é documentação
oficial do projeto — é uma análise do código-fonte público, feita para orientar
decisões de design aqui no repositório `otimizacao`.

Fonte: [github.com/ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil)
(branch `main`, consultado em 2026-07-09).

## Sumário
- [Visão geral](#visão-geral)
- [Arquitetura de build e runtime](#arquitetura-de-build-e-runtime)
- [Modelo de configuração declarativa](#modelo-de-configuração-declarativa)
- [Pipeline de execução](#pipeline-de-execução)
- [Catálogo de funções](#catálogo-de-funções)
- [Modelo de concorrência](#modelo-de-concorrência)
- [Modelo apply/undo](#modelo-applyundo)
- [Logging](#logging)
- [Modelo de segurança do WinUtil](#modelo-de-segurança-do-winutil)
- [Comparação com o Otimize Builder](#comparação-com-o-otimize-builder)
- [Recomendações](#recomendações)

---

## Visão geral

WinUtil é uma aplicação WPF de desktop, escrita inteiramente em PowerShell,
distribuída como **um único arquivo `.ps1`** que é baixado e executado em
memória (`irm | iex`). Ele resolve o mesmo problema de alto nível que o
Otimize Builder: apresentar uma lista de ações do sistema (instalar programas,
mudar registro, ativar/desativar serviços e features do Windows) por trás de
uma UI com checkboxes, e executá-las com feedback de progresso.

A diferença estrutural mais importante em relação ao Otimize Builder:
**não existe um catálogo fechado com faixas de parâmetro, risco e reversão
tipada** — existe um **schema de configuração genérico** (JSON) interpretado
por um pequeno conjunto de funções "worker" que sabem mexer em registro,
serviços, features do Windows e pacotes. A validação de segurança fica quase
inteiramente a cargo de quem edita o JSON, não do runtime.

---

## Arquitetura de build e runtime

- **Bundling**: o repositório é multi-arquivo (`functions/public/*.ps1`,
  `functions/private/*.ps1`, `config/*.json`, XAML da UI). Um compilador
  próprio (`Invoke-Build`, referenciado no README) concatena tudo em um único
  `.ps1` publicado nas releases — é esse arquivo único que `christitus.com/win`
  redireciona.
- **UI declarativa**: a janela principal é definida em XAML (WPF nativo do
  Windows, não Avalonia), carregado em runtime via `[Windows.Markup.XamlReader]`.
  Os elementos (`CheckBox`, `Button`) recebem seus nomes (`WPFTweaksXxx`,
  `WPFInstallXxx`) diretamente das chaves do JSON de configuração — ou seja,
  **o nome da chave no JSON é o mesmo nome do controle WPF**, o que permite
  a UI ser gerada/populada por convenção em vez de código manual por item.
- **Estado global compartilhado**: existe uma hashtable sincronizada global
  chamada `$sync` (equivalente a um "app state" singleton) que guarda
  referências à janela, aos configs carregados dos JSONs, ao runspace pool,
  às seleções atuais do usuário (`$sync.selectedApps`, `$sync.selectedTweaks`)
  e ao caminho de log. Toda função lê/escreve nesse objeto — é o análogo
  funcional de um `IEstadoAplicacao`/DI container, mas sem tipagem.
- **Sem processo separado**: ao contrário do Otimize Builder (UI ↔ Agente via
  named pipe), WinUtil roda tudo **no mesmo processo e com o mesmo nível de
  privilégio da UI** — não há separação entre camada de apresentação e camada
  de execução privilegiada.

---

## Modelo de configuração declarativa

Quatro arquivos JSON definem *o que existe* na ferramenta; nenhuma dessas
definições está hardcoded em PowerShell:

| Arquivo | Conteúdo | Chave usada como nome do controle |
| --- | --- | --- |
| [`config/applications.json`](https://github.com/ChrisTitusTech/winutil/blob/main/config/applications.json) | ~200 programas instaláveis, por categoria | `WPFInstall<Nome>` |
| [`config/tweaks.json`](https://github.com/ChrisTitusTech/winutil/blob/main/config/tweaks.json) | ~66 tweaks (registro, serviço, script, appx) | `WPFTweaks<Nome>` / `WPFToggle<Nome>` |
| [`config/feature.json`](https://github.com/ChrisTitusTech/winutil/blob/main/config/feature.json) | Features opcionais do Windows (Hyper-V, WSL, .NET…) | `WPFFeature<Nome>` |
| [`config/preset.json`](https://github.com/ChrisTitusTech/winutil/blob/main/config/preset.json) | Listas de checkboxes agrupadas em "Standard/Minimal/Advanced" | — |

### Schema de uma entrada de `tweaks.json` (simplificado)

```jsonc
"WPFTweaksTelemetry": {
  "Content": "Telemetry - Disable",          // texto exibido
  "Description": "Disables Microsoft Telemetry.",
  "category": "Essential Tweaks",
  "panel": "1",
  "Order": "a072_",
  "registry": [
    {
      "Path": "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
      "Name": "AllowTelemetry",
      "Type": "DWord",
      "Value": "0",            // valor ao aplicar
      "OriginalValue": "1"     // valor ao desfazer (undo)
    }
  ],
  "service": [
    { "Name": "DiagTrack", "StartupType": "Disabled", "OriginalType": "Automatic" }
  ],
  "InvokeScript": [ "# bloco PowerShell livre executado ao aplicar" ],
  "UndoScript":   [ "# bloco PowerShell livre executado ao desfazer" ],
  "appx": [ "Microsoft.549981C3F5F10" ]
}
```

Cada entrada é **auto-descritiva e auto-reversível**: carrega tanto o valor
"aplicado" quanto o valor "original" para desfazer. Não existe conceito de
faixa segura/permitida/limite absoluto como no catálogo do Otimize Builder —
o valor é um literal fixo por tweak, sem parametrização em tempo de execução
pelo usuário (exceto casos pontuais como DNS custom).

---

## Pipeline de execução

```
Usuário marca checkboxes na UI
        │
        ▼
Botão "Apply" dispara função pública (Invoke-WPFtweaksbutton / Invoke-WPFInstall / Invoke-WPFFeatureInstall)
        │
        ▼
Invoke-WPFRunspace agenda um runspace em background (não trava a UI)
        │
        ▼
Dentro do runspace: função orquestradora por domínio
   ├─ Invoke-WinUtilTweaks         (tweaks)
   ├─ Invoke-WinUtilFeatureInstall (features opcionais do Windows)
   └─ Install-WinUtilProgramWinget / …Choco (apps)
        │
        ▼
Orquestrador lê a entrada correspondente do JSON já carregado em $sync.configs
        │
        ▼
Dispara 1..N ações via funções "worker" (uma por tipo de efeito colateral):
   Set-WinUtilRegistry · Set-WinUtilService · Enable-WindowsOptionalFeature
   · Remove-WinUtilAPPX · Invoke-Command -ScriptBlock (InvokeScript/UndoScript)
        │
        ▼
Write-WinUtilLog registra cada passo; progresso é refletido na UI via
Set-WinUtilProgressbar / Set-WinUtilTaskbarItem
```

Ponto-chave: **o orquestrador não sabe o que está fazendo** — ele apenas itera
propriedades do objeto JSON (`registry`, `service`, `InvokeScript`, `appx`) e
delega para a função worker correspondente ao nome da propriedade. Isso é o
que permite adicionar um tweak novo editando só o JSON, sem tocar em código
PowerShell.

---

## Catálogo de funções

### Orquestradores (`functions/public`, disparados por evento de UI)

| Função | Dispara a partir de | Delega para |
| --- | --- | --- |
| `Invoke-WPFInstall` / `Invoke-WPFUnInstall` | Botão instalar/desinstalar apps | `Install-WinUtilProgramWinget`, `Install-WinUtilProgramChoco` |
| `Invoke-WPFInstallUpgrade` | Botão "upgrade all" | mesma cadeia, com `winget upgrade` |
| `Invoke-WPFtweaksbutton` | Botão aplicar tweaks | `Invoke-WinUtilTweaks` |
| `Invoke-WPFundoall` | Botão desfazer tudo | `Invoke-WinUtilTweaks -undo` |
| `Invoke-WPFFeatureInstall` | Botão aplicar features | `Invoke-WinUtilFeatureInstall` |
| `Invoke-WPFPresets` | Seleção de preset (Standard/Minimal/Advanced) | `Update-WinUtilSelections` + `Reset-WPFCheckBoxes` |
| `Invoke-WPFToggleAllCategories` | "Selecionar tudo" por categoria | manipula `$sync.selectedTweaks/Apps` |
| `Invoke-WPFAppxRemoval` | Remoção avulsa de apps embutidos (fora de tweaks) | `Remove-WinUtilAPPX` |
| `Invoke-WPFUltimatePerformance` | Toggle do plano de energia "Ultimate Performance" | `powercfg -duplicatescheme` / `-delete` |
| `Invoke-WPFOOSU` | Baixa e executa o binário externo O&O ShutUp10++ | `Start-Process` sobre executável baixado |
| `Invoke-WPFSSHServer` | Ativa/configura o serviço OpenSSH | `Invoke-WinUtilSSHServer` |
| `Invoke-WPFPanelAutologin` | Configura autologon | grava chaves em `HKLM\...\Winlogon` (usuário/senha em texto, ver seção de segurança) |
| `Invoke-WPFSystemRepair` | Botão "System Corruption Scan" | `sfc /scannow`, `DISM /RestoreHealth` |
| `Invoke-WPFFixesUpdate` / `…Network` / `…Winget` / `…NTPPool` | Botões de "Fixes" | reset de serviços do Windows Update, `netsh winsock reset`, reinstala WinGet via `Add-AppxPackage`, troca servidor NTP |
| `Invoke-WPFUpdatesdefault` / `…disable` / `…security` | Radio buttons de política de Windows Update | grava políticas de grupo via registro (`HKLM\...\WindowsUpdate\AU`) |
| `Invoke-WPFImpex` | Import/export da seleção atual para arquivo `.json` | serializa `$sync.selectedTweaks/Apps` |

### Orquestradores intermediários (`functions/private`)

| Função | Papel |
| --- | --- |
| `Invoke-WinUtilTweaks` | Já detalhado — itera `registry/service/InvokeScript/UndoScript/appx` de um tweak, aplica ou desfaz (`$undo`) |
| `Invoke-WinUtilFeatureInstall` | Itera lista `feature` e chama `Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart`; também roda `InvokeScript` de features que precisam de passo extra (ex.: WSL precisa de `wsl --set-default-version 2` depois de habilitado) |
| `Invoke-WinUtilCurrentSystem` | No load da UI, varre o sistema atual (registro/serviços) para pré-marcar checkboxes já aplicadas — usa a mesma definição do JSON, mas em modo leitura |
| `Invoke-WinUtilScript` | Wrapper único para rodar `InvokeScript`/`UndoScript`: cria `[scriptblock]::Create($texto)` e executa com `Invoke-Command`, capturando exceção e logando |
| `Invoke-WinUtilExplorerUpdate` | Depois de tweaks que mexem no Explorer, reinicia `explorer.exe` para refletir a mudança sem exigir logoff |
| `Invoke-WinUtilFontScaling` | Aplica DPI/escala de fonte customizada via registro (`HKCU\Control Panel\Desktop`) |
| `Invoke-WinUtilInstallPSProfile` / `Invoke-WinUtilUninstallPSProfile` | Baixa e instala/remove o perfil PowerShell customizado do autor (edita `$PROFILE`) |
| `Invoke-WinUtilISO` / `…ISOScript` / `…ISOUSB` | Módulo separado (MicroWin): baixa uma ISO oficial do Windows, remove bloatware da imagem via DISM offline e grava em pendrive — fora do escopo de "tweaks" comuns |
| `Invoke-WinUtilSponsors` | Puxa lista de patrocinadores para exibir na UI (não afeta o sistema) |
| `Invoke-WinutilThemeChange` | Alterna claro/escuro da própria UI do WinUtil (não do Windows) |

### Funções "worker" (efeito colateral real no sistema)

| Função | Mecanismo | Observações |
| --- | --- | --- |
| `Set-WinUtilRegistry` | `New-Item` (cria path se faltar) + `Set-ItemProperty` / `Remove-ItemProperty` (se valor for `<RemoveEntry>`) | Monta `HKU:` sob demanda para poder editar hives de outros usuários. Try/catch para `SecurityException`, `ItemNotFoundException`, `UnauthorizedAccessException` |
| `Set-WinUtilService` | `Set-Service -StartupType`; para `AutomaticDelayedStart` em PowerShell 5 cai para `sc.exe config <svc> start= delayed-auto` (cmdlet nativo não suporta esse valor em versões antigas) | Verifica `Get-Service` antes; no-op se já estiver no estado alvo |
| `Enable-WindowsOptionalFeature` (cmdlet nativo, não função própria) | `-Online -FeatureName X -All -NoRestart` | Usado por `Invoke-WinUtilFeatureInstall`; não usa `DISM.exe` diretamente |
| `Remove-WinUtilAPPX` | `Get-AppxPackage -AllUsers <pattern> \| Remove-AppxPackage` + `Get-AppxProvisionedPackage -Online \| Remove-AppxProvisionedPackage` | Remove tanto a instância do usuário atual quanto o pacote provisionado (evita reinstalação para novos perfis de usuário) |
| `Install-WinUtilProgramWinget` | `Start-Process winget -ArgumentList "install --id X --silent --accept-package-agreements --accept-source-agreements --source winget" -Wait -PassThru` | Sequencial (um processo por vez, `-Wait`); não valida `ExitCode`, só loga; suporta prefixo `msstore:` para trocar a fonte |
| `Install-WinUtilProgramChoco` | Mesmo padrão via `choco install <pkg> -y`, fallback quando winget não está disponível | `Test-WinUtilPackageManager` decide qual gerenciador usar |
| `Install-WinUtilWinget` | Bootstrap: instala o próprio App Installer/winget via `Add-AppxPackage` a partir de um `.msixbundle` baixado, caso o winget não exista no sistema | |
| `Set-WinUtilDNS` | `Set-DnsClientServerAddress -InterfaceIndex X -ServerAddresses (...)` para todos os adaptadores ativos | Mapa fixo de provedores (Google, Cloudflare, etc.) para IPs |
| `Invoke-WinUtilSSHServer` | `Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0` + `Set-Service sshd -StartupType Automatic` + `Start-Service sshd` + regra de firewall | |

### Infraestrutura de UI/execução

| Função | Papel |
| --- | --- |
| `Initialize-WinUtilRunspacePool` | Cria `[runspacefactory]::CreateRunspacePool(1, N, $sessionState, $Host)` onde `N = NUMBER_OF_PROCESSORS`; injeta no `InitialSessionState` **todas** as funções públicas/privadas e a variável `$sync`, para que qualquer runspace filho tenha acesso ao mesmo conjunto de funções sem re-`dot-source` |
| `Invoke-WPFRunspace` | `[powershell]::Create().AddScript($sb).AddArgument(...)`, associa ao pool (`.RunspacePool = $sync.runspace`), dispara com `BeginInvoke()` (assíncrono, não bloqueia) e registra limpeza via `[System.Threading.ThreadPool]::RegisterWaitForSingleObject` para chamar `EndInvoke` + `Dispose` quando terminar |
| `Close-WinUtilRunspacePool` | Fecha e dispõe o pool ao fechar a janela (evita threads penduradas) |
| `Write-WinUtilLog` | `[$timestamp] [$Level] [$Component] $Message` gravado em `%LOCALAPPDATA%\winutil\logs\winutil_<data>.log` (fallback se `$sync.logPath` não existir); erro de IO cai para `Write-Host` |
| `Set-WinUtilProgressbar` / `Set-WinUtilTaskbarItem` | Atualizam a barra de progresso da janela e o overlay de progresso no ícone da barra de tarefas do Windows (`TaskbarItemInfo`), via `Dispatcher.Invoke` (marshaling para a UI thread) |
| `Get-WinUtilVariables` | Reflection sobre o próprio script para listar todas as variáveis `$WPF*`/`$sync` disponíveis — usado internamente para debug/validação, não em produção |
| `Get-WinUtilToggleStatus` | Lê o estado atual do registro/serviço associado a um toggle para decidir se o checkbox deve nascer marcado ou não |
| `Find-AppsByNameOrDescription` / `Find-TweaksByNameOrDescription` | Filtro da caixa de busca da UI sobre os JSONs carregados em memória |
| `Update-WinUtilSelections` / `Reset-WPFCheckBoxes` | Sincronizam o estado lógico (`$sync.selectedTweaks/Apps`) com o estado visual dos `CheckBox` — necessário porque presets podem mudar seleção sem clique direto do usuário |

---

## Modelo de concorrência

O ponto mais transferível tecnicamente para qualquer app desktop (WPF, WinForms, Avalonia) é este padrão, porque resolve exatamente o problema que o Otimize Builder já resolveu de forma análoga com `Task.Run`:

1. Um **pool de runspaces** é criado uma vez, dimensionado por `NUMBER_OF_PROCESSORS`, com todas as funções do app pré-carregadas no `InitialSessionState` (equivalente a um `IServiceProvider` compartilhado entre threads).
2. Cada ação longa (instalar N programas, aplicar M tweaks) roda em um `[powershell]` isolado desse pool, disparado com `BeginInvoke()` — **nunca síncrono na thread de UI**.
3. A limpeza de recursos (`EndInvoke` + `Dispose`) é feita por callback do `ThreadPool.RegisterWaitForSingleObject`, não por polling — evita vazamento de handles sem exigir um "aguardar" bloqueante em lugar nenhum.
4. Atualizações de UI (barra de progresso, texto de status) feitas de dentro do runspace passam obrigatoriamente por `Dispatcher.Invoke`, porque WPF (como Avalonia) não permite tocar elementos visuais fora da UI thread.

Isso é conceitualmente o mesmo problema que `ARQUITETURA.md` já resolve no
Otimize Builder com `Task.Run` + `Interlocked.CompareExchange` (tick de
sensores) e com a fila de progresso do `ScanAsync` — a diferença é que o
WinUtil precisa desse mecanismo mais pesado (pool de runspaces PowerShell)
porque PowerShell não tem `async/await` nativo equivalente ao `Task` do .NET;
em C#, `Task.Run`/`IProgress<T>` já cobre o mesmo caso de uso com bem menos
código.

---

## Modelo apply/undo

Cada tweak carrega, na própria definição JSON, tanto o **valor de destino**
quanto o **valor original** (`Value`/`OriginalValue`, `InvokeScript`/`UndoScript`,
`StartupType`/`OriginalType`). `Invoke-WinUtilTweaks -undo` simplesmente troca
qual par de propriedades é lido. Não há:

- Snapshot real do estado do sistema antes de aplicar (o "original" é um
  literal fixo assumido como "o padrão de fábrica do Windows", não o valor
  medido naquela máquina antes da mudança);
- Backup transacional nem rollback automático por falha de validação;
- Distinção entre "não sei o valor anterior" e "o valor anterior era X" —
  se o JSON estiver errado sobre o valor de fábrica, o undo aplica o valor
  errado silenciosamente.

Isso contrasta diretamente com o par `comando_interno`/`reversao` +
`IEstadoSistema.Ler/Escrever/Restaurar` do Otimize Builder (`CATALOGO.md`),
que captura o valor **real e atual** da máquina antes de escrever, tornando o
rollback fiel independentemente de qual era o estado de partida.

---

## Logging

Log em texto plano, uma linha por evento, formato
`[timestamp] [LEVEL] [Component] mensagem`, gravado em
`%LOCALAPPDATA%\winutil\logs\winutil_<data-hora>.log`. Não é estruturado
(sem JSON/campo de correlação), não integra com Event Log do Windows, e não
distingue "tentei" de "consegui" de forma consistente — muitas chamadas
(ex.: `Install-WinUtilProgramWinget`) logam o exit code do processo mas não
interrompem nem marcam falha de forma que a UI possa reagir.

---

## Modelo de segurança do WinUtil

Pontos relevantes para avaliar **o que não replicar**:

| Característica | Risco |
| --- | --- |
| `irm \| iex` | Execução remota sem verificação de assinatura/hash; qualquer comprometimento do domínio ou do CDN executa código arbitrário como admin, sem revisão do usuário |
| `InvokeScript`/`UndoScript` livre no JSON | Qualquer tweak pode conter PowerShell arbitrário — não há sandboxing nem lista de operações permitidas; equivalente a não ter catálogo fechado |
| Sem faixas de parâmetro | Valores são literais fixos por tweak; não existe conceito de "risco assumido com consentimento explícito" como no Otimize Builder — o único "consentimento" é a checkbox inicial |
| Roda no processo da UI, já elevado | Não há separação de privilégio entre camada de apresentação e camada de execução (diferente do modelo Agente + serviço do Otimize Builder) |
| `Invoke-WPFPanelAutologin` grava senha em texto no registro | Prática reconhecidamente insegura do próprio Windows (`DefaultPassword` em `HKLM\...\Winlogon`), replicada sem aviso adicional na ferramenta |
| Sem backup obrigatório antes de aplicar | Existe um tweak opcional "Restore Point - Create", mas nada força ou verifica que rodou antes de aplicar o resto |

---

## Comparação com o Otimize Builder

| Conceito | WinUtil | Otimize Builder |
| --- | --- | --- |
| Definição de ações | JSON genérico interpretado por convenção de nome de chave (`registry`, `service`, `InvokeScript`) | Catálogo tipado (`AcaoCatalogo`), `comando_interno` versionado em código C#, nunca vindo de config solta |
| Quem decide valores | Literal fixo no JSON, editado por humano | Faixa segura/permitida/limite absoluto validada em runtime (`ParametroNumerico.Validar`) |
| Quem pode propor uma ação | Usuário marca checkbox — sem intermediário "IA" | Cérebro (LLM ou local) propõe IDs; guard (`LeitorRespostaCerebro`) descarta o que não está no catálogo |
| Consentimento | Implícito na checkbox | Fluxo explícito de 2 checkboxes + auditoria para "risco assumido" |
| Reversão | Valor "original" hardcoded no JSON | `Ler/Escrever/Restaurar` sobre o estado real medido na máquina, por `IEstadoSistema` |
| Execução privilegiada | Mesmo processo da UI | Processo de Agente separado (+ `HardwareOptimizer.WindowsService` para monitoramento) |
| Backup antes de mudar | Opcional, não obrigatório | Bloqueante (`VerificadorPreCondicoes`) — nada prossegue sem `backup_confirmado` |
| Rollback por falha | Inexistente (undo é manual, não automático) | Automático por categoria se `RunnerValidacao` detectar regressão |
| Concorrência/threading | Pool de runspaces PowerShell + `BeginInvoke` | `Task.Run` + `Interlocked.CompareExchange` (mesmo problema, solução nativa .NET) |
| Instalação de programas | `winget`/`choco` via `Start-Process -Wait`, sequencial | Fora do escopo atual do catálogo (`HardwareOptimizer.Features.Drivers` cobre driver, não app de terceiros) |
| Interpretação de features do Windows | `Enable-WindowsOptionalFeature` genérico | Sem equivalente hoje — não há módulo de features opcionais no catálogo |

---

## Recomendações

**Vale considerar adotar:**

1. **Convenção nome-de-chave → binding de UI**: o padrão de gerar controles a
   partir da chave do JSON (evitando UI hand-coded por item) é aplicável a
   telas do Otimize Builder com muitos itens homogêneos (ex.: lista de
   serviços na `SRV_DESATIVAR_SERVICO`, lista de programas em `Drivers`),
   desde que a fonte continue sendo o catálogo tipado, não um JSON solto.
2. **Padrão de limpeza assíncrona sem polling** (`RegisterWaitForSingleObject`
   no WinUtil) — no .NET isso já é resolvido de forma mais simples com
   `Task`/`await`, mas vale garantir que todo `Task.Run` de longa duração no
   Agent tenha um caminho de cancelamento e liberação de recurso simétrico,
   sem sleep-poll (já é a prática atual do projeto, conforme
   `ARQUITETURA.md`).
3. **Suporte a "Enable-WindowsOptionalFeature" como categoria nova de
   `comando_interno`** (Hyper-V, WSL, .NET Framework legado, Sandbox) — é uma
   lacuna real do catálogo atual e o mecanismo do Windows é simples de
   encapsular atrás de `IExecutorProcesso`/uma nova porta, mantendo faixas de
   risco e pré-condições como as demais ações.
4. **Log de exit code por processo externo** (winget/pnputil/etc.) — o padrão
   do WinUtil de logar `ExitCode` mesmo sem abortar é insuficiente sozinho,
   mas confirma que vale sempre capturar e persistir esse dado nos executores
   de processo do Agent (`IExecutorProcesso`), já que decisões de retry/rollback
   podem depender dele.

**Não vale replicar** (o Otimize Builder já resolve melhor):

1. `InvokeScript`/`UndoScript` livre — o catálogo fechado com `comando_interno`
   versionado é estritamente mais seguro; não introduzir um "escape hatch" de
   script arbitrário por config.
2. Download-and-execute sem verificação — manter distribuição via instalador
   assinado / processo de release já usado no projeto.
3. Execução no mesmo processo/privilégio da UI — manter a separação
   Agente/UI via IPC (`HardwareOptimizer.Ipc`) já é superior ao modelo do
   WinUtil.
4. Valor "original" hardcoded para undo — manter o padrão atual de
   `Ler`/`Escrever`/`Restaurar` sobre o estado real medido.

Para aprofundar qualquer um dos pontos acima em código, ver
[ARQUITETURA.md](ARQUITETURA.md), [CATALOGO.md](CATALOGO.md) e
[SEGURANCA.md](SEGURANCA.md).
