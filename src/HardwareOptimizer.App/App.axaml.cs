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
            var licenca = new ServicoLicencaLemonSqueezy(
                NullLogger<ServicoLicencaLemonSqueezy>.Instance);

            desktop.MainWindow = new ShellWindow
            {
                DataContext = new ShellViewModel(roteador, licenca),
            };

            // Valida a licença em background assim que o app sobe
            _ = Task.Run(() => licenca.ValidarOnlineAsync());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
