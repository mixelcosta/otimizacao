using System.Runtime.Versioning;
using Avalonia.Controls;
using HardwareOptimizer.App.ViewModels;

namespace HardwareOptimizer.App.Views;

public partial class ShellWindow : Window
{
    public ShellWindow() => InitializeComponent();

    [SupportedOSPlatform("windows")]
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is ShellViewModel vm)
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (hwnd != nint.Zero)
                vm.RegistrarNotificacao(hwnd);
        }
    }
}
