namespace HardwareOptimizer.Core.Contracts;

public sealed record CategoriaLimpeza(string Id, string Nome, long TamanhoBytes)
{
    public string TamanhoFormatado => TamanhoBytes switch
    {
        >= 1_073_741_824 => $"{TamanhoBytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{TamanhoBytes / 1_048_576.0:F1} MB",
        >= 1_024         => $"{TamanhoBytes / 1_024.0:F0} KB",
        _                => TamanhoBytes > 0 ? $"{TamanhoBytes} B" : "—",
    };
}

public sealed record ResultadoLimpeza(long TotalBytes, IReadOnlyList<string> Erros)
{
    public string TotalFormatado => TotalBytes switch
    {
        >= 1_073_741_824 => $"{TotalBytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{TotalBytes / 1_048_576.0:F1} MB",
        >= 1_024         => $"{TotalBytes / 1_024.0:F0} KB",
        _                => TotalBytes > 0 ? $"{TotalBytes} B" : "0 KB",
    };
}
