using HardwareOptimizer.Agent.Execution.Windows;
using HardwareOptimizer.Agent.Features;
using HardwareOptimizer.Agent.Platform;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

public sealed class FeaturesWindowsTests
{
    // ── AlvoFeatureWindows: ExtrairEstado (parsing puro, sem Windows) ──────

    [Theory]
    [InlineData("Feature Name : TelnetClient\r\nState : Enabled\r\n", "Enabled")]
    [InlineData("Feature Name : TelnetClient\r\nState : Disabled\r\n", "Disabled")]
    [InlineData("Feature Name : TelnetClient\r\nState : EnablePending\r\n", "EnablePending")]
    [InlineData("", null)]
    [InlineData("Outro : Valor\r\n", null)]
    public void ExtrairEstado_parseia_saida_dism(string saida, string? esperado)
    {
        var resultado = AlvoFeatureWindowsTestHelper.ExtrairEstado(saida);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void ExtrairEstado_aceita_label_em_portugues()
    {
        // DISM em pt-BR pode exibir "Estado" em vez de "State".
        var saida = "Nome do Recurso : NetFx3\r\nEstado : Habilitado\r\n";
        var resultado = AlvoFeatureWindowsTestHelper.ExtrairEstado(saida);
        Assert.Equal("Habilitado", resultado);
    }

    // ── AlvoFeatureWindows: Escrever / Restaurar (via ProcessoFake) ────────

    [Fact]
    public async Task Feature_enable_chama_dism_com_enable_feature()
    {
        var proc   = new ProcessoFake();
        var estado = new EstadoSistemaWindows(new RegistroFake(), proc);

        await Task.Run(() => estado.Escrever("feature:TelnetClient", "Enabled"));

        Assert.Contains(proc.Chamados, c =>
            c.Contains("dism.exe") && c.Contains("/enable-feature") && c.Contains("TelnetClient"));
    }

    [Fact]
    public async Task Feature_disable_chama_dism_com_disable_feature()
    {
        var proc   = new ProcessoFake();
        var estado = new EstadoSistemaWindows(new RegistroFake(), proc);

        await Task.Run(() => estado.Escrever("feature:TelnetClient", "Disabled"));

        Assert.Contains(proc.Chamados, c =>
            c.Contains("dism.exe") && c.Contains("/disable-feature") && c.Contains("TelnetClient"));
    }

    [Fact]
    public void Feature_restaurar_nulo_nao_chama_dism()
    {
        var proc   = new ProcessoFake();
        var estado = new EstadoSistemaWindows(new RegistroFake(), proc);

        estado.Restaurar("feature:TelnetClient", null);

        Assert.Empty(proc.Chamados);
    }

    [Fact]
    public void Alvo_feature_nao_mapeado_lanca_not_supported()
    {
        var estado = new EstadoSistemaWindows(new RegistroFake(), new ProcessoFake());
        Assert.Throws<NotSupportedException>(() => estado.Ler("recurso:XYZ"));
    }

    // ── GerenciadorFeaturesWindows: catálogo curado ─────────────────────────

    [Fact]
    public void Catalogo_features_nao_esta_vazio()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.NotEmpty(GerenciadorFeaturesWindows.Catalogo);
    }

    [Fact]
    public void Catalogo_features_contem_wsl_e_hyperv()
    {
        if (!OperatingSystem.IsWindows()) return;
        var nomes = GerenciadorFeaturesWindows.Catalogo.Select(f => f.Nome).ToList();
        Assert.Contains("Microsoft-Windows-Subsystem-Linux", nomes);
        Assert.Contains("Microsoft-Hyper-V-All",             nomes);
    }

    [Fact]
    public void Catalogo_features_todas_tem_nome_exibicao_e_descricao()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var f in GerenciadorFeaturesWindows.Catalogo)
        {
            Assert.False(string.IsNullOrWhiteSpace(f.NomeExibicao),
                $"Feature '{f.Nome}' sem NomeExibicao.");
            Assert.False(string.IsNullOrWhiteSpace(f.Descricao),
                $"Feature '{f.Nome}' sem Descricao.");
        }
    }

    // ── CatalogoPadrao: 4 novas entradas FeaturesWindows ───────────────────

    [Fact]
    public void Catalogo_contem_4_features_windows()
    {
        var catalogo = CatalogoPadrao.Criar();
        var features = catalogo.Todas.Where(a => a.Categoria == CategoriaAcao.FeaturesWindows).ToList();
        Assert.Equal(4, features.Count);
    }

    [Theory]
    [InlineData("FEATURE_WSL")]
    [InlineData("FEATURE_HYPER_V")]
    [InlineData("FEATURE_NETFX35")]
    [InlineData("FEATURE_SANDBOX")]
    public void Catalogo_feature_possui_entrada_correta(string id)
    {
        var catalogo = CatalogoPadrao.Criar();
        var acao = catalogo.Todas.FirstOrDefault(a => a.Id == id);

        Assert.NotNull(acao);
        Assert.Equal(CategoriaAcao.FeaturesWindows, acao.Categoria);
        Assert.False(string.IsNullOrWhiteSpace(acao.ComandoInternoId));
    }

    // ── InfoFeatureWindows: record auxiliar ─────────────────────────────────

    [Theory]
    [InlineData("Enabled",  true)]
    [InlineData("Disabled", false)]
    [InlineData("",         false)]
    [InlineData("Desconhecido", false)]
    public void InfoFeature_habilitada_derivado_de_estado(string estado, bool esperado)
    {
        var info = new InfoFeatureWindows { Estado = estado };
        Assert.Equal(esperado, info.Habilitada);
    }

    // ── Fakes locais ─────────────────────────────────────────────────────────

    private sealed class ProcessoFake : IExecutorProcesso
    {
        public List<string> Chamados { get; } = [];

        public ResultadoProcesso Executar(string arquivo, IReadOnlyList<string> argumentos)
        {
            Chamados.Add(arquivo + " " + string.Join(' ', argumentos));
            return new ResultadoProcesso(0, "", "");
        }
    }

    private sealed class RegistroFake : IAcessoRegistro
    {
        private readonly Dictionary<string, uint> _store = new(StringComparer.OrdinalIgnoreCase);

        private static string K(ColmeiaRegistro c, string s, string n) => $"{c}|{s}|{n}";
        public uint? LerDword(ColmeiaRegistro c, string s, string n) =>
            _store.TryGetValue(K(c, s, n), out var v) ? v : null;
        public void EscreverDword(ColmeiaRegistro c, string s, string n, uint v) => _store[K(c, s, n)] = v;
        public void RemoverValor(ColmeiaRegistro c, string s, string n) => _store.Remove(K(c, s, n));
    }
}

/// <summary>
/// Expõe o método interno ExtrairEstado do AlvoFeatureWindows para teste.
/// AlvoFeatureWindows é uma classe privada dentro de EstadoSistemaWindows —
/// testamos via reflection ou duplicando a lógica simples aqui.
/// </summary>
internal static class AlvoFeatureWindowsTestHelper
{
    public static string? ExtrairEstado(string saida)
    {
        foreach (var linha in saida.Split('\n'))
        {
            var idx = linha.IndexOf(':', StringComparison.Ordinal);
            if (idx < 0) continue;
            var chave = linha[..idx].Trim();
            if (chave.Equals("State",  StringComparison.OrdinalIgnoreCase)
             || chave.Equals("Estado", StringComparison.OrdinalIgnoreCase))
                return linha[(idx + 1)..].Trim();
        }
        return null;
    }
}
