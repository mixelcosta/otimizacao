using System.Text.Json;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Guard determinístico da resposta do cérebro. Converte o texto/JSON do LLM em
/// uma <see cref="MatrizDecisao"/> válida: descarta qualquer ação que não exista
/// no catálogo e força cada parâmetro à faixa segura (usando o padrão seguro
/// quando o valor proposto é inválido). É o que mantém a regra invariante mesmo
/// se o modelo alucinar — a saída do LLM é tratada como NÃO confiável.
/// </summary>
public sealed class LeitorRespostaCerebro
{
    public MatrizDecisao Ler(
        string respostaLlm, CatalogoAcoes catalogo, OrigemDecisao origem, string? modelo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        var avisos = new List<string>();
        var json = ExtrairJson(respostaLlm);
        if (json is null)
        {
            avisos.Add("Resposta do cérebro não continha JSON interpretável; matriz vazia.");
            return Vazia(origem, modelo, avisos);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            avisos.Add("JSON inválido na resposta do cérebro: " + ex.Message);
            return Vazia(origem, modelo, avisos);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("acoes", out var acoes)
                || acoes.ValueKind != JsonValueKind.Array)
            {
                avisos.Add("Resposta sem a lista 'acoes'; matriz vazia.");
                return Vazia(origem, modelo, avisos);
            }

            var itens = new List<ItemDecisao>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var elemento in acoes.EnumerateArray())
            {
                var item = Interpretar(elemento, catalogo, vistos, avisos);
                if (item is not null)
                {
                    itens.Add(item);
                }
            }

            // Reordena por prioridade declarada e, em empate, por menor risco; renumera.
            var ordenados = itens
                .OrderBy(i => i.Prioridade)
                .ThenBy(i => (int)i.Risco)
                .Select((item, indice) => item with { Prioridade = indice + 1 })
                .ToList();

            return new MatrizDecisao
            {
                Origem = origem,
                Modelo = modelo,
                Itens = ordenados,
                Avisos = avisos,
            };
        }
    }

    private static ItemDecisao? Interpretar(
        JsonElement elemento, CatalogoAcoes catalogo, HashSet<string> vistos, List<string> avisos)
    {
        if (elemento.ValueKind != JsonValueKind.Object
            || !elemento.TryGetProperty("id", out var idProp)
            || idProp.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var id = idProp.GetString()!;
        var acao = catalogo.Obter(id);
        if (acao is null)
        {
            avisos.Add($"Ação '{id}' ignorada: não consta no catálogo whitelisted.");
            return null;
        }

        if (!vistos.Add(id))
        {
            return null; // duplicada: mantém a primeira ocorrência.
        }

        var prioridade = elemento.TryGetProperty("prioridade", out var p)
            && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var valorP)
            ? valorP
            : 999;

        var justificativa = elemento.TryGetProperty("justificativa", out var j)
            && j.ValueKind == JsonValueKind.String
            ? j.GetString()!
            : acao.Descricao;

        var parametrosBrutos = LerParametrosBrutos(elemento);
        var parametros = ForcarFaixaSegura(acao, parametrosBrutos, avisos);

        return new ItemDecisao
        {
            AcaoId = acao.Id,
            Prioridade = prioridade,
            Categoria = acao.Categoria,
            Risco = acao.Risco,
            GanhoEsperado = null,
            Justificativa = justificativa,
            Parametros = parametros,
        };
    }

    private static Dictionary<string, string> LerParametrosBrutos(JsonElement elemento)
    {
        var brutos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (elemento.TryGetProperty("parametros", out var par) && par.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in par.EnumerateObject())
            {
                brutos[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => string.Empty,
                };
            }
        }

        return brutos;
    }

    /// <summary>
    /// Para cada parâmetro declarado pela ação, usa o valor proposto somente se
    /// ele for aceito sob o perfil seguro; caso contrário, aplica o padrão seguro.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ForcarFaixaSegura(
        AcaoOtimizacao acao, IReadOnlyDictionary<string, string> propostos, List<string> avisos)
    {
        var finais = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parametro in acao.Parametros)
        {
            if (propostos.TryGetValue(parametro.Nome, out var valor))
            {
                var resultado = parametro.Validar(valor, TipoPerfil.Seguro);
                if (resultado.Situacao == SituacaoParametro.Aceito)
                {
                    finais[parametro.Nome] = valor;
                    continue;
                }

                avisos.Add(
                    $"Ação '{acao.Id}': valor '{valor}' do parâmetro '{parametro.Nome}' "
                    + $"rejeitado pelo guard; usando padrão seguro '{parametro.ValorPadraoSeguro}'.");
            }

            finais[parametro.Nome] = parametro.ValorPadraoSeguro;
        }

        return finais;
    }

    private static MatrizDecisao Vazia(OrigemDecisao origem, string? modelo, IReadOnlyList<string> avisos) =>
        new()
        {
            Origem = origem,
            Modelo = modelo,
            Itens = Array.Empty<ItemDecisao>(),
            Avisos = avisos,
        };

    /// <summary>Extrai o primeiro objeto JSON do texto, tolerando cercas de markdown.</summary>
    private static string? ExtrairJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{', StringComparison.Ordinal);
        var fim = texto.LastIndexOf('}');
        return inicio >= 0 && fim > inicio ? texto[inicio..(fim + 1)] : null;
    }
}
