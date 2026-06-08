# Glossário

Termos do domínio do **Agente de Otimização e Confiabilidade de Hardware**.
Cada verbete aponta, quando útil, o componente que o materializa e o documento
de referência.

> Convenção: o código é modelado em português; nomes em inglês do documento de
> arquitetura original aparecem entre parênteses como referência semântica.

---

## Conceitos centrais

**Agente Local**
O serviço .NET deste repositório que **executa** as ações no equipamento. Recebe
pedidos da UI por [IPC](#ipc-inter-process-communication) e só modifica o sistema
com aprovação explícita. Ver [ARQUITETURA.md](ARQUITETURA.md).

**Cérebro** (*brain*)
O plano que **propõe** otimizações — nunca executa. Tem duas implementações por
trás de `ICerebro`: o **cérebro local** (offline, padrão) e o **cérebro LLM**
(nuvem). Ver [ARQUITETURA.md](ARQUITETURA.md) e o README do projeto.

- **Cérebro local** (`CerebroLocal`) — determinístico e offline; o padrão do
  MVP. Nada sai da máquina.
- **Cérebro LLM** (`CerebroLlm`) — usa um modelo de linguagem via o SDK oficial
  da Anthropic; só recebe o [inventário sanitizado](#sanitização).

**Catálogo** (de ações *whitelisted*)
Conjunto **fechado, auditado e parametrizado** de ações de otimização
(`CatalogoPadrao`, versão `2024.06-mvp`). O cérebro só pode **selecionar IDs** e
definir parâmetros dentro das faixas; nada fora do catálogo é executado. Ver
[CATALOGO.md](CATALOGO.md).

**Ação de otimização** (`AcaoOtimizacao`)
Uma entrada do catálogo: `id`, `categoria`, parâmetros com faixas,
[`comando_interno`](#comando-interno), reversão, risco e
[pré-condições](#pré-condição).

**Comando interno** (`comando_interno`)
A implementação **determinística e versionada** que executa uma ação (ex.:
`cmd.gpu.tdr_delay.v1`). **Nunca** vem do LLM; é registrado em
`RegistroComandos` e ligado ao catálogo por teste de consistência.

**Categoria** (`CategoriaAcao`)
Agrupa as ações por área: `Cpu`, `Memoria`, `Gpu`, `SistemaOperacional`,
`Drivers`, `Servicos`, `Rede`. A execução é feita **uma categoria por vez**,
nessa ordem.

**Matriz de decisão** (`MatrizDecisao`)
Saída do cérebro: a proposta **priorizada** de itens (cada um com `acaoId`,
prioridade, risco, justificativa e parâmetros já na faixa segura), mais os
`avisos` do [guard](#guard). Ver [CONTRATOS.md](CONTRATOS.md#matrizdecisao).

**Guard** (da resposta do cérebro)
A barreira (`LeitorRespostaCerebro` / `LeitorRespostaVisao`) que trata a saída do
LLM como **não confiável**: descarta qualquer ação fora do catálogo e força cada
parâmetro à [faixa segura](#faixa_segura). Garante a regra "o LLM só escolhe do
catálogo" mesmo se o modelo alucinar.

---

## Limites e parâmetros

**`faixa_segura`** (*safe range*)
Faixa de valores recomendada de um parâmetro numérico. O
[perfil seguro](#perfil-seguro) só usa esta faixa.

**`faixa_permitida`** (*allowed range*)
Faixa mais ampla, disponível ao [perfil customizado](#perfil-customizado)
assumindo o risco. Invariante: `faixa_segura ⊆ faixa_permitida`.

**`limite_absoluto`** (*absolute limit*)
Teto técnico que **nenhum** perfil ultrapassa — **bloqueio rígido**. Invariante:
`faixa_permitida.max ≤ limite_absoluto`.

```
              faixa_segura            faixa_permitida           limite_absoluto
   ───────────[==========]──────────[==================]──────────────|──────────▶
              ↑ aceito             ↑ risco assumido     ↑ rejeitado    ↑ bloqueio
                                     (consentimento)                     rígido
```

**Risco assumido** (`RiscoAssumido`)
Situação de um valor que está **fora da faixa segura, mas dentro da permitida**.
É aceito apenas no perfil customizado e **dispara o consentimento**. Ver
[SEGURANCA.md](SEGURANCA.md).

**Bloqueio rígido** (`BloqueioLimiteAbsoluto`)
Recusa incondicional de um valor acima do `limite_absoluto`. Não há opção de
prosseguir, em nenhum perfil.

**Situação do parâmetro** (`SituacaoParametro`)
O veredito do validador para um valor proposto: `Aceito`, `RiscoAssumido`,
`Rejeitado` ou `BloqueioLimiteAbsoluto`. Ver
[CATALOGO.md](CATALOGO.md#como-o-validador-decide).

---

## Perfis e consentimento

**Perfil** (`Perfil`)
Conjunto de ações + valores escolhidos para aplicar. Construído por
`ConstrutorPerfil`.

**Perfil seguro** (*safe profile*)
Perfil padrão: valores sempre na `faixa_segura`. Não exige consentimento além da
aprovação por categoria.

**Perfil customizado** (*custom profile*)
Perfil em que o usuário define valores; pode entrar em
[risco assumido](#risco-assumido) e então exige o
[consentimento](#consentimento).

**Consentimento**
O fluxo obrigatório para aplicar algo fora da faixa segura: **aviso de
responsabilidade + 2 checkboxes obrigatórios + confirmação**, com auditoria.
A regra "confirmar só com os dois marcados" é
`AvaliadorConsentimento.PodeHabilitarConfirmacao`. Ver
[SEGURANCA.md](SEGURANCA.md#perfis-e-fluxo-de-consentimento).

**Aprovação por ação**
Na UI/IPC, o usuário aprova **IDs específicos**; o agente monta o perfil seguro
só com eles e executa. Ver [API_IPC.md](API_IPC.md#aprovar).

**Pré-condição** (`pre_condicoes`)
Checagem obrigatória antes de executar uma ação (ex.: `backup_confirmado`,
`servico_consta_na_lista_segura`). Revalidada na execução por
`VerificadorPreCondicoes` (defesa em profundidade).

---

## Execução, validação e segurança

**Backup bloqueante**
O backup obrigatório (`ServicoBackup`): **sem backup confirmado, nada
prossegue**. Inclui verificação de integridade.

**Executor controlado** (`ExecutorControlado`)
Aplica o perfil **uma categoria por vez**, com [rollback](#rollback--reversão)
automático por categoria em caso de falha ou [regressão](#regressão).

**Rollback / reversão**
Desfazer as alterações de uma categoria, voltando ao estado anterior (cada ação
tem sua `reversao`). Disparado por falha ou regressão.

**Regressão**
Piora medida após aplicar uma categoria — queda de score, temperatura alta,
erros [WHEA](#whea), [TDR](#tdr-timeout-detection-and-recov), artefatos,
[BSOD](#bsod-blue-screen-of-death). Detectada pelo
[runner de estresse](#runner-de-estresse) e leva ao rollback.

**Runner de estresse / validação** (`RunnerValidacao`)
Roda uma ferramenta de estresse, faz o **parser** da saída (`ParserEstresse`) e
a **análise de regressão** (`AnalisadorRegressao`) comparando antes/depois. Ver
[TESTES.md](TESTES.md).

**Dry-run / simulação** (`EstadoSistemaSimulado`)
Modo **padrão**: os comandos operam sobre um `IEstadoSistema` abstrato que
reproduz ler/escrever/restaurar **sem tocar** o sistema real — torna executor e
rollback totalmente testáveis.

**Sanitização**
O pipeline (`Sanitizador`) que produz uma versão do
[inventário](#inventário) **segura para a nuvem**: serial/uuid/MAC **hasheados**;
nome de máquina/usuário e chave de produto **removidos**. Ver
[SEGURANCA.md](SEGURANCA.md#privacidade-e-sanitização).

**PII** (*Personally Identifiable Information*)
Dados pessoais identificáveis (nome de usuário/máquina, chave de produto). São
**removidos** na sanitização; `CerebroLlm` recusa enviar inventário que ainda
contenha PII.

**Auditoria**
Registro persistido (SQLite) de consentimentos e execuções: data/hora, perfil,
valores e versão do catálogo. Ver [ARQUITETURA.md](ARQUITETURA.md).

**Elevação / UAC / root**
Privilégio administrativo. Exigido **apenas para aplicar** mudanças; diagnóstico
é read-only (princípio do menor privilégio).

---

## Coleta, sensores e relatório

**Inventário** (`Inventario`)
A "impressão digital" do equipamento: placa, CPU, memória, GPU, SO, rede e
identificadores. Coletado em modo **read-only** por `ColetorInventario`. Ver
[CONTRATOS.md](CONTRATOS.md#inventario).

**Sensores** (`LeituraSensores`)
Leitura em tempo real (temperatura, clock, voltagem, fan, consumo). No Linux vem
de `/sys/class/hwmon`; no Windows, de WMI. Ver
[CONTRATOS.md](CONTRATOS.md#leiturasensores).

**hwmon**
Subsistema do Linux (`/sys/class/hwmon`) que expõe sensores de hardware. Fonte
do `LeitorSensoresLinux`.

**WMI / CIM** (*Windows Management Instrumentation*)
API do Windows para consultar hardware/SO. Fonte do `LeitorWindows` e do
`LeitorSensoresWindows`.

**Score / nota** (`CalculadoraScore`)
Nota **0-100 por domínio** (Hardware, Bios, Cpu, Gpu, Ram, Windows,
Estabilidade) e nota final ponderada do [relatório](#relatório-executivo).

**Relatório executivo** (`RelatorioExecutivo`)
Resumo legível com nota final, classificação, scores por domínio, alterações e
destaques. Ver [CONTRATOS.md](CONTRATOS.md#relatorioexecutivo).

---

## BIOS e visão

**BIOS / UEFI / Legacy**
Firmware da placa-mãe. **UEFI** e **Legacy** são modos de boot; o inventário
registra `modo` e `secureBoot`. O sistema **nunca aplica** mudanças de BIOS — só
identifica, verifica com o fabricante e **orienta** (`ModuloBios`).

**Secure Boot**
Recurso de boot seguro do UEFI. Reportado no inventário (`secureBoot`).

**Banco curado de BIOS** (`BancoCuradoBios`)
Base confiável usada na verificação de versão, complementada por cache SQLite
(`ProvedorBiosComCache`). A **fonte** é sempre visível (ponto de atenção do
documento).

**Visão** (*fluxo_visao*)
Módulo multimodal (`Cerebro/Visao`) que interpreta **fotos** — tela de
BIOS/UEFI, etiqueta da placa, mensagem de erro, benchmark — devolvendo
[leitura visual](#leitura-visual) estruturada + [confiança](#confiança) +
próximo passo.

**Leitura visual** (`LeituraVisual`)
Saída da visão: `tipoTela`, `campos` lidos, `confianca` e `proximoPasso`. Ver
[CONTRATOS.md](CONTRATOS.md#leituravisual).

**Conferência visual** (`ConferenciaVisual`)
Cruza a leitura visual com o inventário coletado (ex.: versão de BIOS lida ×
coletada); se a [confiança](#confiança) for baixa, **pede nova foto**.

**Confiança**
Nível de certeza da leitura visual: `alta`, `media`, `baixa`. Confiança baixa
nunca é aceita cegamente.

---

## Plataforma e infraestrutura

**IPC** (*Inter-Process Communication*)
A fronteira entre a UI e o agente. Transporte por **named pipe**, uma requisição
JSON por linha. Ver [API_IPC.md](API_IPC.md).

**Named pipe**
Mecanismo de IPC cross-platform (modo Byte para funcionar em Linux/macOS) usado
por `ServidorNamedPipe`/`ClienteNamedPipe`.

**`Resultado` / `Resultado<T>`**
Tipo de resultado (sucesso/erro com mensagem) usado no fluxo de validação em vez
de exceções de controle. Ver [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md).

**MVVM** (*Model-View-ViewModel*)
Padrão da UI Avalonia (`HardwareOptimizer.App`): a View (XAML) liga-se a
ViewModels que consomem o `IRoteadorIpc`.

**Self-contained / single-file**
Publicação que embute o runtime .NET — o binário roda **sem .NET instalado**.
Ver [INSTALACAO.md](INSTALACAO.md).

---

## Siglas de estabilidade

**WHEA** (*Windows Hardware Error Architecture*)
Subsistema de erros de hardware do Windows. Eventos WHEA durante a validação
indicam instabilidade → **regressão** → rollback.

**TDR** (*Timeout Detection and Recovery*)
Mecanismo do Windows que reinicia o driver de vídeo travado. O parâmetro
`GPU_TDR_DELAY` (`TdrDelay`) ajusta o tempo de espera; valores altos **mascaram**
instabilidade real.

**BSOD** (*Blue Screen of Death*)
Tela azul — falha crítica do Windows. Detectada na validação como erro grave que
força reversão.

**HAGS** (*Hardware-Accelerated GPU Scheduling*)
Agendamento de GPU por hardware (ação `GPU_HAGS`).

---

Sente falta de um termo? Veja o índice em [README.md](README.md) ou abra uma
issue (ver [../CONTRIBUTING.md](../CONTRIBUTING.md)).
