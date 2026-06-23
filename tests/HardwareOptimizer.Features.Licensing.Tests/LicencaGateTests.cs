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

    // ---- ValidadorChaveLicenca ----

    [Fact]
    public void ValidadorChave_chave_gerada_para_mesma_maquina_e_valida()
    {
        var validador = new ValidadorChaveLicenca();
        var chave = validador.GerarChave("test-machine-guid-123");
        Assert.True(validador.ChaveValida(chave, "test-machine-guid-123"));
    }

    [Fact]
    public void ValidadorChave_chave_de_outra_maquina_e_invalida()
    {
        var validador = new ValidadorChaveLicenca();
        var chave = validador.GerarChave("maquina-A");
        Assert.False(validador.ChaveValida(chave, "maquina-B"));
    }

    [Fact]
    public void ValidadorChave_chave_vazia_e_invalida()
    {
        var validador = new ValidadorChaveLicenca();
        Assert.False(validador.ChaveValida("", "qualquer-maquina"));
    }

    [Fact]
    public void ValidadorChave_chave_aleatoria_e_invalida()
    {
        var validador = new ValidadorChaveLicenca();
        Assert.False(validador.ChaveValida("AAAAA-BBBBB-CCCCC-DDDDD", "test-machine-guid-123"));
    }

    [Fact]
    public void ValidadorChave_formato_com_tracos_e_aceito()
    {
        var validador = new ValidadorChaveLicenca();
        var chave = validador.GerarChave("minha-maquina");
        Assert.Contains("-", chave);
        Assert.True(validador.ChaveValida(chave, "minha-maquina"));
    }

    [Fact]
    public void ValidadorChave_chave_sem_tracos_tambem_valida()
    {
        var validador = new ValidadorChaveLicenca();
        var chave = validador.GerarChave("maquina-x").Replace("-", "");
        Assert.True(validador.ChaveValida(chave, "maquina-x"));
    }

    // ---- ResultadoAtivacao — campos NomeCliente / EmailCliente ----

    [Fact]
    public void ResultadoAtivacao_Ok_com_nome_e_email_preenche_campos()
    {
        var r = ResultadoAtivacao.Ok(TipoLicenca.Premium, "João Silva", "joao@test.com");

        Assert.True(r.Sucesso);
        Assert.Equal("João Silva", r.NomeCliente);
        Assert.Equal("joao@test.com", r.EmailCliente);
    }

    [Fact]
    public void ResultadoAtivacao_Ok_sem_nome_mantem_null()
    {
        var r = ResultadoAtivacao.Ok(TipoLicenca.Premium);

        Assert.Null(r.NomeCliente);
        Assert.Null(r.EmailCliente);
    }

    [Fact]
    public void ResultadoAtivacao_Falhar_nao_tem_nome_nem_email()
    {
        var r = ResultadoAtivacao.Falhar("chave inválida");

        Assert.Null(r.NomeCliente);
        Assert.Null(r.EmailCliente);
    }

    // ---- LicencaConfig ----

    [Fact]
    public void LicencaConfig_url_compra_nao_e_vazia()
    {
        Assert.NotEmpty(LicencaConfig.UrlCompra);
    }

    [Fact]
    public void LicencaConfig_grace_period_e_positivo()
    {
        Assert.True(LicencaConfig.DiasGracePeriodo > 0);
    }

    // ---- ValidarOnlineAsync nos fakes ----

    [Fact]
    public async Task LicencaGratuita_validar_online_retorna_gratuita()
    {
        IServicoLicenca licenca = new LicencaGratuita();
        var resultado = await licenca.ValidarOnlineAsync();
        Assert.True(resultado.Sucesso);
        Assert.Equal(TipoLicenca.Gratuita, resultado.NovoTipo);
    }

    [Fact]
    public async Task LicencaPremium_validar_online_retorna_premium()
    {
        IServicoLicenca licenca = new LicencaPremium();
        var resultado = await licenca.ValidarOnlineAsync();
        Assert.True(resultado.Sucesso);
        Assert.Equal(TipoLicenca.Premium, resultado.NovoTipo);
    }

    // Fakes para testar o comportamento do gate sem DPAPI/Registry
    private sealed class LicencaGratuita : IServicoLicenca
    {
        public TipoLicenca TipoAtual => TipoLicenca.Gratuita;
        public string? NomeCliente => null;
        public string? EmailCliente => null;
        public bool TemAcesso(FuncionalidadePremium f) => false;
        public Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Premium));
        public Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));
        public Task<ResultadoAtivacao> ValidarOnlineAsync(CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));
    }

    private sealed class LicencaPremium : IServicoLicenca
    {
        public TipoLicenca TipoAtual => TipoLicenca.Premium;
        public string? NomeCliente => null;
        public string? EmailCliente => null;
        public bool TemAcesso(FuncionalidadePremium f) => true;
        public Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Premium));
        public Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));
        public Task<ResultadoAtivacao> ValidarOnlineAsync(CancellationToken ct = default)
            => Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Premium));
    }
}
