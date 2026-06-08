# Documentação — Agente de Otimização de Hardware

Índice central da documentação. Os documentos são padronizados, técnicos e
escritos para serem facilmente interpretados por **uma pessoa ou um agente de
IA**.

---

## 👤 Para o usuário final

| Documento | Conteúdo |
| --- | --- |
| [INSTALACAO.md](INSTALACAO.md) | Instalação passo a passo (binário, código, Docker, publicação). |
| [MANUAL.md](MANUAL.md) | Manual de uso: fluxo seguro e cada comando explicado. |
| [FAQ.md](FAQ.md) | Perguntas frequentes (uso e desenvolvimento). |
| [GLOSSARIO.md](GLOSSARIO.md) | Glossário dos termos do sistema. |

## 🛠️ Para o desenvolvimento

| Documento | Conteúdo |
| --- | --- |
| [ARQUITETURA.md](ARQUITETURA.md) | Planos, projetos, dependências e decisões de design. |
| [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md) | Setup, convenções e como estender o sistema. |
| [TESTES.md](TESTES.md) | Estratégia de testes e rastreabilidade das regras. |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | Como contribuir (branch, commit, PR, checklist). |

## 📑 Referência técnica

| Documento | Conteúdo |
| --- | --- |
| [SEGURANCA.md](SEGURANCA.md) | Modelo de segurança, privacidade e consentimento. |
| [CATALOGO.md](CATALOGO.md) | Catálogo de ações whitelisted e seus limites. |
| [CONTRATOS.md](CONTRATOS.md) | Contratos de dados (JSON) e schemas. |
| [API_IPC.md](API_IPC.md) | Protocolo IPC (métodos, requisição/resposta). |
| [arquitetura_otimizador.json](arquitetura_otimizador.json) | Documento de arquitetura original (referência). |

## 📋 Projeto

| Documento | Conteúdo |
| --- | --- |
| [../README.md](../README.md) | Visão geral do projeto. |
| [../CHANGELOG.md](../CHANGELOG.md) | Histórico de versões (por fase do roadmap). |
| [../SECURITY.md](../SECURITY.md) | Política de segurança (reporte de vulnerabilidades). |

---

## Convenções desta documentação

- **Idioma:** português; o domínio do código também é modelado em português.
- **Comando de exemplo:** `hwopt` representa o executável da CLI
  (`HardwareOptimizer.Cli`). Veja [MANUAL.md](MANUAL.md) §0.
- **Blocos de código:** shell para comandos, JSON para contratos/protocolo, C#
  para exemplos de extensão.
- **Tabelas de referência:** campos e limites são apresentados em tabelas.
- **Rastreabilidade:** regras de segurança são sempre ligadas ao componente que
  as garante e ao teste que as cobre.
