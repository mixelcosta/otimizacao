using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ConstrutorPerfilTests
{
    private static ConstrutorPerfil Construtor() => new(CatalogoPadrao.Criar());

    private static SelecaoAcao Selecao(string id, params (string Nome, string Valor)[] pares) => new()
    {
        AcaoId = id,
        Parametros = pares.ToDictionary(p => p.Nome, p => p.Valor, StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public void Perfil_seguro_usa_valor_padrao_e_nao_exige_consentimento()
    {
        var r = Construtor().CriarPerfilSeguro(
            "padrao", new[] { "PWR_PLANO_ALTO_DESEMPENHO", "SO_SYSTEM_RESPONSIVENESS" });

        Assert.True(r.Sucesso);
        Assert.False(r.ExigeConsentimento);
        Assert.NotNull(r.Perfil);

        var selecao = r.Perfil!.Selecoes.Single(s => s.AcaoId == "SO_SYSTEM_RESPONSIVENESS");
        Assert.Equal("20", selecao.Parametros["percentual_reserva"]);
    }

    [Fact]
    public void Perfil_seguro_com_acao_inexistente_eh_bloqueado()
    {
        var r = Construtor().CriarPerfilSeguro("x", new[] { "NAO_EXISTE" });

        Assert.False(r.Sucesso);
        Assert.Null(r.Perfil);
        Assert.NotEmpty(r.Bloqueios);
    }

    [Fact]
    public void Perfil_customizado_com_risco_assumido_exige_consentimento()
    {
        var r = Construtor().CriarPerfilCustomizado(
            "meu_perfil", "usuario", new[] { Selecao("SO_SYSTEM_RESPONSIVENESS", ("percentual_reserva", "5")) });

        Assert.True(r.Sucesso);
        Assert.True(r.ExigeConsentimento);
        Assert.Single(r.RiscosAssumidos);
        Assert.False(r.Perfil!.ConsentimentoRegistrado);
        Assert.False(r.Perfil.PodeAplicar); // customizado não aplica sem consentimento.
    }

    [Fact]
    public void Perfil_customizado_acima_do_limite_absoluto_eh_bloqueado_sem_perfil()
    {
        var r = Construtor().CriarPerfilCustomizado(
            "agressivo", "usuario", new[] { Selecao("SO_SYSTEM_RESPONSIVENESS", ("percentual_reserva", "25")) });

        Assert.False(r.Sucesso);
        Assert.Null(r.Perfil);
        Assert.NotEmpty(r.Bloqueios);
    }

    [Fact]
    public void Perfil_customizado_dentro_da_faixa_segura_ainda_exige_consentimento()
    {
        // Mesmo só com valores seguros, qualquer perfil customizado exige consentimento ao salvar.
        var r = Construtor().CriarPerfilCustomizado(
            "conservador", "usuario", new[] { Selecao("SO_SYSTEM_RESPONSIVENESS", ("percentual_reserva", "20")) });

        Assert.True(r.Sucesso);
        Assert.True(r.ExigeConsentimento);
        Assert.Empty(r.RiscosAssumidos);
    }
}
