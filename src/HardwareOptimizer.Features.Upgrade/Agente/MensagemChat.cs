namespace HardwareOptimizer.Features.Upgrade.Agente;

public sealed record MensagemChat
{
    public required string Role { get; init; }   // "user" | "assistant"
    public required string Conteudo { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}
