using HardwareOptimizer.Features.Atualizacao;

namespace HardwareOptimizer.Features.Atualizacao.Tests;

public class RepositorioVersoesSoftwareEstaticoTests
{
    private readonly RepositorioVersoesSoftwareEstatico _repo = new();

    [Fact]
    public async Task ConsultarAsync_NomeExato_Encontra()
    {
        var resultado = await _repo.ConsultarAsync("7-Zip");

        Assert.NotNull(resultado);
        Assert.Equal("7-Zip", resultado.Nome);
        Assert.False(string.IsNullOrEmpty(resultado.VersaoDisponivel));
    }

    [Fact]
    public async Task ConsultarAsync_NomeComSufixoDeVersaoEArquitetura_EncontraPorSubstring()
    {
        var resultado = await _repo.ConsultarAsync("7-Zip 21.07 (x64)");

        Assert.NotNull(resultado);
        Assert.Equal("7-Zip", resultado.Nome);
    }

    [Fact]
    public async Task ConsultarAsync_BuscaCaseInsensitive_Encontra()
    {
        var resultado = await _repo.ConsultarAsync("google chrome");

        Assert.NotNull(resultado);
        Assert.Equal("Google Chrome", resultado.Nome);
    }

    [Fact]
    public async Task ConsultarAsync_NomeNaoMapeado_RetornaNull()
    {
        var resultado = await _repo.ConsultarAsync("Programa Totalmente Desconhecido XYZ");
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarAsync_NomeVazio_RetornaNull()
    {
        var resultado = await _repo.ConsultarAsync(string.Empty);
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarAsync_EntradaEncontrada_TemUrlDeDownload()
    {
        var resultado = await _repo.ConsultarAsync("VLC media player");

        Assert.NotNull(resultado);
        Assert.False(string.IsNullOrEmpty(resultado.UrlDownload));
    }

    [Theory]
    [InlineData("Mozilla Firefox")]
    [InlineData("Notepad++")]
    [InlineData("WinRAR")]
    [InlineData("Adobe Acrobat Reader DC")]
    [InlineData("Zoom")]
    public async Task ConsultarAsync_ProgramasComunsDoCatalogo_TodosEncontrados(string nome)
    {
        var resultado = await _repo.ConsultarAsync(nome);
        Assert.NotNull(resultado);
    }
}
