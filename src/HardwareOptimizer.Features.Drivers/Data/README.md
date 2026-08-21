# whql_catalog.json

Catálogo estático de referência WHQL (versão mais recente conhecida por Hardware ID), embarcado como `EmbeddedResource`. Consumido por `RepositorioWhqlEstatico`.

- **Origem:** curadoria manual pontual durante o protótipo inicial de `Features.Drivers`.
- **Cobertura:** ~10–20 drivers — não é exaustivo.
- **Status:** seed temporário — `docs/planning-artifacts/architecture/architecture-otimizacao-2026-08-21/ARCHITECTURE-SPINE.md` (AD-4) prevê substituição por consulta real a `IProvedorFonteOficial`; este catálogo vira fallback quando a fonte oficial não responder.
