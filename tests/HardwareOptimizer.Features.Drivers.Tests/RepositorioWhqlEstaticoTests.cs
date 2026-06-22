using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Drivers;

namespace HardwareOptimizer.Features.Drivers.Tests;

public class RepositorioWhqlEstaticoTests
{
    private readonly RepositorioWhqlEstatico _repo = new();

    // ── ExtrairPrefixo ──────────────────────────────────────────────────────

    [Fact]
    public void ExtrairPrefixo_HwidComAmpersand_RetornaTrechoAntes()
    {
        var prefixo = RepositorioWhqlEstatico.ExtrairPrefixo("PCI\\VEN_10DE&DEV_2204&SUBSYS_372E1458");
        Assert.Equal("PCI\\VEN_10DE", prefixo);
    }

    [Fact]
    public void ExtrairPrefixo_HwidSemAmpersand_RetornaNull()
    {
        var prefixo = RepositorioWhqlEstatico.ExtrairPrefixo("USB\\VID_8087");
        Assert.Null(prefixo);
    }

    [Fact]
    public void ExtrairPrefixo_StringVazia_RetornaNull()
    {
        var prefixo = RepositorioWhqlEstatico.ExtrairPrefixo(string.Empty);
        Assert.Null(prefixo);
    }

    // ── ConsultarAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ConsultarAsync_HwidNvidiaComSufixo_EncontraPorPrefixo()
    {
        var resultado = await _repo.ConsultarAsync("PCI\\VEN_10DE&DEV_2204&SUBSYS_372E1458");

        Assert.NotNull(resultado);
        Assert.Equal("NVIDIA", resultado.Fabricante);
        Assert.Equal("572.83", resultado.VersaoDisponivel);
        Assert.True(resultado.CertificadoWhql);
    }

    [Fact]
    public async Task ConsultarAsync_HwidAmd_EncontraFabricanteAmd()
    {
        var resultado = await _repo.ConsultarAsync("PCI\\VEN_1002&DEV_744C");

        Assert.NotNull(resultado);
        Assert.Equal("AMD", resultado.Fabricante);
    }

    [Fact]
    public async Task ConsultarAsync_HwidRealtek_EncontraRealtek()
    {
        var resultado = await _repo.ConsultarAsync("PCI\\VEN_10EC&DEV_8168&SUBSYS_85011043");

        Assert.NotNull(resultado);
        Assert.Contains("Realtek", resultado.Fabricante);
    }

    [Fact]
    public async Task ConsultarAsync_HwidNaoMapeado_RetornaNull()
    {
        var resultado = await _repo.ConsultarAsync("PCI\\VEN_FFFF&DEV_0000");
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarAsync_HwidLogitechSemVersao_VersaoDisponivelNula()
    {
        var resultado = await _repo.ConsultarAsync("USB\\VID_046D&PID_C534");

        Assert.NotNull(resultado);
        Assert.Null(resultado.VersaoDisponivel);
        Assert.Equal("Logitech", resultado.Fabricante);
    }

    [Fact]
    public async Task ConsultarAsync_BuscaCaseInsensitive_Encontra()
    {
        var resultado = await _repo.ConsultarAsync("pci\\ven_10de&dev_2204");
        Assert.NotNull(resultado);
        Assert.Equal("NVIDIA", resultado.Fabricante);
    }

    [Fact]
    public async Task ConsultarAsync_AudioRealtek_Encontra()
    {
        var resultado = await _repo.ConsultarAsync("HDAUDIO\\FUNC_01&VEN_10EC&DEV_0897&SUBSYS_10280964");

        Assert.NotNull(resultado);
        Assert.Contains("Realtek", resultado.Fabricante);
    }
}
