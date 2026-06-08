namespace HardwareOptimizer.Agent.Platform;

/// <summary>Colmeia (hive) do registro do Windows.</summary>
public enum ColmeiaRegistro
{
    /// <summary>HKEY_LOCAL_MACHINE (configurações do sistema; exige elevação).</summary>
    LocalMachine,

    /// <summary>HKEY_CURRENT_USER (configurações do usuário atual).</summary>
    CurrentUser,
}

/// <summary>
/// Porta para o registro do Windows, restrita a valores DWORD (suficiente para o
/// catálogo atual). Abstraída para que a lógica de <see cref="Execution.Windows.EstadoSistemaWindows"/>
/// seja testável em qualquer plataforma com um fake, sem tocar o registro real.
/// </summary>
public interface IAcessoRegistro
{
    /// <summary>Lê um valor DWORD; nulo se a chave ou o valor não existir.</summary>
    uint? LerDword(ColmeiaRegistro colmeia, string subchave, string nome);

    /// <summary>Escreve um valor DWORD, criando a subchave se necessário.</summary>
    void EscreverDword(ColmeiaRegistro colmeia, string subchave, string nome, uint valor);

    /// <summary>Remove um valor (sem erro se ausente) — usado no rollback para "não definido".</summary>
    void RemoverValor(ColmeiaRegistro colmeia, string subchave, string nome);
}
