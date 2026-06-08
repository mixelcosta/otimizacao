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

---

## Métodos

| Método | Parâmetros | Resultado |
| --- | --- | --- |
| `ping` | — | `"pong"` |
| `coletar` | — | [Inventario](CONTRATOS.md#inventario) |
| `sensores` | — | [LeituraSensores](CONTRATOS.md#leiturasensores) |
| `catalogo` | — | lista de ações (resumo) |
| `proposta` | — | [MatrizDecisao](CONTRATOS.md#matrizdecisao) (cérebro, a partir do inventário sanitizado) |
| `relatorio` | — | [RelatorioExecutivo](CONTRATOS.md#relatorioexecutivo) |
| `aprovar` | `{ "acoes": ["ID1","ID2"], "nomePerfil?": "..." }` | RelatorioExecucao da execução |

Método desconhecido retorna `sucesso: false`.

### `catalogo` — item de resultado
`id`, `categoria`, `titulo`, `risco`, `requerReinicio`, `preCondicoes[]`,
`parametros[]` (`nome`, `tipo` numerico/lista_branca, `detalhe`).

### `aprovar` — fluxo de aprovação explícita por ação
A UI envia **apenas os IDs aprovados**. O agente:
1. monta o **perfil seguro** com esses IDs (recusa se algum for inválido);
2. faz **backup obrigatório**;
3. **executa por categoria** com validação e rollback automático.

O resultado é o relatório de execução (`sucesso`, `categorias[]` com `situacao`
Aplicada/Revertida/Bloqueada e as alterações antes/depois).

---

## Exemplos

```jsonc
// → requisição
{ "metodo": "ping" }
// ← resposta
{ "id": "…", "sucesso": true, "resultado": "pong" }
```

```jsonc
// → requisição (aprovação)
{ "metodo": "aprovar", "parametros": { "acoes": ["PWR_PLANO_ALTO_DESEMPENHO", "SO_EFEITOS_VISUAIS_DESEMPENHO"] } }
// ← resposta
{ "id": "…", "sucesso": true, "resultado": { "sucesso": true, "perfilNome": "perfil-ipc", "categorias": [ /* … */ ] } }
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
