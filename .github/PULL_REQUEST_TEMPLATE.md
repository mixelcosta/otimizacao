<!--
Obrigado pela contribuição! Preencha o template abaixo.
Guia completo: ../CONTRIBUTING.md
-->

## Descrição

<!-- O que muda e, principalmente, **por quê**. -->

## Tipo de mudança

- [ ] 🐛 Correção de bug (`fix`)
- [ ] ✨ Nova funcionalidade (`feat`)
- [ ] 📝 Documentação (`docs`)
- [ ] ♻️ Refatoração (`refactor`)
- [ ] ✅ Testes (`test`)
- [ ] 🔧 Build/CI/infra (`chore`/`ci`)

## Issue relacionada

<!-- Ex.: Closes #123 -->

## Como foi testado

<!-- Comandos rodados, cenários cobertos, evidência. -->

```bash
dotnet build HardwareOptimizer.sln -c Release
dotnet test  HardwareOptimizer.sln -c Release
```

## Checklist (Definição de Pronto)

- [ ] Build **sem warnings** (`-c Release`; warnings = erros, exceto UI Avalonia).
- [ ] **Toda a suíte de testes verde** (`dotnet test`).
- [ ] Novo comportamento tem **teste** correspondente.
- [ ] Se mexi no **catálogo**: limites coerentes
      (`faixa_segura ⊆ permitida ⊆ limite_absoluto`) e vínculo
      catálogo↔comando coberto (`RegistroComandosTests`).
- [ ] Se mexi numa **regra invariante**: a
      [rastreabilidade](../docs/SEGURANCA.md#regras-invariantes-rastreabilidade)
      segue válida.
- [ ] **Documentação atualizada** (contratos, API IPC, catálogo, manual) se o
      comportamento visível mudou.
- [ ] **Nenhum dado sensível** (PII, chaves) em código, logs, testes ou exemplos.

## Notas para o revisor

<!-- Pontos de atenção, decisões de design, trade-offs. -->
