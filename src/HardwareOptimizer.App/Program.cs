using Avalonia;

namespace HardwareOptimizer.App;

internal static class Program
{
    // Ponto de entrada da UI desktop (Avalonia).
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
