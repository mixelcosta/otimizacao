using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Cerebro;

/// <summary>
/// Monta os prompts do cérebro. O system prompt fixa as regras invariantes (só
/// IDs do catálogo, JSON estrito, ordem da filosofia); o user prompt traz o
/// inventário <b>sanitizado</b> e um resumo do catálogo com IDs e limites.
/// </summary>
public sealed class ConstrutorPrompt
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string MontarSistema(CatalogoAcoes catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return
            "Você é o cérebro de um sistema de otimização de hardware. Sua função é "
            + "SELECIONAR e PRIORIZAR ações de um catálogo fechado — você NUNCA inventa ações, "
            + "comandos ou parâmetros fora do catálogo fornecido.\n\n"
            + "Regras invariantes:\n"
            + "1. Use APENAS os IDs de ação presentes no catálogo do usuário.\n"
            + "2. Para cada ação escolhida, defina os parâmetros dentro da faixa segura indicada.\n"
            + "3. Priorize segundo a ordem: ESTABILIDADE > SEGURANÇA > EFICIÊNCIA > DESEMPENHO.\n"
            + "4. Busque o maior desempenho SUSTENTÁVEL e validado, não o maior possível.\n"
            + "5. Justifique cada escolha com base nas evidências do inventário.\n\n"
            + "Responda EXCLUSIVAMENTE com um JSON neste formato, sem texto adicional:\n"
            + "{\"acoes\":[{\"id\":\"<ID_DO_CATALOGO>\",\"prioridade\":1,"
            + "\"justificativa\":\"<motivo>\",\"parametros\":{\"<nome>\":\"<valor>\"}}]}\n"
            + $"(Catálogo versão {catalogo.Versao}.)";
    }

    public string MontarUsuario(Inventario inventarioSanitizado, CatalogoAcoes catalogo)
    {
        ArgumentNullException.ThrowIfNull(inventarioSanitizado);
        ArgumentNullException.ThrowIfNull(catalogo);

        var sb = new StringBuilder();
        sb.AppendLine("# Inventário (sanitizado)");
        sb.AppendLine(JsonSerializer.Serialize(inventarioSanitizado, Json));
        sb.AppendLine();
        sb.AppendLine("# Catálogo de ações disponíveis");
        foreach (var acao in catalogo.Todas.OrderBy(a => a.Categoria).ThenBy(a => a.Id, StringComparer.Ordinal))
        {
            sb.Append("- ").Append(acao.Id)
                .Append(" [").Append(acao.Categoria).Append("] risco=").Append(acao.Risco)
                .Append(": ").Append(acao.Titulo);

            foreach (var parametro in acao.Parametros)
            {
                sb.Append(" | param ").Append(parametro.Nome).Append('=').Append(DescreverParametro(parametro));
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Selecione e priorize as ações adequadas a este equipamento. Responda só com o JSON.");
        return sb.ToString();
    }

    private static string DescreverParametro(Parametro parametro) => parametro switch
    {
        ParametroNumerico n => string.Create(
            CultureInfo.InvariantCulture,
            $"faixa_segura [{n.FaixaSegura.Minimo}..{n.FaixaSegura.Maximo}]{n.Unidade} (padrão {n.PadraoSeguro})"),
        ParametroListaBranca l => "um de {" + string.Join(", ", l.ValoresSeguros) + "}",
        _ => "(sem detalhe)",
    };
}
