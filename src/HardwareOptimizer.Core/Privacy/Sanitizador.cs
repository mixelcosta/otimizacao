using System.Security.Cryptography;
using System.Text;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Core.Privacy;

/// <summary>
/// Pipeline de sanitização entre o coletor e o cérebro. Gera uma versão do
/// inventário "segura para nuvem": dados de baixo risco (modelo de placa, versão
/// de BIOS) são preservados; identificadores únicos correlacionáveis são
/// hasheados; dados de identificação pessoal são removidos.
/// </summary>
public sealed class Sanitizador
{
    private readonly string _sal;
    private readonly ILogger _log;

    /// <param name="sal">
    /// Sal aplicado ao hash. Por padrão, um sal por execução, de modo que os
    /// hashes não sejam correlacionáveis entre máquinas/sessões distintas.
    /// </param>
    /// <param name="logger">Logger opcional para registrar o resumo da sanitização.</param>
    public Sanitizador(string? sal = null, ILogger? logger = null)
    {
        _sal = sal ?? Guid.NewGuid().ToString("N");
        _log = logger ?? NullLogger.Instance;
    }

    public ResultadoSanitizacao Sanitizar(Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        var alteracoes = new List<CampoSanitizado>();

        // Identificadores correlacionáveis (serial, uuid) são preservados apenas
        // como hash; dados de identificação pessoal (nomes, chave) são removidos.
        IdentificadoresSensiveis? identificadoresSeguros = null;
        if (inventario.Identificadores is { } ident)
        {
            identificadoresSeguros = new IdentificadoresSensiveis
            {
                NumeroSerie = HashearCampo("identificadores.numero_serie", ident.NumeroSerie, alteracoes),
                UuidPlaca = HashearCampo("identificadores.uuid_placa", ident.UuidPlaca, alteracoes),
                NomeMaquina = RemoverCampo("identificadores.nome_maquina", ident.NomeMaquina, alteracoes),
                NomeUsuario = RemoverCampo("identificadores.nome_usuario", ident.NomeUsuario, alteracoes),
                ChaveProdutoWindows = RemoverCampo(
                    "identificadores.chave_produto_windows", ident.ChaveProdutoWindows, alteracoes),
            };

            // Nada a preservar? Não emite o bloco.
            if (identificadoresSeguros is
                { NumeroSerie: null, UuidPlaca: null, NomeMaquina: null, NomeUsuario: null, ChaveProdutoWindows: null })
            {
                identificadoresSeguros = null;
            }
        }

        // MAC de cada interface é hasheado.
        var redeSegura = new List<InterfaceRede>(inventario.Rede.Count);
        for (var i = 0; i < inventario.Rede.Count; i++)
        {
            var nic = inventario.Rede[i];
            if (!string.IsNullOrWhiteSpace(nic.EnderecoMac))
            {
                alteracoes.Add(new CampoSanitizado($"rede[{i}].endereco_mac", AcaoSanitizacao.Hasheado));
                redeSegura.Add(nic with { EnderecoMac = Hashear(nic.EnderecoMac) });
            }
            else
            {
                redeSegura.Add(nic);
            }
        }

        var inventarioSeguro = inventario with
        {
            // Identificadores correlacionáveis ficam como hash; PII é removida.
            Identificadores = identificadoresSeguros,
            Rede = redeSegura,
        };

        _log.LogInformation(
            "Sanitização concluída: {Qtd} campo(s) sensível(is) tratado(s) antes do envio à nuvem.",
            alteracoes.Count);

        return new ResultadoSanitizacao(inventarioSeguro, alteracoes);
    }

    private string? HashearCampo(string campo, string? valor, List<CampoSanitizado> alteracoes)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        alteracoes.Add(new CampoSanitizado(campo, AcaoSanitizacao.Hasheado));
        return Hashear(valor);
    }

    private static string? RemoverCampo(string campo, string? valor, List<CampoSanitizado> alteracoes)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            alteracoes.Add(new CampoSanitizado(campo, AcaoSanitizacao.Removido));
        }

        return null;
    }

    /// <summary>Hash SHA-256 salgado e truncado, suficiente para correlação sem revelar o valor.</summary>
    public string Hashear(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var bytes = Encoding.UTF8.GetBytes(_sal + ":" + valor);
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
