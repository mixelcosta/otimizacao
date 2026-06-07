using HardwareOptimizer.Core.Common;
using Xunit;

namespace HardwareOptimizer.Core.Tests;

public sealed class ResultadoTests
{
    [Fact]
    public void Ok_indica_sucesso_sem_erros()
    {
        var r = Resultado.Ok();
        Assert.True(r.Sucesso);
        Assert.False(r.Falha);
        Assert.Empty(r.Erros);
    }

    [Fact]
    public void Falhar_acumula_erros()
    {
        var r = Resultado.Falhar("a", "b");
        Assert.True(r.Falha);
        Assert.Equal(2, r.Erros.Count);
        Assert.Contains("a", r.MensagemErro, StringComparison.Ordinal);
    }

    [Fact]
    public void Falhar_sem_mensagem_gera_erro_padrao()
    {
        var r = Resultado.Falhar();
        Assert.True(r.Falha);
        Assert.NotEmpty(r.Erros);
    }

    [Fact]
    public void ResultadoGenerico_ok_carrega_valor()
    {
        var r = Resultado<int>.Ok(42);
        Assert.True(r.Sucesso);
        Assert.Equal(42, r.ValorObrigatorio);
    }

    [Fact]
    public void ResultadoGenerico_falha_lanca_ao_acessar_valor_obrigatorio()
    {
        var r = Resultado<string>.Falhar("erro");
        Assert.True(r.Falha);
        Assert.Throws<InvalidOperationException>(() => r.ValorObrigatorio);
    }
}
