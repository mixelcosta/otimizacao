using System.Globalization;
using HardwareOptimizer.Agent.Execution;
using HardwareOptimizer.Agent.Execution.Windows;
using HardwareOptimizer.Agent.Platform;
using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Cobre a implementação real do Windows com fakes de registro e processo — toda
/// a lógica de tradução, parsing e round-trip de rollback roda fora do Windows.
/// </summary>
public sealed class EstadoSistemaWindowsTests
{
    // ---- Registro --------------------------------------------------------

    [Fact]
    public void Registro_escreve_traduz_decimal_e_le_de_volta()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:SystemResponsiveness", "10");

        Assert.Equal(10u, registro.Valor(ColmeiaRegistro.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness"));
        Assert.Equal("10", estado.Ler("registro:SystemResponsiveness"));
    }

    [Fact]
    public void Registro_visualfx_traduz_simbolico_para_dword()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:VisualFXSetting", "DESEMPENHO");

        Assert.Equal(2u, registro.Valor(ColmeiaRegistro.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting"));
    }

    [Fact]
    public void Registro_network_throttling_aceita_hexadecimal_e_round_trip()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:NetworkThrottlingIndex", "ffffffff");

        // 0xFFFFFFFF preservado e relido em decimal.
        Assert.Equal(uint.MaxValue, registro.Valor(ColmeiaRegistro.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex"));
        Assert.Equal(uint.MaxValue.ToString(CultureInfo.InvariantCulture),
            estado.Ler("registro:NetworkThrottlingIndex"));
    }

    [Fact]
    public void Registro_le_nulo_quando_valor_ausente()
    {
        var estado = new EstadoSistemaWindows(new RegistroFake(), new ProcessoFake());
        Assert.Null(estado.Ler("registro:TdrDelay"));
    }

    [Fact]
    public void Registro_restaura_valor_anterior_ou_remove_quando_nulo()
    {
        var registro = new RegistroFake();
        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());

        estado.Escrever("registro:TdrDelay", "8");
        estado.Restaurar("registro:TdrDelay", "2");
        Assert.Equal(2u, registro.Valor(ColmeiaRegistro.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay"));

        estado.Restaurar("registro:TdrDelay", null);
        Assert.Null(estado.Ler("registro:TdrDelay"));
    }

    // ---- Plano de energia ------------------------------------------------

    [Fact]
    public void Plano_le_guid_ativo_e_aplica_alto_desempenho()
    {
        const string guidAtual = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var processo = new ProcessoFake();
        processo.AoChamar("powercfg /getactivescheme",
            new ResultadoProcesso(0, $"Power Scheme GUID: {guidAtual}  (Balanced)", ""));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Equal(guidAtual, estado.Ler("powercfg:plano_ativo"));

        estado.Escrever("powercfg:plano_ativo", "ALTO_DESEMPENHO");
        Assert.Contains($"powercfg /setactive {EstadoSistemaWindows.GuidAltoDesempenho}", processo.Chamados);

        // Rollback volta ao plano anterior capturado pela leitura.
        estado.Restaurar("powercfg:plano_ativo", guidAtual);
        Assert.Contains($"powercfg /setactive {guidAtual}", processo.Chamados);
    }

    // ---- Suspensão seletiva de USB ---------------------------------------

    [Fact]
    public void Usb_desabilita_define_indices_ac_dc_e_ativa()
    {
        var processo = new ProcessoFake();
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        estado.Escrever("powercfg:usb_suspensao_seletiva", "DESABILITADO");

        Assert.Contains(
            $"powercfg /setacvalueindex SCHEME_CURRENT {EstadoSistemaWindows.SubgrupoUsb} {EstadoSistemaWindows.ConfigUsbSuspensao} 0",
            processo.Chamados);
        Assert.Contains(
            $"powercfg /setdcvalueindex SCHEME_CURRENT {EstadoSistemaWindows.SubgrupoUsb} {EstadoSistemaWindows.ConfigUsbSuspensao} 0",
            processo.Chamados);
        Assert.Contains("powercfg /setactive SCHEME_CURRENT", processo.Chamados);
    }

    [Fact]
    public void Usb_le_indice_atual_do_primeiro_hex()
    {
        var processo = new ProcessoFake();
        processo.AoChamar(
            $"powercfg /query SCHEME_CURRENT {EstadoSistemaWindows.SubgrupoUsb} {EstadoSistemaWindows.ConfigUsbSuspensao}",
            new ResultadoProcesso(0,
                "  Current AC Power Setting Index: 0x00000001\n  Current DC Power Setting Index: 0x00000001", ""));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Equal("1", estado.Ler("powercfg:usb_suspensao_seletiva"));
    }

    // ---- Serviços --------------------------------------------------------

    [Fact]
    public void Servico_desabilita_configura_e_para()
    {
        var processo = new ProcessoFake();
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        estado.Escrever("servico:DiagTrack", "Disabled");

        Assert.Contains("sc config DiagTrack start= disabled", processo.Chamados);
        Assert.Contains("sc stop DiagTrack", processo.Chamados);
    }

    [Fact]
    public void Servico_le_start_type_e_restaura_modo_anterior()
    {
        var processo = new ProcessoFake();
        processo.AoChamar("sc qc DiagTrack",
            new ResultadoProcesso(0, "        START_TYPE         : 2   AUTO_START", ""));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Equal("auto", estado.Ler("servico:DiagTrack"));

        estado.Restaurar("servico:DiagTrack", "auto");
        Assert.Contains("sc config DiagTrack start= auto", processo.Chamados);
    }

    [Fact]
    public void Escrever_falha_de_processo_lanca()
    {
        var processo = new ProcessoFake();
        processo.AoChamar("sc config Fax start= disabled",
            new ResultadoProcesso(5, "", "Acesso negado."));
        var estado = new EstadoSistemaWindows(new RegistroFake(), processo);

        Assert.Throws<InvalidOperationException>(() => estado.Escrever("servico:Fax", "Disabled"));
    }

    // ---- Mapeamento ------------------------------------------------------

    [Theory]
    [InlineData("registro:Inexistente")]
    [InlineData("powercfg:plano_inexistente")]
    [InlineData("desconhecido:x")]
    [InlineData("sem_separador")]
    public void Alvo_nao_mapeado_lanca(string alvo)
    {
        var estado = new EstadoSistemaWindows(new RegistroFake(), new ProcessoFake());
        Assert.Throws<NotSupportedException>(() => estado.Ler(alvo));
    }

    // ---- Seleção do ambiente --------------------------------------------

    [Fact]
    public void Selecionar_retorna_simulado_sem_flag_de_execucao_real()
    {
        var original = Environment.GetEnvironmentVariable("HWOPT_EXECUCAO_REAL");
        try
        {
            Environment.SetEnvironmentVariable("HWOPT_EXECUCAO_REAL", null);
            Assert.IsType<EstadoSistemaSimulado>(EstadoSistemaWindows.Selecionar());
        }
        finally
        {
            Environment.SetEnvironmentVariable("HWOPT_EXECUCAO_REAL", original);
        }
    }

    // ---- Integração com o executor (round-trip de rollback) --------------

    [Fact]
    public async Task Comando_do_catalogo_aplica_e_reverte_sobre_o_estado_real()
    {
        // O estado real do Windows pluga no mesmo RegistroComandos/rollback do MVP.
        var registro = new RegistroFake();
        const string subchave = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        registro.EscreverDword(ColmeiaRegistro.LocalMachine, subchave, "SystemResponsiveness", 20);

        var estado = new EstadoSistemaWindows(registro, new ProcessoFake());
        var comando = RegistroComandos.Padrao(estado).Obter("cmd.so.system_responsiveness.v1")!;

        var alteracao = await comando.AplicarAsync(
            "SO_SYSTEM_RESPONSIVENESS", CategoriaAcao.SistemaOperacional,
            new Dictionary<string, string> { ["percentual_reserva"] = "10" });

        Assert.Equal("20", alteracao.ValorAnterior);
        Assert.Equal(10u, registro.Valor(ColmeiaRegistro.LocalMachine, subchave, "SystemResponsiveness"));

        await comando.ReverterAsync(alteracao);
        Assert.Equal(20u, registro.Valor(ColmeiaRegistro.LocalMachine, subchave, "SystemResponsiveness"));
    }

    // ---- Fakes -----------------------------------------------------------

    private sealed class RegistroFake : IAcessoRegistro
    {
        private readonly Dictionary<string, uint> _valores = new(StringComparer.OrdinalIgnoreCase);

        private static string Chave(ColmeiaRegistro colmeia, string subchave, string nome) =>
            $"{colmeia}|{subchave}|{nome}";

        public uint? Valor(ColmeiaRegistro colmeia, string subchave, string nome) =>
            _valores.TryGetValue(Chave(colmeia, subchave, nome), out var v) ? v : null;

        public uint? LerDword(ColmeiaRegistro colmeia, string subchave, string nome) =>
            Valor(colmeia, subchave, nome);

        public void EscreverDword(ColmeiaRegistro colmeia, string subchave, string nome, uint valor) =>
            _valores[Chave(colmeia, subchave, nome)] = valor;

        public void RemoverValor(ColmeiaRegistro colmeia, string subchave, string nome) =>
            _valores.Remove(Chave(colmeia, subchave, nome));
    }

    private sealed class ProcessoFake : IExecutorProcesso
    {
        private readonly Dictionary<string, ResultadoProcesso> _respostas = new(StringComparer.Ordinal);

        public List<string> Chamados { get; } = new();

        public void AoChamar(string comando, ResultadoProcesso resposta) => _respostas[comando] = resposta;

        public ResultadoProcesso Executar(string arquivo, IReadOnlyList<string> argumentos)
        {
            var comando = arquivo + " " + string.Join(' ', argumentos);
            Chamados.Add(comando);
            return _respostas.TryGetValue(comando, out var resposta)
                ? resposta
                : new ResultadoProcesso(0, string.Empty, string.Empty);
        }
    }
}
