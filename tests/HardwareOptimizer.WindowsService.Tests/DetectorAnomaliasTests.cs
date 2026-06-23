using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.WindowsService;
using Xunit;

namespace HardwareOptimizer.WindowsService.Tests;

public sealed class DetectorAnomaliasTests
{
    // ---- Fábrica de leituras ----

    private static LeituraSensores CpuLeitura(double uso) =>
        new() { Sensores = [Sensor("CPU Total", TipoSensor.Carga, uso)] };

    private static LeituraSensores RamLeitura(double uso) =>
        new() { Sensores = [Sensor("[RAM] Used", TipoSensor.Carga, uso)] };

    private static LeituraSensores Vazia() =>
        new() { Sensores = [] };

    private static Sensor Sensor(string nome, TipoSensor tipo, double valor) =>
        new() { Nome = nome, Tipo = tipo, Valor = valor, Unidade = "%" };

    // ---- RAM ----

    [Fact]
    public void Ram_acima_do_limiar_gera_alerta()
    {
        var detector = new DetectorAnomalias();
        var alertas = detector.Detectar(RamLeitura(DetectorAnomalias.LimiarRamPorcentagem + 1));
        Assert.Single(alertas);
        Assert.Contains("RAM", alertas[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ram_exatamente_no_limiar_nao_gera_alerta()
    {
        var detector = new DetectorAnomalias();
        var alertas = detector.Detectar(RamLeitura(DetectorAnomalias.LimiarRamPorcentagem));
        Assert.Empty(alertas);
    }

    [Fact]
    public void Ram_abaixo_do_limiar_nao_gera_alerta()
    {
        var detector = new DetectorAnomalias();
        var alertas = detector.Detectar(RamLeitura(50.0));
        Assert.Empty(alertas);
    }

    // ---- CPU spike ----

    [Fact]
    public void Cpu_spike_completo_gera_alerta_apos_janela()
    {
        var detector = new DetectorAnomalias();
        IReadOnlyList<string>? ultimo = null;
        for (int i = 0; i < DetectorAnomalias.JanelaSpikeSeg; i++)
            ultimo = detector.Detectar(CpuLeitura(DetectorAnomalias.LimiarCpuSpike + 1));

        Assert.NotNull(ultimo);
        Assert.Single(ultimo);
        Assert.Contains("CPU", ultimo![0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cpu_spike_incompleto_nao_gera_alerta()
    {
        var detector = new DetectorAnomalias();
        IReadOnlyList<string>? ultimo = null;
        for (int i = 0; i < DetectorAnomalias.JanelaSpikeSeg - 1; i++)
            ultimo = detector.Detectar(CpuLeitura(DetectorAnomalias.LimiarCpuSpike + 1));

        Assert.NotNull(ultimo);
        Assert.Empty(ultimo!);
    }

    [Fact]
    public void Cpu_abaixo_do_limiar_nao_gera_alerta_mesmo_apos_janela()
    {
        var detector = new DetectorAnomalias();
        IReadOnlyList<string>? ultimo = null;
        for (int i = 0; i < DetectorAnomalias.JanelaSpikeSeg + 2; i++)
            ultimo = detector.Detectar(CpuLeitura(DetectorAnomalias.LimiarCpuSpike - 1));

        Assert.NotNull(ultimo);
        Assert.Empty(ultimo!);
    }

    [Fact]
    public void Apos_alerta_cpu_queue_e_limpa_e_nao_gera_novo_alerta_imediato()
    {
        var detector = new DetectorAnomalias();

        // Preenche e dispara alerta
        for (int i = 0; i < DetectorAnomalias.JanelaSpikeSeg; i++)
            detector.Detectar(CpuLeitura(DetectorAnomalias.LimiarCpuSpike + 1));

        // Leitura seguinte após reset — janela foi limpa, não deve disparar novamente
        var alertas = detector.Detectar(CpuLeitura(DetectorAnomalias.LimiarCpuSpike + 1));
        Assert.Empty(alertas);
    }

    // ---- Sem sensores ----

    [Fact]
    public void Leitura_vazia_nao_gera_alerta()
    {
        var detector = new DetectorAnomalias();
        var alertas = detector.Detectar(Vazia());
        Assert.Empty(alertas);
    }

    // ---- ExtrairValor ----

    [Fact]
    public void ExtrairValor_retorna_null_quando_sensor_nao_encontrado()
    {
        var leitura = Vazia();
        var resultado = DetectorAnomalias.ExtrairValor(leitura, TipoSensor.Carga, "CPU Total");
        Assert.Null(resultado);
    }

    [Fact]
    public void ExtrairValor_e_case_insensitive()
    {
        var leitura = new LeituraSensores
        {
            Sensores = [Sensor("cpu total", TipoSensor.Carga, 42.0)]
        };
        var resultado = DetectorAnomalias.ExtrairValor(leitura, TipoSensor.Carga, "CPU Total");
        Assert.Equal(42.0, resultado);
    }
}
