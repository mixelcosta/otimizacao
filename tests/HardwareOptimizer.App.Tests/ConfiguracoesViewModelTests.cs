using HardwareOptimizer.App.ViewModels;
using HardwareOptimizer.Features.Licensing;
using Xunit;

namespace HardwareOptimizer.App.Tests;

public sealed class ConfiguracoesViewModelTests
{
    // ── Status inicial ─────────────────────────────────────────────────────────

    [Fact]
    public void Status_inicial_gratuita_quando_licenca_gratuita()
    {
        var vm = CriarVm(TipoLicenca.Gratuita);

        Assert.False(vm.EPremium);
        Assert.Contains("bloqueados", vm.StatusLicenca, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Status_inicial_premium_quando_licenca_premium()
    {
        var vm = CriarVm(TipoLicenca.Premium);

        Assert.True(vm.EPremium);
        Assert.Contains("desbloqueados", vm.StatusLicenca, StringComparison.OrdinalIgnoreCase);
    }

    // ── Ativação bem-sucedida ──────────────────────────────────────────────────

    [Fact]
    public async Task Ativar_com_sucesso_marca_EPremium_true()
    {
        var licenca = new LicencaFake();
        licenca.AtivarResposta = ResultadoAtivacao.Ok(TipoLicenca.Premium, "João Silva", "joao@test.com");
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        vm.ChaveAtivacao = "XXXX-XXXX-XXXX-XXXX";

        await vm.AtivarCommand.ExecuteAsync(null);

        Assert.True(vm.EPremium);
    }

    [Fact]
    public async Task Ativar_com_sucesso_exibe_nome_do_cliente_na_mensagem()
    {
        var licenca = new LicencaFake();
        licenca.AtivarResposta = ResultadoAtivacao.Ok(TipoLicenca.Premium, "João Silva", "joao@test.com");
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        vm.ChaveAtivacao = "XXXX-XXXX-XXXX-XXXX";

        await vm.AtivarCommand.ExecuteAsync(null);

        Assert.Contains("João Silva", vm.MensagemAtivacao, StringComparison.Ordinal);
        Assert.Equal("João Silva", vm.NomeCliente);
        Assert.Equal("joao@test.com", vm.EmailCliente);
    }

    [Fact]
    public async Task Ativar_com_sucesso_sem_nome_exibe_mensagem_generica()
    {
        var licenca = new LicencaFake();
        licenca.AtivarResposta = ResultadoAtivacao.Ok(TipoLicenca.Premium);
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        vm.ChaveAtivacao = "XXXX-XXXX-XXXX-XXXX";

        await vm.AtivarCommand.ExecuteAsync(null);

        Assert.True(vm.EPremium);
        Assert.Contains("sucesso", vm.MensagemAtivacao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ativar_com_sucesso_limpa_campo_de_chave()
    {
        var licenca = new LicencaFake();
        licenca.AtivarResposta = ResultadoAtivacao.Ok(TipoLicenca.Premium);
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        vm.ChaveAtivacao = "XXXX-XXXX-XXXX-XXXX";

        await vm.AtivarCommand.ExecuteAsync(null);

        Assert.Empty(vm.ChaveAtivacao);
    }

    // ── Ativação com falha ─────────────────────────────────────────────────────

    [Fact]
    public async Task Ativar_com_falha_nao_marca_premium()
    {
        var licenca = new LicencaFake();
        licenca.AtivarResposta = ResultadoAtivacao.Falhar("Chave inválida ou já utilizada.");
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        vm.ChaveAtivacao = "INVALIDA";

        await vm.AtivarCommand.ExecuteAsync(null);

        Assert.False(vm.EPremium);
    }

    [Fact]
    public async Task Ativar_com_falha_exibe_mensagem_de_erro()
    {
        var licenca = new LicencaFake();
        licenca.AtivarResposta = ResultadoAtivacao.Falhar("Chave inválida ou já utilizada.");
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        vm.ChaveAtivacao = "INVALIDA";

        await vm.AtivarCommand.ExecuteAsync(null);

        Assert.Contains("Chave inválida", vm.MensagemAtivacao, StringComparison.Ordinal);
    }

    // ── Desativação ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Desativar_reverte_EPremium_para_false()
    {
        var licenca = new LicencaFake(TipoLicenca.Premium);
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        Assert.True(vm.EPremium);

        await vm.DesativarCommand.ExecuteAsync(null);

        Assert.False(vm.EPremium);
    }

    [Fact]
    public async Task Desativar_exibe_mensagem_de_reversao()
    {
        var licenca = new LicencaFake(TipoLicenca.Premium);
        var vm = new ConfiguracoesViewModel(licenca, () => { });

        await vm.DesativarCommand.ExecuteAsync(null);

        Assert.Contains("Gratuita", vm.MensagemAtivacao, StringComparison.OrdinalIgnoreCase);
    }

    // ── Validação online ───────────────────────────────────────────────────────

    [Fact]
    public async Task ValidarOnline_com_licenca_valida_mantém_premium_e_exibe_confirmacao()
    {
        var licenca = new LicencaFake(TipoLicenca.Premium);
        licenca.ValidarResposta = ResultadoAtivacao.Ok(TipoLicenca.Premium);
        var vm = new ConfiguracoesViewModel(licenca, () => { });

        await vm.ValidarOnlineCommand.ExecuteAsync(null);

        Assert.True(vm.EPremium);
        Assert.Contains("válida", vm.MensagemAtivacao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidarOnline_com_assinatura_expirada_reverte_para_gratuita()
    {
        var licenca = new LicencaFake(TipoLicenca.Premium);
        licenca.ValidarResposta = ResultadoAtivacao.Falhar("Assinatura expirada ou cancelada.");
        var vm = new ConfiguracoesViewModel(licenca, () => { });
        Assert.True(vm.EPremium);

        await vm.ValidarOnlineCommand.ExecuteAsync(null);

        Assert.False(vm.EPremium);
        Assert.Contains("expirada", vm.MensagemAtivacao, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ConfiguracoesViewModel CriarVm(TipoLicenca tipo) =>
        new(new LicencaFake(tipo), () => { });

    private sealed class LicencaFake : IServicoLicenca
    {
        private TipoLicenca _tipo;
        private string? _nome;
        private string? _email;

        public LicencaFake(TipoLicenca tipoInicial = TipoLicenca.Gratuita) => _tipo = tipoInicial;

        public ResultadoAtivacao AtivarResposta { get; set; } = ResultadoAtivacao.Ok(TipoLicenca.Premium);
        public ResultadoAtivacao ValidarResposta { get; set; } = ResultadoAtivacao.Ok(TipoLicenca.Premium);

        public TipoLicenca TipoAtual => _tipo;
        public string? NomeCliente => _nome;
        public string? EmailCliente => _email;

        public bool TemAcesso(FuncionalidadePremium _) => _tipo == TipoLicenca.Premium;

        public Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default)
        {
            var r = AtivarResposta;
            if (r.Sucesso)
            {
                _tipo = r.NovoTipo ?? _tipo;
                _nome = r.NomeCliente;
                _email = r.EmailCliente;
            }
            return Task.FromResult(r);
        }

        public Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default)
        {
            _tipo = TipoLicenca.Gratuita;
            _nome = null;
            _email = null;
            return Task.FromResult(ResultadoAtivacao.Ok(TipoLicenca.Gratuita));
        }

        public Task<ResultadoAtivacao> ValidarOnlineAsync(CancellationToken ct = default)
        {
            var r = ValidarResposta;
            _tipo = r.Sucesso ? (r.NovoTipo ?? _tipo) : TipoLicenca.Gratuita;
            return Task.FromResult(r);
        }
    }
}
