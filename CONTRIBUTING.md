# Como Contribuir

Obrigado por contribuir com o **Agente de Otimização e Confiabilidade de
Hardware**. Este guia padroniza o fluxo de trabalho; o detalhamento técnico está
em [docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md).

## Princípio inegociável

Toda contribuição respeita a ordem de prioridade do projeto:
**ESTABILIDADE → SEGURANÇA → EFICIÊNCIA → DESEMPENHO.**
Mudanças que enfraqueçam uma [regra invariante](docs/SEGURANCA.md#regras-invariantes-rastreabilidade)
não são aceitas sem uma alternativa que preserve a mesma garantia (e o teste que
a cobre).

---

## Pré-requisitos

- **.NET 8 SDK** e **Git**.
- Leia [docs/ARQUITETURA.md](docs/ARQUITETURA.md) (planos e dependências) e
  [docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md) (convenções).

```bash
git clone https://github.com/mixelcosta/otimizacao.git
cd otimizacao
dotnet build HardwareOptimizer.sln -c Release
dotnet test  HardwareOptimizer.sln -c Release
```

---

## Fluxo de trabalho

1. **Crie uma branch** a partir da branch de desenvolvimento, com nome
   descritivo:
   - `feat/<resumo>` — nova funcionalidade
   - `fix/<resumo>` — correção de bug
   - `docs/<resumo>` — documentação
   - `test/<resumo>` — apenas testes
   - `chore/<resumo>` — build, CI, dependências
2. **Faça commits pequenos e descritivos** (ver abaixo).
3. **Garanta a build limpa e os testes verdes** (`dotnet test`).
4. **Abra um Pull Request** preenchendo o
   [template](.github/PULL_REQUEST_TEMPLATE.md).

---

## Convenção de commits

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<tipo>(<escopo opcional>): <resumo no imperativo>

<corpo opcional explicando o porquê>
```

Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `perf`, `ci`.

Exemplos:
```
feat(catalogo): adiciona ação NET_RSS_HABILITAR
fix(sanitizador): preserva serial hasheado em vez de anular
docs(glossario): inclui verbetes de TDR e WHEA
```

O resumo no **imperativo** ("adiciona", não "adicionado") e ≤ 72 caracteres.

---

## Convenções de código (resumo)

Detalhe completo em [docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md#convenções-de-código).

| Regra | Detalhe |
| --- | --- |
| **Idioma** | Domínio modelado em português (tipos, métodos, comentários). |
| **Nullable** | Habilitado; trate nulos explicitamente. |
| **`Resultado<T>`** | Para fluxo de validação, em vez de exceções de controle. |
| **Imutabilidade** | Contratos são `record`; coleções são `IReadOnly*`. |
| **Cultura** | `CultureInfo.InvariantCulture` em parsing/format numérico. |
| **Sem efeitos colaterais no Core** | E/S e processos só no `Agent`. |
| **Warnings = erros** | A build falha com warnings (exceto a UI Avalonia/XAML). |

---

## Definição de pronto (Definition of Done)

Antes de pedir revisão, confirme:

- [ ] `dotnet build -c Release` **sem warnings** (são tratados como erros).
- [ ] `dotnet test -c Release` **verde** (toda a suíte).
- [ ] **Novo comportamento tem teste** correspondente.
- [ ] Se mexeu no **catálogo**, o vínculo catálogo↔comando segue coberto
      (`RegistroComandosTests`) e os limites são coerentes
      (`faixa_segura ⊆ permitida ⊆ limite_absoluto`).
- [ ] Se mexeu numa **regra invariante**, a
      [rastreabilidade](docs/SEGURANCA.md#regras-invariantes-rastreabilidade)
      continua válida.
- [ ] **Documentação atualizada** (contratos, API IPC, catálogo, manual) quando
      o comportamento visível mudou.
- [ ] **Nenhum dado sensível** vaza em logs, testes ou exemplos.

---

## Como estender o sistema

Receitas passo a passo (nova ação do catálogo, novo método IPC, novo leitor de
plataforma/sensor, novo caso de visão) estão em
[docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md#como-estender).

---

## Reportando bugs e propondo melhorias

- **Bug:** abra uma issue com o template de
  [bug](.github/ISSUE_TEMPLATE/bug_report.md).
- **Ideia/feature:** use o template de
  [funcionalidade](.github/ISSUE_TEMPLATE/feature_request.md).
- **Vulnerabilidade de segurança:** **não** abra issue pública — siga a
  [SECURITY.md](SECURITY.md).

---

## Código de conduta

Seja respeitoso e objetivo. Revisões focam no código e nas garantias do sistema,
nunca na pessoa. Discordâncias técnicas se resolvem com evidência (teste,
medição, referência ao documento de arquitetura).
