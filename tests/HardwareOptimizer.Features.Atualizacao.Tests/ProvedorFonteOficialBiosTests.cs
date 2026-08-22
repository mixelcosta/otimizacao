using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Atualizacao;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

public class ProvedorFonteOficialBiosTests
{
    [Fact]
    public async Task ConsultarAsync_EntradaConhecida_RetornaVersaoEFonteComoUrlDownload()
    {
        var repo = new ProvedorFake(new InfoBiosFabricante
        {
            Fabricante = "ASUS",
            Modelo = "ROG STRIX B550-F",
            VersaoMaisRecente = "3405",
            LinkManual = "https://www.asus.com/support/",
            Fonte = "https://www.asus.com/motherboards-components/motherboards/rog/rog-strix-b550-f-gaming/helpdesk_bios/",
            Ganho = HardwareOptimizer.Core.Bios.GanhoEstimado.Medio,
        });
        var provedor = new ProvedorFonteOficialBios(repo);

        var info = await provedor.ConsultarAsync("asus|rog strix b550-f");

        Assert.NotNull(info);
        Assert.Equal("3405", info!.VersaoDisponivel);
        Assert.Equal(
            "https://www.asus.com/motherboards-components/motherboards/rog/rog-strix-b550-f-gaming/helpdesk_bios/",
            info.UrlDownload);
    }

    [Fact]
    public async Task ConsultarAsync_SemEntrada_RetornaNull()
    {
        var provedor = new ProvedorFonteOficialBios(new ProvedorFake(null));

        var info = await provedor.ConsultarAsync("fabricante|modelo desconhecido");

        Assert.Null(info);
    }

    [Fact]
    public async Task ConsultarAsync_NuncaSinalizaCertificacaoWhql()
    {
        var repo = new ProvedorFake(new InfoBiosFabricante
        {
            Fabricante = "MSI",
            Modelo = "MAG B550 TOMAHAWK",
            VersaoMaisRecente = "7C91vH9",
            Fonte = "https://www.msi.com/Motherboard/MAG-B550-TOMAHAWK/support",
        });
        var provedor = new ProvedorFonteOficialBios(repo);

        var info = await provedor.ConsultarAsync("msi|mag b550 tomahawk");

        Assert.NotNull(info);
        Assert.False(info!.CertificadoWhql);
    }

    private sealed class ProvedorFake(InfoBiosFabricante? entrada) : IProvedorInfoBios
    {
        public Task<InfoBiosFabricante?> ObterAsync(string chaveBusca, CancellationToken cancellationToken = default) =>
            Task.FromResult(entrada);
    }
}
