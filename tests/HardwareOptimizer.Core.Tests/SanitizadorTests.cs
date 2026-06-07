using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Core.Privacy;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class SanitizadorTests
{
    private static Inventario InventarioComSegredos() => new()
    {
        Placa = new PlacaMae { Fabricante = "ASUS", Modelo = "ROG STRIX B550-F", VersaoBios = "2806" },
        Cpu = new Processador { Nome = "Ryzen 5 5600X" },
        SistemaOperacional = new SistemaOperacionalInfo { Tipo = SistemaOperacionalTipo.Windows },
        Rede = new[]
        {
            new InterfaceRede { Nome = "eth0", EnderecoMac = "AA:BB:CC:DD:EE:FF" },
        },
        Identificadores = new IdentificadoresSensiveis
        {
            NumeroSerie = "SN-12345",
            UuidPlaca = "uuid-abcdef",
            NomeMaquina = "PC-DO-MICHEL",
            NomeUsuario = "michel",
            ChaveProdutoWindows = "XXXXX-YYYYY-ZZZZZ",
        },
    };

    [Fact]
    public void Sanitizar_hasheia_correlacionaveis_e_remove_pii()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());
        var ident = resultado.InventarioSeguro.Identificadores;

        Assert.NotNull(ident);

        // Correlacionáveis: preservados apenas como hash (o valor bruto não vaza).
        Assert.StartsWith("sha256:", ident!.NumeroSerie);
        Assert.StartsWith("sha256:", ident.UuidPlaca);
        Assert.DoesNotContain("SN-12345", ident.NumeroSerie!, StringComparison.Ordinal);

        // PII: removida.
        Assert.Null(ident.NomeMaquina);
        Assert.Null(ident.NomeUsuario);
        Assert.Null(ident.ChaveProdutoWindows);
    }

    [Fact]
    public void Sanitizar_preserva_dados_de_baixo_risco()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());

        Assert.Equal("ROG STRIX B550-F", resultado.InventarioSeguro.Placa.Modelo);
        Assert.Equal("2806", resultado.InventarioSeguro.Placa.VersaoBios);
        Assert.Equal("Ryzen 5 5600X", resultado.InventarioSeguro.Cpu.Nome);
    }

    [Fact]
    public void Sanitizar_hasheia_mac()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());

        var mac = resultado.InventarioSeguro.Rede[0].EnderecoMac;
        Assert.NotNull(mac);
        Assert.StartsWith("sha256:", mac);
        Assert.DoesNotContain("AA:BB", mac, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Relatorio_classifica_remocao_e_hash_corretamente()
    {
        var resultado = new Sanitizador("sal-fixo").Sanitizar(InventarioComSegredos());

        var porCampo = resultado.CamposAlterados.ToDictionary(c => c.Campo, c => c.Acao);

        Assert.Equal(AcaoSanitizacao.Hasheado, porCampo["identificadores.numero_serie"]);
        Assert.Equal(AcaoSanitizacao.Hasheado, porCampo["identificadores.uuid_placa"]);
        Assert.Equal(AcaoSanitizacao.Removido, porCampo["identificadores.nome_usuario"]);
        Assert.Equal(AcaoSanitizacao.Removido, porCampo["identificadores.nome_maquina"]);
        Assert.Equal(AcaoSanitizacao.Removido, porCampo["identificadores.chave_produto_windows"]);
        Assert.Equal(AcaoSanitizacao.Hasheado, porCampo["rede[0].endereco_mac"]);
    }

    [Fact]
    public void Hash_eh_deterministico_para_o_mesmo_sal()
    {
        var a = new Sanitizador("sal-fixo");
        var b = new Sanitizador("sal-fixo");

        Assert.Equal(a.Hashear("valor"), b.Hashear("valor"));
    }
}
