using HardwareOptimizer.Agent.Drivers;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Drivers;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Features.Drivers.Tests;

public class AtualizadorDriversTests
{
    private static AtualizadorDrivers Criar(
        IReadOnlyList<InfoDriver> dispositivos,
        IProvedorFonteOficial provedor)
    {
        return new AtualizadorDrivers(
            new ColetorHwidFake(dispositivos),
            provedor,
            NullLogger<AtualizadorDrivers>.Instance);
    }

    [Fact]
    public async Task VarrerAsync_DriverSemEntradaWhql_MantemDesconhecido()
    {
        var atualizador = Criar(
            [new InfoDriver { HardwareId = "PCI\\VEN_FFFF", Descricao = "Genérico", Status = StatusDriver.Desconhecido }],
            new ProvedorFake([]));

        var resultado = await atualizador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.Desconhecido, resultado[0].Status);
    }

    [Fact]
    public async Task VarrerAsync_VersaoNovaDisponivel_RetornaAtualizacaoDisponivel()
    {
        var repo = new ProvedorFake([
            new InfoDriver
            {
                HardwareId = "PCI\\VEN_10DE",
                Descricao = "NVIDIA",
                VersaoDisponivel = "999.0",
                CertificadoWhql = true,
                Status = StatusDriver.Desconhecido,
            }
        ]);
        var atualizador = Criar(
            [new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA GPU", VersaoAtual = "531.0", Status = StatusDriver.Desconhecido }],
            repo);

        var resultado = await atualizador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.AtualizacaoDisponivel, resultado[0].Status);
        Assert.Equal("999.0", resultado[0].VersaoDisponivel);
        Assert.True(resultado[0].CertificadoWhql);
    }

    [Fact]
    public async Task VarrerAsync_MesmaVersao_RetornaAtualizado()
    {
        var repo = new ProvedorFake([
            new InfoDriver
            {
                HardwareId = "PCI\\VEN_10DE",
                Descricao = "NVIDIA",
                VersaoDisponivel = "531.0",
                CertificadoWhql = true,
                Status = StatusDriver.Desconhecido,
            }
        ]);
        var atualizador = Criar(
            [new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA GPU", VersaoAtual = "531.0", Status = StatusDriver.Desconhecido }],
            repo);

        var resultado = await atualizador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.Atualizado, resultado[0].Status);
    }

    [Fact]
    public async Task VarrerAsync_VersaoDisponivelNula_RetornaAtualizado()
    {
        var repo = new ProvedorFake([
            new InfoDriver
            {
                HardwareId = "USB\\VID_046D",
                Descricao = "Logitech",
                VersaoDisponivel = null,
                Status = StatusDriver.Desconhecido,
            }
        ]);
        var atualizador = Criar(
            [new InfoDriver { HardwareId = "USB\\VID_046D", Descricao = "Logitech Mouse", VersaoAtual = "1.0", Status = StatusDriver.Desconhecido }],
            repo);

        var resultado = await atualizador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.Atualizado, resultado[0].Status);
    }

    [Fact]
    public async Task VarrerAsync_MultiplosDispositivos_TodosProcessados()
    {
        var atualizador = Criar(
            [
                new InfoDriver { HardwareId = "USB\\VID_046D", Descricao = "Logitech Mouse", Status = StatusDriver.Desconhecido },
                new InfoDriver { HardwareId = "USB\\VID_045E", Descricao = "Microsoft KB", Status = StatusDriver.Desconhecido },
                new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA", Status = StatusDriver.Desconhecido },
            ],
            new ProvedorFake([]));

        var resultado = await atualizador.VarrerAsync();

        Assert.Equal(3, resultado.Count);
    }

    [Fact]
    public async Task VarrerAsync_ListaVazia_RetornaListaVazia()
    {
        var atualizador = Criar([], new ProvedorFake([]));

        var resultado = await atualizador.VarrerAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task VarrerAsync_ErroNoRepositorio_MantemDesconhecido()
    {
        var atualizador = Criar(
            [new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA", Status = StatusDriver.Desconhecido }],
            new ProvedorComErro());

        var resultado = await atualizador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.Desconhecido, resultado[0].Status);
    }

    [Fact]
    public async Task RestaurarBackupAsync_PastaDeBackupNaoExiste_RetornaFalhaClara()
    {
        var atualizador = Criar([], new ProvedorFake([]));
        var caminhoInexistente = Path.Combine(
            Path.GetTempPath(), "hwopt-backup-teste-" + Guid.NewGuid().ToString("N"));

        var resultado = await atualizador.RestaurarBackupAsync(caminhoInexistente);

        Assert.True(resultado.Falha);
        Assert.Contains("não encontrado", resultado.MensagemErro, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ColetorHwidFake(IReadOnlyList<InfoDriver> dispositivos) : IColetorHwid
    {
        public IReadOnlyList<InfoDriver> Coletar() => dispositivos;
    }

    private sealed class ProvedorFake(IReadOnlyList<InfoDriver> entradas) : IProvedorFonteOficial
    {
        public Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default)
        {
            var encontrado = entradas.FirstOrDefault(e =>
                e.HardwareId.Equals(identificador, StringComparison.OrdinalIgnoreCase));
            InfoFonteOficial? info = encontrado is null
                ? null
                : new InfoFonteOficial
                {
                    VersaoDisponivel = encontrado.VersaoDisponivel,
                    UrlDownload = encontrado.UrlDownload,
                    CertificadoWhql = encontrado.CertificadoWhql,
                };
            return Task.FromResult(info);
        }
    }

    private sealed class ProvedorComErro : IProvedorFonteOficial
    {
        public Task<InfoFonteOficial?> ConsultarAsync(string identificador, CancellationToken ct = default)
            => throw new InvalidOperationException("Erro simulado de repositório.");
    }
}
