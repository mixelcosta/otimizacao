# Guia de Desenvolvimento

Como configurar, entender e estender o sistema.

## Sumário
- [Setup](#setup)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Convenções de código](#convenções-de-código)
- [Logging](#logging)
- [Como estender](#como-estender)
- [Estratégia de testes](#estratégia-de-testes)

---

## Setup

```bash
# Pré-requisitos: .NET 8 SDK + Git
git clone https://github.com/mixelcosta/otimizacao.git
cd otimizacao
dotnet build HardwareOptimizer.sln -c Release
dotnet test  HardwareOptimizer.sln -c Release
```

Configurações compartilhadas estão em `Directory.Build.props` (net8.0, nullable,
analisadores, **warnings tratados como erros**). A UI (`HardwareOptimizer.App`)
desliga warnings-as-errors por causa do código gerado pelo XAML.

---

## Estrutura de pastas

Veja o mapa completo no [README](../README.md) e em
[ARQUITETURA.md](ARQUITETURA.md). Resumo:

```
src/Core      domínio puro (contratos, catálogo, validação, perfis, consentimento, privacidade, BIOS, score)
src/Agent     efeitos colaterais (coletor, sensores, backup, executor, validação, persistência)
src/Cerebro   LLM (matriz, guard, local/LLM, visão)
src/Ipc       protocolo + roteador + named pipe
src/App       UI Avalonia (MVVM)
src/Cli       linha de comando
tests/*       um projeto de teste por camada
```

---

## Convenções de código

| Convenção | Detalhe |
| --- | --- |
| **Idioma** | Domínio modelado em português (tipos, métodos, comentários). |
| **Nullable** | Habilitado; trate nulos explicitamente. |
| **`Resultado` / `Resultado<T>`** | Use para fluxo de validação em vez de exceções de controle. |
| **Imutabilidade** | Contratos são `record` imutáveis; coleções são `IReadOnlyList`/`IReadOnlyDictionary`. |
| **Async** | `Task`/`async`; `ConfigureAwait(false)` em bibliotecas. |
| **Cultura** | Use `CultureInfo.InvariantCulture` em parsing/format numérico. |
| **Sem efeitos colaterais no Core** | E/S e processos só no Agent. |

---

## Logging

Todo componente relevante recebe um `ILogger` opcional (padrão
`NullLogger.Instance`) — bibliotecas não escrevem por conta própria. A CLI conecta
um destino em arquivo (`ArquivoLoggerProvider`) e passa os loggers.

```csharp
public sealed class MeuServico
{
    private readonly ILogger _log;
    public MeuServico(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;
}
```

Formato do log: `timestamp [nível] Classe - mensagem` (a categoria é a classe, o
que aponta o ponto exato). Arquivo: `data/logs/otimizador-AAAAMMDD.log`.

---

## Como estender

### Adicionar uma ação ao catálogo
1. Em `Core/Catalog/CatalogoPadrao.cs`, adicione a `AcaoOtimizacao` (id,
   categoria, parâmetros com faixas, `comando_interno`, reversão, risco,
   pré-condições).
2. Em `Agent/Execution/RegistroComandos.cs` (`Padrao`), registre o
   `comando_interno` correspondente (um `ComandoEstadoSistema`).
3. Garanta a coerência: `faixa_segura ⊆ permitida ⊆ limite_absoluto`.
4. O teste `RegistroComandosTests.Todo_comando_interno_do_catalogo_esta_registrado`
   passa a cobrir o vínculo automaticamente.

### Adicionar um método IPC
1. Em `Ipc/RoteadorIpc.cs`, adicione um `case` no `switch` de `TratarAsync` e o
   handler (chamando o módulo do Agent/Cerebro).
2. Documente em [API_IPC.md](API_IPC.md).
3. Adicione um teste em `IpcTests`.

### Adicionar um leitor de plataforma / sensor
1. Implemente `ILeitorPlataforma` (inventário) ou `ILeitorSensores` (sensores).
2. Selecione-o no orquestrador (`ColetorInventario` / `ServicoSensores`).
3. Para o Linux, prefira ler de `/sys` e `/proc` (injete os caminhos-base para
   testar com arquivos fabricados, como em `LeitorSensoresLinux`).

### Adicionar um caso de visão
1. Acrescente um valor em `CasoUsoVisao` e a pergunta direcionada em
   `ConstrutorPromptVisao`.
2. Trate o campo no `LeitorRespostaVisao`/`ConferenciaVisual` se houver
   cruzamento com o inventário.

---

## Estratégia de testes

- **Lógica pura** (Core, guard, parsers, score, matriz) é testada de forma
  determinística.
- **E/S** é testada com **injeção** (caminhos-base, fakes) — ex.: hwmon
  fabricado, leitor de plataforma falso, cliente LLM falso.
- **Integração** cobre os fluxos: executor + runner (regressão→rollback), IPC
  loopback de named pipe, fluxo ponta a ponta.
- **Regras invariantes** têm rastreabilidade explícita (ver
  [SEGURANCA.md](SEGURANCA.md) e [TESTES.md](TESTES.md)).

```bash
dotnet test HardwareOptimizer.sln -c Release           # tudo
dotnet test tests/HardwareOptimizer.Core.Tests         # só o Core
```
