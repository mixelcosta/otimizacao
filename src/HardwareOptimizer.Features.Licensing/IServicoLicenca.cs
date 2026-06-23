namespace HardwareOptimizer.Features.Licensing;

public interface IServicoLicenca
{
    TipoLicenca TipoAtual { get; }

    string? NomeCliente { get; }
    string? EmailCliente { get; }

    bool TemAcesso(FuncionalidadePremium funcionalidade);

    Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default);

    Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default);

    Task<ResultadoAtivacao> ValidarOnlineAsync(CancellationToken ct = default);
}
