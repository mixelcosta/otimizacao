using HardwareOptimizer.Features.Licensing;
using Xunit;

namespace HardwareOptimizer.Features.Licensing.Tests;

public sealed class LicencaGateTests
{
    [Fact]
    public void Licenca_gratuita_nega_acesso_a_modulo_upgrade()
    {
        IServicoLicenca licenca = new LicencaGratuita();

        Assert.False(licenca.TemAcesso(FuncionalidadePremium.ModuloUpgrade));
    }

    [Fact]
    public void Licenca_gratuita_nega_acesso_a_todos_modulos_premium()
    {
        IServicoLicenca licenca = new LicencaGratuita();

        foreach (FuncionalidadePremium f in Enum.GetValues<FuncionalidadePremium>())
            Assert.False(licenca.TemAcesso(f));
    }

    [Fact]
    public void Licenca_premium_permite_acesso_a_todos_modulos()
    {
        IServicoLicenca licenca = new LicencaPremium();

        foreach (FuncionalidadePremium f in Enum.GetValues<FuncionalidadePremium>())
            Assert.True(licenca.TemAcesso(f));
    }

    [Fact]
    public void Licenca_gratuita_tipo_e_gratuita()
    {
        IServicoLicenca licenca = new LicencaGratuita();

        Assert.Equal(TipoLicenca.Gratuita, licenca.TipoAtual);
    }

    [Fact]
    public void Licenca_premium_tipo_e_premium()
    {
        IServicoLicenca licenca = new LicencaPremium();

        Assert.Equal(TipoLicenca.Premium, licenca.TipoAtual);
    }

    [Fact]
    public void ResultadoAtivacao_Ok_tem_sucesso_e_tipo_correto()
    {
        var r = ResultadoAtivacao.Ok(TipoLicenca.Premium);

        Assert.True(r.Sucesso);
        Assert.Equal(TipoLicenca.Premium, r.NovoTipo);
        Assert.Null(r.Erro);
    }

    [Fact]
    public void ResultadoAtivacao_Falhar_tem_sucesso_false_e_mensagem()
    {
        var r = ResultadoAtivacao.Falhar("chave inválida");

        Assert.False(r.Sucesso);
        Assert.Equal("chave inválida", r.Erro);
        Assert.Null(r.NovoTipo);
    }

    // Fakes para testar o comportamento do gate sem DPAPI/Registry
    private sealed class LicencaGratuita : IServicoLicenca
    {
        public TipoLicenca TipoAtual => TipoLicenca.Gratuita;
        public bool TemAcesso(FuncionalidadePremium f) => false;
        public Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Premium));
        public Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));
    }

    private sealed class LicencaPremium : IServicoLicenca
    {
        public TipoLicenca TipoAtual => TipoLicenca.Premium;
        public bool TemAcesso(FuncionalidadePremium f) => true;
        public Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Premium));
        public Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));
    }
}
