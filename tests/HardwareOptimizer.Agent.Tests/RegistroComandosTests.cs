using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class RegistroComandosTests
{
    [Fact]
    public void Todo_comando_interno_do_catalogo_esta_registrado()
    {
        var catalogo = CatalogoPadrao.Criar();
        var registro = RegistroComandos.Padrao(new EstadoSistemaSimulado());

        var faltando = catalogo.Todas
            .Select(a => a.ComandoInternoId)
            .Where(id => !registro.Contem(id))
            .Distinct()
            .ToList();

        Assert.True(faltando.Count == 0, "Comandos internos sem implementação registrada: " + string.Join(", ", faltando));
    }

    [Fact]
    public void Registro_rejeita_ids_duplicados()
    {
        var estado = new EstadoSistemaSimulado();
        var c1 = new ComandoEstadoSistema("dup", estado, _ => "a", _ => "v");
        var c2 = new ComandoEstadoSistema("dup", estado, _ => "b", _ => "w");

        Assert.Throws<ArgumentException>(() => new RegistroComandos(new IComandoInterno[] { c1, c2 }));
    }

    [Fact]
    public async Task Comando_com_parametro_ausente_lanca_ao_aplicar()
    {
        var registro = RegistroComandos.Padrao(new EstadoSistemaSimulado());
        var comando = registro.Obter("cmd.so.system_responsiveness.v1")!;

        // O comando depende do parâmetro 'percentual_reserva'; sem ele, deve lançar.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => comando.AplicarAsync("ACAO", CategoriaAcao.SistemaOperacional, new Dictionary<string, string>()));
    }

    [Fact]
    public void Obter_id_inexistente_retorna_nulo()
    {
        var registro = RegistroComandos.Padrao(new EstadoSistemaSimulado());
        Assert.Null(registro.Obter("cmd.inexistente"));
    }
}
