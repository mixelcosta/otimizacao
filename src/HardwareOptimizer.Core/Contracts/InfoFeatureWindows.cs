namespace HardwareOptimizer.Core.Contracts;

/// <summary>Estado de um recurso opcional do Windows (Windows Optional Feature).</summary>
public record InfoFeatureWindows
{
    public string Nome         { get; init; } = "";
    public string NomeExibicao { get; init; } = "";
    public string Descricao    { get; init; } = "";

    /// <summary>
    /// Estado reportado pelo DISM: "Enabled", "Disabled", "EnablePending",
    /// "DisablePending" ou "Desconhecido" quando leitura não foi possível.
    /// </summary>
    public string Estado { get; init; } = "";

    public bool Habilitada => Estado == "Enabled";
}
