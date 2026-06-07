using System.Globalization;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>Intervalo numérico fechado [Minimo, Maximo].</summary>
public sealed record FaixaNumerica
{
    public FaixaNumerica(double minimo, double maximo)
    {
        if (maximo < minimo)
        {
            throw new ArgumentException(
                $"Faixa inválida: máximo ({maximo}) menor que mínimo ({minimo}).", nameof(maximo));
        }

        Minimo = minimo;
        Maximo = maximo;
    }

    public double Minimo { get; }

    public double Maximo { get; }

    public bool Contem(double valor) => valor >= Minimo && valor <= Maximo;

    /// <summary>Verdadeiro se este intervalo está inteiramente contido em <paramref name="externa"/>.</summary>
    public bool EstaContidaEm(FaixaNumerica externa) =>
        Minimo >= externa.Minimo && Maximo <= externa.Maximo;

    public override string ToString() =>
        $"[{Minimo.ToString(CultureInfo.InvariantCulture)}, {Maximo.ToString(CultureInfo.InvariantCulture)}]";
}
