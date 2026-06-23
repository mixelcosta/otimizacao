using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace HardwareOptimizer.Features.Licensing;

/// <summary>
/// Valida chaves de licença geradas offline via HMAC-SHA256.
/// Chaves são vinculadas ao ID da máquina — não podem ser compartilhadas entre PCs.
///
/// Formato: XXXXX-XXXXX-XXXXX-XXXXX  (20 hex chars = primeiros 10 bytes do HMAC)
///
/// Para gerar uma chave para um cliente:
///   new ValidadorChaveLicenca().GerarChave(machineGuid)
/// </summary>
public sealed class ValidadorChaveLicenca
{
    // Segredo embutido como bytes (não aparece como string literal em análise binária)
    // ATENÇÃO: em produção, injetar via CI/CD como variável de build secreta.
    private static ReadOnlySpan<byte> Segredo => new byte[]
    {
        0x4F, 0x74, 0x69, 0x6D, 0x69, 0x7A, 0x65, 0x42,
        0x75, 0x69, 0x6C, 0x64, 0x65, 0x72, 0x4B, 0x65,
        0x79, 0x32, 0x36, 0x21, 0x23, 0x40, 0x26, 0x24,
        0x2A, 0x5E, 0x25, 0x7C
    };

    private const int ComprimentoChaveHex = 20;

    /// <summary>Retorna true quando a chave fornecida é válida para o ID de máquina informado.</summary>
    public bool ChaveValida(string chave, string machineId)
    {
        if (string.IsNullOrWhiteSpace(chave) || string.IsNullOrWhiteSpace(machineId))
            return false;

        var normalizada = Normalizar(chave);
        if (normalizada.Length != ComprimentoChaveHex)
            return false;

        var esperado = Computar(machineId);
        return ConstantTimeEquals(normalizada, esperado);
    }

    /// <summary>
    /// Gera a chave de licença válida para o ID de máquina informado.
    /// Uso exclusivo da ferramenta de geração de chaves do desenvolvedor.
    /// </summary>
    public string GerarChave(string machineId)
    {
        var hex = Computar(machineId);
        return $"{hex[0..5]}-{hex[5..10]}-{hex[10..15]}-{hex[15..20]}";
    }

    private static string Computar(string machineId)
    {
        var dados = Encoding.UTF8.GetBytes(machineId);
        var hash = HMACSHA256.HashData(Segredo.ToArray(), dados);
        return Convert.ToHexString(hash)[..ComprimentoChaveHex].ToUpperInvariant();
    }

    private static string Normalizar(string chave) =>
        chave.Replace("-", "").Replace(" ", "").ToUpperInvariant();

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
