# Auditoria de Legado — Deep Recon (Brownfield)

> Gerado por Winston (arquiteto) via fluxo BMAD Brownfield de Auditoria e Refatoração Ativa.
> Escopo: `src/**` (12 projetos, ~265 arquivos `.cs`, ~21k linhas). Não cobre regras de negócio — apenas qualidade de código, performance e segurança, respeitando os invariantes de `docs/ARQUITETURA.md`.

## Resumo executivo

| Prioridade | Itens |
| --- | --- |
| 🔴 Alta | 1 |
| 🟡 Média | 4 |
| 🟢 Baixa | 3 |

O achado de maior impacto é **um bug de build já conhecido e documentado** (não descoberto agora): a solução não compila em clone limpo. Os demais são débitos de manutenibilidade (arquivos grandes, tratamento de exceção silencioso, cobertura de teste desigual) sem risco imediato.

---

## 🔴 Alta prioridade

### A1 — `HardwareOptimizer.Features.LifeCounter` não compila em clone limpo (`CS1566`)

- **Onde:** `src/HardwareOptimizer.Features.LifeCounter/HardwareOptimizer.Features.LifeCounter.csproj:19` referencia `EmbeddedResource Include="Data\tbw_database.json"`, mas o arquivo nunca existiu no histórico do git deste repositório.
- **Impacto:** `dotnet build HardwareOptimizer.sln -c Release` falha com `CS1566`. Como `Features.LifeCounter` é dependência transitiva de `Ipc`, `App`, `WindowsService`, `Cli` e de seus projetos de teste, **a solução inteira falha de compilar em qualquer clone novo** (confirmado nesta sessão).
- **Já é um item conhecido:** documentado em `docs/implementation-artifacts/deferred-work.md` (commit `4b129f5`, que corrigiu o mesmo padrão de bug em `Features.Upgrade`/`Features.Drivers`). O texto já registra que `Features.LifeCounter` "tem o mesmo bug" e que precisa de história própria.
- **Por que não corrigi automaticamente:** ao contrário dos catálogos de `Upgrade`/`Drivers` (recuperados de uma cópia local existente), `tbw_database.json` é uma base curada de TBW máximo por modelo de SSD/HDD (Samsung, WD, Seagate, Crucial, Kingston…) — **dado de produto, não código**. Inventar esses valores seria fabricar dados técnicos que o app apresentaria ao usuário como fato; isso está fora do escopo desta auditoria (qualidade de código) e do meu mandato de "não alterar regras de negócio".
- **Recomendação:** abrir uma história dedicada (conforme já sinalizado em `deferred-work.md`) para localizar a fonte de dados original ou curar a base novamente, com revisão humana dos valores antes de versionar o JSON.
- **Ação nesta sessão:** nenhuma alteração de dado. Ver seção "Execução (Etapa 2)" abaixo para o que foi corrigido em vez disso.

---

## 🟡 Média prioridade

### M1 — `catch { }` silencioso em caminhos de coleta/limpeza (`Agent`)

- **Onde:** `Agent/Cleanup/GerenciadorLimpeza.cs` (6 ocorrências), `Agent/Collector/LeitorWindows.cs` (5 ocorrências), `Agent/Sensors/LeitorSensoresWindows.cs` (4 ocorrências).
- **Padrão:** blocos `catch { }` ou `catch { return null; }` sem log, em geral protegendo acesso a arquivo/registro/sensor que pode falhar por permissão ou hardware ausente.
- **Risco:** comportamento "best-effort" pode ser intencional aqui (não é um bug de regra de negócio), mas o silêncio total dificulta diagnosticar por que uma leitura de sensor ou cálculo de espaço em disco veio vazia/zerada em campo.
- **Recomendação:** adicionar log em nível `Debug`/`Trace` nesses `catch` (sem mudar o `return`/fluxo), preservando o comportamento atual e ganhando rastreabilidade.

### M2 — Arquivos com complexidade concentrada (candidatos a divisão)

| Arquivo | Linhas |
| --- | --- |
| `Ipc/RoteadorIpc.cs` | 1134 |
| `App/ViewModels/OtimizadorWindowsViewModel.cs` | 921 |
| `Agent/Collector/LeitorWindows.cs` | 870 |
| `Cli/Program.cs` | 773 |
| `App/ViewModels/DriversViewModel.cs` | 546 |

- `RoteadorIpc.cs` concentra o roteamento de **todos** os métodos IPC (protocolo `App` ↔ `Agent`/`Features.*`) num único arquivo — é o maior ponto único de acoplamento da solução.
- **Recomendação:** não refatorar agora por baixo risco/alto custo (é o contrato entre processos, coberto por testes de integração — tocar aqui sem uma história dedicada é desproporcional ao pedido de auditoria). Registrar como candidato a particionamento por área de feature (`RoteadorIpc.Drivers.cs`, `RoteadorIpc.Upgrade.cs`, via `partial class`) numa história futura, mantendo o mesmo `Metodo` de roteamento.

### M3 — Cobertura de teste desigual em classes com efeito colateral real (I/O, processo, registro)

Classes que executam `Process.Start`/registro/rede e não têm um teste unitário localizável por nome no diretório `tests/` (heurística por nome de classe — pode haver cobertura indireta via interface):

- `Agent/Cleanup/GerenciadorLimpeza.cs`, `Agent/Platform/AcessoRegistroWindows.cs`, `Agent/Services/ColetorServicos.cs`, `Agent/Startup/GerenciadorInicializacao.cs`
- `Features.Licensing/ServicoLicencaLemonSqueezy.cs`, `Features.Licensing/ServicoLicencaLocal.cs`
- `Cerebro/ClienteLlmAnthropic.cs`, `Cerebro/Visao/ClienteVisaoAnthropic.cs`
- `WindowsService/MonitorWorker.cs`, `App/Services/ServicoNotificacaoWindows.cs`, `App/Services/ServicoRelatorio.cs`

- **Observação:** parte disso é esperado (wrappers finos sobre API do SO/HTTP são normalmente testados via fake da interface, não da classe concreta). Vale uma checagem pontual por projeto, não uma exigência geral de 1:1.
- **Recomendação:** priorizar teste de contrato para `ServicoLicencaLemonSqueezy`/`ServicoLicencaLocal` (lógica de licenciamento, maior custo de bug) antes de sensores/coletores read-only.

### M4 — Interpolação de string em `CommandText` SQL (estilo, não vulnerabilidade)

- **Onde:** `Agent/Persistence/RepositorioSqlite.cs:186` — `comando.CommandText = $"SELECT COUNT(*) FROM {tabela};"`.
- **Análise:** `tabela` só recebe literais fixos (`"inventarios"`, `"consentimentos"`, `"execucoes"`) de três chamadas internas — **não há injeção de SQL possível hoje**, e o código já documenta isso em comentário.
- **Recomendação (baixo custo):** trocar a interpolação por um `switch` que mapeia para `CommandText` literal por tabela, eliminando o padrão de "string interpolada em SQL" para não virar precedente copiado para um caso com entrada externa no futuro.

---

## 🟢 Baixa prioridade

### B1 — `async void` fora de handler de evento

- `App/Views/BiosGuideView.axaml.cs:12` — `OnCarregarFotoClick` é `async void`, mas isso é o padrão correto para handlers de evento UI (Avalonia/WPF exigem `void`). Não é debito, apenas registrado para não ser sinalizado erroneamente por uma varredura automatizada futura.

### B2 — Duplicação estrutural Windows/Linux nos coletores

- `LeitorWindows.cs` (870 linhas) / `LeitorLinux.cs` (294 linhas) e `LeitorSensoresWindows.cs` / `LeitorSensoresLinux.cs` implementam a mesma interface com lógica própria por plataforma — duplicação esperada (não há API comum realista entre WMI/registro do Windows e `/sys`/`/proc` do Linux). Sem ação recomendada.

### B3 — Sem `#pragma warning disable` nem `[Obsolete]` no código

- Não foram encontrados supressores de warning nem símbolos obsoletos ativos em `src/`. `TreatWarningsAsErrors=true` + `AnalysisLevel=latest` (via `Directory.Build.props`) já mantêm a régua alta — ponto positivo, não débito.

---

## O que NÃO foi tocado (por decisão, não por descuido)

- **Regras de negócio e protocolo IPC** (`RoteadorIpc`, `ExecutorControlado`, `ValidadorAcao`, fluxo de consentimento/BIOS) — fora do escopo pedido.
- **Dado de produto** (`tbw_database.json`) — ver A1.
- Nenhum arquivo de `tests/` foi modificado além do necessário para validar que o build volta a passar (ver Etapa 2).

## Metodologia

- Inspeção de `.csproj` (dependências, `TargetFramework`, `Nullable`) dos 12 projetos de `src/`.
- `dotnet build HardwareOptimizer.sln -c Release` para warnings/erros reais do compilador (não só grep estático).
- Varredura por padrões: `TODO|FIXME|HACK`, `catch(Exception)`/`catch{}`, `async void`, `dynamic`, `Process.Start`, `CommandText`, segredos hardcoded (`password|secret|apikey`), `pragma warning disable`, `[Obsolete]`.
- Contagem de linhas por arquivo (`wc -l`) para identificar concentração de complexidade.
- Heurística de cobertura: nome de classe presente em algum arquivo de `tests/`.
