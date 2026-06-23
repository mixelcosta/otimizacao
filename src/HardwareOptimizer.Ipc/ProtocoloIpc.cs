using System.Text.Json;
using System.Text.Json.Serialization;
using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Ipc;

/// <summary>Requisição IPC: método + parâmetros opcionais (JSON livre).</summary>
public sealed record RequisicaoIpc
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required string Metodo { get; init; }

    public JsonElement? Parametros { get; init; }
}

/// <summary>Resposta IPC. O resultado é serializado apenas no transporte.</summary>
public sealed record RespostaIpc
{
    public required string Id { get; init; }

    public required bool Sucesso { get; init; }

    public object? Resultado { get; init; }

    public string? Erro { get; init; }

    public static RespostaIpc Ok(string id, object? resultado) =>
        new() { Id = id, Sucesso = true, Resultado = resultado };

    public static RespostaIpc Falha(string id, string erro) =>
        new() { Id = id, Sucesso = false, Erro = erro };
}

/// <summary>Resumo de uma ação do catálogo, próprio para serialização/UI.</summary>
public sealed record AcaoResumoDto
{
    public required string Id { get; init; }

    public required CategoriaAcao Categoria { get; init; }

    public required string Titulo { get; init; }

    public required NivelRisco Risco { get; init; }

    public bool RequerReinicio { get; init; }

    public IReadOnlyList<string> PreCondicoes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ParametroResumoDto> Parametros { get; init; } = Array.Empty<ParametroResumoDto>();

    public static AcaoResumoDto De(AcaoOtimizacao acao) => new()
    {
        Id = acao.Id,
        Categoria = acao.Categoria,
        Titulo = acao.Titulo,
        Risco = acao.Risco,
        RequerReinicio = acao.RequerReinicio,
        PreCondicoes = acao.PreCondicoes,
        Parametros = acao.Parametros.Select(ParametroResumoDto.De).ToList(),
    };
}

/// <summary>Resumo de um parâmetro (numérico ou lista branca) para a UI.</summary>
public sealed record ParametroResumoDto
{
    public required string Nome { get; init; }

    public required string Tipo { get; init; }

    public string? Detalhe { get; init; }

    public static ParametroResumoDto De(Parametro parametro) => parametro switch
    {
        ParametroNumerico n => new ParametroResumoDto
        {
            Nome = n.Nome,
            Tipo = "numerico",
            Detalhe = $"seguro {n.FaixaSegura}, permitido {n.FaixaPermitida}, "
                + $"limite_absoluto {n.LimiteAbsoluto}, padrão {n.PadraoSeguro}{n.Unidade}",
        },
        ParametroListaBranca l => new ParametroResumoDto
        {
            Nome = l.Nome,
            Tipo = "lista_branca",
            Detalhe = string.Join(", ", l.ValoresSeguros),
        },
        _ => new ParametroResumoDto { Nome = parametro.Nome, Tipo = "desconhecido" },
    };
}

/// <summary>Status da licença retornado pelo método <c>obterstatuslicenca</c>.</summary>
public sealed record StatusLicencaDto
{
    public required string Tipo { get; init; }
    public required bool ModuloUpgrade { get; init; }
    public required bool ContadorVidaUtil { get; init; }
    public required bool GerenciadorDrivers { get; init; }
    public required bool GuiaBiosIa { get; init; }
}

/// <summary>Opções de serialização compartilhadas pelo protocolo IPC.</summary>
public static class ProtocoloIpc
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
