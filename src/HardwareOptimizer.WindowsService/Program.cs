using HardwareOptimizer.WindowsService;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(o => o.ServiceName = "OtimizeBuilderMonitor")
    .ConfigureServices(services =>
    {
        services.AddHostedService<MonitorWorker>();
    })
    .Build();

await host.RunAsync();
