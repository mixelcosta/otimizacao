using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;
using Xunit;

namespace HardwareOptimizer.App.Tests;

public sealed class DashboardViewModelTests
{
    private static IRoteadorIpc RoteadorVazio() =>
        new RoteadorFake(_ => RespostaIpc.Ok("1", "ok"));

    private static Sensor S(string nome, TipoSensor tipo, double valor) =>
        new() { Nome = nome, Tipo = tipo, Valor = valor, Unidade = "" };

    private static LeituraSensores Leitura(params Sensor[] sensores) =>
        new() { Sensores = sensores };

    [Fact]
    public void AtualizarSensores_com_cpu_temp_atualiza_CpuTemp()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[CPU] Ryzen / Core 0", TipoSensor.Temperatura, 65.0)));

        Assert.Equal("65°C", vm.CpuTemp);
    }

    [Fact]
    public void AtualizarSensores_cpu_temp_acima_85_nivel_alerta_2()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[CPU] Core 0", TipoSensor.Temperatura, 90.0)));

        Assert.Equal(2, vm.CpuNivelAlerta);
    }

    [Fact]
    public void AtualizarSensores_cpu_temp_acima_70_nivel_alerta_1()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[CPU] Core 0", TipoSensor.Temperatura, 75.0)));

        Assert.Equal(1, vm.CpuNivelAlerta);
    }

    [Fact]
    public void AtualizarSensores_cpu_temp_abaixo_70_nivel_alerta_0()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[CPU] Core 0", TipoSensor.Temperatura, 60.0)));

        Assert.Equal(0, vm.CpuNivelAlerta);
    }

    [Fact]
    public void AtualizarSensores_extrai_max_de_multiplos_cores()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(
            S("[CPU] Core 0", TipoSensor.Temperatura, 55.0),
            S("[CPU] Core 1", TipoSensor.Temperatura, 72.0),
            S("[CPU] Core 2", TipoSensor.Temperatura, 68.0)));

        Assert.Equal("72°C", vm.CpuTemp);
    }

    [Fact]
    public void AtualizarSensores_gpu_temp_nivel_alerta_2_acima_85()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[GPU] RTX / GPU Core", TipoSensor.Temperatura, 87.0)));

        Assert.Equal(2, vm.GpuNivelAlerta);
    }

    [Fact]
    public void AtualizarSensores_gpu_temp_nivel_alerta_1_acima_75()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[GPU] RTX / GPU Core", TipoSensor.Temperatura, 80.0)));

        Assert.Equal(1, vm.GpuNivelAlerta);
    }

    [Fact]
    public void AtualizarSensores_ram_uso_gb_atualiza_RamUso()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[RAM] DDR4 / Memory Used", TipoSensor.Outro, 8.5)));

        Assert.Contains("GB", vm.RamUso, StringComparison.Ordinal);
        Assert.Contains("8", vm.RamUso, StringComparison.Ordinal);
        Assert.Contains("5", vm.RamUso, StringComparison.Ordinal);
    }

    [Fact]
    public void AtualizarSensores_ram_pct_nivel_alerta_2_acima_90()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[RAM] Total / Virtual Memory Load", TipoSensor.Carga, 92.0)));

        Assert.Equal(2, vm.RamNivelAlerta);
    }

    [Fact]
    public void AtualizarSensores_storage_read_atualiza_StorageRead()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura(S("[STORAGE] SSD / Read Rate", TipoSensor.Outro, 250.0)));

        Assert.Contains("MB/s", vm.StorageRead, StringComparison.Ordinal);
        Assert.Contains("250", vm.StorageRead, StringComparison.Ordinal);
    }

    [Fact]
    public void AtualizarSensores_sem_sensores_mantem_tracejado()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        vm.AtualizarSensores(Leitura());

        Assert.Equal("--", vm.CpuTemp);
        Assert.Equal("--", vm.GpuTemp);
        Assert.Equal("--", vm.RamUso);
    }

    [Fact]
    public void AtualizarSensores_historico_cpu_cresce_ate_60()
    {
        var vm = new DashboardViewModel(RoteadorVazio());

        for (int i = 0; i < 70; i++)
            vm.AtualizarSensores(Leitura(S("[CPU] Core 0", TipoSensor.Temperatura, 50.0 + i)));

        Assert.Equal(60, vm.HistCpuTemp.Count);
    }

    private sealed class RoteadorFake : IRoteadorIpc
    {
        private readonly Func<RequisicaoIpc, RespostaIpc> _responder;
        public RoteadorFake(Func<RequisicaoIpc, RespostaIpc> responder) => _responder = responder;
        public Task<RespostaIpc> TratarAsync(RequisicaoIpc requisicao, CancellationToken cancellationToken = default) =>
            Task.FromResult(_responder(requisicao));
    }
}
