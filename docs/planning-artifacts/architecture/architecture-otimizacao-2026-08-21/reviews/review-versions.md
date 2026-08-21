---
name: 'Revisão de Atualidade de Stack — Architecture Spine'
type: review
target: 'docs/planning-artifacts/architecture/architecture-otimizacao-2026-08-21/ARCHITECTURE-SPINE.md'
purpose: 'Verificar contra pesquisa web/checagem de realidade se toda tecnologia nomeada na espinha (Stack, AD-3/AD-4, FR-19) ainda é atual, suportada e serve o propósito declarado'
created: '2026-08-21'
---

# Revisão de Atualidade de Stack — Architecture Spine

**Método:** cada item foi verificado por pesquisa web (não por memória de treinamento) e cruzado contra o estado real do repositório (`Directory.Build.props` e `.csproj` de todos os projetos em `src/` e `tests/`), já que a espinha declara explicitamente que o Stack "já em uso... ratificado aqui, não reinventado".

## Veredito geral

A stack é internamente consistente com o código já existente (todo o repo já está em `net8.0`/`LangVersion 12`, Avalonia 12, `CommunityToolkit.Mvvm` 8.4.2, `xunit` 2.4.2/`coverlet.collector` 6.0.0 — a espinha não inventa nada novo aqui), mas dois pontos nomeados carregam risco de atualidade real e não registrado: o prazo de fim de suporte do .NET 8 cai a menos de 3 meses da data da espinha, e a integração com TechPowerUp (AD-3/FR-19) é tratada como "sem dependência nova no stack" quando a pesquisa mostra que o acesso pleno à base da TechPowerUp hoje passa por um programa comercial de licenciamento, não por um endpoint público trivial.

## Findings

### 1. [HIGH] .NET 8 chega ao fim do suporte em 10/nov/2026 — não mencionado na espinha nem no Deferred

- **Onde:** Stack (`.NET 8 (net8.0 / net8.0-windows)`), linha 126.
- **Achado:** .NET 8 e .NET 9 atingem End of Support simultaneamente em **10 de novembro de 2026** (confirmado no .NET Blog da Microsoft, "dotnet-8-9-end-of-support"). A data da espinha é 2026-08-21 — ou seja, o fim de suporte do .NET 8 está a **menos de 3 meses** da data em que esta espinha foi escrita. .NET 10 já é a LTS vigente desde 11/nov/2025, com suporte até nov/2028.
- **Por que importa:** a espinha trata a versão do .NET como decisão já resolvida e não registra o prazo de EOL como risco aceito nem como item do Deferred. Qualquer história que ainda esteja em desenvolvimento ou não migrada depois de meados de novembro de 2026 roda em um runtime sem mais patches de segurança. Não é necessariamente um bloqueador — este PRD estende uma base de código já em `net8.0`, e migrar todo o `otimizacao` para `net10.0` é fora do escopo desta feature — mas o silêncio da espinha sobre esse prazo é a lacuna: nenhuma decisão explícita ("aceitamos o risco" vs. "a migração para net10.0 é um pré-requisito de outra iniciativa") foi registrada.
- **Recomendação:** adicionar uma linha ao Deferred (ou uma nota na tabela Stack) reconhecendo o prazo de EOL de 10/nov/2026 e apontando a decisão (aceitar o risco pelo tempo de vida desta feature, ou amarrar a migração para `net10.0` como pré-requisito de uma iniciativa separada de plataforma).
- **Fontes:** [.NET 8 and .NET 9 will reach End of Support on November 10, 2026 — .NET Blog](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/); [Announcing .NET 10 — .NET Blog](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/).

### 2. [HIGH] AD-3/FR-19: TechPowerUp hoje é um programa comercial de licenciamento, não um "HTTP simples sem dependência nova"

- **Onde:** linha 132 (*"Nenhuma dependência nova precisa entrar no stack para os FRs deste PRD — TechPowerUp e a fonte oficial de driver/BIOS são integrações HTTP, cobertas pelo runtime já presente."*) e AD-3 (linha 70: *"TechPowerUp → única fonte do ganho estimado/benchmark (FR-19)..."*).
- **Achado:** techpowerup.com está ativo e mantém bases de GPU/CPU/SSD curadas manualmente (não agregadas de terceiros) — a tecnologia em si segue servindo o propósito. Porém a página oficial `techpowerup.com/database-licensing/` (ativa em 2026, com menção a acesso via REST API **e MCP**) descreve um modelo de duas camadas: **acesso gratuito cobre só um subconjunto curado** (flagships, placas/CPUs notáveis, cobertura completa apenas da geração atual) e o **dataset completo exige licenciamento comercial sob contato direto — "não há pacote padrão", preço não publicado**.
- **Por que importa:** um app de sugestão de upgrade para hardware existente do usuário provavelmente precisa de cobertura de peças de gerações passadas/orçamento médio, não só flagships atuais — exatamente o segmento que a camada gratuita da TechPowerUp parece não cobrir integralmente. Isso não invalida AD-3 (a regra "sem cobertura = omite o número" absorve bem esse caso), mas a frase "sem dependência nova no stack, é só uma integração HTTP" subestima que o caminho para cobertura ampla passa por uma negociação comercial/jurídica (custo, termos de uso, SLA), não por uma decisão puramente técnica. O item já aberto no Deferred ("Mecanismo de extração/atualização da base TechPowerUp... e cadência de refresh") cobre parte disso, mas não registra que a via "oficial" hoje é paga/sob contrato e que a via gratuita tem cobertura estreita — informação nova que deveria ser dobrada nesse item aberto.
- **Recomendação:** atualizar o item correspondente do Deferred para citar explicitamente que (a) existe um programa oficial de licenciamento (REST API/MCP) com camada gratuita limitada a itens em destaque/geração atual, (b) cobertura completa requer contrato comercial sem tabela de preço pública, e (c) isso deve entrar na decisão "scraping vs. API vs. curadoria manual" como uma opção com custo e prazo de negociação, não como HTTP grátis já disponível.
- **Fontes:** [Hardware Database Licensing & API Access — TechPowerUp](https://www.techpowerup.com/database-licensing/) (conteúdo indexado via busca — fetch direto retornou HTTP 403); [TechPowerUp (Wikipedia)](https://en.wikipedia.org/wiki/TechPowerUp) confirma a empresa/site como ativo em 2026.
- **Nota de confiança:** o fetch direto da página de licenciamento retornou 403 (proteção anti-bot); a descrição acima vem de resultados de busca que citam o conteúdo da própria página (título, seções de "Free vs. Licensed Access" e "Contacting for Licensing"), não de acesso direto ao HTML. Recomenda-se que alguém do time acesse a página manualmente para confirmar os termos exatos antes de comprometer AD-3/FR-19 a essa fonte.

### 3. [MEDIUM] xUnit v2 (em uso no repo) está oficialmente em modo de manutenção — a espinha não sinaliza a versão real nem o desvio

- **Onde:** Stack, linha 129 (`xUnit + coverlet.collector`, sem versão pinada — diferente de `.NET`/`C#` que ganham versão exata).
- **Achado:** o repositório inteiro (todos os `tests/*.csproj`) usa `xunit 2.4.2` / `xunit.runner.visualstudio 2.4.5` / `coverlet.collector 6.0.0`. Pesquisa confirma que **os pacotes do xUnit v2 estão marcados como deprecated**: v2 está em modo de manutenção (só correções críticas, nenhuma feature nova), e todo o desenvolvimento ativo está em v3 (que já chegou à versão 4.0.0 em 14/ago/2026, dias antes da data desta espinha). `coverlet.collector` também está bem atrás: repo usa 6.0.0, a versão atual no NuGet é 10.0.1.
- **Por que importa:** não é bloqueante — v2 segue recebendo correções críticas — mas é uma convenção da espinha ("Testes: xUnit + coverlet.collector") que ratifica uma versão específica sem nomeá-la nem reconhecer que é a linha legada. Migrar para v3 tem custo de breaking changes (documentado no próprio guia de migração do xUnit) e possivelmente atrito com o `Microsoft.Testing.Platform`/geração de cobertura, então não é uma decisão trivial a se tomar de passagem — mas merece registro consciente, não silêncio.
- **Recomendação:** ou (a) pinar a versão real em uso na tabela Stack para tornar o desvio visível e auditável, ou (b) adicionar ao Deferred a decisão "permanecer em xUnit v2 (maintenance mode) por esta feature vs. migrar para v3" como trade-off explícito.
- **Fontes:** [What's New in v3? — xUnit.net](https://xunit.net/docs/getting-started/v3/whats-new); [Core Framework v3 4.0.0 — xUnit.net](https://xunit.net/releases/v3/4.0.0); [coverlet.collector — NuGet Gallery](https://www.nuget.org/packages/coverlet.collector).

### 4. [LOW] Avalonia pinado no repo (12.0.4) está uma versão menor atrás do estável mais recente (12.1.1)

- **Onde:** Stack, linha 128 (`UI: Avalonia + CommunityToolkit.Mvvm`, sem versão — repo usa `Avalonia.Desktop`/`Avalonia.Themes.Fluent` 12.0.4).
- **Achado:** Avalonia 12.1 foi lançado em maio/2026 (12.0 saiu em abril/2026) e é a versão estável mais recente no NuGet (12.1.1). O framework Avalonia 12 como linha principal segue ativo e é a escolha correta — não há indício de descontinuação. O desvio é só de patch/minor dentro da mesma major, risco baixo.
- **Recomendação:** nenhuma ação arquitetural necessária; é um bump de rotina a ser feito no ritmo normal de manutenção de dependências, não uma decisão de espinha.
- **Fontes:** [What's New — Avalonia UI](https://avaloniaui.net/whats-new); [NuGet Gallery — Avalonia 12.1.1](https://www.nuget.org/packages/avalonia).

## Itens verificados e confirmados atuais (sem ação necessária)

- **CommunityToolkit.Mvvm 8.4.2** (pinado no repo) é de fato a versão mais recente no NuGet, atualizada em março/2026 — nenhuma obsolescência.
- **Named pipes** (`System.IO.Pipes`, base de `HardwareOptimizer.Ipc`) é um mecanismo de IPC estável do runtime .NET, não uma "tecnologia de terceiros" sujeita a descontinuação — sem risco de atualidade identificável.
- **TechPowerUp como site/empresa** segue ativo em 2026 (base de GPU/CPU/SSD curada manualmente, cobertura de gerações atuais confirmada por anúncios recentes de produto) — a tecnologia em si serve o propósito de FR-19; o risco identificado (finding 2) é sobre o modelo de acesso, não sobre a existência/adequação da fonte.

## Resumo por severidade

| Severidade | Finding |
| --- | --- |
| HIGH | .NET 8 EOL em 10/nov/2026, não registrado na espinha (~3 meses após a data da espinha) |
| HIGH | AD-3/FR-19 subestima TechPowerUp como "sem dependência nova"; acesso pleno é licenciamento comercial, gratuito é limitado a itens em destaque/geração atual |
| MEDIUM | xUnit v2 em uso é oficialmente maintenance-mode (v3 é a linha ativa); espinha não nomeia a versão nem o desvio |
| LOW | Avalonia 12.0.4 pinado no repo está uma minor atrás do estável (12.1.1) |
