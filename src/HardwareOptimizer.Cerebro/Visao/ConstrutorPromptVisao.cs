namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Monta os prompts direcionados do módulo de visão. O system prompt exige JSON
/// estrito com nível de confiança; o user prompt foca a pergunta no caso de uso.
/// </summary>
public sealed class ConstrutorPromptVisao
{
    public string MontarSistema() =>
        "Você lê fotos de telas e etiquetas de hardware. Extraia apenas o que está visível; "
        + "NUNCA invente valores. Se não tiver certeza, use confiança \"baixa\".\n\n"
        + "Responda EXCLUSIVAMENTE com um JSON neste formato, sem texto adicional:\n"
        + "{\"tipoTela\":\"biosUefi|etiquetaPlaca|mensagemErro|benchmark|desconhecida\","
        + "\"campos\":{\"<nome>\":\"<valor lido>\"},"
        + "\"confianca\":\"alta|media|baixa\","
        + "\"proximoPasso\":\"<o que o usuário deve fazer a seguir>\"}";

    public string MontarUsuario(CasoUsoVisao caso) => caso switch
    {
        CasoUsoVisao.LerVersaoBios =>
            "Esta é uma tela de BIOS/UEFI. Identifique o fabricante e a placa e leia a VERSÃO da BIOS. "
            + "Use os campos 'fabricante', 'modelo' e 'versao'.",
        CasoUsoVisao.LerEtiquetaPlaca =>
            "Esta é a etiqueta de uma placa-mãe. Leia o fabricante e o modelo. "
            + "Use os campos 'fabricante' e 'modelo'.",
        CasoUsoVisao.LerMensagemErro =>
            "Esta é uma mensagem de erro ou tela azul. Leia o código de parada e a mensagem principal. "
            + "Use os campos 'codigo' e 'mensagem'.",
        CasoUsoVisao.LerBenchmark =>
            "Esta é uma tela de benchmark/estresse (ex.: OCCT, Cinebench). Leia temperatura, clock, "
            + "consumo e pontuação quando visíveis. Use campos como 'temperatura', 'clock', 'consumo', 'pontuacao'.",
        _ =>
            "Que tela é esta? Identifique o tipo e leia os campos relevantes visíveis.",
    };
}
