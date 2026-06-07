using System.Globalization;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Parâmetro de uma ação do catálogo. Cada parâmetro sabe validar um valor
/// proposto sob um determinado perfil, aplicando as regras invariantes do
/// documento. A implementação é fechada (apenas os tipos deste arquivo).
/// </summary>
public abstract class Parametro
{
    protected Parametro(string nome, string descricao)
    {
        Nome = nome;
        Descricao = descricao;
    }

    public string Nome { get; }

    public string Descricao { get; }

    /// <summary>Valor padrão usado pelo perfil seguro (sempre dentro da faixa segura).</summary>
    public abstract string ValorPadraoSeguro { get; }

    /// <summary>Valida o valor proposto considerando o tipo de perfil.</summary>
    public abstract ResultadoParametro Validar(string valorBruto, TipoPerfil perfil);

    /// <summary>Checa a coerência interna do próprio parâmetro (sanidade do catálogo).</summary>
    public abstract Resultado VerificarCoerencia();
}

/// <summary>
/// Parâmetro numérico com três níveis de controle:
/// faixa segura (padrão recomendado), faixa permitida (mais ampla, perfil
/// customizado) e limite absoluto (teto técnico que NENHUM perfil ultrapassa).
/// </summary>
public sealed class ParametroNumerico : Parametro
{
    public ParametroNumerico(
        string nome,
        string descricao,
        FaixaNumerica faixaSegura,
        FaixaNumerica faixaPermitida,
        double limiteAbsoluto,
        double padraoSeguro,
        string? unidade = null)
        : base(nome, descricao)
    {
        FaixaSegura = faixaSegura;
        FaixaPermitida = faixaPermitida;
        LimiteAbsoluto = limiteAbsoluto;
        PadraoSeguro = padraoSeguro;
        Unidade = unidade;
    }

    public FaixaNumerica FaixaSegura { get; }

    public FaixaNumerica FaixaPermitida { get; }

    public double LimiteAbsoluto { get; }

    public double PadraoSeguro { get; }

    public string? Unidade { get; }

    public override string ValorPadraoSeguro => PadraoSeguro.ToString(CultureInfo.InvariantCulture);

    public override ResultadoParametro Validar(string valorBruto, TipoPerfil perfil)
    {
        if (!double.TryParse(valorBruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
        {
            return ResultadoParametro.Rejeitado(Nome, valorBruto, $"'{valorBruto}' não é um número válido.");
        }

        // 1) Limite absoluto: bloqueio rígido. Vale para QUALQUER perfil.
        if (valor > LimiteAbsoluto)
        {
            return ResultadoParametro.BloqueioLimiteAbsoluto(
                Nome, valorBruto, $"{Formatar(valor)} > limite absoluto {Formatar(LimiteAbsoluto)}.");
        }

        // 2) Fora da faixa permitida (abaixo do mínimo ou acima do máximo): inválido.
        if (!FaixaPermitida.Contem(valor))
        {
            return ResultadoParametro.Rejeitado(
                Nome, valorBruto, $"{Formatar(valor)} fora da faixa permitida {FaixaPermitida}.");
        }

        // 3) Perfil seguro só aceita valores dentro da faixa segura.
        if (perfil == TipoPerfil.Seguro && !FaixaSegura.Contem(valor))
        {
            return ResultadoParametro.Rejeitado(
                Nome, valorBruto,
                $"Perfil seguro exige faixa segura {FaixaSegura}; {Formatar(valor)} está fora dela.");
        }

        // 4) Dentro da permitida, fora da segura: risco assumido (apenas perfil customizado chega aqui).
        if (!FaixaSegura.Contem(valor))
        {
            return ResultadoParametro.RiscoAssumido(
                Nome, valorBruto, $"{Formatar(valor)} fora da faixa segura {FaixaSegura}.");
        }

        return ResultadoParametro.Aceito(Nome, valorBruto);
    }

    public override Resultado VerificarCoerencia()
    {
        var erros = new List<string>();

        if (!FaixaSegura.EstaContidaEm(FaixaPermitida))
        {
            erros.Add($"Parâmetro '{Nome}': faixa segura {FaixaSegura} não está contida na permitida {FaixaPermitida}.");
        }

        if (FaixaPermitida.Maximo > LimiteAbsoluto)
        {
            erros.Add($"Parâmetro '{Nome}': máximo da faixa permitida {Formatar(FaixaPermitida.Maximo)} ultrapassa o limite absoluto {Formatar(LimiteAbsoluto)}.");
        }

        if (!FaixaSegura.Contem(PadraoSeguro))
        {
            erros.Add($"Parâmetro '{Nome}': padrão seguro {Formatar(PadraoSeguro)} fora da faixa segura {FaixaSegura}.");
        }

        return erros.Count == 0 ? Resultado.Ok() : Resultado.Falhar(erros);
    }

    private static string Formatar(double valor) => valor.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Parâmetro cujo valor deve constar em uma lista branca fechada de opções
/// seguras (ex.: nome de serviço passível de ser desativado).
/// </summary>
public sealed class ParametroListaBranca : Parametro
{
    private readonly IReadOnlyList<string> _valoresSeguros;

    public ParametroListaBranca(
        string nome,
        string descricao,
        IReadOnlyList<string> valoresSeguros,
        string padraoSeguro)
        : base(nome, descricao)
    {
        _valoresSeguros = valoresSeguros;
        PadraoSeguro = padraoSeguro;
    }

    public IReadOnlyList<string> ValoresSeguros => _valoresSeguros;

    public string PadraoSeguro { get; }

    public override string ValorPadraoSeguro => PadraoSeguro;

    public override ResultadoParametro Validar(string valorBruto, TipoPerfil perfil)
    {
        _ = perfil; // a lista branca vale igualmente para perfil seguro e customizado.

        return _valoresSeguros.Contains(valorBruto, StringComparer.OrdinalIgnoreCase)
            ? ResultadoParametro.Aceito(Nome, valorBruto)
            : ResultadoParametro.Rejeitado(
                Nome, valorBruto, $"'{valorBruto}' não consta na lista segura.");
    }

    public override Resultado VerificarCoerencia()
    {
        if (_valoresSeguros.Count == 0)
        {
            return Resultado.Falhar($"Parâmetro '{Nome}': lista branca vazia.");
        }

        return _valoresSeguros.Contains(PadraoSeguro, StringComparer.OrdinalIgnoreCase)
            ? Resultado.Ok()
            : Resultado.Falhar($"Parâmetro '{Nome}': padrão seguro '{PadraoSeguro}' não consta na lista branca.");
    }
}
