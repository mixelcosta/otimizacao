# Manual de Orientações — Agente de Otimização de Hardware

Guia passo a passo de uso. Para **instalar**, veja o
[Guia de Instalação](INSTALACAO.md).

Nos exemplos abaixo, `hwopt` representa o executável:
- Binário pronto: `./HardwareOptimizer.Cli` (Linux/macOS) ou
  `HardwareOptimizer.Cli.exe` (Windows).
- Do código-fonte: `dotnet run --project src/HardwareOptimizer.Cli --`.

---

## 1. Antes de tudo: a filosofia de segurança

O sistema segue uma ordem **inegociável** de prioridades:

> **ESTABILIDADE → SEGURANÇA → EFICIÊNCIA → DESEMPENHO**

E um conjunto de regras invariantes que valem para qualquer operação:

1. **Nunca** otimiza sem diagnóstico baseado em evidências do equipamento.
2. **Sem backup, nada prossegue.**
3. Aplica **uma categoria por vez**, validando depois de cada uma.
4. O cérebro (LLM) **propõe**; o agente só executa **com sua aprovação**.
5. A **BIOS é sempre manual**: o sistema identifica, verifica e orienta — não aplica.
6. Busca o maior desempenho **sustentável e validado**, não o maior possível.
7. O LLM **só escolhe ações de um catálogo fechado**; nunca inventa comandos.
8. O inventário é **sanitizado** antes de ir à nuvem.
9. O **perfil seguro** é o padrão; o perfil customizado exige consentimento explícito.

---

## 2. Fluxo recomendado (passo a passo)

A maneira segura de usar a ferramenta, do diagnóstico à validação:

### Passo 1 — Diagnosticar
```bash
hwopt coletar      # inventário (placa, CPU, memória, GPU, SO, rede)
hwopt sensores     # temperatura, clock, voltagem, fan, consumo
hwopt relatorio    # nota 0-100 por domínio + resumo executivo
```

### Passo 2 — Ver o que pode ser feito
```bash
hwopt catalogo     # ações disponíveis e seus limites
hwopt proposta     # o cérebro propõe a matriz de decisão (perfil seguro)
```

### Passo 3 — Aplicar com segurança (requer elevação)
O caminho completo está no comando `demo` (em **modo simulação**, sem tocar o
sistema real):
```bash
hwopt demo
```
Ele exercita: coleta → sanitização → proposta → **perfil seguro** → **backup
obrigatório** → execução por categoria → **bloqueio rígido** de valores acima do
limite → **risco assumido** com consentimento → **validação que reverte
regressão** → relatório.

> Na execução real (fora do `demo`), cada categoria só é aplicada após o backup
> confirmado, e é **revertida automaticamente** se a validação detectar
> regressão (WHEA, artefatos, tela azul, superaquecimento, etc.).

### Passo 4 — Conferir
```bash
hwopt relatorio    # compare a nota antes/depois
```

---

## 3. Referência de comandos

| Comando      | O que faz                                                                 |
| ------------ | ------------------------------------------------------------------------- |
| `ajuda`      | Lista os comandos.                                                        |
| `coletar`    | Inventário (read-only) em JSON.                                           |
| `sanitizar`  | Mostra a versão "segura para nuvem" do inventário + o que foi tratado.    |
| `catalogo`   | Lista o catálogo de ações e seus limites (faixa segura/permitida/absoluto). |
| `sensores`   | Lê os sensores em tempo real.                                             |
| `relatorio`  | Relatório executivo + nota 0-100 por domínio.                            |
| `proposta`   | Cérebro propõe a matriz de decisão a partir do inventário sanitizado.     |
| `bios`       | Identifica a BIOS, verifica com o fabricante e gera o guia (**não aplica**). |
| `visao <img>`| Interpreta uma foto e cruza com o inventário (exige LLM configurado).     |
| `demo`       | Fluxo completo ponta a ponta (modo simulação seguro).                    |
| `servir`     | Hospeda o servidor IPC (named pipe) para a UI.                           |
| `ipc-demo`   | Demonstra o IPC (servidor + cliente no mesmo processo).                   |

---

## 4. Perfis e consentimento

- **Perfil seguro (padrão):** usa sempre a *faixa segura* de cada ação. Não
  exige consentimento além da aprovação normal por categoria.
- **Perfil customizado:** você define os valores dentro da *faixa permitida*.
  - Valores **acima do limite absoluto** são **bloqueados** (sem opção de prosseguir).
  - Valores fora da faixa segura são marcados como **"risco assumido por você"**.
  - Salvar/aplicar exige o **fluxo de consentimento**:
    1. Ler o aviso de responsabilidade;
    2. Marcar **os dois checkboxes** obrigatórios;
    3. Confirmar — só então o botão **"Confirmar alteração"** é habilitado.
  - Tudo é registrado em **auditoria** (data, perfil, valores, versão do catálogo).

### Entendendo os limites de cada parâmetro
```
              faixa_segura            faixa_permitida           limite_absoluto
   ───────────[==========]──────────[==================]──────────────|──────────▶
              ↑ aceito             ↑ risco assumido     ↑ rejeitado    ↑ bloqueio
                                     (consentimento)                     rígido
```

---

## 5. Módulo BIOS

```bash
hwopt bios
```
O sistema:
1. Identifica fabricante, modelo, versão e modo (UEFI/Legacy);
2. Compara com a versão do fabricante (banco curado + cache);
3. Decide de forma **conservadora** (só recomenda atualizar com ganho real);
4. Gera um **guia passo a passo** específico do fabricante (EZ Flash, Q-Flash,
   M-Flash, Instant Flash), com avisos (não desligar, use nobreak, risco de
   brick) e ajustes recomendados (XMP/EXPO, Resizable BAR).

> O software **não atualiza a BIOS**. A atualização é manual, por sua conta, e o
> risco (incl. perda de garantia) é seu. A fonte é sempre exibida.

---

## 6. Módulo de Visão (fotos)

Exige LLM configurado (`ANTHROPIC_API_KEY` + `HWOPT_LLM_MODELO`).

```bash
hwopt visao foto-da-bios.png bios        # lê a versão da BIOS na tela
hwopt visao etiqueta.jpg etiqueta        # lê fabricante/modelo da placa
hwopt visao erro.png erro                # lê código de erro / tela azul
hwopt visao occt.png benchmark           # lê temperatura/score no benchmark
```
A leitura vem com **nível de confiança** e é **cruzada com o inventário**. Se a
confiança for baixa, o sistema **pede uma nova foto** — nunca confia cegamente.

---

## 7. Relatório e score

```bash
hwopt relatorio
```
Gera notas de 0 a 100 por domínio (**Hardware, BIOS, CPU, GPU, RAM, Windows,
Estabilidade**) e uma **nota final ponderada** (Estabilidade pesa mais). Cada
nota tem critérios explicáveis.

---

## 8. Interface gráfica (passo a passo)

```bash
dotnet run --project src/HardwareOptimizer.App
```
1. **Coletar inventário** — preenche o resumo do equipamento.
2. **Ler sensores** — lista temperatura/clock/voltagem/fan/consumo.
3. **Propor ações** — preenche a matriz de decisão; as ações de risco muito
   baixo já vêm **marcadas**.
4. **Marque** as ações desejadas e clique em **Aprovar selecionadas** — o agente
   monta o perfil seguro, faz backup e executa por categoria com validação.

---

## 9. IPC (para integradores)

A UI conversa com o agente por **named pipe** (JSON, uma requisição por linha):

```bash
hwopt servir           # hospeda o agente no pipe "hwopt-agente"
hwopt servir meu-pipe  # nome de pipe customizado
```
Métodos disponíveis: `ping`, `coletar`, `sensores`, `catalogo`, `proposta`,
`relatorio` e `aprovar` (este recebe `{"acoes":["ID1","ID2"]}` — os IDs que a UI
aprovou). Veja `ipc-demo` para um exemplo cliente+servidor.

---

## 10. Avisos importantes

- **Overclock real é BIOS** e, por design, **não** entra no executor — é manual.
- Mudanças fora da faixa segura podem afetar **estabilidade e garantia**.
- Mantenha o **backup** gerado pelo sistema antes de aplicar e **valide com
  testes de estresse** depois.
- Em caso de instabilidade após aplicar, a validação reverte a categoria; se
  precisar, restaure a partir do backup em `data/backups/`.
