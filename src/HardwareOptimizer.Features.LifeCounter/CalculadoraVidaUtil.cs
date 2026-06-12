using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using HardwareOptimizer.Agent.Smart; // ILeitorSmart
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace HardwareOptimizer.Features.LifeCounter;

public sealed class CalculadoraVidaUtil
{
    private static readonly Lazy<IReadOnlyList<EntradaTbw>> _database =
        new(CarregarDatabase);

    private readonly ILeitorSmart _leitor;
    private readonly ILogger<CalculadoraVidaUtil> _log;

    public CalculadoraVidaUtil(ILeitorSmart leitor, ILogger<CalculadoraVidaUtil> log)
    {
        _leitor = leitor;
        _log = log;
    }

    [SupportedOSPlatform("windows")]
    public IReadOnlyList<SaudeDisco> Calcular()
    {
        var dados = _leitor.LerDados();
        var resultado = new List<SaudeDisco>();

        foreach (var d in dados)
        {
            var tbwFabricante = BuscarTbwFabricante(d.Modelo);
            var porcentagem = CalcularPorcentagem(d.TbwEscritoGb, tbwFabricante);
            var nivel = ClassificarNivel(porcentagem, d.TemErrosNaoCorrigiveis, d.SetoresPendentes);

            resultado.Add(new SaudeDisco
            {
                Modelo = d.Modelo,
                Letra = "?",
                TbwEscritoGb = d.TbwEscritoGb,
                TbwFabricanteGb = tbwFabricante,
                HorasUso = d.HorasUso,
                PorcentagemVidaRestante = porcentagem,
                Nivel = nivel,
                TemErrosNaoCorrigiveis = d.TemErrosNaoCorrigiveis,
                SetoresComProblema = d.SetoresPendentes,
            });
        }

        return resultado;
    }

    private static long BuscarTbwFabricante(string modelo)
    {
        var db = _database.Value;
        var entrada = db.FirstOrDefault(e =>
            modelo.Contains(e.ModeloFragmento, StringComparison.OrdinalIgnoreCase));
        return entrada?.TbwGb ?? 0;
    }

    private static double CalcularPorcentagem(long escrito, long maximo)
    {
        if (maximo <= 0) return 100.0; // TBW desconhecido → assume 100% restante
        var usado = Math.Min((double)escrito / maximo, 1.0);
        return Math.Round((1.0 - usado) * 100.0, 1);
    }

    private static NivelSaudeDisco ClassificarNivel(
        double porcentagem, bool temErros, int setoresPendentes)
    {
        if (temErros || setoresPendentes > 0 || porcentagem < 10)
            return NivelSaudeDisco.Critico;
        if (porcentagem < 30)
            return NivelSaudeDisco.Atencao;
        return NivelSaudeDisco.Bom;
    }

    private static IReadOnlyList<EntradaTbw> CarregarDatabase()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string recurso = "HardwareOptimizer.Features.LifeCounter.Data.tbw_database.json";
        using var stream = assembly.GetManifestResourceStream(recurso);
        if (stream is null) return Array.Empty<EntradaTbw>();

        var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("discos")
            .EnumerateArray()
            .Select(e => new EntradaTbw(
                e.GetProperty("modeloFragmento").GetString() ?? string.Empty,
                e.GetProperty("tbwGb").GetInt64()))
            .ToList();
    }

    private sealed record EntradaTbw(string ModeloFragmento, long TbwGb);
}
