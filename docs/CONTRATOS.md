# Contratos de Dados

Referência dos objetos de dados trocados entre os planos e serializados em JSON.

## Convenção de serialização
- **camelCase** nas propriedades; **enums como string** (camelCase).
- Campos nulos são omitidos na saída.
- JSON Schemas (draft 2020-12) em [`../schemas/`](../schemas/):
  `inventario.schema.json`, `recomendacao.schema.json`,
  `resultado_validacao.schema.json`.

## Sumário
- [Inventario](#inventario)
- [LeituraSensores](#leiturasensores)
- [Recomendacao](#recomendacao)
- [MatrizDecisao](#matrizdecisao)
- [ResultadoValidacao](#resultadovalidacao)
- [RelatorioExecutivo](#relatorioexecutivo)
- [LeituraVisual](#leituravisual)

---

## Inventario

| Campo | Tipo | Observação |
| --- | --- | --- |
| `placa` | objeto | `fabricante`, `modelo`, `versaoBios?`, `dataBios?`, `modo?` (UEFI/Legacy), `secureBoot?` |
| `cpu` | objeto | `nome`, `nucleos?`, `threads?`, `tempIdleC?` |
| `memoria` | array | itens: `tamanhoGb?`, `velocidadeMhz?`, `fabricante?` |
| `gpu` | array | itens: `nome`, `tempIdleC?`, `versaoDriver?` |
| `sistemaOperacional` | objeto | `tipo` (windows/linux/desconhecido), `nome?`, `versao?`, `arquitetura?` |
| `rede` | array | itens: `nome`, `tipo?`, `enderecoMac?` (**sensível**) |
| `identificadores` | objeto? | `numeroSerie?`, `uuidPlaca?`, `nomeMaquina?`, `nomeUsuario?`, `chaveProdutoWindows?` (**sensíveis**) |
| `coletadoEm` | data/hora | ISO 8601 |

> Após a sanitização, `identificadores` traz os correlacionáveis **hasheados** e
> a PII **nula** (ver [SEGURANCA.md](SEGURANCA.md)).

```json
{
  "placa": { "fabricante": "ASUS", "modelo": "ROG STRIX B550-F", "versaoBios": "2806", "modo": "UEFI", "secureBoot": true },
  "cpu": { "nome": "Ryzen 5 5600X", "nucleos": 6, "threads": 12, "tempIdleC": 38 },
  "memoria": [ { "tamanhoGb": 16, "velocidadeMhz": 3200 } ],
  "gpu": [ { "nome": "RTX 3060", "tempIdleC": 41 } ],
  "sistemaOperacional": { "tipo": "windows", "nome": "Windows 11", "arquitetura": "X64" }
}
```

---

## LeituraSensores

| Campo | Tipo | Observação |
| --- | --- | --- |
| `momento` | data/hora | quando foi lido |
| `sensores` | array | itens: `nome`, `tipo` (temperatura/clock/voltagem/fan/potencia/outro), `valor`, `unidade` |

```json
{ "momento": "2026-06-08T12:00:00Z",
  "sensores": [ { "nome": "Core 0", "tipo": "temperatura", "valor": 45, "unidade": "°C" } ] }
```

---

## Recomendacao

Proposta unitária do cérebro (o contrato "recomendacao" do documento).

| Campo | Tipo | Observação |
| --- | --- | --- |
| `categoria` | string | |
| `acaoId` | string? | ID de uma ação do catálogo (obrigatório se executável) |
| `valorAtual` / `valorRecomendado` | string? | |
| `acao` | string | nome legível |
| `justificativa` | string | |
| `risco` | enum | nenhum/muitoBaixo/baixo/medio/alto |
| `ganhoEsperado` | string? | |
| `fonte` | string? | sempre visível (obrigatória para BIOS) |
| `passosUsuario` | array<string> | |

---

## MatrizDecisao

Saída do cérebro (proposta priorizada).

| Campo | Tipo | Observação |
| --- | --- | --- |
| `origem` | enum | `local` ou `nuvem` |
| `modelo` | string? | modelo usado (quando nuvem) |
| `itens` | array | itens de decisão |
| `avisos` | array<string> | avisos do guard (itens descartados, parâmetros corrigidos) |

`itens[]`: `acaoId`, `prioridade` (1 = maior), `categoria`, `risco`,
`ganhoEsperado?`, `justificativa`, `parametros` (mapa nome→valor, já na faixa
segura).

---

## ResultadoValidacao

| Campo | Tipo | Observação |
| --- | --- | --- |
| `categoria` | string | |
| `ferramenta` | string | ex.: OCCT |
| `antes` / `depois` | objeto? | `score?`, `tempMaxC?`, `clockMhz?`, `consumoW?` |
| `regressao` | bool | |
| `erros` | array<string> | ex.: `WHEA: 0`, `Tela azul (BSOD)` |
| `estabilidade` | string | `Totalmente validado` / `Reprovado` |

---

## RelatorioExecutivo

| Campo | Tipo | Observação |
| --- | --- | --- |
| `geradoEm` | data/hora | |
| `resumoExecutivo` | string | |
| `notaFinal` | int (0-100) | média ponderada |
| `classificacao` | string | Excelente/Bom/Regular/Requer atenção |
| `scores` | array | `dominio`, `valor` (0-100), `classificacao`, `criterios[]` |
| `alteracoes` | array | `alvo`, `antes?`, `depois?` |
| `destaques` | array<string> | |
| `regressaoDetectada` | bool | |

Domínios: `Hardware`, `Bios`, `Cpu`, `Gpu`, `Ram`, `Windows`, `Estabilidade`.

---

## LeituraVisual

Saída do módulo de visão.

| Campo | Tipo | Observação |
| --- | --- | --- |
| `tipoTela` | enum | biosUefi/etiquetaPlaca/mensagemErro/benchmark/desconhecida |
| `campos` | mapa | nome→valor lido (ex.: `versao`, `fabricante`) |
| `confianca` | enum | alta/media/baixa |
| `proximoPasso` | string? | |

A leitura é cruzada com o inventário (`ConferenciaVisual`); confiança baixa pede
nova foto.
