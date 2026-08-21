using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Atualizacao;
using HardwareOptimizer.Features.Drivers;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

public class ProvedorFonteOficialDriverTests
{
    [Fact]
    public async Task ConsultarAsync_EntradaConhecida_RetornaVersao()
    {
        var repo = new RepositorioFake(new InfoDriver
        {
            HardwareId = "PCI\\VEN_10DE",
            Descricao = "NVIDIA",
            VersaoDisponivel = "572.83",
            Status = StatusDriver.Desconhecido,
        });
        var provedor = new ProvedorFonteOficialDriver(repo);

        var info = await provedor.ConsultarAsync("PCI\\VEN_10DE");

        Assert.NotNull(info);
        Assert.Equal("572.83", info!.VersaoDisponivel);
    }

    [Fact]
    public async Task ConsultarAsync_SemEntrada_RetornaNull()
    {
        var provedor = new ProvedorFonteOficialDriver(new RepositorioFake(null));

        var info = await provedor.ConsultarAsync("PCI\\VEN_FFFF");

        Assert.Null(info);
    }

    [Fact]
    public async Task ConsultarAsync_EntradaSemVersao_RetornaVersaoNula()
    {
        var repo = new RepositorioFake(new InfoDriver
        {
            HardwareId = "USB\\VID_046D",
            Descricao = "Logitech",
            VersaoDisponivel = null,
            Status = StatusDriver.Desconhecido,
        });
        var provedor = new ProvedorFonteOficialDriver(repo);

        var info = await provedor.ConsultarAsync("USB\\VID_046D");

        Assert.NotNull(info);
        Assert.Null(info!.VersaoDisponivel);
    }

    private sealed class RepositorioFake(InfoDriver? entrada) : IRepositorioDriversWhql
    {
        public Task<InfoDriver?> ConsultarAsync(string hardwareId, CancellationToken ct = default) =>
            Task.FromResult(entrada);
    }
}
