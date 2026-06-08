# Modelo de Segurança e Privacidade

Como o sistema garante que otimizar é **seguro, reversível e consentido**.

## Sumário
- [Ordem de prioridade](#ordem-de-prioridade)
- [Regras invariantes (rastreabilidade)](#regras-invariantes-rastreabilidade)
- [Parametrização em três níveis](#parametrização-em-três-níveis)
- [Perfis e fluxo de consentimento](#perfis-e-fluxo-de-consentimento)
- [Privacidade e sanitização](#privacidade-e-sanitização)
- [Read-only x requer aprovação](#read-only-x-requer-aprovação)

---

## Ordem de prioridade

**ESTABILIDADE → SEGURANÇA → EFICIÊNCIA → DESEMPENHO.**
Busca-se o maior desempenho **sustentável e validado**, nunca o maior possível.

---

## Regras invariantes (rastreabilidade)

Cada regra é garantida em código e coberta por teste:

| Regra | Onde é garantida | Teste |
| --- | --- | --- |
| O LLM nunca gera comandos; só escolhe IDs do catálogo | `ValidadorAcao`, `LeitorRespostaCerebro`, `RegistroComandos` | `ValidadorAcaoTests`, `GuardRespostaTests` |
| Nenhum valor ultrapassa o `limite_absoluto` (bloqueio rígido) | `ParametroNumerico.Validar` | `ValidadorAcaoTests`, `ConstrutorPerfilTests` |
| Perfil seguro por padrão (usa a `faixa_segura`) | `ConstrutorPerfil.CriarPerfilSeguro` | `ConstrutorPerfilTests` |
| Perfil customizado exige consentimento (aviso + 2 checkboxes + confirmação) | `AvaliadorConsentimento` | `ConsentimentoTests` |
| Sem backup confirmado, nada prossegue | `VerificadorPreCondicoes` | `ExecutorControladoTests` |
| Uma categoria por vez, com rollback por categoria | `ExecutorControlado` | `ExecutorControladoTests` |
| Regressão validada reverte a categoria | `RunnerValidacao` + `ExecutorControlado` | `ValidacaoTests` |
| Inventário sanitizado antes da nuvem | `Sanitizador`, `CerebroLlm` (recusa PII) | `SanitizadorTests`, `CerebroTests` |
| BIOS é sempre manual (o sistema só orienta) | `ModuloBios` (não aplica) | `ModuloBiosTests` |

---

## Parametrização em três níveis

Cada parâmetro numérico de uma ação tem três faixas:

```
              faixa_segura            faixa_permitida           limite_absoluto
   ───────────[==========]──────────[==================]──────────────|──────────▶
              ↑ aceito             ↑ risco assumido     ↑ rejeitado    ↑ bloqueio
                                     (consentimento)                     rígido
```

| Faixa | Significado |
| --- | --- |
| `faixa_segura` | Padrão recomendado. O **perfil seguro** só usa esta faixa. |
| `faixa_permitida` | Mais ampla. O **perfil customizado** pode usar, assumindo o risco. |
| `limite_absoluto` | Teto técnico que **nenhum** perfil ultrapassa (bloqueio rígido). |

Invariante do catálogo (verificada por `CatalogoAcoes.VerificarCoerencia`):
`faixa_segura ⊆ faixa_permitida` e `faixa_permitida.max ≤ limite_absoluto`.

---

## Perfis e fluxo de consentimento

- **Perfil seguro (padrão):** valores sempre na `faixa_segura`. Não exige
  consentimento além da aprovação por categoria.
- **Perfil customizado:** o usuário define os valores.
  - Acima do `limite_absoluto` → **bloqueado** (sem opção de prosseguir).
  - Fora da `faixa_segura` (dentro da permitida) → **"risco assumido"**.
  - Salvar/aplicar dispara o **fluxo de consentimento**:

```
Aviso de responsabilidade
   └─▶ ☐ "Li e aceito os riscos…"            (obrigatório)
   └─▶ ☐ "Desejo prosseguir…"                (obrigatório)
         └─▶ [Confirmar alteração]  ← habilitado SÓ com os dois marcados
               └─▶ Auditoria: data/hora, perfil, valores, versão do catálogo
```

A regra de habilitação ("Go" só com os dois checkboxes) é
`AvaliadorConsentimento.PodeHabilitarConfirmacao`.

---

## Privacidade e sanitização

O inventário é uma "impressão digital" do equipamento. Antes de qualquer envio à
nuvem, o `Sanitizador` produz uma versão **segura para nuvem**:

| Campo sensível | Tratamento |
| --- | --- |
| `numero_serie`, `uuid_placa` | **Hasheados** (SHA-256 salgado, truncado) — correlação sem expor o valor. |
| `nome_maquina`, `nome_usuario`, `chave_produto_windows` | **Removidos**. |
| `endereco_mac` (por interface) | **Hasheado**. |
| modelo de placa, versão de BIOS, etc. | **Preservados** (baixo risco). |

Garantias adicionais:
- **Modo local por padrão:** sem `ANTHROPIC_API_KEY`/`HWOPT_LLM_MODELO`, nada sai
  da máquina (cérebro local).
- **Defesa em profundidade:** `CerebroLlm` **recusa** enviar um inventário que
  ainda contenha PII (nomes, chave de produto).
- **Log do que foi tratado:** o relatório de sanitização lista cada campo
  alterado.

---

## Read-only x requer aprovação

| Operação | Classe | Exige elevação? |
| --- | --- | --- |
| Coletar inventário, ler sensores, BIOS, relatório, proposta | read-only | Não |
| Backup | escrita local | Não |
| Executar ações (CPU, Memória, GPU, SO, Drivers, Serviços, Rede) | modifica o sistema | **Sim** (UAC/root) |
| BIOS (aplicar) | — | **Manual pelo usuário** (o software não aplica) |

Princípio do **menor privilégio**: elevar apenas para aplicar mudanças.
