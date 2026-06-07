using HardwareOptimizer.Core.Common;

namespace HardwareOptimizer.Core.Catalog;

/// <summary>
/// Fábrica do catálogo embutido. Cada ação aqui é auditada e versionada; os
/// comandos internos correspondentes vivem no agente local (nunca no LLM).
/// </summary>
public static class CatalogoPadrao
{
    /// <summary>Versão do catálogo, registrada na auditoria de consentimento.</summary>
    public const string Versao = "2024.06-mvp";

    public static CatalogoAcoes Criar() => new(Versao, ConstruirAcoes());

    private static IEnumerable<AcaoOtimizacao> ConstruirAcoes()
    {
        yield return new AcaoOtimizacao
        {
            Id = "PWR_PLANO_ALTO_DESEMPENHO",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Ativar plano de energia de alto desempenho",
            Descricao = "Seleciona o plano de energia de alto desempenho do Windows (powercfg).",
            ComandoInternoId = "cmd.pwr.plano_alto_desempenho.v1",
            Reversao = "Restaurar plano de energia anterior exportado no backup.",
            Risco = NivelRisco.MuitoBaixo,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "PWR_USB_SUSPENSAO_SELETIVA",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Desativar suspensão seletiva de USB",
            Descricao = "Impede que o Windows suspenda dispositivos USB, reduzindo microcortes de periféricos.",
            ComandoInternoId = "cmd.pwr.usb_suspensao_seletiva.v1",
            Reversao = "Reativar a suspensão seletiva de USB.",
            Risco = NivelRisco.MuitoBaixo,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "SO_EFEITOS_VISUAIS_DESEMPENHO",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Ajustar efeitos visuais para desempenho",
            Descricao = "Desativa animações e efeitos visuais não essenciais da interface.",
            ComandoInternoId = "cmd.so.efeitos_visuais.v1",
            Reversao = "Restaurar a configuração anterior de efeitos visuais.",
            Risco = NivelRisco.Nenhum,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        // Parâmetro numérico exemplar (menor = mais agressivo). Padrão do Windows = 20.
        yield return new AcaoOtimizacao
        {
            Id = "SO_SYSTEM_RESPONSIVENESS",
            Categoria = CategoriaAcao.SistemaOperacional,
            Titulo = "Ajustar reserva de responsividade do sistema",
            Descricao = "Define o percentual de CPU reservado a tarefas de segundo plano " +
                        "(registro SystemResponsiveness). Reduzir prioriza primeiro plano.",
            Parametros = new Parametro[]
            {
                new ParametroNumerico(
                    nome: "percentual_reserva",
                    descricao: "Percentual reservado a tarefas de baixa prioridade.",
                    faixaSegura: new FaixaNumerica(10, 20),
                    faixaPermitida: new FaixaNumerica(0, 20),
                    limiteAbsoluto: 20,
                    padraoSeguro: 20,
                    unidade: "%"),
            },
            ComandoInternoId = "cmd.so.system_responsiveness.v1",
            Reversao = "Restaurar o valor anterior de SystemResponsiveness.",
            Risco = NivelRisco.Baixo,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        // Parâmetro numérico exemplar (maior = mais tolerante, porém mascara falhas). Padrão = 2s.
        yield return new AcaoOtimizacao
        {
            Id = "GPU_TDR_DELAY",
            Categoria = CategoriaAcao.Gpu,
            Titulo = "Ajustar tempo de recuperação do driver de vídeo (TDR)",
            Descricao = "Define o tempo (s) antes de o Windows reiniciar o driver de vídeo travado " +
                        "(registro TdrDelay). Valores altos mascaram instabilidade real.",
            Parametros = new Parametro[]
            {
                new ParametroNumerico(
                    nome: "tempo_segundos",
                    descricao: "Tempo de espera antes do reset do driver de vídeo.",
                    faixaSegura: new FaixaNumerica(2, 8),
                    faixaPermitida: new FaixaNumerica(2, 60),
                    limiteAbsoluto: 60,
                    padraoSeguro: 2,
                    unidade: "s"),
            },
            ComandoInternoId = "cmd.gpu.tdr_delay.v1",
            Reversao = "Restaurar o valor anterior de TdrDelay.",
            Risco = NivelRisco.Medio,
            RequerAprovacao = true,
            RequerReinicio = true,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "GPU_HAGS",
            Categoria = CategoriaAcao.Gpu,
            Titulo = "Ativar agendamento de GPU acelerado por hardware (HAGS)",
            Descricao = "Habilita o Hardware Accelerated GPU Scheduling, quando suportado pelo driver.",
            ComandoInternoId = "cmd.gpu.hags.v1",
            Reversao = "Desativar o agendamento de GPU acelerado por hardware.",
            Risco = NivelRisco.Baixo,
            RequerAprovacao = true,
            RequerReinicio = true,
            PreCondicoes = new[] { "backup_confirmado" },
        };

        // Lista branca de serviços considerados seguros de desativar (conservadora).
        yield return new AcaoOtimizacao
        {
            Id = "SRV_DESATIVAR_SERVICO",
            Categoria = CategoriaAcao.Servicos,
            Titulo = "Desativar serviço não essencial",
            Descricao = "Desativa um serviço presente na lista branca de serviços seguros.",
            Parametros = new Parametro[]
            {
                new ParametroListaBranca(
                    nome: "nome_servico",
                    descricao: "Nome do serviço do Windows a desativar.",
                    valoresSeguros: new[]
                    {
                        "SysMain", "DiagTrack", "Fax", "RetailDemo",
                        "MapsBroker", "XblGameSave", "XboxNetApiSvc",
                    },
                    padraoSeguro: "DiagTrack"),
            },
            ComandoInternoId = "cmd.srv.desativar_servico.v1",
            Reversao = "Reativar o serviço com o tipo de inicialização anterior.",
            Risco = NivelRisco.Medio,
            RequerAprovacao = true,
            RequerReinicio = false,
            PreCondicoes = new[] { "backup_confirmado", "servico_consta_na_lista_segura" },
        };

        yield return new AcaoOtimizacao
        {
            Id = "NET_THROTTLING_DESABILITAR",
            Categoria = CategoriaAcao.Rede,
            Titulo = "Desabilitar limitação de rede (NetworkThrottlingIndex)",
            Descricao = "Remove a limitação de throughput de rede imposta pelo agendador multimídia.",
            ComandoInternoId = "cmd.net.throttling_index.v1",
            Reversao = "Restaurar o NetworkThrottlingIndex anterior (padrão 10).",
            Risco = NivelRisco.Baixo,
            RequerAprovacao = true,
            RequerReinicio = true,
            PreCondicoes = new[] { "backup_confirmado" },
        };
    }
}
