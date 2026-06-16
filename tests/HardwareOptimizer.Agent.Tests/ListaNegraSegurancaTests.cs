using HardwareOptimizer.Agent.Security;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ListaNegraSegurancaTests
{
    // ── Serviços: críticos devem ser bloqueados ───────────────────────────

    [Theory]
    [InlineData("lsass")]
    [InlineData("LSASS")]           // case-insensitive
    [InlineData("csrss")]
    [InlineData("winlogon")]
    [InlineData("services")]
    [InlineData("smss")]
    [InlineData("wininit")]
    [InlineData("rpcss")]
    [InlineData("dcomlaunch")]
    [InlineData("winmgmt")]
    [InlineData("eventlog")]
    [InlineData("mpssvc")]
    [InlineData("windefend")]
    [InlineData("wscsvc")]
    [InlineData("power")]
    [InlineData("plugplay")]
    [InlineData("dnscache")]
    [InlineData("dhcp")]
    [InlineData("nsi")]
    [InlineData("cryptsvc")]
    [InlineData("disk")]
    [InlineData("volmgr")]
    [InlineData("dwm")]
    [InlineData("bfe")]
    [InlineData("schedule")]
    [InlineData("netlogon")]
    public void Servico_critico_nao_seguro(string nome)
    {
        var svc = Svc(nome, "Manual");
        Assert.False(ListaNegraServicos.EhSeguro(svc));
    }

    [Theory]
    [InlineData("Boot")]
    [InlineData("System")]
    [InlineData("BOOT")]
    [InlineData("SYSTEM")]
    public void Servico_com_modo_critico_nao_seguro(string modoInicio)
    {
        var svc = Svc("meuservico", modoInicio);
        Assert.False(ListaNegraServicos.EhSeguro(svc));
    }

    [Theory]
    [InlineData("Spooler",   "Auto")]
    [InlineData("wuauserv",  "Auto")]
    [InlineData("SysMain",   "Auto")]
    [InlineData("WSearch",   "Auto")]
    [InlineData("Themes",    "Auto")]
    [InlineData("AudioSrv",  "Auto")]
    [InlineData("Audiosrv",  "Manual")]
    [InlineData("AppXSvc",   "Manual")]
    [InlineData("DiagTrack", "Auto")]
    public void Servico_nao_critico_seguro(string nome, string modo)
    {
        var svc = Svc(nome, modo);
        Assert.True(ListaNegraServicos.EhSeguro(svc));
    }

    // ── Programas: componentes do sistema devem ser bloqueados ───────────

    [Theory]
    [InlineData("Microsoft Visual C++ 2022 Redistributable (x64)",   "Microsoft Corporation")]
    [InlineData("Microsoft .NET Framework 4.8",                       "Microsoft Corporation")]
    [InlineData(".NET Desktop Runtime 8.0.3",                         "Microsoft Corporation")]
    [InlineData("Microsoft DirectX",                                  "Microsoft Corporation")]
    [InlineData("DirectX End-User Runtime",                           "Microsoft Corporation")]
    [InlineData("Windows SDK 10.0.22621.0",                          "Microsoft Corporation")]
    [InlineData("Update for Windows 11 (KB5034123)",                 "Microsoft Corporation")]
    [InlineData("Security Update for .NET Framework",                 "Microsoft Corporation")]
    [InlineData("Intel(R) Management Engine Components",              "Intel Corporation")]
    [InlineData("Microsoft Windows Desktop Runtime - 7.0.3 (x64)",   "Microsoft Corporation")]
    [InlineData("Microsoft Edge WebView2 Runtime",                    "Microsoft Corporation")]
    public void Programa_sistema_nao_seguro(string nome, string fabricante)
    {
        var prog = Prog(nome, fabricante, "MsiExec.exe /X{GUID}");
        Assert.False(ListaNegraProgramas.EhSeguro(prog));
    }

    [Theory]
    [InlineData("Microsoft Edge",             "Microsoft Corporation")]
    [InlineData("Microsoft Teams",            "Microsoft Corporation")]
    [InlineData("Microsoft OneDrive",         "Microsoft Corporation")]
    [InlineData("Microsoft 365 - pt-br",      "Microsoft Corporation")]
    [InlineData("Microsoft Office 2021",      "Microsoft Corporation")]
    [InlineData("Microsoft Visual Studio Code","Microsoft Corporation")]
    public void Produto_microsoft_seguro_exibido(string nome, string fabricante)
    {
        var prog = Prog(nome, fabricante, "MsiExec.exe /X{GUID}");
        Assert.True(ListaNegraProgramas.EhSeguro(prog));
    }

    [Fact]
    public void Programa_sem_uninstall_string_nao_seguro()
    {
        var prog = new ProgramaInstalado
        {
            Nome = "Spotify", Fabricante = "Spotify AB",
            UninstallString = null, QuietUninstallString = null,
        };
        Assert.False(ListaNegraProgramas.EhSeguro(prog));
    }

    [Theory]
    [InlineData("Spotify",          "Spotify AB")]
    [InlineData("Discord",          "Discord Inc.")]
    [InlineData("Steam",            "Valve Corporation")]
    [InlineData("VLC media player", "VideoLAN")]
    [InlineData("7-Zip 23.01",      "Igor Pavlov")]
    [InlineData("Google Chrome",    "Google LLC")]
    [InlineData("Adobe Acrobat",    "Adobe Inc.")]
    [InlineData("WinRAR",           "win.rar GmbH")]
    public void Programa_terceiro_seguro_exibido(string nome, string fabricante)
    {
        var prog = Prog(nome, fabricante, $"MsiExec.exe /X{{GUID-{nome}}}");
        Assert.True(ListaNegraProgramas.EhSeguro(prog));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ServicoWindows Svc(string nome, string modoInicio) =>
        new() { Nome = nome, Descricao = nome, Status = "Running", ModoInicio = modoInicio };

    private static ProgramaInstalado Prog(string nome, string fabricante, string uninstall) =>
        new() { Nome = nome, Fabricante = fabricante, UninstallString = uninstall };
}
