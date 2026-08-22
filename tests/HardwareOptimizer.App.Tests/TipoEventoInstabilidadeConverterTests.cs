using System.Globalization;
using HardwareOptimizer.App.Converters;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.Tests;

public class TipoEventoInstabilidadeConverterTests
{
    [Theory]
    [InlineData(TipoEventoInstabilidade.Bsod, "TELA AZUL")]
    [InlineData(TipoEventoInstabilidade.Whea, "ERRO DE HARDWARE (WHEA)")]
    [InlineData(TipoEventoInstabilidade.CrashAplicacao, "CRASH DE APLICATIVO")]
    public void Convert_TipoConhecido_RetornaRotuloPtBr(TipoEventoInstabilidade tipo, string esperado)
    {
        var resultado = TipoEventoInstabilidadeConverter.Instance.Convert(
            tipo, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void ConvertBack_LancaNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            TipoEventoInstabilidadeConverter.Instance.ConvertBack(
                "TELA AZUL", typeof(TipoEventoInstabilidade), null, CultureInfo.InvariantCulture));
    }
}
