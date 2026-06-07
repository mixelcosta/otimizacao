using System.Security.Cryptography;
using System.Text;
using HardwareOptimizer.Core.Contracts;

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

    /// <param name="sal">
    /// Sal aplicado ao hash. Por padrão, um sal por execução, de modo que os
    /// hashes não sejam correlacionáveis entre máquinas/sessões distintas.
    /// </param>
    public Sanitizador(string? sal = null)
    {
        _sal = sal ?? Guid.NewGuid().ToString("N");
    }

    public ResultadoSanitizacao Sanitizar(Inventario inventario)
    {
        ArgumentNullException.ThrowIfNull(inventario);

        var alteracoes = new List<CampoSanitizado>();

        // Identificadores sensíveis são removidos do payload de nuvem por completo.
        // Os que têm valor de correlação são preservados apenas como hash, à parte.
        if (inventario.Identificadores is { } ident)
        {
            RegistrarHash("identificadores.numero_serie", ident.NumeroSerie, alteracoes);
            RegistrarHash("identificadores.uuid_placa", ident.UuidPlaca, alteracoes);
            RegistrarRemocao("identificadores.nome_maquina", ident.NomeMaquina, alteracoes);
            RegistrarRemocao("identificadores.nome_usuario", ident.NomeUsuario, alteracoes);
            RegistrarRemocao("identificadores.chave_produto_windows", ident.ChaveProdutoWindows, alteracoes);
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
            // Bloco de identificadores não acompanha o payload de nuvem.
            Identificadores = null,
            Rede = redeSegura,
        };

        return new ResultadoSanitizacao(inventarioSeguro, alteracoes);
    }

    private void RegistrarHash(string campo, string? valor, List<CampoSanitizado> alteracoes)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            alteracoes.Add(new CampoSanitizado(campo, AcaoSanitizacao.Hasheado));
        }
    }

    private static void RegistrarRemocao(string campo, string? valor, List<CampoSanitizado> alteracoes)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            alteracoes.Add(new CampoSanitizado(campo, AcaoSanitizacao.Removido));
        }
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
