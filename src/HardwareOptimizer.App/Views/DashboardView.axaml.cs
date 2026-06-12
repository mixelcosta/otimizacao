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

        vm.HistCpuTemp.CollectionChanged += (_, _) =>
        {
            if (vm.HistCpuTemp.Count > 0)
                ChartCpuTemp.AdicionarValor(vm.HistCpuTemp[^1]);
        };
        vm.HistGpuTemp.CollectionChanged += (_, _) =>
        {
            if (vm.HistGpuTemp.Count > 0)
                ChartGpuTemp.AdicionarValor(vm.HistGpuTemp[^1]);
        };
        vm.HistCpuClock.CollectionChanged += (_, _) =>
        {
            if (vm.HistCpuClock.Count > 0)
                ChartCpuClock.AdicionarValor(vm.HistCpuClock[^1]);
        };
        vm.HistGpuClock.CollectionChanged += (_, _) =>
        {
            if (vm.HistGpuClock.Count > 0)
                ChartGpuClock.AdicionarValor(vm.HistGpuClock[^1]);
        };
    }
}
