namespace HardwareOptimizer.Core.Bios;

/// <summary>
/// Gera o guia passo a passo específico do fabricante: tecla de setup,
/// utilitário de flash, procedimento, avisos de segurança e ajustes
/// recomendados (perfil de memória, Resizable BAR) com seu risco.
/// </summary>
public sealed class GeradorGuiaBios
{
    private static readonly IReadOnlyList<string> AvisosPadrao = new[]
    {
        "NÃO desligue nem reinicie o computador durante a gravação da BIOS.",
        "Use nobreak (ou bateria carregada, em notebooks) para evitar queda de energia.",
        "Baixe o arquivo apenas da página oficial do modelo EXATO da sua placa.",
        "Uma falha durante o flash pode inutilizar a placa (brick).",
    };

    private static readonly IReadOnlyList<string> AjustesPadrao = new[]
    {
        "Perfil de memória XMP (Intel) / EXPO ou DOCP (AMD): habilita a velocidade anunciada da RAM. Risco: Baixo a Médio — validar com teste de memória.",
        "Resizable BAR / Smart Access Memory: pode melhorar desempenho de GPU quando CPU e placa de vídeo suportam. Risco: Baixo.",
    };

    public GuiaBios Gerar(IdentificacaoBios identificacao)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        var (tecla, utilitario) = ProcedimentoFabricante(identificacao.Fabricante);

        var passos = new[]
        {
            $"Acesse a página oficial de suporte do modelo {identificacao.Modelo} e baixe a versão de BIOS desejada.",
            "Extraia o arquivo e copie-o para um pendrive formatado em FAT32.",
            $"Reinicie e pressione {tecla} para entrar no setup da BIOS/UEFI.",
            $"Abra o utilitário {utilitario}.",
            "Selecione o arquivo de BIOS no pendrive e confirme a atualização.",
            "Aguarde a conclusão sem interromper; o sistema reiniciará automaticamente.",
            "Após reiniciar, confirme a nova versão (o sistema relê a versão pelo inventário).",
        };

        return new GuiaBios
        {
            TeclaSetup = tecla,
            Utilitario = utilitario,
            Passos = passos,
            Avisos = AvisosPadrao,
            AjustesRecomendados = AjustesPadrao,
        };
    }

    private static (string Tecla, string Utilitario) ProcedimentoFabricante(string fabricante) =>
        fabricante switch
        {
            "ASUS" => ("Del (ou F2)", "ASUS EZ Flash 3 (menu Tool/Advanced)"),
            "Gigabyte" => ("Del", "Q-Flash (tecla End no boot ou via BIOS)"),
            "MSI" => ("Del", "M-Flash"),
            "ASRock" => ("F2 ou Del", "ASRock Instant Flash"),
            _ => ("Del ou F2 (varia por fabricante)", "utilitário de atualização do próprio fabricante"),
        };
}
