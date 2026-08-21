---
title: 'Corrigir bug de build que impede compilação de Features.Upgrade e Features.Drivers'
type: 'bugfix'
created: '2026-08-21'
status: 'done'
route: 'one-shot'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A regra `data/` (sem âncora) no `.gitignore` excluía do controle de versão `src/HardwareOptimizer.Features.Upgrade/Data/hardware_catalog.json` e `src/HardwareOptimizer.Features.Drivers/Data/whql_catalog.json` — `EmbeddedResource` referenciados pelos respectivos `.csproj`. Um clone novo do repositório falhava o build com `CS1566` nos dois projetos.

**Approach:** Ancorar a regra do `.gitignore` na raiz (`/data/`), preservando a intenção original (ignorar a pasta de runtime `data/backups` do `ServicoBackup`) sem capturar as pastas `Data/` dos projetos de feature. Recuperar os dois arquivos JSON de uma cópia local existente do mesmo repositório e versioná-los.

</frozen-after-approval>

## Suggested Review Order

**Correção do gitignore**

- Regra ancorada na raiz, com comentário explicando por que a barra inicial é obrigatória — evita a regressão exata que causou o bug.
  [`.gitignore:20`](../../.gitignore#L20)

**Catálogos recuperados e versionados**

- Catálogo de compatibilidade de hardware (CPU/GPU/placa-mãe) — dado hand-curated, ~15 peças.
  [`hardware_catalog.json`](../../src/HardwareOptimizer.Features.Upgrade/Data/hardware_catalog.json)

- Nota de proveniência — origem, cobertura e status do catálogo, pra quem for expandi-lo depois.
  [`Data/README.md`](../../src/HardwareOptimizer.Features.Upgrade/Data/README.md)

- Catálogo estático WHQL (versão mais recente por Hardware ID) — seed temporário até `IProvedorFonteOficial` existir.
  [`whql_catalog.json`](../../src/HardwareOptimizer.Features.Drivers/Data/whql_catalog.json)

- Nota de proveniência equivalente para o catálogo de drivers.
  [`Data/README.md`](../../src/HardwareOptimizer.Features.Drivers/Data/README.md)

**Achados fora de escopo (deferred)**

- `Features.LifeCounter` tem o mesmo bug de build (catálogo `tbw_database.json` nunca existiu) — não corrigido aqui, história própria necessária.
  [`deferred-work.md`](deferred-work.md)
