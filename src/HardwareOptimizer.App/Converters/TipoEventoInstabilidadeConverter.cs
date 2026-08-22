using System.Globalization;
using Avalonia.Data.Converters;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.Converters;

/// <summary>
/// Rótulo PT-BR pra <see cref="TipoEventoInstabilidade"/> — sem isso, a UI
/// mostrava o nome bruto do enum em inglês (ex. "CrashAplicacao" já é PT-BR,
/// mas "Bsod"/"Whea" não são rótulos legíveis pro usuário final), inconsistente
/// com a própria justificativa desta história de que o público-alvo é PT-BR
/// (achado da revisão independente da Story 1.5).
/// </summary>
public sealed class TipoEventoInstabilidadeConverter : IValueConverter
{
    public static readonly TipoEventoInstabilidadeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            TipoEventoInstabilidade.Bsod => "TELA AZUL",
            TipoEventoInstabilidade.Whea => "ERRO DE HARDWARE (WHEA)",
            TipoEventoInstabilidade.CrashAplicacao => "CRASH DE APLICATIVO",
            _ => value?.ToString(),
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
