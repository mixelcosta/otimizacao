using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Core.Privacy;

/// <summary>Ação aplicada a um campo durante a sanitização.</summary>
public enum AcaoSanitizacao
{
    /// <summary>Campo removido por completo (dado de identificação pessoal).</summary>
    Removido = 0,

    /// <summary>Substituído por hash, preservando correlação sem expor o valor.</summary>
    Hasheado = 1,
}

/// <summary>Registro de um campo sensível tratado, para o log do que foi enviado.</summary>
public sealed record CampoSanitizado(string Campo, AcaoSanitizacao Acao);

/// <summary>
/// Resultado do pipeline de sanitização: a versão "segura para nuvem" do
/// inventário e o relatório do que foi alterado.
/// </summary>
public sealed class ResultadoSanitizacao
{
    public ResultadoSanitizacao(Inventario inventarioSeguro, IReadOnlyList<CampoSanitizado> camposAlterados)
    {
        InventarioSeguro = inventarioSeguro;
        CamposAlterados = camposAlterados;
    }

    public Inventario InventarioSeguro { get; }

    public IReadOnlyList<CampoSanitizado> CamposAlterados { get; }
}
