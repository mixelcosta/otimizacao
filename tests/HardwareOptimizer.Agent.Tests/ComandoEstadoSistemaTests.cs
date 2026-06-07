using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class ComandoEstadoSistemaTests
{
    private static Dictionary<string, string> Sem() => new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task Aplicar_grava_valor_e_registra_anterior_existente()
    {
        var estado = new EstadoSistemaSimulado(new Dictionary<string, string> { ["alvo"] = "antigo" });
        var comando = new ComandoEstadoSistema("cmd.x", estado, _ => "alvo", _ => "novo");

        var registro = await comando.AplicarAsync("ACAO", CategoriaAcao.Rede, Sem());

        Assert.Equal("antigo", registro.ValorAnterior);
        Assert.Equal("novo", registro.ValorNovo);
        Assert.Equal("novo", estado.Ler("alvo"));
    }

    [Fact]
    public async Task Reverter_valor_preexistente_restaura_o_anterior()
    {
        var estado = new EstadoSistemaSimulado(new Dictionary<string, string> { ["alvo"] = "antigo" });
        var comando = new ComandoEstadoSistema("cmd.x", estado, _ => "alvo", _ => "novo");

        var registro = await comando.AplicarAsync("ACAO", CategoriaAcao.Rede, Sem());
        await comando.ReverterAsync(registro);

        Assert.Equal("antigo", estado.Ler("alvo"));
    }

    [Fact]
    public async Task Reverter_valor_novo_remove_a_chave()
    {
        var estado = new EstadoSistemaSimulado();
        var comando = new ComandoEstadoSistema("cmd.x", estado, _ => "alvo", _ => "novo");

        var registro = await comando.AplicarAsync("ACAO", CategoriaAcao.Rede, Sem());
        Assert.Equal("novo", estado.Ler("alvo"));

        await comando.ReverterAsync(registro);
        Assert.Null(estado.Ler("alvo")); // não existia antes -> volta a não definido
    }

    [Fact]
    public async Task Resolver_valor_usa_parametro_informado()
    {
        var estado = new EstadoSistemaSimulado();
        var comando = new ComandoEstadoSistema(
            "cmd.x", estado, _ => "alvo", p => p["valor"]);

        var registro = await comando.AplicarAsync(
            "ACAO", CategoriaAcao.Cpu, new Dictionary<string, string> { ["valor"] = "123" });

        Assert.Equal("123", registro.ValorNovo);
    }
}
