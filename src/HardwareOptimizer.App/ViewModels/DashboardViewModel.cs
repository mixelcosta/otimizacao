using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IRoteadorIpc _agente;

    public DashboardViewModel(IRoteadorIpc agente)
    {
        _agente = agente;
    }

    // CPU
    [ObservableProperty] private string _cpuTemp = "--";
    [ObservableProperty] private string _cpuClock = "--";
    [ObservableProperty] private string _cpuUso = "--";
    [ObservableProperty] private int _cpuNivelAlerta;
    [ObservableProperty] private string _cpuChartLabel = "CPU Temp";

    // GPU
    [ObservableProperty] private string _gpuTemp = "--";
    [ObservableProperty] private string _gpuClock = "--";
    [ObservableProperty] private string _gpuVram = "--";
    [ObservableProperty] private int _gpuNivelAlerta;
    [ObservableProperty] private string _gpuChartLabel = "GPU Temp";

    // RAM
    [ObservableProperty] private string _ramUso = "--";
    [ObservableProperty] private string _ramPorcentagem = "--";
    [ObservableProperty] private int _ramNivelAlerta;

    // Storage
    [ObservableProperty] private string _storageRead = "--";
    [ObservableProperty] private string _storageWrite = "--";
    [ObservableProperty] private int _storageNivelAlerta;

    // Histórico para gráficos (alimentado pelo ShellViewModel via callback)
    public ObservableCollection<double> HistCpuTemp { get; } = new();
    public ObservableCollection<double> HistGpuTemp { get; } = new();
    public ObservableCollection<double> HistCpuClock { get; } = new();
    public ObservableCollection<double> HistGpuClock { get; } = new();

    /// <summary>Chamado pelo ShellViewModel a cada leitura de sensores.</summary>
    public void AtualizarSensores(LeituraSensores leitura)
    {
        // Temperatura: pega o valor máximo (sensor mais quente = mais conservador)
        var cpuTempVal  = ExtrairMax(leitura, TipoSensor.Temperatura, "[CPU]");
        var gpuTempVal  = ExtrairMax(leitura, TipoSensor.Temperatura, "[GPU]");
        var cpuCargaVal = ExtrairMax(leitura, TipoSensor.Carga, "[CPU]");
        var gpuCargaVal = ExtrairMax(leitura, TipoSensor.Carga, "[GPU]");

        // Clock: pega o valor máximo (core mais rápido = mais representativo)
        var cpuClockVal = ExtrairMax(leitura, TipoSensor.Clock, "[CPU]");
        var gpuClockVal = ExtrairMax(leitura, TipoSensor.Clock, "[GPU]");

        // RAM: GB usados (Data) + % via Load
        var ramUsadoVal = ExtrairSensor(leitura, TipoSensor.Outro, "Memory Used");
        var ramPctVal   = ExtrairSensor(leitura, TipoSensor.Carga, "[RAM]");

        // Storage: throughput Read Rate / Write Rate
        var storageReadVal  = ExtrairSensor(leitura, TipoSensor.Outro, "Read Rate");
        var storageWriteVal = ExtrairSensor(leitura, TipoSensor.Outro, "Write Rate");

        if (cpuTempVal.HasValue)
        {
            CpuTemp = $"{cpuTempVal.Value:F0}°C";
            CpuNivelAlerta = cpuTempVal.Value switch { > 85 => 2, > 70 => 1, _ => 0 };
            CpuChartLabel = "CPU Temp";
            AdicionarHistorico(HistCpuTemp, cpuTempVal.Value);
        }
        else if (cpuCargaVal.HasValue)
        {
            // Fallback: CPU temp não disponível sem admin — usa uso % do processador
            CpuTemp = $"{cpuCargaVal.Value:F0}%";
            CpuNivelAlerta = cpuCargaVal.Value switch { > 90 => 2, > 70 => 1, _ => 0 };
            CpuChartLabel = "CPU Load";
            AdicionarHistorico(HistCpuTemp, cpuCargaVal.Value);
        }

        if (gpuTempVal.HasValue)
        {
            GpuTemp = $"{gpuTempVal.Value:F0}°C";
            GpuNivelAlerta = gpuTempVal.Value switch { > 85 => 2, > 75 => 1, _ => 0 };
            GpuChartLabel = "GPU Temp";
            AdicionarHistorico(HistGpuTemp, gpuTempVal.Value);
        }
        else if (gpuCargaVal.HasValue)
        {
            // Fallback: GPU temp não disponível sem admin — usa utilização % via perf counters
            GpuTemp = $"{gpuCargaVal.Value:F0}%";
            GpuNivelAlerta = gpuCargaVal.Value switch { > 90 => 2, > 70 => 1, _ => 0 };
            GpuChartLabel = "GPU Load";
            AdicionarHistorico(HistGpuTemp, gpuCargaVal.Value);
        }

        if (cpuClockVal.HasValue)
        {
            CpuClock = $"{cpuClockVal.Value:F0} MHz";
            AdicionarHistorico(HistCpuClock, cpuClockVal.Value);
        }

        if (gpuClockVal.HasValue)
        {
            GpuClock = $"{gpuClockVal.Value:F0} MHz";
            AdicionarHistorico(HistGpuClock, gpuClockVal.Value);
        }

        if (ramUsadoVal.HasValue)
            RamUso = $"{ramUsadoVal.Value:F1} GB";

        if (ramPctVal.HasValue)
        {
            RamPorcentagem = $"{ramPctVal.Value:F0}%";
            RamNivelAlerta = ramPctVal.Value switch { > 90 => 2, > 75 => 1, _ => 0 };
        }

        if (storageReadVal.HasValue)  StorageRead  = $"{storageReadVal.Value:F1} MB/s";
        if (storageWriteVal.HasValue) StorageWrite = $"{storageWriteVal.Value:F1} MB/s";
    }

    private static double? ExtrairSensor(LeituraSensores leitura, TipoSensor tipo, string fragmento)
    {
        return leitura.Sensores
            .Where(s => s.Tipo == tipo &&
                        s.Nome.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Nome)
            .FirstOrDefault()?.Valor;
    }

    private static double? ExtrairMax(LeituraSensores leitura, TipoSensor tipo, string fragmento)
    {
        var valores = leitura.Sensores
            .Where(s => s.Tipo == tipo &&
                        s.Nome.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Valor)
            .ToList();
        return valores.Count > 0 ? valores.Max() : null;
    }

    private static void AdicionarHistorico(ObservableCollection<double> hist, double valor)
    {
        while (hist.Count >= 60)
            hist.RemoveAt(0);
        hist.Add(valor);
    }
}
