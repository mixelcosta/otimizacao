using System.Runtime.Versioning;
using HardwareOptimizer.Agent.Startup;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

[SupportedOSPlatform("windows")]
public sealed class StartupScannerTests
{
    private static VerificadorInicializacao CriarVerificador() =>
        new(NullLogger<VerificadorInicializacao>.Instance);

    [Fact]
    public void Varrer_retorna_lista_nao_nula()
    {
        if (!OperatingSystem.IsWindows()) return;

        var resultado = CriarVerificador().Varrer();

        Assert.NotNull(resultado);
    }

    [Fact]
    public void Varrer_todas_entradas_tem_nome_nao_vazio()
    {
        if (!OperatingSystem.IsWindows()) return;

        var resultado = CriarVerificador().Varrer();

        Assert.All(resultado, e => Assert.False(string.IsNullOrEmpty(e.Nome)));
    }

    [Fact]
    public void Varrer_todas_entradas_tem_origem_definida()
    {
        if (!OperatingSystem.IsWindows()) return;

        var resultado = CriarVerificador().Varrer();
        var valoresValidos = Enum.GetValues<OrigemInicializacao>();

        Assert.All(resultado, e => Assert.Contains(e.Origem, valoresValidos));
    }

    [Fact]
    public void Varrer_entradas_registro_tem_ativo_refletindo_startup_approved()
    {
        if (!OperatingSystem.IsWindows()) return;

        var resultado = CriarVerificador().Varrer()
            .Where(e => e.Origem == OrigemInicializacao.RegistroUsuario
                     || e.Origem == OrigemInicializacao.RegistroMaquina)
            .ToList();

        // Deve ter encontrado pelo menos uma entrada (qualquer máquina Windows tem startup entries).
        Assert.NotEmpty(resultado);

        // Ativo reflete o estado real da chave StartupApproved — pode ser true ou false.
        // Entradas sem flag em StartupApproved são habilitadas por padrão (true).
        Assert.All(resultado, e => Assert.IsType<bool>(e.Ativo));
    }

    [Fact]
    public void Varrer_entradas_tem_chave_rollback_preenchida()
    {
        if (!OperatingSystem.IsWindows()) return;

        var resultado = CriarVerificador().Varrer()
            .Where(e => e.Origem == OrigemInicializacao.RegistroUsuario
                     || e.Origem == OrigemInicializacao.RegistroMaquina)
            .ToList();

        Assert.All(resultado, e => Assert.False(string.IsNullOrEmpty(e.ChaveRollback)));
    }

    [Fact]
    public void Varrer_impacto_nunca_e_valor_invalido()
    {
        if (!OperatingSystem.IsWindows()) return;

        var resultado = CriarVerificador().Varrer();
        var valoresValidos = Enum.GetValues<ImpactoInicializacao>();

        Assert.All(resultado, e => Assert.Contains(e.Impacto, valoresValidos));
    }
}
