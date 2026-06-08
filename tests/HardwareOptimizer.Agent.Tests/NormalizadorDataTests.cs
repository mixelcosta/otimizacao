using HardwareOptimizer.Agent.Collector;
using Xunit;

namespace HardwareOptimizer.Agent.Tests;

/// <summary>
/// Normalização de datas das fontes de inventário (bug real visto no Windows:
/// a data da BIOS vinha como "/Date(ms)/" do ConvertTo-Json).
/// </summary>
public sealed class NormalizadorDataTests
{
    [Theory]
    [InlineData("/Date(1754611200000)/", "2025-08-08")]      // ConvertTo-Json (PS 5.1)
    [InlineData("/Date(1754611200000+0000)/", "2025-08-08")] // com offset
    [InlineData("20250808000000.000000+000", "2025-08-08")]  // CIM DATETIME bruto
    [InlineData("08/08/2025", "2025-08-08")]                 // DMI/Linux (MM/dd/yyyy)
    [InlineData("2025-08-08", "2025-08-08")]                 // já ISO
    public void Normaliza_formatos_conhecidos_para_iso(string entrada, string esperado)
    {
        Assert.Equal(esperado, NormalizadorData.Normalizar(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vazio_ou_nulo_retorna_nulo(string? entrada)
    {
        Assert.Null(NormalizadorData.Normalizar(entrada));
    }

    [Fact]
    public void Formato_desconhecido_e_preservado()
    {
        // Não reconhece, mas não perde a informação original.
        Assert.Equal("AMI 5041", NormalizadorData.Normalizar("  AMI 5041  "));
    }

    [Fact]
    public void Date_json_invalido_e_preservado()
    {
        Assert.Equal("/Date(abc)/", NormalizadorData.Normalizar("/Date(abc)/"));
    }
}
