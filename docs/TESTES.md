# Guia de Testes

Como rodar, o que é coberto e a rastreabilidade das regras.

## Executar

```bash
dotnet test HardwareOptimizer.sln -c Release            # toda a suíte
dotnet test tests/HardwareOptimizer.Core.Tests          # um projeto
dotnet test --filter "FullyQualifiedName~Validacao"     # por nome
```

A suíte roda no CI (`.github/workflows/ci.yml`) a cada push/PR.

## Projetos de teste

| Projeto | Cobre | Testes |
| --- | --- | --- |
| `HardwareOptimizer.Core.Tests` | Catálogo, validador, perfis, consentimento, sanitização, BIOS, versão, score, `Resultado`, `FaixaNumerica`. | 87 |
| `HardwareOptimizer.Agent.Tests` | Executor + rollback, comandos, pré-condições, coletor, backup, persistência, sensores, validação/regressão, BIOS+cache, startup scanner, HWID, S.M.A.R.T., serviços Windows. | 151 |
| `HardwareOptimizer.Cerebro.Tests` | Guard da resposta, matriz, cérebro local/LLM, privacidade, visão (parser, módulo, conferência). | 26 |
| `HardwareOptimizer.Features.Licensing.Tests` | `IServicoLicenca`, `ServicoLicencaLocal`, gating de funcionalidades, `ValidadorChaveLicenca` (HMAC offline). | 20 |
| `HardwareOptimizer.Features.Upgrade.Tests` | `ValidadorCompatibilidade`, `CalculadoraGargalo`, `AgenteUpgrade`. | 20 |
| `HardwareOptimizer.Features.LifeCounter.Tests` | `CalculadoraVidaUtil`, TBW estimado, S.M.A.R.T. | 8 |
| `HardwareOptimizer.Features.Drivers.Tests` | `AtualizadorDrivers`, `ColetorHwid`, repositório WHQL. | 17 |
| `HardwareOptimizer.Ipc.Tests` | Roteador (todos os métodos) + loopback real de named pipe. | 24 |
| `HardwareOptimizer.App.Tests` | ViewModels da UI (com roteador falso). | 83 |
| `HardwareOptimizer.WindowsService.Tests` | `DetectorAnomalias`: RAM/CPU spike, janela de histórico, `ExtrairValor`. | 10 |

**Total: 446 testes** (0 falhas).

## Rastreabilidade — regras invariantes ↔ testes

| Regra | Teste |
| --- | --- |
| Limite absoluto bloqueia (rígido) | `ValidadorAcaoTests`, `ConstrutorPerfilTests` |
| Perfil seguro usa só a faixa segura | `ConstrutorPerfilTests` |
| Customizado exige consentimento (2 checkboxes) | `ConsentimentoTests` |
| Sem backup, nada prossegue | `ExecutorControladoTests` |
| Uma categoria por vez + rollback | `ExecutorControladoTests` |
| Regressão reverte automaticamente | `ValidacaoTests` |
| LLM só escolhe do catálogo (guard) | `GuardRespostaTests`, `CerebroTests` |
| Sanitização não vaza segredo | `SanitizadorTests`, `FluxoCompletoTests` |
| Catálogo ↔ comandos consistentes | `RegistroComandosTests` |
| Confiança baixa pede nova foto (visão) | `VisaoTests` |
| Licença Gratuita nega funcionalidades Premium | `IpcTests.ObterStatusLicenca_*` |
| Driver inexistente/url vazia retorna falha | `IpcTests.InstalarDriver_*` |
| Foto sem base64 retorna falha | `IpcTests.AnalisarBiosFoto_*` |
| Serviços críticos do SO são bloqueados | `IpcTests` (via `ListaNegraServicos`) |
| Chave de licença inválida é rejeitada | `LicencaGateTests.ValidadorChave_*` |
| Chave de outra máquina não ativa Premium | `LicencaGateTests.ValidadorChave_chave_de_outra_maquina_e_invalida` |
| CPU spike detectado após janela mínima | `DetectorAnomaliasTests.Cpu_spike_completo_*` |
| RAM alta gera alerta imediato | `DetectorAnomaliasTests.Ram_acima_do_limiar_*` |

## Tipos de teste

- **Unitário determinístico** — lógica pura (parsers, validador, score, matriz).
- **Com injeção** — E/S testada com fakes/caminhos-base (hwmon fabricado, leitor
  de plataforma falso, cliente LLM falso, coletor falso, licença falsa).
- **Integração** — executor + runner (regressão→rollback), fluxo ponta a ponta
  (`FluxoCompletoTests`), IPC loopback de named pipe.

> Ao adicionar funcionalidade, adicione o teste correspondente e mantenha a
> build limpa (warnings = erros). O `IpcTests` usa `LicencaFake` e `ColetorFake`
> para testar rotas sem dependências externas.
