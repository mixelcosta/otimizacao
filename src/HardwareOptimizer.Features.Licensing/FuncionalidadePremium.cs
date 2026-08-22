namespace HardwareOptimizer.Features.Licensing;

/// <summary>
/// Só <see cref="ContadorVidaUtil"/> é de fato usado hoje — os demais valores de
/// Trilha Grátis (Upgrade, Drivers, Guia BIOS IA, Diagnóstico de Manutenção)
/// nunca deveriam gatear navegação (EXPERIENCE.md, decisão [ADOPTED]; corrigido
/// em 2026-08-22 via bmad-correct-course). Mantidos aqui só como reserva pra
/// uma futura Assinatura Premium (Fase 2 do PRD, módulos ainda não definidos).
/// </summary>
public enum FuncionalidadePremium
{
    ModuloUpgrade = 0,
    ContadorVidaUtil = 1,
    GerenciadorDrivers = 2,
    GuiaBiosIa = 3,
}

public enum TipoLicenca
{
    Gratuita = 0,
    Premium = 1,
}
