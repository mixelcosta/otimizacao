using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Atualizacao;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

/// <summary>Cobre a I/O & Edge-Case Matrix da spec-1-4-bios-alerta-risco.</summary>
public class VerificadorBiosTests
{
    private static VerificadorBios Criar(IProvedorFonteOficial provedor) =>
        new(provedor, NullLogger<VerificadorBios>.Instance);

    private static PlacaMae Placa(string? versaoBios) => new()
    {
        Fabricante = "ASUSTeK Computer Inc.",
        Modelo = "ROG STRIX B550-F",
        VersaoBios = versaoBios,
    };

    [Fact]
    public async Task VerificarAsync_BiosDesatualizada_RetornaInfoComVersaoAtualEOficialEGuia()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["asus|rog strix b550-f"] = new InfoFonteOficial
            {
                VersaoDisponivel = "3405",
                UrlDownload = "https://www.asus.com/support/",
            },
        });
        var verificador = Criar(provedor);

        var info = await verificador.VerificarAsync(Placa("2806"));

        Assert.NotNull(info);
        Assert.Equal("ASUS", info!.Fabricante);
        Assert.Equal("ROG STRIX B550-F", info.Modelo);
        Assert.Equal("2806", info.VersaoAtual);
        Assert.Equal("3405", info.VersaoDisponivel);
        Assert.Equal("https://www.asus.com/support/", info.UrlDownload);
        Assert.False(string.IsNullOrWhiteSpace(info.TeclaSetup));
        Assert.False(string.IsNullOrWhiteSpace(info.Utilitario));
        Assert.NotEmpty(info.Passos);
        Assert.NotEmpty(info.Avisos);
    }

    [Fact]
    public async Task VerificarAsync_MesmaVersao_NaoRetornaAlerta()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["asus|rog strix b550-f"] = new InfoFonteOficial { VersaoDisponivel = "3405" },
        });
        var verificador = Criar(provedor);

        var info = await verificador.VerificarAsync(Placa("3405"));

        Assert.Null(info);
    }

    [Fact]
    public async Task VerificarAsync_VersaoInstaladaMaisNova_NaoRetornaAlerta()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["asus|rog strix b550-f"] = new InfoFonteOficial { VersaoDisponivel = "3200" },
        });
        var verificador = Criar(provedor);

        var info = await verificador.VerificarAsync(Placa("3405"));

        Assert.Null(info);
    }

    [Fact]
    public async Task VerificarAsync_PlacaSemCoberturaNoCatalogo_RetornaNull()
    {
        var verificador = Criar(new ProvedorFake(new Dictionary<string, InfoFonteOficial>()));

        var info = await verificador.VerificarAsync(Placa("2806"));

        Assert.Null(info);
    }

    [Fact]
    public async Task VerificarAsync_ProvedorRetornaVersaoVazia_TratadoComoSemCobertura()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["asus|rog strix b550-f"] = new InfoFonteOficial { VersaoDisponivel = "" },
        });
        var verificador = Criar(provedor);

        var info = await verificador.VerificarAsync(Placa("2806"));

        Assert.Null(info);
    }

    [Fact]
    public async Task VerificarAsync_VersaoAtualNulaOuVazia_NaoRetornaAlerta()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["asus|rog strix b550-f"] = new InfoFonteOficial { VersaoDisponivel = "3405" },
        });
        var verificador = Criar(provedor);

        var info = await verificador.VerificarAsync(Placa(versaoBios: null));

        Assert.Null(info);
    }

    [Fact]
    public async Task VerificarAsync_ConsultaLancaExcecao_TratadoComoSemCoberturaSemPropagar()
    {
        var verificador = Criar(new ProvedorComErro());

        var info = await verificador.VerificarAsync(Placa("2806"));

        Assert.Null(info);
    }

    [Fact]
    public async Task VerificarAsync_CancelamentoDoProvedor_Propaga()
    {
        var verificador = Criar(new ProvedorComCancelamento());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            verificador.VerificarAsync(Placa("2806")));
    }

    [Fact]
    public async Task VerificarAsync_PlacaNula_Lanca()
    {
        var verificador = Criar(new ProvedorFake(new Dictionary<string, InfoFonteOficial>()));

        await Assert.ThrowsAsync<ArgumentNullException>(() => verificador.VerificarAsync(null!));
    }

    // ── Integração real com BancoCuradoBios — outros formatos de versão do
    // catálogo, além do numérico puro da ASUS (MSI/Gigabyte usam formatos
    // alfanuméricos diferentes; ver deferred/edge-case da revisão independente).

    [Fact]
    public async Task VerificarAsync_ComBancoCuradoBiosReal_PlacaMsiAlfanumericaDesatualizada_RetornaAlerta()
    {
        var verificador = Criar(new ProvedorFonteOficialBios(new BancoCuradoBios()));

        var info = await verificador.VerificarAsync(new PlacaMae
        {
            Fabricante = "MSI",
            Modelo = "MAG B550 TOMAHAWK",
            VersaoBios = "7C91vA4", // mais antiga que "7C91vH9" do catálogo real
        });

        Assert.NotNull(info);
        Assert.Equal("7C91vH9", info!.VersaoDisponivel);
        Assert.Equal("M-Flash", info.Utilitario);
    }

    [Fact]
    public async Task VerificarAsync_ComBancoCuradoBiosReal_PlacaGigabytePrefixadaDesatualizada_RetornaAlerta()
    {
        var verificador = Criar(new ProvedorFonteOficialBios(new BancoCuradoBios()));

        var info = await verificador.VerificarAsync(new PlacaMae
        {
            Fabricante = "Gigabyte",
            Modelo = "B550 AORUS ELITE",
            VersaoBios = "F10", // mais antiga que "F16" do catálogo real
        });

        Assert.NotNull(info);
        Assert.Equal("F16", info!.VersaoDisponivel);
        Assert.Equal("Q-Flash (tecla End no boot ou via BIOS)", info.Utilitario);
    }

    [Fact]
    public async Task VerificarAsync_ComBancoCuradoBiosReal_PlacaGigabyteMesmaVersaoPrefixada_NaoRetornaAlerta()
    {
        var verificador = Criar(new ProvedorFonteOficialBios(new BancoCuradoBios()));

        var info = await verificador.VerificarAsync(new PlacaMae
        {
            Fabricante = "Gigabyte",
            Modelo = "B550 AORUS ELITE",
            VersaoBios = "F16",
        });

        Assert.Null(info);
    }

    private sealed class ProvedorFake(IReadOnlyDictionary<string, InfoFonteOficial> catalogo) : IProvedorFonteOficial
    {
        public Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default) =>
            Task.FromResult(catalogo.TryGetValue(identificador, out var info) ? info : null);
    }

    private sealed class ProvedorComCancelamento : IProvedorFonteOficial
    {
        public Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default) =>
            throw new OperationCanceledException();
    }

    private sealed class ProvedorComErro : IProvedorFonteOficial
    {
        public Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default) =>
            throw new InvalidOperationException("Erro simulado de repositório.");
    }
}
