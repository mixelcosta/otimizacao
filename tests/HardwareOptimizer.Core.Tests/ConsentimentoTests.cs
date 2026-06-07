using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Consent;
using HardwareOptimizer.Core.Profiles;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ConsentimentoTests
{
    private static Perfil PerfilCustomizado() => new()
    {
        Nome = "perfil_teste",
        Tipo = TipoPerfil.Customizado,
        Autor = "usuario",
        Selecoes = new[]
        {
            new SelecaoAcao
            {
                AcaoId = "SO_SYSTEM_RESPONSIVENESS",
                Parametros = new Dictionary<string, string> { ["percentual_reserva"] = "5" },
            },
        },
    };

    [Fact]
    public void Go_nao_habilita_sem_os_dois_checkboxes()
    {
        var avaliador = new AvaliadorConsentimento();

        Assert.False(avaliador.PodeHabilitarConfirmacao(new[] { TermoConsentimento.IdAceiteRiscos }));
        Assert.False(avaliador.PodeHabilitarConfirmacao(Array.Empty<string>()));
    }

    [Fact]
    public void Go_habilita_com_os_dois_checkboxes()
    {
        var avaliador = new AvaliadorConsentimento();

        var pode = avaliador.PodeHabilitarConfirmacao(
            new[] { TermoConsentimento.IdAceiteRiscos, TermoConsentimento.IdDesejoProsseguir });

        Assert.True(pode);
    }

    [Fact]
    public void Avaliar_gera_registro_de_auditoria_quando_completo()
    {
        var avaliador = new AvaliadorConsentimento();
        var resposta = new RespostaConsentimento(
            new[] { TermoConsentimento.IdAceiteRiscos, TermoConsentimento.IdDesejoProsseguir },
            confirmacaoFinal: true);

        var r = avaliador.Avaliar(resposta, PerfilCustomizado(), "cat-v1");

        Assert.True(r.Sucesso);
        Assert.Equal("perfil_teste", r.ValorObrigatorio.NomePerfil);
        Assert.Equal("cat-v1", r.ValorObrigatorio.VersaoCatalogo);
        Assert.Contains("SO_SYSTEM_RESPONSIVENESS.percentual_reserva = 5", r.ValorObrigatorio.ValoresEscolhidos);
    }

    [Fact]
    public void Avaliar_falha_sem_confirmacao_final()
    {
        var avaliador = new AvaliadorConsentimento();
        var resposta = new RespostaConsentimento(
            new[] { TermoConsentimento.IdAceiteRiscos, TermoConsentimento.IdDesejoProsseguir },
            confirmacaoFinal: false);

        Assert.True(avaliador.Avaliar(resposta, PerfilCustomizado(), "cat-v1").Falha);
    }

    [Fact]
    public void Avaliar_falha_com_apenas_um_checkbox()
    {
        var avaliador = new AvaliadorConsentimento();
        var resposta = new RespostaConsentimento(
            new[] { TermoConsentimento.IdAceiteRiscos }, confirmacaoFinal: true);

        Assert.True(avaliador.Avaliar(resposta, PerfilCustomizado(), "cat-v1").Falha);
    }
}
