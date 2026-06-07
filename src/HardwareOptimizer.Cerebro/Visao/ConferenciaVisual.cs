using HardwareOptimizer.Core.Bios;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro.Visao;

/// <summary>
/// Cruza a leitura visual com o inventário coletado. Implementa a regra do
/// documento: nunca confiar cegamente na leitura visual; se a confiança for
/// baixa, pedir nova foto; caso contrário, validar contra os dados coletados.
/// </summary>
public sealed class ConferenciaVisual
{
    public ResultadoConferencia Conferir(LeituraVisual leitura, Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(inventario);

        if (leitura.Confianca == NivelConfianca.Baixa)
        {
            return PedirNovaFoto("Confiança baixa na leitura visual.");
        }

        return leitura.TipoTela switch
        {
            TipoTela.BiosUefi => ConferirBios(leitura, inventario.Placa),
            TipoTela.EtiquetaPlaca => ConferirEtiqueta(leitura, inventario.Placa),
            TipoTela.MensagemErro or TipoTela.Benchmark =>
                Inconclusivo("Leitura aceita; não há campo equivalente no inventário para cruzar."),
            _ => PedirNovaFoto("Tela não identificada com clareza."),
        };
    }

    private static ResultadoConferencia ConferirBios(LeituraVisual leitura, PlacaMae placa)
    {
        var versaoLida = leitura.Campo("versao");
        if (string.IsNullOrWhiteSpace(versaoLida) || string.IsNullOrWhiteSpace(placa.VersaoBios))
        {
            return Inconclusivo("Sem versão de BIOS suficiente para comparar leitura e inventário.");
        }

        return VersaoBios.Comparar(versaoLida, placa.VersaoBios) == 0
            ? Confere($"Versão de BIOS confere com o inventário ({placa.VersaoBios}).")
            : Diverge($"Versão lida '{versaoLida}' difere da coletada '{placa.VersaoBios}'.");
    }

    private static ResultadoConferencia ConferirEtiqueta(LeituraVisual leitura, PlacaMae placa)
    {
        var fabricanteLido = NormalizadorFabricante.Normalizar(leitura.Campo("fabricante"));
        var fabricanteInv = NormalizadorFabricante.Normalizar(placa.Fabricante);
        var modeloLido = (leitura.Campo("modelo") ?? string.Empty).Trim();

        var fabricanteOk = string.Equals(fabricanteLido, fabricanteInv, StringComparison.OrdinalIgnoreCase);
        var modeloOk = !string.IsNullOrWhiteSpace(modeloLido)
            && placa.Modelo.Contains(modeloLido, StringComparison.OrdinalIgnoreCase);

        return fabricanteOk && modeloOk
            ? Confere($"Etiqueta confere com o inventário ({fabricanteInv} {placa.Modelo}).")
            : Diverge($"Etiqueta '{fabricanteLido} {modeloLido}' difere do inventário "
                + $"'{fabricanteInv} {placa.Modelo}'.");
    }

    private static ResultadoConferencia Confere(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Confere, Mensagem = mensagem };

    private static ResultadoConferencia Diverge(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Diverge, Mensagem = mensagem };

    private static ResultadoConferencia Inconclusivo(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Inconclusivo, Mensagem = mensagem };

    private static ResultadoConferencia PedirNovaFoto(string mensagem) =>
        new() { Situacao = SituacaoConferencia.Inconclusivo, Mensagem = mensagem, PedirNovaFoto = true };
}
