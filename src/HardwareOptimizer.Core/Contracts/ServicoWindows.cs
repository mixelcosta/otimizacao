namespace HardwareOptimizer.Core.Contracts;

public sealed record ServicoWindows
{
    public required string Nome { get; init; }

    /// <summary>DisplayName — nome legível do serviço.</summary>
    public required string Descricao { get; init; }

    /// <summary>ProcessId: 0 quando o serviço está parado.</summary>
    public int Pid { get; init; }

    /// <summary>"Running", "Stopped", "Start Pending", etc.</summary>
    public required string Status { get; init; }

    /// <summary>Conta/grupo de execução (StartName do Win32_Service).</summary>
    public string? Grupo { get; init; }

    /// <summary>"Auto", "Manual", "Disabled".</summary>
    public string? ModoInicio { get; init; }
}
