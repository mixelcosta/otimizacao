using System.Globalization;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Reporting;

/// <summary>
/// Monta o relatório executivo final: calcula as notas, redige o resumo e
/// consolida destaques e o antes/depois das alterações. Lógica pura.
/// </summary>
public sealed class GeradorRelatorio
{
    private readonly CalculadoraScore _calculadora;

    public GeradorRelatorio(CalculadoraScore? calculadora = null)
    {
        _calculadora = calculadora ?? new CalculadoraScore();
    }

    public RelatorioExecutivo Gerar(
        Inventario inventario,
        IReadOnlyList<ResultadoValidacao> validacoes,
        IReadOnlyList<AlteracaoResumo> alteracoes,
        ISet<Dominio> dominiosOtimizados)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        ArgumentNullException.ThrowIfNull(validacoes);
        ArgumentNullException.ThrowIfNull(alteracoes);
        ArgumentNullException.ThrowIfNull(dominiosOtimizados);

        var resultado = _calculadora.Calcular(inventario, validacoes, dominiosOtimizados);
        var regressao = validacoes.Any(v => v.Regressao);
        var classificacao = Score.Classificar(resultado.NotaFinal);

        var destaques = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"Nota final: {resultado.NotaFinal}/100 ({classificacao})"),
            string.Create(CultureInfo.InvariantCulture, $"{alteracoes.Count} alteração(ões) aplicada(s)"),
            regressao ? "Regressão detectada" : "Nenhuma regressão detectada",
        };

        var estabilidade = resultado.Obter(Dominio.Estabilidade);
        var resumo = string.Create(
            CultureInfo.InvariantCulture,
            $"Nota final {resultado.NotaFinal}/100 ({classificacao}). " +
            $"{alteracoes.Count} alteração(ões) aplicada(s); " +
            $"{(regressao ? "houve regressão" : "sem regressões")}. " +
            $"Estabilidade: {estabilidade?.Classificacao ?? "n/d"}.");

        return new RelatorioExecutivo
        {
            ResumoExecutivo = resumo,
            NotaFinal = resultado.NotaFinal,
            Classificacao = classificacao,
            Scores = resultado.Scores,
            Alteracoes = alteracoes,
            Destaques = destaques,
            RegressaoDetectada = regressao,
        };
    }
}
