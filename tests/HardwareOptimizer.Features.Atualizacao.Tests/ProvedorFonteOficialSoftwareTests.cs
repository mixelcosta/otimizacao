using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Atualizacao;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

public class ProvedorFonteOficialSoftwareTests
{
    [Fact]
    public async Task ConsultarAsync_EntradaConhecida_RetornaVersao()
    {
        var repo = new RepositorioFake(new InfoSoftware
        {
            Nome = "7-Zip",
            VersaoDisponivel = "24.07",
            UrlDownload = "https://www.7-zip.org/",
        });
        var provedor = new ProvedorFonteOficialSoftware(repo);

        var info = await provedor.ConsultarAsync("7-Zip 21.07 (x64)");

        Assert.NotNull(info);
        Assert.Equal("24.07", info!.VersaoDisponivel);
        Assert.Equal("https://www.7-zip.org/", info.UrlDownload);
    }

    [Fact]
    public async Task ConsultarAsync_SemEntrada_RetornaNull()
    {
        var provedor = new ProvedorFonteOficialSoftware(new RepositorioFake(null));

        var info = await provedor.ConsultarAsync("Programa Desconhecido Qualquer");

        Assert.Null(info);
    }

    [Fact]
    public async Task ConsultarAsync_NuncaSinalizaCertificacaoWhql()
    {
        var repo = new RepositorioFake(new InfoSoftware { Nome = "VLC media player", VersaoDisponivel = "3.0.21" });
        var provedor = new ProvedorFonteOficialSoftware(repo);

        var info = await provedor.ConsultarAsync("VLC media player");

        Assert.NotNull(info);
        Assert.False(info!.CertificadoWhql);
    }

    private sealed class RepositorioFake(InfoSoftware? entrada) : IRepositorioVersoesSoftware
    {
        public Task<InfoSoftware?> ConsultarAsync(string nomePrograma, CancellationToken ct = default) =>
            Task.FromResult(entrada);
    }
}
