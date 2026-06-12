using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.App.Views;
using HardwareOptimizer.Features.Licensing;
using HardwareOptimizer.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    [SupportedOSPlatform("windows")]
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var roteador = new RoteadorIpc();
            var licenca = new ServicoLicencaLocal(
                NullLogger<ServicoLicencaLocal>.Instance);

            desktop.MainWindow = new ShellWindow
            {
                DataContext = new ShellViewModel(roteador, licenca),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
