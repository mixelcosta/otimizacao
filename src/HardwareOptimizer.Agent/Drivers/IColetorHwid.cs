using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Drivers;

/// <summary>Coleta informações de drivers instalados no sistema.</summary>
public interface IColetorHwid
{
    IReadOnlyList<InfoDriver> Coletar();
}
