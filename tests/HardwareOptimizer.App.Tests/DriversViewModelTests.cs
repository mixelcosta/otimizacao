using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.Tests;

public class DriversViewModelTests
{
    // ── Popular ──────────────────────────────────────────────────────────────

    [Fact]
    public void Popular_ComDrivers_PopulaListaOrdenadaPorDescricao()
    {
        var vm = new DriversViewModel();
        vm.Popular([
            new InfoDriver { HardwareId = "PCI\\VEN_10EC", Descricao = "Realtek Audio", Status = StatusDriver.Desconhecido },
            new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA RTX 4080", Status = StatusDriver.Atualizado },
        ]);

        Assert.True(vm.TemResultados);
        Assert.Equal(2, vm.Drivers.Count);
        Assert.Equal("NVIDIA RTX 4080", vm.Drivers[0].Descricao);
        Assert.Equal("Realtek Audio", vm.Drivers[1].Descricao);
    }

    [Fact]
    public void Popular_ListaVazia_TemResultadosFalso()
    {
        var vm = new DriversViewModel();
        vm.Popular([]);

        Assert.False(vm.TemResultados);
        Assert.Empty(vm.Drivers.ToList());
    }

    [Fact]
    public void Popular_ComDoisDrivers_StatusTextContaContagem()
    {
        var vm = new DriversViewModel();
        vm.Popular([
            new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA", Status = StatusDriver.Atualizado },
            new InfoDriver { HardwareId = "PCI\\VEN_10EC", Descricao = "Realtek", Status = StatusDriver.Desconhecido },
        ]);

        Assert.Contains("2", vm.StatusText);
    }

    [Fact]
    public void Popular_ListaVazia_StatusTextIndicaNenhumDispositivo()
    {
        var vm = new DriversViewModel();
        vm.Popular([]);

        Assert.Contains("Nenhum", vm.StatusText);
    }

    [Fact]
    public void Popular_TwiceInRow_SubstituiListaAnterior()
    {
        var vm = new DriversViewModel();
        vm.Popular([
            new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA", Status = StatusDriver.Atualizado },
        ]);
        vm.Popular([
            new InfoDriver { HardwareId = "USB\\VID_046D", Descricao = "Logitech", Status = StatusDriver.Desconhecido },
            new InfoDriver { HardwareId = "USB\\VID_045E", Descricao = "Microsoft", Status = StatusDriver.Desconhecido },
        ]);

        Assert.Equal(2, vm.Drivers.Count);
        Assert.Equal("Logitech", vm.Drivers[0].Descricao);
    }
}

public class InfoDriverViewModelTests
{
    [Fact]
    public void Status_Atualizado_TextoECorCorretos()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "NVIDIA",
            Status = StatusDriver.Atualizado,
        });

        Assert.Equal("ATUALIZADO", vm.StatusTexto);
    }

    [Fact]
    public void Status_AtualizacaoDisponivel_TextoCorreto()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "NVIDIA",
            VersaoDisponivel = "999.0",
            Status = StatusDriver.AtualizacaoDisponivel,
        });

        Assert.Equal("ATUALIZAÇÃO", vm.StatusTexto);
    }

    [Fact]
    public void Status_Desconhecido_TextoTraco()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_FFFF",
            Descricao = "Dispositivo",
            Status = StatusDriver.Desconhecido,
        });

        Assert.Equal("—", vm.StatusTexto);
    }

    [Fact]
    public void Fabricante_Nulo_ExibeTraco()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "Test",
            Fabricante = null,
            Status = StatusDriver.Desconhecido,
        });

        Assert.Equal("—", vm.Fabricante);
    }

    [Fact]
    public void Fabricante_Vazio_ExibeTraco()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "Test",
            Fabricante = "   ",
            Status = StatusDriver.Desconhecido,
        });

        Assert.Equal("—", vm.Fabricante);
    }

    [Fact]
    public void VersaoAtual_Nula_ExibeTraco()
    {
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "Test",
            VersaoAtual = null,
            Status = StatusDriver.Desconhecido,
        });

        Assert.Equal("—", vm.VersaoAtual);
    }

    [Fact]
    public void HwidCurto_HwidComMaisDe50Chars_TruncadoComReticencias()
    {
        var hwid = "PCI\\VEN_10DE&DEV_2204&SUBSYS_372E1458&REV_A1\\4&1234567890&0&00E0";
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = hwid,
            Descricao = "Test",
            Status = StatusDriver.Desconhecido,
        });

        Assert.True(vm.HwidCurto.Length <= 50);
        Assert.EndsWith("…", vm.HwidCurto);
    }

    [Fact]
    public void HwidCurto_HwidCurto_NaoTrunca()
    {
        var hwid = "USB\\VID_8087";
        var vm = new InfoDriverViewModel(new InfoDriver
        {
            HardwareId = hwid,
            Descricao = "Intel USB",
            Status = StatusDriver.Desconhecido,
        });

        Assert.Equal(hwid, vm.HwidCurto);
    }
}
