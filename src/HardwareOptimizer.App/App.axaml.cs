using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.App.Views;
using HardwareOptimizer.Ipc;

namespace HardwareOptimizer.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // App em processo: a UI fala com o agente pelo roteador local
            // (a mesma API exposta por named pipe quando UI e agente são separados).
            var roteador = new RoteadorIpc();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(roteador),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
