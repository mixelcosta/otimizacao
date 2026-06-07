using HardwareOptimizer.Core.Catalog;
using HardwareOptimizer.Core.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Core.Profiles;

/// <summary>
/// Constrói e valida perfis contra o catálogo. Implementa a regra
/// <c>validacao_ao_salvar</c>: bloqueia limite absoluto, marca riscos assumidos
/// e sinaliza quando o fluxo de consentimento é obrigatório.
/// </summary>
public sealed class ConstrutorPerfil
{
    private readonly CatalogoAcoes _catalogo;
    private readonly ValidadorAcao _validador;
    private readonly ILogger _log;

    public ConstrutorPerfil(CatalogoAcoes catalogo, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        _catalogo = catalogo;
        _validador = new ValidadorAcao(catalogo);
        _log = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Cria o perfil seguro (padrão): cada ação recebe o valor padrão seguro de
    /// seus parâmetros. Não exige consentimento além da aprovação por categoria.
    /// </summary>
    public ResultadoConstrucaoPerfil CriarPerfilSeguro(string nome, IEnumerable<string> acaoIds)
    {
        ArgumentNullException.ThrowIfNull(acaoIds);

        var selecoes = new List<SelecaoAcao>();
        var bloqueios = new List<string>();

        foreach (var id in acaoIds)
        {
            var acao = _catalogo.Obter(id);
            if (acao is null)
            {
                bloqueios.Add($"Ação '{id}' não consta no catálogo whitelisted.");
                continue;
            }

            var parametros = acao.Parametros.ToDictionary(
                p => p.Nome, p => p.ValorPadraoSeguro, StringComparer.OrdinalIgnoreCase);

            selecoes.Add(new SelecaoAcao { AcaoId = id, Parametros = parametros });
        }

        return Montar(nome, TipoPerfil.Seguro, "sistema", selecoes, bloqueios);
    }

    /// <summary>
    /// Cria um perfil customizado a partir das seleções do usuário. Sempre exige
    /// o fluxo de consentimento ao salvar/aplicar.
    /// </summary>
    public ResultadoConstrucaoPerfil CriarPerfilCustomizado(
        string nome, string autor, IEnumerable<SelecaoAcao> selecoes)
    {
        ArgumentNullException.ThrowIfNull(selecoes);
        return Montar(nome, TipoPerfil.Customizado, autor, selecoes.ToList(), new List<string>());
    }

    private ResultadoConstrucaoPerfil Montar(
        string nome,
        TipoPerfil tipo,
        string autor,
        IReadOnlyList<SelecaoAcao> selecoes,
        List<string> bloqueios)
    {
        var validacoes = new List<ResultadoValidacaoAcao>();
        var riscos = new List<RiscoAssumido>();

        // Perfil customizado sempre exige consentimento explícito ao salvar/aplicar.
        var exigeConsentimento = tipo == TipoPerfil.Customizado;

        foreach (var selecao in selecoes)
        {
            var validacao = _validador.Validar(selecao.AcaoId, selecao.Parametros, tipo);
            validacoes.Add(validacao);

            if (!validacao.Aplicavel)
            {
                bloqueios.AddRange(validacao.ComoResultado().Erros);
            }

            if (validacao.ExigeConsentimento)
            {
                exigeConsentimento = true;
                foreach (var p in validacao.Parametros.Where(p => p.ExigeConsentimento))
                {
                    riscos.Add(new RiscoAssumido(selecao.AcaoId, p.Parametro, p.Valor, p.Mensagem));
                }
            }
        }

        var sucesso = bloqueios.Count == 0;

        if (!sucesso)
        {
            _log.LogWarning(
                "Perfil '{Nome}' ({Tipo}) NÃO salvo: {Qtd} bloqueio(s) -> {Bloqueios}",
                nome, tipo, bloqueios.Count, string.Join(" | ", bloqueios));
        }
        else
        {
            _log.LogInformation(
                "Perfil '{Nome}' ({Tipo}) válido. Risco assumido em {Riscos} parâmetro(s); exige consentimento={Consent}.",
                nome, tipo, riscos.Count, exigeConsentimento);
        }

        Perfil? perfil = sucesso
            ? new Perfil
            {
                Nome = nome,
                Tipo = tipo,
                Autor = autor,
                Selecoes = selecoes,
                ConsentimentoRegistrado = false,
            }
            : null;

        return new ResultadoConstrucaoPerfil(sucesso, perfil, exigeConsentimento, validacoes, bloqueios, riscos);
    }
}
