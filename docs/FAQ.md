# Perguntas Frequentes (FAQ)

## Uso

**O programa pode danificar meu computador?**
A filosofia é ESTABILIDADE em primeiro lugar: sem backup nada prossegue, aplica
uma categoria por vez, reverte automaticamente em regressão e bloqueia valores
acima do limite técnico. Ainda assim, mudanças fora da faixa segura são "risco
assumido" e exigem consentimento explícito.

**Ele atualiza a BIOS pra mim?**
Não. O módulo de BIOS **identifica, verifica com o fabricante e orienta** com um
guia passo a passo. A atualização é **manual** e por sua conta.

**Preciso de internet?**
Não para o uso padrão: o **cérebro local** (offline) é o default e nada sai da
máquina. Internet só é usada se você configurar o LLM (proposta/visão na nuvem).

**Meus dados vão para a nuvem?**
Apenas se você habilitar o LLM, e **somente o inventário sanitizado** (sem nomes
nem chave de produto; serial/uuid/MAC hasheados). Veja [SEGURANCA.md](SEGURANCA.md).

**Preciso ser administrador?**
Só para **aplicar** otimizações. Diagnóstico (coletar, sensores, relatório,
proposta, BIOS) funciona sem elevação.

**`sensores` diz que não há sensores.**
A máquina (ou contêiner) não expõe `/sys/class/hwmon`, ou falta permissão. Em
VMs isso é comum.

**Como reverto uma mudança?**
A validação reverte a categoria automaticamente em caso de regressão. Os backups
ficam em `data/backups/`.

## Configuração

**Como ligo o LLM (proposta/visão na nuvem)?**
Defina `ANTHROPIC_API_KEY` e `HWOPT_LLM_MODELO` (ID de um modelo Claude com
visão). Veja [INSTALACAO.md](INSTALACAO.md).

**Onde ficam logs e banco?**
`data/logs/`, `data/otimizador.db`, `data/backups/` (relativos ao executável). O
caminho do log também sai em stderr.

## Desenvolvimento

**Por que o código está em português?**
Decisão de design para alinhar com o público; os schemas refletem a serialização
(camelCase).

**Por que warnings são tratados como erros?**
Qualidade reforçada pelo compilador. A UI Avalonia é a exceção (código gerado de
XAML).

**Como adiciono uma ação de otimização?**
Veja [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md) §Como estender — é catálogo +
comando interno + teste de consistência.

**O LLM pode inventar um comando perigoso?**
Não. Ele só escolhe IDs do catálogo; um **guard** descarta qualquer coisa fora
do catálogo e força parâmetros à faixa segura, mesmo se o modelo alucinar.
