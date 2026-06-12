using System.Runtime.Versioning;
using HardwareOptimizer.Agent.Smart;
using HardwareOptimizer.Core.Contracts;
using HardwareOptimizer.Features.LifeCounter;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HardwareOptimizer.Features.LifeCounter.Tests;

[SupportedOSPlatform("windows")]
public sealed class CalculadoraVidaUtilTests
{
    private static CalculadoraVidaUtil CriarCom(params DadosSmartBrutos[] dados) =>
        new(new LeitorFake(dados), NullLogger<CalculadoraVidaUtil>.Instance);

    // Samsung 870 EVO tem fragmento "870 EVO" com TBW de 300GB no banco
    [Fact]
    public void Disco_saudavel_retorna_nivel_bom()
    {
        // Samsung 870 EVO 500GB, TBW fabricante ≈ 300GB
        // Escrito 50GB → vida restante ≈ 83% → Bom
        var calc = CriarCom(new DadosSmartBrutos("Samsung 870 EVO 500GB", "S1", 50, 1000, false, 0));

        var resultado = calc.Calcular();

        Assert.Single(resultado);
        Assert.Equal(NivelSaudeDisco.Bom, resultado[0].Nivel);
        Assert.True(resultado[0].PorcentagemVidaRestante > 50);
    }

    [Fact]
    public void Disco_com_erros_nao_corrigiveis_retorna_critico()
    {
        var calc = CriarCom(new DadosSmartBrutos("Samsung 870 EVO 500GB", "S1", 10, 500, true, 0));

        var resultado = calc.Calcular();

        Assert.Equal(NivelSaudeDisco.Critico, resultado[0].Nivel);
    }

    [Fact]
    public void Disco_com_setores_pendentes_retorna_critico()
    {
        var calc = CriarCom(new DadosSmartBrutos("Samsung 870 EVO 500GB", "S1", 10, 500, false, 5));

        var resultado = calc.Calcular();

        Assert.Equal(NivelSaudeDisco.Critico, resultado[0].Nivel);
    }

    [Fact]
    public void Disco_com_90pct_tbw_escrito_retorna_critico()
    {
        // Samsung 870 EVO 500GB TBW=300GB; escrever 280GB → ~7% restante < 10%
        var calc = CriarCom(new DadosSmartBrutos("Samsung 870 EVO 500GB", "S1", 280, 5000, false, 0));

        var resultado = calc.Calcular();

        Assert.Equal(NivelSaudeDisco.Critico, resultado[0].Nivel);
    }

    [Fact]
    public void Disco_com_75pct_tbw_escrito_retorna_atencao()
    {
        // Samsung 870 EVO 500GB TBW=300GB; escrever 225GB → 25% restante (< 30%) → Atencao
        var calc = CriarCom(new DadosSmartBrutos("Samsung 870 EVO 500GB", "S1", 225, 2000, false, 0));

        var resultado = calc.Calcular();

        Assert.Equal(NivelSaudeDisco.Atencao, resultado[0].Nivel);
    }

    [Fact]
    public void Disco_sem_tbw_na_database_assume_100pct_vida_restante()
    {
        // Modelo desconhecido → TBW=0 → porcentagem=100%
        var calc = CriarCom(new DadosSmartBrutos("Disco XYZ Desconhecido 9000", "S1", 999, 1000, false, 0));

        var resultado = calc.Calcular();

        Assert.Equal(100.0, resultado[0].PorcentagemVidaRestante);
        Assert.Equal(NivelSaudeDisco.Bom, resultado[0].Nivel);
    }

    [Fact]
    public void Multiplos_discos_retorna_todos()
    {
        var calc = CriarCom(
            new DadosSmartBrutos("Samsung 870 EVO 500GB", "S1", 50, 1000, false, 0),
            new DadosSmartBrutos("Disco XYZ 9000", "S2", 50, 500, false, 0));

        var resultado = calc.Calcular();

        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public void Nenhum_disco_retorna_lista_vazia()
    {
        var calc = CriarCom();

        var resultado = calc.Calcular();

        Assert.Empty(resultado);
    }

    private sealed class LeitorFake : ILeitorSmart
    {
        private readonly IReadOnlyList<DadosSmartBrutos> _dados;
        public LeitorFake(params DadosSmartBrutos[] dados) => _dados = dados;
        public IReadOnlyList<DadosSmartBrutos> LerDados() => _dados;
    }
}
