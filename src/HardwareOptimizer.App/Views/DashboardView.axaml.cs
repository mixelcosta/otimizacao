using Avalonia.Controls;
using HardwareOptimizer.App.ViewModels;

namespace HardwareOptimizer.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindCharts();
    }

    private void BindCharts()
    {
        if (DataContext is not DashboardViewModel vm) return;

        vm.CpuTempAtualizada  = v => ChartCpuTemp.AdicionarValor(v);
        vm.GpuTempAtualizada  = v => ChartGpuTemp.AdicionarValor(v);
        vm.CpuClockAtualizado = v => ChartCpuClock.AdicionarValor(v);
        vm.GpuClockAtualizado = v => ChartGpuClock.AdicionarValor(v);
    }
}
