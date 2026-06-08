using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Fonte de leituras de sensores via LibreHardwareMonitor. Abstrai a biblioteca
/// (e o driver de kernel) por trás de uma única chamada, tornando
/// <see cref="LeitorSensoresLhm"/> testável fora do Windows com um fake.
/// A implementação real é defensiva: nunca lança, devolve o que conseguiu ler.
/// </summary>
public interface IFonteSensoresLhm
{
    IReadOnlyList<Sensor> Ler();
}
