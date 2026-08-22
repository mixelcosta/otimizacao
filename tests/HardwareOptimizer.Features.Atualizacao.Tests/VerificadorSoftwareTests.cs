using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Atualizacao;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

/// <summary>Cobre a I/O & Edge-Case Matrix da spec-1-3-software-desatualizado.</summary>
public class VerificadorSoftwareTests
{
    private static VerificadorSoftware Criar(IProvedorFonteOficial provedor) =>
        new(provedor, NullLogger<VerificadorSoftware>.Instance);

    [Fact]
    public async Task VerificarAsync_SoftwareDesatualizado_ApareceComVersaoAtualEOficial()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["7-Zip 21.07 (x64)"] = new InfoFonteOficial
            {
                VersaoDisponivel = "24.07",
                UrlDownload = "https://www.7-zip.org/",
            },
        });
        var verificador = Criar(provedor);

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "7-Zip 21.07 (x64)", Versao = "21.07" },
        ]);

        Assert.Single(resultado);
        Assert.Equal("7-Zip 21.07 (x64)", resultado[0].Nome);
        Assert.Equal("21.07", resultado[0].VersaoAtual);
        Assert.Equal("24.07", resultado[0].VersaoDisponivel);
        Assert.Equal("https://www.7-zip.org/", resultado[0].UrlDownload);
        Assert.Equal(StatusSoftware.AtualizacaoDisponivel, resultado[0].Status);
    }

    [Fact]
    public async Task VerificarAsync_MesmaVersao_NaoAparece()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["VLC media player"] = new InfoFonteOficial { VersaoDisponivel = "3.0.21" },
        });
        var verificador = Criar(provedor);

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "VLC media player", Versao = "3.0.21" },
        ]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_SemCoberturaNoCatalogo_NaoAparece()
    {
        var verificador = Criar(new ProvedorFake(new Dictionary<string, InfoFonteOficial>()));

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "Programa Sem Catálogo", Versao = "1.0" },
        ]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_ListaVazia_RetornaListaVazia()
    {
        var verificador = Criar(new ProvedorFake(new Dictionary<string, InfoFonteOficial>()));

        var resultado = await verificador.VerificarAsync([]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_ConsultaLancaExcecao_TratadoComoSemCoberturaSemPropagar()
    {
        var verificador = Criar(new ProvedorComErro());

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "Qualquer Programa", Versao = "1.0" },
        ]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_ExcecaoEmUmItem_ContinuaParaOsDemais()
    {
        var provedor = new ProvedorComErroSeletivo(
            falhaPara: "Programa Com Erro",
            demais: new InfoFonteOficial { VersaoDisponivel = "2.0" });
        var verificador = Criar(provedor);

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "Programa Com Erro", Versao = "1.0" },
            new ProgramaInstalado { Nome = "Programa Ok", Versao = "1.0" },
        ]);

        Assert.Single(resultado);
        Assert.Equal("Programa Ok", resultado[0].Nome);
    }

    [Fact]
    public async Task VerificarAsync_VersaoInstaladaNulaOuVazia_NaoAparece()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["Programa Sem Versao Lida"] = new InfoFonteOficial { VersaoDisponivel = "9.9" },
        });
        var verificador = Criar(provedor);

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "Programa Sem Versao Lida", Versao = null },
        ]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_ProvedorRetornaVersaoVazia_TratadoComoSemCobertura()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["Programa X"] = new InfoFonteOficial { VersaoDisponivel = "" },
        });
        var verificador = Criar(provedor);

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "Programa X", Versao = "1.0" },
        ]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_VersaoInstaladaSomenteEspacos_NaoAparece()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["Programa Y"] = new InfoFonteOficial { VersaoDisponivel = "9.9" },
        });
        var verificador = Criar(provedor);

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "Programa Y", Versao = "   " },
        ]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_ProvedorRetornaVersaoSomenteEspacos_TratadoComoSemCobertura()
    {
        var provedor = new ProvedorFake(new Dictionary<string, InfoFonteOficial>
        {
            ["Programa Z"] = new InfoFonteOficial { VersaoDisponivel = "   " },
        });
        var verificador = Criar(provedor);

        var resultado = await verificador.VerificarAsync([
            new ProgramaInstalado { Nome = "Programa Z", Versao = "1.0" },
        ]);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VerificarAsync_CancelamentoDoProvedor_Propaga()
    {
        var verificador = Criar(new ProvedorComCancelamento());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            verificador.VerificarAsync([
                new ProgramaInstalado { Nome = "Qualquer Programa", Versao = "1.0" },
            ]));
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

    private sealed class ProvedorComErroSeletivo(string falhaPara, InfoFonteOficial demais) : IProvedorFonteOficial
    {
        public Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default)
        {
            if (identificador == falhaPara)
                throw new InvalidOperationException("Erro simulado de repositório.");
            return Task.FromResult<InfoFonteOficial?>(demais);
        }
    }
}
