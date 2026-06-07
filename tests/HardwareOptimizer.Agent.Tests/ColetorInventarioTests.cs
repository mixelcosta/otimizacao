using HardwareOptimizer.Agent.Collector;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ColetorInventarioTests
{
    [Fact]
    public async Task Coletor_delega_ao_leitor_informado()
    {
        var esperado = new Inventario
        {
            Placa = new PlacaMae { Fabricante = "ACME", Modelo = "X1" },
            Cpu = new Processador { Nome = "CPU Teste" },
            SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Linux },
        };

        var coletor = new ColetorInventario(new LeitorFake(esperado));

        var obtido = await coletor.ColetarAsync();

        Assert.Same(esperado, obtido);
    }

    [Fact]
    public async Task LeitorLinux_le_dados_reais_quando_em_linux()
    {
        if (!OperatingSystem.IsLinux())
        {
            return; // Teste específico de plataforma.
        }

        var inventario = await new LeitorLinux().LerAsync();

        Assert.Equal(SistemaOperacionalTipo.Linux, inventario.SistemaOperacional.Tipo);
        Assert.False(string.IsNullOrWhiteSpace(inventario.Cpu.Nome));
        Assert.NotEqual("Desconhecido", inventario.Cpu.Nome);
    }

    private sealed class LeitorFake : ILeitorPlataforma
    {
        private readonly Inventario _inventario;

        public LeitorFake(Inventario inventario) => _inventario = inventario;

        public SistemaOperacionalTipo Tipo => _inventario.SistemaOperacional.Tipo;

        public Task<Inventario> LerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_inventario);
    }
}
