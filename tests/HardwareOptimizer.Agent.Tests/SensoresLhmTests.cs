using HardwareOptimizer.Agent.Sensors;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Cobre a lógica de sensores baseada em LibreHardwareMonitor com fakes — a fonte
/// real (driver) não roda aqui, mas filtragem, fallback e empacotamento sim.
/// </summary>
public sealed class SensoresLhmTests
{
    private static Sensor Temp(string nome, double c) =>
        new() { Nome = nome, Tipo = TipoSensor.Temperatura, Valor = c, Unidade = "°C" };

    // ---- LeitorSensoresLhm ----------------------------------------------

    [Fact]
    public async Task Lhm_descarta_valores_nao_finitos()
    {
        var fonte = new FonteFake(new[]
        {
            Temp("CPU", 55),
            new Sensor { Nome = "Clock", Tipo = TipoSensor.Clock, Valor = double.NaN, Unidade = "MHz" },
            new Sensor { Nome = "Volt", Tipo = TipoSensor.Voltagem, Valor = double.PositiveInfinity, Unidade = "V" },
            new Sensor { Nome = "Fan", Tipo = TipoSensor.Fan, Valor = 1200, Unidade = "RPM" },
        });
        var leitor = new LeitorSensoresLhm(fonte);

        var leitura = await leitor.LerAsync();

        Assert.Equal(2, leitura.Sensores.Count); // só os finitos
        Assert.Equal(55, leitura.TemperaturaMaxC);
        Assert.DoesNotContain(leitura.Sensores, s => double.IsNaN(s.Valor) || double.IsInfinity(s.Valor));
    }

    [Fact]
    public async Task Lhm_sem_sensores_retorna_leitura_vazia()
    {
        var leitor = new LeitorSensoresLhm(new FonteFake(Array.Empty<Sensor>()));

        var leitura = await leitor.LerAsync();

        Assert.Empty(leitura.Sensores);
        Assert.Null(leitura.TemperaturaMaxC);
    }

    [Fact]
    public void Lhm_reporta_plataforma_windows()
    {
        var leitor = new LeitorSensoresLhm(new FonteFake(Array.Empty<Sensor>()));
        Assert.Equal(SistemaOperacionalTipo.Windows, leitor.Tipo);
    }

    // ---- LeitorSensoresComposto -----------------------------------------

    [Fact]
    public async Task Composto_cai_para_o_proximo_quando_o_primeiro_vem_vazio()
    {
        var vazio = new LeitorFake(new LeituraSensores());
        var comDados = new LeitorFake(new LeituraSensores { Sensores = new[] { Temp("CPU", 60) } });
        var composto = new LeitorSensoresComposto(new ILeitorSensores[] { vazio, comDados });

        var leitura = await composto.LerAsync();

        Assert.Single(leitura.Sensores);
        Assert.Equal(60, leitura.TemperaturaMaxC);
        Assert.Equal(1, vazio.Chamadas);
        Assert.Equal(1, comDados.Chamadas);
    }

    [Fact]
    public async Task Composto_para_no_primeiro_com_dados_sem_chamar_o_resto()
    {
        var comDados = new LeitorFake(new LeituraSensores { Sensores = new[] { Temp("CPU", 50) } });
        var segundo = new LeitorFake(new LeituraSensores { Sensores = new[] { Temp("GPU", 70) } });
        var composto = new LeitorSensoresComposto(new ILeitorSensores[] { comDados, segundo });

        var leitura = await composto.LerAsync();

        Assert.Equal(50, leitura.TemperaturaMaxC); // o primeiro venceu
        Assert.Equal(1, comDados.Chamadas);
        Assert.Equal(0, segundo.Chamadas); // curto-circuito
    }

    [Fact]
    public async Task Composto_todos_vazios_retorna_vazio()
    {
        var composto = new LeitorSensoresComposto(new ILeitorSensores[]
        {
            new LeitorFake(new LeituraSensores()),
            new LeitorFake(new LeituraSensores()),
        });

        var leitura = await composto.LerAsync();

        Assert.Empty(leitura.Sensores);
    }

    [Fact]
    public void Composto_rejeita_lista_vazia()
    {
        Assert.Throws<ArgumentException>(() =>
            new LeitorSensoresComposto(Array.Empty<ILeitorSensores>()));
    }

    // ---- Fakes -----------------------------------------------------------

    private sealed class FonteFake : IFonteSensoresLhm
    {
        private readonly IReadOnlyList<Sensor> _sensores;
        public FonteFake(IReadOnlyList<Sensor> sensores) => _sensores = sensores;
        public IReadOnlyList<Sensor> Ler() => _sensores;
    }

    private sealed class LeitorFake : ILeitorSensores
    {
        private readonly LeituraSensores _leitura;
        public LeitorFake(LeituraSensores leitura) => _leitura = leitura;
        public int Chamadas { get; private set; }
        public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

        public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(_leitura);
        }
    }
}
