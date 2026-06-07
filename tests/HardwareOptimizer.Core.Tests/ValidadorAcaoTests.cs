using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ValidadorAcaoTests
{
    private static ValidadorAcao Validador() => new(CatalogoPadrao.Criar());

    private static Dictionary<string, string> Params(params (string Nome, string Valor)[] pares) =>
        pares.ToDictionary(p => p.Nome, p => p.Valor, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Acao_fora_do_catalogo_eh_recusada()
    {
        var r = Validador().Validar("ACAO_INEXISTENTE", Params(), TipoPerfil.Seguro);

        Assert.False(r.AcaoConhecida);
        Assert.False(r.Aplicavel);
    }

    [Fact]
    public void Valor_acima_do_limite_absoluto_eh_bloqueio_rigido()
    {
        // SO_SYSTEM_RESPONSIVENESS: limite absoluto = 20.
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "25")), TipoPerfil.Customizado);

        Assert.True(r.TemBloqueioRigido);
        Assert.False(r.Aplicavel);
        Assert.Contains(r.Parametros, p => p.Situacao == SituacaoParametro.BloqueioLimiteAbsoluto);
    }

    [Fact]
    public void Valor_na_faixa_segura_eh_aceito_sem_consentimento()
    {
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "20")), TipoPerfil.Seguro);

        Assert.True(r.Aplicavel);
        Assert.False(r.ExigeConsentimento);
    }

    [Fact]
    public void Valor_fora_da_segura_dentro_da_permitida_eh_risco_assumido()
    {
        // faixa segura [10,20], permitida [0,20]; 5 é risco assumido no perfil customizado.
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "5")), TipoPerfil.Customizado);

        Assert.True(r.Aplicavel);
        Assert.True(r.ExigeConsentimento);
        Assert.Contains(r.Parametros, p => p.Situacao == SituacaoParametro.RiscoAssumido);
    }

    [Fact]
    public void Perfil_seguro_recusa_valor_fora_da_faixa_segura()
    {
        var r = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("percentual_reserva", "5")), TipoPerfil.Seguro);

        Assert.False(r.Aplicavel);
        Assert.Contains(r.Parametros, p => p.Situacao == SituacaoParametro.Rejeitado);
    }

    [Fact]
    public void Lista_branca_aceita_valor_da_lista_e_recusa_fora()
    {
        var aceito = Validador().Validar(
            "SRV_DESATIVAR_SERVICO", Params(("nome_servico", "DiagTrack")), TipoPerfil.Seguro);
        var recusado = Validador().Validar(
            "SRV_DESATIVAR_SERVICO", Params(("nome_servico", "ServicoCritico")), TipoPerfil.Customizado);

        Assert.True(aceito.Aplicavel);
        Assert.False(recusado.Aplicavel);
    }

    [Fact]
    public void Parametro_faltante_ou_desconhecido_gera_erro()
    {
        var faltante = Validador().Validar("SO_SYSTEM_RESPONSIVENESS", Params(), TipoPerfil.Seguro);
        var desconhecido = Validador().Validar(
            "SO_SYSTEM_RESPONSIVENESS", Params(("inexistente", "1")), TipoPerfil.Seguro);

        Assert.False(faltante.Aplicavel);
        Assert.False(desconhecido.Aplicavel);
        Assert.NotEmpty(desconhecido.Erros);
    }

    [Fact]
    public void Acao_sem_parametros_eh_aplicavel()
    {
        var r = Validador().Validar("PWR_PLANO_ALTO_DESEMPENHO", Params(), TipoPerfil.Seguro);

        Assert.True(r.Aplicavel);
        Assert.False(r.ExigeConsentimento);
    }
}
