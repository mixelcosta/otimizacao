using HardwareOptimizer.Core.Catalog;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class CatalogoTests
{
    [Fact]
    public void CatalogoPadrao_eh_internamente_coerente()
    {
        var catalogo = CatalogoPadrao.Criar();

        var coerencia = catalogo.VerificarCoerencia();

        Assert.True(coerencia.Sucesso, coerencia.MensagemErro);
    }

    [Fact]
    public void CatalogoPadrao_contem_acoes_do_documento()
    {
        var catalogo = CatalogoPadrao.Criar();

        Assert.True(catalogo.Contem("PWR_PLANO_ALTO_DESEMPENHO"));
        Assert.True(catalogo.Contem("SRV_DESATIVAR_SERVICO"));
    }

    [Fact]
    public void Construtor_rejeita_ids_duplicados()
    {
        var acao = new AcaoOtimizacao
        {
            Id = "DUP",
            Categoria = Common.CategoriaAcao.Rede,
            Titulo = "x",
            Descricao = "x",
            ComandoInternoId = "cmd.x",
            Reversao = "x",
            Risco = Common.NivelRisco.Nenhum,
        };

        Assert.Throws<ArgumentException>(() => new CatalogoAcoes("v1", new[] { acao, acao }));
    }

    [Fact]
    public void Parametro_incoerente_eh_detectado()
    {
        // Faixa segura fora da permitida: deve falhar na verificação de coerência.
        var parametro = new ParametroNumerico(
            nome: "x",
            descricao: "x",
            faixaSegura: new FaixaNumerica(0, 100),
            faixaPermitida: new FaixaNumerica(0, 50),
            limiteAbsoluto: 50,
            padraoSeguro: 10);

        Assert.True(parametro.VerificarCoerencia().Falha);
    }
}
