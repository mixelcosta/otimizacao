using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Profiles;

/// <summary>Uma ação escolhida para um perfil, com os valores de seus parâmetros.</summary>
public sealed record SelecaoAcao
{
    public required string AcaoId { get; init; }

    public IReadOnlyDictionary<string, string> Parametros { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Perfil de parametrização. O perfil seguro usa sempre a faixa segura de cada
/// ação; o customizado carrega valores definidos pelo usuário e só é válido
/// após o fluxo de consentimento (<see cref="ConsentimentoRegistrado"/>).
/// </summary>
public sealed record Perfil
{
    public required string Nome { get; init; }

    public required TipoPerfil Tipo { get; init; }

    public DateTimeOffset DataCriacao { get; init; } = DateTimeOffset.UtcNow;

    public string Autor { get; init; } = "sistema";

    /// <summary>Para perfis customizados, indica se o consentimento já foi registrado.</summary>
    public bool ConsentimentoRegistrado { get; init; }

    public required IReadOnlyList<SelecaoAcao> Selecoes { get; init; }

    public bool Customizado => Tipo == TipoPerfil.Customizado;

    /// <summary>Customizado só pode ser aplicado após consentimento registrado.</summary>
    public bool PodeAplicar => !Customizado || ConsentimentoRegistrado;
}
