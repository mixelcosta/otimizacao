namespace HardwareOptimizer.Core.Common;

/// <summary>Sistemas operacionais suportados. MVP prioriza Windows 11.</summary>
public enum SistemaOperacionalTipo
{
    Desconhecido = 0,
    Windows = 1,
    Linux = 2,
}

/// <summary>
/// Categorias de otimização. A ordem dos membros segue exatamente
/// <c>categorias_ordem</c> do documento de arquitetura, de modo que a ordenação
/// natural do enum já corresponde à ordem de execução por categoria.
/// </summary>
public enum CategoriaAcao
{
    Cpu = 0,
    Memoria = 1,
    Gpu = 2,
    SistemaOperacional = 3,
    Drivers = 4,
    Servicos = 5,
    Rede = 6,
}

/// <summary>Classificação de risco de uma ação, do documento.</summary>
public enum NivelRisco
{
    Nenhum = 0,
    MuitoBaixo = 1,
    Baixo = 2,
    Medio = 3,
    Alto = 4,
}

/// <summary>Perfil de parametrização: seguro (padrão) ou customizado pelo usuário.</summary>
public enum TipoPerfil
{
    Seguro = 0,
    Customizado = 1,
}

/// <summary>Desfecho da validação de um único valor de parâmetro.</summary>
public enum SituacaoParametro
{
    /// <summary>Dentro da faixa segura: aprovado sem ressalvas.</summary>
    Aceito = 0,

    /// <summary>Dentro da faixa permitida, porém fora da faixa segura: risco assumido pelo usuário.</summary>
    RiscoAssumido = 1,

    /// <summary>Valor inválido ou fora da faixa permitida: rejeitado.</summary>
    Rejeitado = 2,

    /// <summary>Ultrapassa o limite absoluto: bloqueio rígido, sem opção de prosseguir.</summary>
    BloqueioLimiteAbsoluto = 3,
}
