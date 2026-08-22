using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Manutencao;

namespace HardwareOptimizer.Features.Manutencao.Tests;

/// <summary>Cobre a I/O & Edge-Case Matrix da spec-2-1-deteccao-pasta-termica-ressecada.</summary>
public class DetectorPastaTermicaTests
{
    private static LeituraSensores Leitura(double? temperaturaC) => new()
    {
        Sensores = temperaturaC is null
            ? Array.Empty<Sensor>()
            : new[]
            {
                new Sensor { Nome = "CPU Package", Tipo = TipoSensor.Temperatura, Valor = temperaturaC.Value, Unidade = "°C" },
            },
    };

    // ── Matriz: temperatura idle anormalmente alta ──────────────────────────

    [Fact]
    public void Detectar_TemperaturaIdleAcimaDoLimiar_RetornaAchadoComTemperaturasECusto()
    {
        var idle = Leitura(60.0);
        var carga = Leitura(72.0);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.NotNull(achado);
        Assert.Equal(60.0, achado!.TemperaturaIdleC);
        Assert.Equal(72.0, achado.TemperaturaCargaC);
        Assert.NotNull(achado.Custo);
        Assert.True(achado.Custo.ValorEstimado > 0);
        Assert.False(string.IsNullOrWhiteSpace(achado.Justificativa));
    }

    [Fact]
    public void Detectar_TemperaturaIdleExatamenteNoLimiar_RetornaAchado()
    {
        var idle = Leitura(DetectorPastaTermica.TemperaturaIdleSuspeitaC);
        var carga = Leitura(DetectorPastaTermica.TemperaturaIdleSuspeitaC + 10);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.NotNull(achado);
    }

    // ── Matriz: temperatura idle normal ──────────────────────────────────────

    [Fact]
    public void Detectar_TemperaturaIdleNormal_RetornaNull()
    {
        var idle = Leitura(45.0);
        var carga = Leitura(70.0);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.Null(achado);
    }

    [Fact]
    public void Detectar_TemperaturaIdleLogoAbaixoDoLimiar_RetornaNull()
    {
        var idle = Leitura(DetectorPastaTermica.TemperaturaIdleSuspeitaC - 0.1);
        var carga = Leitura(80.0);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.Null(achado);
    }

    // ── Matriz: sensor de temperatura indisponível ───────────────────────────

    [Fact]
    public void Detectar_SemSensorDeTemperaturaEmNenhumaLeitura_RetornaNull()
    {
        var idle = Leitura(null);
        var carga = Leitura(null);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.Null(achado);
    }

    [Fact]
    public void Detectar_SemSensorDeTemperaturaNaLeituraIdle_RetornaNull()
    {
        // Mesmo com carga alta, sem leitura idle não há como julgar o sintoma
        // (a regra de decisão é toda baseada na temperatura idle) — guard
        // anti-alucinação: nunca inventa achado sem lastro real.
        var idle = Leitura(null);
        var carga = Leitura(90.0);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.Null(achado);
    }

    [Fact]
    public void Detectar_IdleAltaMasSemSensorNaLeituraDeCarga_TemperaturaCargaFicaNull()
    {
        // Corrigido na revisão independente: nenhum fallback pro valor idle —
        // um valor copiado não é uma medição real sob carga (guard anti-alucinação
        // vale pro campo inteiro, não só pra decisão do achado existir ou não).
        var idle = Leitura(60.0);
        var carga = Leitura(null);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.NotNull(achado);
        Assert.Equal(60.0, achado!.TemperaturaIdleC);
        Assert.Null(achado.TemperaturaCargaC);
    }

    // ── Matriz: leitura inválida (NaN/±Infinity) ─────────────────────────────

    /// <summary>
    /// Achado da revisão independente (Edge Case Hunter): `NaN &lt; limiar`
    /// avalia `false` em IEEE754, então uma leitura idle `NaN` (sensor com
    /// falha, mas não `null`) fazia a comparação original passar batido e
    /// produzia um achado fabricado — viola o guard anti-alucinação.
    /// </summary>
    [Fact]
    public void Detectar_TemperaturaIdleNaN_RetornaNull()
    {
        var idle = Leitura(double.NaN);
        var carga = Leitura(70.0);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.Null(achado);
    }

    [Fact]
    public void Detectar_TemperaturaIdlePositiveInfinity_RetornaNull()
    {
        var idle = Leitura(double.PositiveInfinity);
        var carga = Leitura(70.0);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.Null(achado);
    }

    [Fact]
    public void Detectar_TemperaturaCargaNaN_AchadoExisteMasCampoCargaFicaNull()
    {
        // A leitura sob carga é só contexto (não participa da decisão) — mas o
        // valor inválido nunca deve ser exposto como se fosse real.
        var idle = Leitura(60.0);
        var carga = Leitura(double.NaN);

        var achado = DetectorPastaTermica.Detectar(idle, carga);

        Assert.NotNull(achado);
        Assert.Equal(60.0, achado!.TemperaturaIdleC);
        Assert.Null(achado.TemperaturaCargaC);
    }

    // ── Guard de argumentos ───────────────────────────────────────────────────

    [Fact]
    public void Detectar_LeituraIdleNula_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DetectorPastaTermica.Detectar(null!, Leitura(70.0)));
    }

    [Fact]
    public void Detectar_LeituraCargaNula_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DetectorPastaTermica.Detectar(Leitura(60.0), null!));
    }
}
