namespace HardwareOptimizer.Features.Licensing;

public interface IServicoLicenca
{
    TipoLicenca TipoAtual { get; }

    bool TemAcesso(FuncionalidadePremium funcionalidade);

    Task<ResultadoAtivacao> AtivarAsync(string chave, CancellationToken ct = default);

    Task<ResultadoAtivacao> DesativarAsync(CancellationToken ct = default);
}
