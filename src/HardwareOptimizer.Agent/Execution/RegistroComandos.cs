using System.Globalization;

namespace HardwareOptimizer.Agent.Execution;

/// <summary>
/// Mapeia cada <c>comando_interno</c> do catálogo à sua implementação
/// determinística versionada. É a fronteira entre a seleção (LLM) e a execução
/// (agente): só IDs aqui registrados podem rodar.
/// </summary>
public sealed class RegistroComandos
{
    private readonly IReadOnlyDictionary<string, IComandoInterno> _comandos;

    public RegistroComandos(IEnumerable<IComandoInterno> comandos)
    {
        ArgumentNullException.ThrowIfNull(comandos);
        var mapa = new Dictionary<string, IComandoInterno>(StringComparer.OrdinalIgnoreCase);
        foreach (var comando in comandos)
        {
            if (!mapa.TryAdd(comando.Id, comando))
            {
                throw new ArgumentException($"Comando interno duplicado: '{comando.Id}'.", nameof(comandos));
            }
        }

        _comandos = mapa;
    }

    public bool Contem(string comandoId) => comandoId is not null && _comandos.ContainsKey(comandoId);

    public IComandoInterno? Obter(string comandoId) =>
        comandoId is not null && _comandos.TryGetValue(comandoId, out var c) ? c : null;

    /// <summary>
    /// Registro padrão para o catálogo embutido, operando sobre um
    /// <see cref="IEstadoSistema"/> (simulado no MVP). Os alvos e valores abaixo
    /// refletem as chaves reais que as implementações Windows manipulariam.
    /// </summary>
    public static RegistroComandos Padrao(IEstadoSistema estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        IComandoInterno Fixo(string id, string alvo, string valor) =>
            new ComandoEstadoSistema(id, estado, _ => alvo, _ => valor);

        IComandoInterno DeParametro(string id, string alvo, string parametro) =>
            new ComandoEstadoSistema(
                id, estado,
                _ => alvo,
                p => p.TryGetValue(parametro, out var v) ? v : throw FaltaParametro(parametro, id));

        return new RegistroComandos(new[]
        {
            Fixo("cmd.pwr.plano_alto_desempenho.v1", "powercfg:plano_ativo", "ALTO_DESEMPENHO"),
            Fixo("cmd.pwr.usb_suspensao_seletiva.v1", "powercfg:usb_suspensao_seletiva", "DESABILITADO"),
            Fixo("cmd.so.efeitos_visuais.v1", "registro:VisualFXSetting", "DESEMPENHO"),
            DeParametro("cmd.so.system_responsiveness.v1", "registro:SystemResponsiveness", "percentual_reserva"),
            DeParametro("cmd.gpu.tdr_delay.v1", "registro:TdrDelay", "tempo_segundos"),
            Fixo("cmd.gpu.hags.v1", "registro:HwSchMode", "2"),
            Fixo("cmd.net.throttling_index.v1", "registro:NetworkThrottlingIndex", "ffffffff"),
            new ComandoEstadoSistema(
                "cmd.srv.desativar_servico.v1",
                estado,
                p => "servico:" + (p.TryGetValue("nome_servico", out var nome)
                    ? nome
                    : throw FaltaParametro("nome_servico", "cmd.srv.desativar_servico.v1")),
                _ => "Disabled"),
        });
    }

    private static InvalidOperationException FaltaParametro(string parametro, string comandoId) =>
        new(string.Format(
            CultureInfo.InvariantCulture,
            "Parâmetro '{0}' ausente para o comando '{1}'.", parametro, comandoId));
}
