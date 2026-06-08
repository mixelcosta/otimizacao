using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace HardwareOptimizer.Agent.Platform;

/// <summary>
/// Implementação real de <see cref="IAcessoRegistro"/> sobre
/// <see cref="Microsoft.Win32.Registry"/>. Só é instanciada sob Windows (ver
/// <see cref="Execution.Windows.EstadoSistemaWindows.Selecionar"/>), por isso a
/// anotação de plataforma.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AcessoRegistroWindows : IAcessoRegistro
{
    public uint? LerDword(ColmeiaRegistro colmeia, string subchave, string nome)
    {
        using var chave = BaseDe(colmeia).OpenSubKey(subchave, writable: false);
        var valor = chave?.GetValue(nome);

        // Um DWORD volta como int (boxed). 0xFFFFFFFF chega como -1; converte sem
        // perda para uint mantendo a semântica de 32 bits.
        return valor is null
            ? null
            : unchecked((uint)Convert.ToInt64(valor, CultureInfo.InvariantCulture));
    }

    public void EscreverDword(ColmeiaRegistro colmeia, string subchave, string nome, uint valor)
    {
        using var chave = BaseDe(colmeia).CreateSubKey(subchave, writable: true);
        chave.SetValue(nome, unchecked((int)valor), RegistryValueKind.DWord);
    }

    public void RemoverValor(ColmeiaRegistro colmeia, string subchave, string nome)
    {
        using var chave = BaseDe(colmeia).OpenSubKey(subchave, writable: true);
        chave?.DeleteValue(nome, throwOnMissingValue: false);
    }

    private static RegistryKey BaseDe(ColmeiaRegistro colmeia) => colmeia switch
    {
        ColmeiaRegistro.LocalMachine => Registry.LocalMachine,
        ColmeiaRegistro.CurrentUser => Registry.CurrentUser,
        _ => throw new ArgumentOutOfRangeException(nameof(colmeia), colmeia, "Colmeia não suportada."),
    };
}
