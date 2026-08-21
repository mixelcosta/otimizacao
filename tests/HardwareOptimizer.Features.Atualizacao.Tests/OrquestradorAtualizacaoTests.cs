using HardwareOptimizer.Agent.Drivers;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.Atualizacao;
using HardwareOptimizer.Features.Drivers;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

/// <summary>
/// Cobre o que é seguro testar sem invocar processos reais do SO (pnputil).
/// Os fluxos de backup/instalação (<c>AtualizadorDrivers.ExportarBackupAsync</c>/
/// <c>InstalarAsync</c>) sempre disparam um processo real e não são cobertos por
/// teste unitário aqui — mesmo critério já usado em
/// <c>HardwareOptimizer.Features.Drivers.Tests.AtualizadorDriversTests</c>.
/// </summary>
public class OrquestradorAtualizacaoTests
{
    private static OrquestradorAtualizacao Criar(
        IReadOnlyList<InfoDriver> dispositivos,
        IProvedorFonteOficial provedor)
    {
        var atualizador = new AtualizadorDrivers(
            new ColetorHwidFake(dispositivos),
            provedor,
            NullLogger<AtualizadorDrivers>.Instance);
        return new OrquestradorAtualizacao(atualizador);
    }

    [Fact]
    public async Task VarrerAsync_DriverDesatualizado_RetornaAtualizacaoDisponivel()
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
        var orquestrador = Criar(
            [new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA GPU", VersaoAtual = "531.0", Status = StatusDriver.Desconhecido }],
            repo);

        var resultado = await orquestrador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.AtualizacaoDisponivel, resultado[0].Status);
        Assert.Equal("999.0", resultado[0].VersaoDisponivel);
    }

    [Fact]
    public async Task VarrerAsync_SemDriverDesatualizado_RetornaAtualizado()
    {
        var repo = new ProvedorFake([
            new InfoDriver
            {
                HardwareId = "PCI\\VEN_10DE",
                Descricao = "NVIDIA",
                VersaoDisponivel = "531.0",
                Status = StatusDriver.Desconhecido,
            }
        ]);
        var orquestrador = Criar(
            [new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA GPU", VersaoAtual = "531.0", Status = StatusDriver.Desconhecido }],
            repo);

        var resultado = await orquestrador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.Atualizado, resultado[0].Status);
    }

    [Fact]
    public async Task VarrerAsync_ConsultaFalha_MantemDesconhecidoSemPropagarExcecao()
    {
        var orquestrador = Criar(
            [new InfoDriver { HardwareId = "PCI\\VEN_10DE", Descricao = "NVIDIA", Status = StatusDriver.Desconhecido }],
            new ProvedorComErro());

        var resultado = await orquestrador.VarrerAsync();

        Assert.Single(resultado);
        Assert.Equal(StatusDriver.Desconhecido, resultado[0].Status);
    }

    [Fact]
    public async Task VarrerAsync_ListaVazia_RetornaListaVazia()
    {
        var orquestrador = Criar([], new ProvedorFake([]));

        var resultado = await orquestrador.VarrerAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ReverterAsync_BackupInexistente_RetornaFalhaClaraSemFalhaSilenciosa()
    {
        var orquestrador = Criar([], new ProvedorFake([]));
        var caminhoInexistente = Path.Combine(
            Path.GetTempPath(), "hwopt-orquestrador-teste-" + Guid.NewGuid().ToString("N"));

        var resultado = await orquestrador.ReverterAsync(caminhoInexistente);

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
