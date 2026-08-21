---
title: 'Revisão Adversarial — ARCHITECTURE-SPINE (Módulo de Sugestão de Upgrade)'
type: architecture-review
reviews: 'docs/planning-artifacts/architecture/architecture-otimizacao-2026-08-21/ARCHITECTURE-SPINE.md'
against: 'docs/planning-artifacts/prds/prd-otimizacao-2026-08-20/prd.md'
method: 'pares de duas unidades independentes, cada uma obedecendo todo AD à risca, construindo algo incompatível entre si'
created: '2026-08-21'
status: draft
---

# Revisão Adversarial da Espinha de Arquitetura

**Método.** Para cada AD, Rule ou Convention da espinha, tentei escrever duas implementações de "uma unidade um nível abaixo" (duas histórias, dois devs, dois times) que cada uma passa em uma leitura literal e de boa-fé do texto — e ainda assim produzem algo que não integra: shape de dado diferente, dono duplo de uma entidade, caminho de mutação divergente, nome não fixado, contrato de IPC divergente. Cada par é listado com: o texto da espinha que ambas as unidades obedecem, o que a Unidade A constrói, o que a Unidade B constrói, e por que as duas não integram. Ordenados por gravidade (mais grave primeiro).

**Total de pares encontrados: 13** (9 de gravidade alta/estrutural, 4 de gravidade média/sutil).

---

## P1 — AD-8: dois donos textuais para a composição do Relatório de Resultado

**Texto da espinha (AD-8):** *"`Features.Atualizacao` (ou o consumidor de IPC que monta o Relatório de Resultado) é o único ponto que compõe recomendações das duas fatias e aplica 'menor custo primeiro'"*.

A própria Rule oferece dois donos possíveis com um "ou" — isso não é uma leitura forçada, é o texto.

- **Unidade A (dev de `Features.Atualizacao`):** implementa `Features.Atualizacao.MontadorRelatorio` (ou nome equivalente) como serviço de domínio que internamente chama `Features.Manutencao` e `Features.Upgrade`, ordena por custo, e devolve um objeto pronto. `RoteadorIpc` expõe **um** `Metodo` (`ObterRelatorioResultado`) que apenas invoca esse serviço e serializa o retorno.
- **Unidade B (dev de `Ipc`):** lê a mesma frase e decide que "o consumidor de IPC" é o dono. Implementa o merge e o "menor custo primeiro" **dentro do handler do `RoteadorIpc`**, chamando `Features.Manutencao.ObterRecomendacoes()` e `Features.Upgrade.ObterSugestoes()` diretamente. `Features.Atualizacao` nunca chega a ter um método de composição — vira só o dono de FR-1 a FR-7.

**Por que não integra.** Duas implementações completas e mutuamente exclusivas de "quem ordena por custo" — uma dentro de `Features.Atualizacao` (camada de domínio), outra dentro de `Ipc` (camada de fronteira, que a espinha em nenhum outro lugar autoriza a conter lógica de negócio — o único "proibido: sem lógica de negócio" explícito no diagrama é para `App`, não para `Ipc`, mas o parágrafo de Design Paradigm descreve `Ipc` como "fronteira", não como camada de domínio). Se as duas stories forem pegas por times diferentes, ambas passam a "existir": há dois algoritmos de ordenação por custo, potencialmente com critérios de desempate diferentes, e nenhuma delas é obviamente a errada pela letra do AD-8.

**Fechar com:** AD-8 precisa nomear exatamente um dono (`Features.Atualizacao`, sem alternativa), com assinatura de método fixada no Structural Seed.

---

## P2 — AD-8: shape do resultado composto — dois valores agregados vs. lista ordenada de itens

**Texto da espinha:** AD-8 diz que o orquestrador "compõe recomendações das duas fatias e aplica 'menor custo primeiro'". O Glossário do PRD (§3) descreve o Relatório de Resultado como exatamente **duas linhas escalares**: `Otimização do S.O. = X%` / `Upgrade hardware = X%`, sem preço nem lista. FR-10 (que AD-8 implementa) fala de "recomendação de Diagnóstico de Manutenção" e "recomendação de Upgrade de Peça" aparecendo **uma antes da outra** — linguagem de lista item a item, não de dois agregados percentuais.

- **Unidade A (segue o Glossário/FR-14 ao pé da letra):** modela o retorno de `Features.Atualizacao` como `RelatorioResultado { OtimizacaoSoPercentual: decimal?, UpgradeHardwarePercentual: decimal? }` — dois campos escalares. FR-10/"menor custo primeiro" é satisfeito implicitamente porque só existem duas linhas fixas; não há lista para ordenar.
- **Unidade B (segue FR-10 ao pé da letra):** modela o retorno como `RelatorioResultado { Recomendacoes: List<RecomendacaoOrdenada> }`, uma lista mista de itens de Manutenção e de Upgrade já ordenados por custo — porque é o único shape em que "a de menor custo aparece primeiro" (FR-10) faz sentido literal.

**Por que não integra.** O contrato de IPC de retorno de `ObterRelatorioResultado` é literalmente um objeto diferente nas duas leituras (dois campos escalares vs. uma lista). Se `App`/UI for construído contra o shape da Unidade A e `Features.Atualizacao` for entregue no shape da Unidade B (ou vice-versa), a tela de Relatório de Resultado quebra em runtime — sem nenhum teste de contrato pegando isso antes, porque nada na espinha fixa o DTO.

**Fechar com:** Structural Seed precisa conter o shape exato do DTO de retorno de `ObterRelatorioResultado` (ou uma decisão explícita: "duas linhas fixas + FR-10 se aplica só dentro da tela de Diagnóstico, não do Relatório de Resultado").

---

## P3 — AD-8 pressupõe um "Custo" comparável que não existe em nenhum contrato de `Core`

**Texto da espinha:** AD-8 exige "aplica 'menor custo primeiro'" comparando uma recomendação de `Features.Manutencao` (ex.: troca de pasta térmica, R$ 40) com uma de `Features.Upgrade` (preço de peça na Vitrine). O Structural Seed só lista dois contratos novos em `Core`: `EventoInstabilidade` e `GanhoEstimado`. Não há um value object de custo/preço compartilhado.

- **Unidade A (`Features.Manutencao`):** modela o custo do serviço como `AcaoOtimizacao`/`CategoriaAcao` estendido — reaproveita o catálogo existente de otimização de SO (que AD-7 diz ser um catálogo separado, mas AD-7 só proíbe crescer `.Cpu`/`.Memoria`, não proíbe adicionar uma categoria nova tipo `.Manutencao` reaproveitando o tipo existente). O campo de custo vira um `decimal Preco` dentro de `AcaoOtimizacao`.
- **Unidade B (`Features.Upgrade`):** já tem preço de peça vindo da Loja Parceira (FR-16/FR-18), num tipo próprio de catálogo de Vitrine (`TipoPecaUpgrade` + dados de listagem), sem qualquer relação com `AcaoOtimizacao`.

**Por que não integra.** `Features.Atualizacao` (ou quem quer que implemente AD-8, ver P1) precisa comparar dois valores de custo para decidir a ordem — mas um vem de um `AcaoOtimizacao.Preco` recém-inventado (unidade de moeda? nullable? "grátis" representado como zero ou como ausência?), o outro vem do preço dinâmico da Loja Parceira (pode variar por loja, ter parcelamento — FR-18). Não há conversão nem tipo comum. Cada fatia serializa "custo" do seu jeito e a comparação de AD-8 não tem como ser implementada de forma determinística sem que alguém defina esse contrato — o que a espinha nunca faz.

**Fechar com:** um AD novo (ou extensão de AD-8) fixando um value object `Custo`/`FaixaDeCusto` em `Core`, usado por ambas as fatias.

---

## P4 — AD-4: "único componente" `IProvedorFonteOficial` contradito pelo próprio Capability Map, que lista três donos

**Texto da espinha (AD-4):** *"um único componente (`IProvedorFonteOficial` ou equivalente, novo) resolve versão-mais-recente para driver/software/BIOS"*. Mas a tabela **Capability → Architecture Map** da própria espinha diz: `FR-1, FR-2 (verificação driver/software/BIOS) | Features.Atualizacao + Features.Drivers (driver) + Core/Bios (BIOS) | AD-4` — três donos distintos para a mesma capacidade.

- **Unidade A (dev de `Features.Atualizacao`):** lê AD-4 literalmente — implementa `IProvedorFonteOficial` como serviço único, chamado para driver, software **e** BIOS. Refatora `AtualizadorDrivers`/`Features.Drivers` para injetar essa dependência em vez de sua lógica de verificação de versão antiga.
- **Unidade B (dev de `Features.Drivers`, pegando a expansão "existente, expandida" mencionada no Design Paradigm):** lê o Capability Map — entende que driver já tem dono (`Features.Drivers`) e continua evoluindo `RepositorioWhqlEstatico`/lógica própria de verificação de versão para drivers, sem nunca instanciar `IProvedorFonteOficial`. `IProvedorFonteOficial` fica restrito, na prática, a software e BIOS.

**Por que não integra.** Se as duas stories forem pegas em paralelo, existem dois caminhos concorrentes de "qual é a versão mais recente do driver X": um via `IProvedorFonteOficial` (Unidade A) e outro via a lógica antiga do `Features.Drivers` (Unidade B, que a Unidade A pode até ter tentado substituir sem coordenação). Resultado plausível: `Features.Drivers` continua reportando versão via allowlist antiga/`RepositorioWhqlEstatico` como "fallback" (que AD-4 diz que deveria virar fallback, não primário) enquanto `Features.Atualizacao` acha que já centralizou tudo em `IProvedorFonteOficial` — dois números de "versão mais recente" divergentes para o mesmo driver, sem ninguém errado pela letra do texto.

**Fechar com:** o Capability Map precisa ser reescrito para não listar `Features.Drivers` como dono de FR-1/FR-2 em paralelo a `Features.Atualizacao`, ou AD-4 precisa dizer explicitamente que `Features.Drivers` é refatorado para consumir `IProvedorFonteOficial` (não manter lógica própria).

---

## P5 — `GanhoEstimado` (AD-3) só tem shape definido para a trilha paga; a linha `Otimização do S.O. = X%` (FR-14) não tem contrato nem algoritmo

**Texto da espinha:** AD-3 fixa `GanhoEstimado { Percentual, MargemConfianca, AtualizadoEm }` explicitamente como o contrato para "toda estimativa vinda de TechPowerUp" (a linha `Upgrade hardware`). Nada na espinha diz como a **outra** linha do mesmo Relatório de Resultado — `Otimização do S.O. = X%` — é calculada ou tipada. Nenhum FR do Núcleo de Atualização (FR-1 a FR-7) produz um percentual; FR-14 apenas assume que ele existe.

- **Unidade A (`Features.Atualizacao`):** reaproveita `GanhoEstimado` também para a linha de SO, computando `Percentual` como uma heurística própria (ex.: baseada em quantos itens desatualizados existem) — com `MargemConfianca`/`AtualizadoEm` preenchidos com valores dummy porque não fazem sentido semântico para essa trilha (não vem de benchmark).
- **Unidade B (`Features.Atualizacao`, outra story/outro dev):** cria um tipo paralelo, sem relação com `GanhoEstimado` — ex. `PercentualOtimizacao(decimal Valor)` — porque a linha de SO nunca veio de TechPowerUp e reaproveitar um contrato pensado para "margem de confiança de benchmark de hardware" pareceria semanticamente errado.

**Por que não integra.** Um dos dois vira o campo `OtimizacaoSoPercentual` do DTO do Relatório de Resultado (ver P2) com um shape, o outro com outro — e nenhuma das duas leituras é contrariada pelo texto da espinha, porque o texto simplesmente nunca fala da linha de SO. Pior: nenhum FR sequer define **o que** esse percentual mede (itens corrigidos? redução esperada de BSOD? ganho de desempenho de driver?) — é uma lacuna dupla (algoritmo + contrato).

**Fechar com:** um AD (ou extensão de AD-3) definindo o algoritmo e o contrato de `Otimização do S.O. = X%`, ou decisão explícita de reuso de `GanhoEstimado`.

---

## P6 — "RAM soldada / slot livre" não tem AD dono nenhum — AD-6 escopa `Armazenamento` só para storage

**Texto da espinha (AD-6):** `Armazenamento` é definido explicitamente como "capacidade, tipo de interface SATA/NVMe, slots livres/ocupados" — vocabulário de **disco**. FR-17 (conversão de notebook) exige saber se a **RAM** é soldada ou tem slot livre — um fato de hardware completamente diferente, sem componente de `Inventario` designado em nenhum AD.

- **Unidade A (dev de `Features.Upgrade`, implementando FR-17):** decide que "slot livre" é conceitualmente parecido com o que AD-6 descreve para storage, e estende o novo componente `Armazenamento` com um campo `RamSoldada: bool` — reaproveitando o componente mais recente/mais próximo em vez de mexer em `Inventario.Memoria` existente.
- **Unidade B (dev de `Agent/Storage` ou de `Core`, implementando o mesmo FR-17 em paralelo):** entende que RAM é histórico de `Inventario.Memoria` (componente já existente, não tocado por AD-6) e adiciona `RamSoldada`/`SlotsLivres` lá.

**Por que não integra.** Dois lugares diferentes do `Inventario` acabam com um campo semanticamente igual (`RamSoldada`/slot livre de RAM) — um dentro do novo `Armazenamento` (que AD-6 define como sendo sobre disco), outro dentro do `Memoria` pré-existente. Quem consome (Vitrine, Teto de Compatibilidade FR-12/FR-13) não sabe qual ler, e nenhum teste de contrato pega isso porque as duas leituras de "onde a RAM soldada mora" são igualmente razoáveis dado o texto da espinha.

**Fechar com:** um AD novo fixando onde vive o fato "RAM soldada / slot de RAM livre" — provavelmente uma extensão nomeada de `Inventario.Memoria`, não de `Armazenamento`.

---

## P7 — AD-1 ("toda ação mutante nova... passa por `ExecutorControlado`") não diz se a escrita factual de FR-9 conta como "ação mutante"

**Texto da espinha (AD-1):** *"toda ação mutante nova (driver, software, futura config) declara `PreCondicoes` e passa por `ExecutorControlado.AplicarPerfilAsync`"*. FR-9 exige salvar no Inventário um dado factual informado pelo usuário (ex.: data da última troca de pasta térmica) — isso é uma escrita, mas não é "driver, software ou config do sistema operacional".

- **Unidade A (`Features.Manutencao`):** trata a escrita de FR-9 como dado de aplicação comum — grava direto via um repositório simples (`RepositorioInventario.AtualizarDadoFactual(...)`), sem `PreCondicoes` nem `ExecutorControlado`, porque não é uma mutação do Windows/hardware — é metadado do app.
- **Unidade B (`Features.Manutencao`, dev diferente, lendo "toda ação mutante nova" ao pé da letra):** roteia a mesma escrita por `ExecutorControlado.AplicarPerfilAsync` com uma `PreCondicoes` trivial, seguindo a letra do AD-1 ("toda ação mutante nova" — sem exceção explícita para dado factual), gerando inclusive um ponto de restauração desnecessário no fluxo de FR-9/FR-11.

**Por que não integra.** Duas rotas de escrita coexistindo para o mesmo tipo de dado (o "dado factual" de manutenção) — uma passa pelo pipeline de aprovação/rollback pesado (`ExecutorControlado`), outra não. Isso quebra a garantia que AD-1 tenta proteger: se um dev futuro reusa `ExecutorControlado` esperando que **toda** mutação registrada nele seja reversível/auditada, a rota da Unidade A (escrita direta) é invisível para esse mecanismo — um "segundo caminho de mutação" na prática, exatamente o que AD-1 diz que quer prevenir, só que criado por uma leitura legítima do próprio texto.

**Fechar com:** AD-1 precisa distinguir explicitamente "mutação de estado de hardware/SO" (sempre via `ExecutorControlado`) de "mutação de metadado de produto informado pelo usuário" (caminho direto permitido), com exemplos.

---

## P8 — AD-2 enumera nominalmente `ValidadorCompatibilidade`/`GeradorSugestoes`/`CalculadoraGargalo`; o novo cliente TechPowerUp (FR-19) não está na lista

**Texto da espinha (AD-2):** *"`UpgradeViewModel` não pode conter catálogo, regra de compatibilidade ou cálculo de ganho próprios — consome exclusivamente `ValidadorCompatibilidade`/`GeradorSugestoes`/`CalculadoraGargalo` (ou seus sucessores)"*. O cliente de benchmark novo fica em `Features.Upgrade/Benchmark` (Structural Seed), sem estar nomeado como um desses três nem explicitamente marcado como "sucessor" de nenhum deles.

- **Unidade A:** interpreta que o cliente de benchmark é lógica interna de `CalculadoraGargalo` (um "sucessor" dela, que passa a chamar o benchmark client internamente) — `UpgradeViewModel` continua só falando com os três nomes originais.
- **Unidade B:** interpreta ao pé da letra que a regra só restringe "catálogo, regra de compatibilidade ou cálculo de ganho **próprios**" — como o cliente TechPowerUp não é "cálculo próprio da ViewModel" e não está coberto pela lista fechada dos três nomes, conecta `UpgradeViewModel` diretamente a `Features.Upgrade/Benchmark` para obter o percentual de ganho, chamando-o em paralelo a `CalculadoraGargalo`.

**Por que não integra.** Na Unidade A, `UpgradeViewModel` tem uma única dependência de fatia (`CalculadoraGargalo`, que já embute ganho). Na Unidade B, `UpgradeViewModel` tem duas dependências de fatia diretas, uma das quais (`Benchmark`) não passa pelo contrato de `GanhoEstimado` (AD-3) de forma óbvia se o dev do Benchmark client não souber que a ViewModel vai consumi-lo cru. O `Metodo` de IPC correspondente também diverge: um `Metodo` (`ObterSugestoesUpgrade`) já com ganho embutido vs. dois `Metodo`s (sugestões + ganho separado) que a UI precisa juntar — reabrindo exatamente a "lógica de negócio na UI" que AD-2 existe para proibir, só que por um caminho que a letra do AD-2 não fecha.

**Fechar com:** nomear explicitamente `Features.Upgrade/Benchmark` como subordinado de `CalculadoraGargalo` (ou seu sucessor formal) no Structural Seed, não deixar como peça solta.

---

## P9 — `LeitorEventLog` (AD-5): filtro por período dentro do leitor vs. no consumidor

**Texto da espinha (AD-5):** *"um novo leitor em `Agent` (ex. `Agent/EventLog/`) lê o Event Log do Windows... sob demanda"*. FR-4 exige que o histórico seja "consultável por período (ex.: 'últimos 30 dias')" — mas a espinha não diz se o filtro de período é parâmetro do leitor (`Agent`) ou pós-processamento em `Core`/`Features.Atualizacao`.

- **Unidade A (`Agent`):** `LeitorEventLog.Ler(DateTime desde)` — filtro aplicado na query nativa do Windows Event Log (mais eficiente, evita trazer histórico enorme pela fronteira `Agent`↔`Core`).
- **Unidade B (`Features.Atualizacao`/`Core`):** assume que `LeitorEventLog.Ler()` sempre traz tudo, e implementa o filtro de período como lógica de domínio pura em `Core`/`Features.Atualizacao` (mais fácil de testar sem depender do Agent), esperando um método sem parâmetro de data.

**Por que não integra.** Assinatura de método incompatível entre quem implementa `Agent/EventLog` e quem implementa o consumidor em `Features.Atualizacao` — se cada time estimar a interface a partir da sua própria leitura de "sob demanda" + "consultável por período", a integração falha na assinatura, não na lógica.

**Fechar com:** fixar a assinatura de `LeitorEventLog` (com ou sem parâmetro de período) no Structural Seed ou no contrato `EventoInstabilidade`.

---

## P10 — Convenção de nomenclatura não reserva sufixo para "orquestrador"/"correlacionador" — colisão de responsabilidade dentro de `Features.Atualizacao`

**Texto da espinha (Consistency Conventions):** a lista de sufixos existentes (`Servico*`, `Coletor*`, `Leitor*`, `Gerador*`, `Validador*`, `Repositorio*`, `Provedor*`) não cobre o tipo de componente que `Features.Atualizacao` precisa para (a) a correlação causa-raiz de AD-5/FR-5 e (b) a composição/ordenação de AD-8/FR-10 — duas responsabilidades de domínio novas, sem sufixo definido, ambas "moram em `Features.Atualizacao`" segundo o Capability Map.

- **Unidade A (story de FR-5):** cria `CorrelacionadorCausaRaiz` (sufixo novo, não documentado) como o serviço de domínio "canônico" de `Features.Atualizacao`, e mais tarde tenta anexar a lógica de composição de AD-8 dentro dele por conveniência (já é "o" serviço de domínio da fatia).
- **Unidade B (story de AD-8, pegada por outro dev, em paralelo):** não sabe que `CorrelacionadorCausaRaiz` existe ainda (ou não quer acoplar responsabilidades diferentes) e cria `MontadorRelatorioResultado` como um serviço irmão, órfão, sem relação declarada com o primeiro.

**Por que não integra.** `Features.Atualizacao` termina com dois pontos de entrada de domínio concorrentes e sem hierarquia clara — não é incompatibilidade de dado, é incompatibilidade de "onde a lógica mora", que gera divergência de teste (cada um testado isoladamente, nenhum teste de integração cobre os dois juntos) e risco real de um terceiro dev, ao implementar FR-6/FR-7 dentro da mesma fatia, não saber qual dos dois é "o" orquestrador para pendurar a lógica nova.

**Fechar com:** adicionar ao menos um sufixo/nome de padrão para "serviço de orquestração de fatia" na tabela de Consistency Conventions, e nomear explicitamente o(s) componente(s) de `Features.Atualizacao` no Structural Seed (hoje só lista a pasta `ProvedorFonteOficial/` dentro dela).

---

## P11 — Semântica de `GanhoEstimado.AtualizadoEm`: timestamp da base vs. timestamp do cálculo

**Texto da espinha (AD-3):** `GanhoEstimado { Percentual, MargemConfianca, AtualizadoEm }`. FR-19 (consequence) diz "a data da última atualização da **base** de benchmark" — sugerindo um valor único, no nível do dataset, não por estimativa individual.

- **Unidade A (`Features.Upgrade/Benchmark`):** implementa `AtualizadoEm` como a data em que **aquele item específico do catálogo TechPowerUp** foi raspado/curado pela última vez — pode variar peça a peça.
- **Unidade B (mesma fatia, outro método/outra story):** implementa `AtualizadoEm` como a data de refresh **global** da base (uma constante para todas as instâncias de `GanhoEstimado` geradas na mesma sessão/build).

**Por que não integra.** SM-5 ("cobertura da base de benchmark") e o texto de UI exigido por FR-14 ("~X%, estimativa") dependem de qual semântica é usada — se um item raro da base não é atualizado há 2 anos mas o dataset global foi atualizado ontem, a Unidade B mostra "atualizado ontem" para um dado na prática obsoleto, exatamente o tipo de "estimativa sem lastro suficiente" que o guard de FR-19/SM-C3 tenta evitar. Não é um bug de tipo, é um bug de confiança do produto que a espinha não previne porque não define a granularidade do campo.

**Fechar com:** especificar no Structural Seed se `AtualizadoEm` é por item ou por dataset.

---

## P12 — Fronteira de processo ambígua entre `Features.Atualizacao` e as fatias que ela consulta em AD-8

**Texto da espinha:** Design Paradigm define `Ipc` como "fronteira única entre o processo privilegiado (`Agent`/`WindowsService`) e a UI". O diagrama mermaid mostra `FAtualizacao -.-> FManutencao` e `FAtualizacao -.-> FUpgrade` como setas de "consulta", sem dizer se essas fatias rodam todas no mesmo processo (então é chamada de método in-proc) ou se alguma roda separada.

- **Unidade A:** assume (corretamente, pela leitura mais natural do parágrafo) que todas as `Features.*` vivem no processo do `Agent`/`WindowsService`, e implementa a consulta de AD-8 como chamada de interface C# direta, in-proc.
- **Unidade B:** nota que `Features.Manutencao` depende de `Agent/Sensors` e pensa em isolá-la (por exemplo, para não travar a UI durante uma medição de estresse mais longa) atrás de um `Metodo` de IPC próprio — fazendo com que `Features.Atualizacao`, ao "consultar para ordenação" (AD-8), dispare uma chamada de named pipe para si mesma/para o mesmo processo, em vez de uma chamada de método.

**Por que não integra.** Isso é mais especulativo que os pares anteriores (a leitura mais natural favorece a Unidade A), mas nada no texto **exclui** explicitamente a Unidade B — e se acontecer, o "único ponto de composição" de AD-8 passa a ter uma dependência circular de IPC (Ipc chama Features.Atualizacao chama Ipc) que a espinha nunca contempla nem proíbe.

**Fechar com:** declarar explicitamente no Design Paradigm que toda comunicação entre fatias `Features.*` é in-proc (nunca via `Ipc`), reservando `Ipc` estritamente para a fronteira `App`↔`Agent`.

---

## P13 — `Metodo` de IPC: quem decide nome e shape quando duas fatias competem pelo mesmo caso de uso

**Texto da espinha (Consistency Conventions):** *"Todo caso de uso novo entra em `RoteadorIpc` como um novo valor de `Metodo`"* — não diz quem arbitra nome/shape quando duas fatias, construídas por times diferentes, precisam do mesmo dado composto (o caso central: quem expõe o `Metodo` que devolve o Relatório de Resultado, já coberto em P1/P2, é só o exemplo mais óbvio).

- **Unidade A:** ao implementar a tela de Diagnóstico de Manutenção, cria `Metodo.ObterDiagnosticoManutencao` retornando só os achados de `Features.Manutencao`.
- **Unidade B:** ao implementar o Relatório de Resultado (que precisa saber se existe recomendação de manutenção pendente, para FR-10/AD-8), em vez de reusar o `Metodo` da Unidade A, cria um segundo `Metodo.ObterResumoManutencaoParaRelatorio` com um subconjunto de campos ligeiramente diferente (porque "não sabia" ou não queria acoplar a tela de Relatório ao contrato completo da tela de Diagnóstico).

**Por que não integra.** Dois `Metodo`s de `RoteadorIpc` para a mesma fatia com shapes parcialmente sobrepostos e nomes que não seguem nenhum padrão declarado — cada um passa isoladamente em "todo caso de uso novo entra como `Metodo` novo", mas juntos formam uma superfície de IPC duplicada e sem fonte única de verdade, o tipo exato de inconsistência que a convenção deveria evitar e não evita porque não fala de reuso/composição de `Metodo`s existentes.

**Fechar com:** adicionar à convenção de IPC uma regra de "um caso de uso, um `Metodo`; composições reusam o retorno de `Metodo`s existentes via chamada in-proc à fatia, nunca duplicam campo".

---

## Resumo por AD/Convenção afetada

| AD / Convenção | Pares que expõem furo |
| --- | --- |
| AD-8 (ordenação Manutenção/Vitrine) | P1, P2, P3, P10, P12, P13 |
| AD-4 (`IProvedorFonteOficial`) | P4 |
| AD-3 (`GanhoEstimado`) | P5, P11 |
| AD-6 (`Armazenamento`) | P6 |
| AD-1 (aprovação/rollback) | P7 |
| AD-2 (fonte única de sugestão) | P8 |
| AD-5 (Event Log) | P9 |
| Naming convention | P10 |
| Convenção de IPC (`Metodo`) | P13 |

**Observação geral:** AD-8 é, disparadamente, o ponto mais frágil da espinha — não porque a regra esteja errada, mas porque ela resolve "quem decide a ordem" sem resolver "onde essa decisão mora, com que shape de dado, e contra qual contrato de custo compartilhado". Qualquer revisão da espinha que só tenha tempo para um AD deveria endurecer AD-8 primeiro (P1 + P2 + P3 juntos cobrem isso).
