using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class SensoresTests : IDisposable
{
    private readonly string _dir;

    public SensoresTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwopt-sensores-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    private void Escrever(string caminhoRelativo, string conteudo)
    {
        var completo = Path.Combine(_dir, caminhoRelativo);
        Directory.CreateDirectory(Path.GetDirectoryName(completo)!);
        File.WriteAllText(completo, conteudo);
    }

    [Fact]
    public async Task LeitorLinux_le_hwmon_e_clock_de_arquivos_fabricados()
    {
        Escrever("hwmon/hwmon0/name", "coretemp");
        Escrever("hwmon/hwmon0/temp1_input", "45000");   // 45 °C
        Escrever("hwmon/hwmon0/temp1_label", "Core 0");
        Escrever("hwmon/hwmon0/fan1_input", "1200");      // 1200 RPM
        Escrever("hwmon/hwmon0/in1_input", "1200");       // 1.2 V
        Escrever("hwmon/hwmon0/power1_input", "15000000"); // 15 W
        Escrever("cpu/cpu0/cpufreq/scaling_cur_freq", "3600000"); // 3600 MHz
        Escrever("cpu/cpu1/cpufreq/scaling_cur_freq", "4000000"); // 4000 MHz

        var leitor = new LeitorSensoresLinux(
            baseHwmon: Path.Combine(_dir, "hwmon"), baseCpu: Path.Combine(_dir, "cpu"));
        var leitura = await leitor.LerAsync();

        var temp = leitura.PorTipo(TipoSensor.Temperatura).Single();
        Assert.Equal("Core 0", temp.Nome);
        Assert.Equal(45, temp.Valor);
        Assert.Equal("°C", temp.Unidade);

        Assert.Equal(1200, leitura.PorTipo(TipoSensor.Fan).Single().Valor);
        Assert.Equal(1.2, leitura.PorTipo(TipoSensor.Voltagem).Single().Valor);
        Assert.Equal(15, leitura.PorTipo(TipoSensor.Potencia).Single().Valor);
        Assert.Equal(4000, leitura.PorTipo(TipoSensor.Clock).Single().Valor); // maior entre cpu0/cpu1
    }

    [Fact]
    public async Task LeitorLinux_sem_hwmon_retorna_leitura_vazia_sem_lancar()
    {
        var leitor = new LeitorSensoresLinux(
            baseHwmon: Path.Combine(_dir, "inexistente"), baseCpu: Path.Combine(_dir, "inexistente"));

        var leitura = await leitor.LerAsync();

        Assert.Empty(leitura.Sensores);
    }

    [Fact]
    public void TemperaturaMaxC_retorna_a_maior_temperatura()
    {
        var leitura = new LeituraSensores
        {
            Sensores = new[]
            {
                new Sensor { Nome = "a", Tipo = TipoSensor.Temperatura, Valor = 45, Unidade = "°C" },
                new Sensor { Nome = "b", Tipo = TipoSensor.Temperatura, Valor = 71, Unidade = "°C" },
                new Sensor { Nome = "fan", Tipo = TipoSensor.Fan, Valor = 1500, Unidade = "RPM" },
            },
        };

        Assert.Equal(71, leitura.TemperaturaMaxC);
    }

    [Fact]
    public async Task Servico_delega_ao_leitor_informado()
    {
        var esperada = new LeituraSensores
        {
            Sensores = new[] { new Sensor { Nome = "x", Tipo = TipoSensor.Temperatura, Valor = 50, Unidade = "°C" } },
        };

        var leitura = await new ServicoSensores(new LeitorFake(esperada)).LerAsync();

        Assert.Same(esperada, leitura);
    }

    [Fact]
    public async Task Servico_real_no_linux_nao_lanca()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var leitura = await new ServicoSensores().LerAsync();
        Assert.NotNull(leitura);
    }

    private sealed class LeitorFake : ILeitorSensores
    {
        private readonly LeituraSensores _leitura;

        public LeitorFake(LeituraSensores leitura) => _leitura = leitura;

        public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Linux;

        public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_leitura);
    }
}
