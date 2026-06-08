# Política de Segurança

Este projeto **modifica configurações de hardware e do sistema operacional**.
Levamos segurança a sério em duas frentes: a **segurança do código** (este
documento) e a **segurança operacional** das otimizações (o modelo descrito em
[docs/SEGURANCA.md](docs/SEGURANCA.md)).

## Versões suportadas

Por estar em MVP, apenas a versão mais recente recebe correções de segurança.

| Versão | Suportada |
| --- | --- |
| 0.1.x | ✅ |
| < 0.1 | ❌ |

---

## Como reportar uma vulnerabilidade

**Não abra uma issue pública** para vulnerabilidades de segurança.

1. Envie um e-mail para **michelfilipe15@gmail.com** com o assunto
   `[SECURITY] otimizacao`, ou use o
   [Private Vulnerability Reporting](https://docs.github.com/pt/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
   do GitHub, se habilitado.
2. Inclua:
   - descrição e impacto;
   - passos para reproduzir (PoC, se houver);
   - versão/commit afetado e ambiente (SO, .NET);
   - sugestão de correção, se tiver.

**Prazos previstos**
- Confirmação de recebimento: até **3 dias úteis**.
- Avaliação inicial e severidade: até **10 dias úteis**.
- Correção: conforme a severidade; divulgação coordenada após o fix.

Pedimos **divulgação responsável**: dê tempo para corrigir antes de tornar
público. Reconhecemos publicamente quem reportar, se desejar.

---

## Escopo

**No escopo** (queremos saber):
- Bypass de qualquer [regra invariante](docs/SEGURANCA.md#regras-invariantes-rastreabilidade)
  — ex.: aplicar valor acima do `limite_absoluto`, executar ação fora do
  catálogo, pular o backup obrigatório ou o consentimento.
- Falha de **sanitização** que vaze PII (nome, chave de produto) ou
  correlacionáveis não hasheados para a nuvem.
- Injeção via resposta do LLM que escape do **guard** (`LeitorRespostaCerebro`).
- Execução de comando arbitrário, escalonamento de privilégio ou
  path traversal na coleta/persistência.
- Exposição de segredos (`ANTHROPIC_API_KEY`) em logs ou na saída.

**Fora do escopo** (geralmente):
- Necessidade de elevação (UAC/root) para **aplicar** otimizações — é por
  projeto (princípio do menor privilégio).
- O fato de a BIOS ser ajustada manualmente — também é por projeto.
- Riscos que o usuário **assume explicitamente** via consentimento ao usar o
  perfil customizado dentro da `faixa_permitida`.

---

## Boas práticas para quem usa o projeto

- Rode primeiro em **modo simulação** (padrão) antes de aplicar de verdade.
- **Nunca** versione `ANTHROPIC_API_KEY` — use variáveis de ambiente.
- Mantenha **backup** (o sistema exige, mas tenha o seu também).
- Revise o [catálogo](docs/CATALOGO.md) e os limites antes de aprovar ações.

Detalhes do modelo de segurança operacional (parametrização em três níveis,
fluxo de consentimento, privacidade): [docs/SEGURANCA.md](docs/SEGURANCA.md).
