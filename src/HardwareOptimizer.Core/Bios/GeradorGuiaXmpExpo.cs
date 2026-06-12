namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Gera guia passo a passo para ativação de XMP (Intel) / EXPO ou DOCP (AMD)
/// específico por fabricante de placa-mãe. Nunca aplica — apenas orienta.
/// </summary>
public sealed class GeradorGuiaXmpExpo
{
    public GuiaXmpExpo Gerar(IdentificacaoBios identificacao, string? tipoRam = null)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        var (teclaSetup, passos, aviso) = FabricanteParaGuia(identificacao.Fabricante, tipoRam);

        return new GuiaXmpExpo
        {
            Fabricante = identificacao.Fabricante,
            Modelo = identificacao.Modelo,
            TeclaSetup = teclaSetup,
            Passos = passos,
            Aviso = aviso,
            TipoRam = tipoRam ?? "DDR4/DDR5",
        };
    }

    private static (string Tecla, IReadOnlyList<string> Passos, string Aviso) FabricanteParaGuia(
        string fabricante, string? tipoRam)
    {
        var nomeConfig = ObterNomeConfiguracao(fabricante, tipoRam);

        return fabricante switch
        {
            "ASUS" => (
                "Del (ou F2)",
                new[]
                {
                    "Reinicie o PC e pressione Del (ou F2) para entrar na BIOS UEFI.",
                    "Na tela inicial da BIOS, clique em 'Ai Tweaker' (modo Advanced) ou use o perfil 'D.O.C.P.' visível na tela inicial.",
                    $"Localize a opção '{nomeConfig}' e mude de 'Disabled' para o perfil desejado (ex: '3200MHz' ou o perfil mais alto disponível).",
                    "Pressione F10 para salvar e sair. O sistema reiniciará.",
                    "Confirme a velocidade da RAM com CPU-Z após reiniciar (guia de Memória → DRAM Frequency).",
                },
                $"⚠ Se o PC não inicializar após ativar {nomeConfig}, pressione Del no POST para entrar na BIOS e reduza para um perfil inferior."
            ),

            "MSI" => (
                "Del",
                new[]
                {
                    "Reinicie e pressione Del para entrar na BIOS MSI Click BIOS.",
                    "Clique em 'Settings' → 'Advanced' → 'DRAM Configuration'.",
                    $"Localize 'Memory Try It!' ou '{nomeConfig}' e selecione o perfil mais alto compatível.",
                    "Pressione F10 para Salvar e Reiniciar.",
                    "Verifique a velocidade da RAM com CPU-Z.",
                },
                $"⚠ MSI 'Memory Try It!' inclui perfis pré-testados: prefira este ao {nomeConfig} manual para maior estabilidade."
            ),

            "Gigabyte" => (
                "Del",
                new[]
                {
                    "Reinicie e pressione Del para entrar na BIOS Gigabyte.",
                    "Clique em 'Tweaker' → role até 'Extreme Memory Profile (X.M.P.)'.",
                    $"Mude de 'Disabled' para 'Profile 1' (ou '{nomeConfig}' em placas AMD).",
                    "Pressione F10 → 'Yes' para salvar e sair.",
                    "Se ocorrer falha no boot, pressione Del durante o POST para reverter ao padrão.",
                },
                "⚠ Gigabyte recomenda 'Profile 1' como ponto seguro; 'Profile 2' pode ser instável em kits extremos."
            ),

            "ASRock" => (
                "F2 ou Del",
                new[]
                {
                    "Reinicie e pressione F2 (ou Del) para entrar na BIOS ASRock UEFI.",
                    "Vá em 'OC Tweaker' → 'DRAM Timing Configuration'.",
                    $"Mude '{nomeConfig}' de 'Auto' para o perfil desejado.",
                    "Pressione F10 para salvar. O sistema reiniciará uma ou duas vezes.",
                    "Valide com CPU-Z após o boot.",
                },
                "⚠ ASRock reinicia duas vezes após ativar XMP/EXPO; isso é normal."
            ),

            _ => (
                "Del ou F2 (varia por fabricante)",
                new[]
                {
                    "Reinicie e pressione Del, F2 ou F10 (dependendo do fabricante) para entrar na BIOS.",
                    "Procure por 'XMP', 'EXPO', 'DOCP' ou 'Memory Profile' nas abas OC, Tweaker ou Advanced.",
                    "Selecione o perfil desejado (geralmente Profile 1).",
                    "Salve com F10 e reinicie.",
                },
                "⚠ Consulte o manual da placa-mãe se não encontrar a opção de perfil de memória."
            ),
        };
    }

    private static string ObterNomeConfiguracao(string fabricante, string? tipoRam) =>
        (fabricante, tipoRam?.ToUpperInvariant()) switch
        {
            ("ASUS", "DDR5") => "EXPO",
            ("ASUS", _) => "D.O.C.P.",
            ("MSI", "DDR5") => "EXPO",
            ("MSI", _) => "XMP",
            ("Gigabyte", "DDR5") => "EXPO",
            ("Gigabyte", _) => "X.M.P.",
            ("ASRock", "DDR5") => "EXPO/XMP",
            ("ASRock", _) => "XMP",
            _ => "XMP/EXPO",
        };
}

public sealed record GuiaXmpExpo
{
    public required string Fabricante { get; init; }
    public required string Modelo { get; init; }
    public required string TeclaSetup { get; init; }
    public required IReadOnlyList<string> Passos { get; init; }
    public required string Aviso { get; init; }
    public required string TipoRam { get; init; }
}
