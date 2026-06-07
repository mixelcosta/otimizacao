using HardwareOptimizer.Core.Catalog;

namespace HardwareOptimizer.Core.Profiles;

/// <summary>
/// Desfecho da construção/validação de um perfil. Distingue bloqueios (impedem
/// salvar) de riscos assumidos (permitidos mediante consentimento).
/// </summary>
public sealed class ResultadoConstrucaoPerfil
{
    public ResultadoConstrucaoPerfil(
        bool sucesso,
        Perfil? perfil,
        bool exigeConsentimento,
        IReadOnlyList<ResultadoValidacaoAcao> validacoes,
        IReadOnlyList<string> bloqueios,
        IReadOnlyList<RiscoAssumido> riscosAssumidos)
    {
        Sucesso = sucesso;
        Perfil = perfil;
        ExigeConsentimento = exigeConsentimento;
        Validacoes = validacoes;
        Bloqueios = bloqueios;
        RiscosAssumidos = riscosAssumidos;
    }

    /// <summary>Verdadeiro se o perfil é válido (sem bloqueios). Pode ainda exigir consentimento.</summary>
    public bool Sucesso { get; }

    public Perfil? Perfil { get; }

    /// <summary>Exige o fluxo de consentimento antes de persistir/aplicar.</summary>
    public bool ExigeConsentimento { get; }

    public IReadOnlyList<ResultadoValidacaoAcao> Validacoes { get; }

    /// <summary>Motivos que impedem salvar (limite absoluto, ação fora do catálogo, valor inválido).</summary>
    public IReadOnlyList<string> Bloqueios { get; }

    /// <summary>Parâmetros fora da faixa segura aceitos sob responsabilidade do usuário.</summary>
    public IReadOnlyList<RiscoAssumido> RiscosAssumidos { get; }
}

/// <summary>Um parâmetro marcado como "risco assumido pelo usuário".</summary>
public sealed record RiscoAssumido(string AcaoId, string Parametro, string Valor, string Detalhe);
