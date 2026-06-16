using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.Agent.Security;

/// <summary>
/// Define quais serviços Windows são seguros para exibir e gerenciar via UI.
/// Serviços críticos do SO e de segurança nunca são exibidos ao usuário.
/// </summary>
public static class ListaNegraServicos
{
    // Serviços que, se parados, travam ou corrompem o Windows imediatamente
    private static readonly HashSet<string> _criticos = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Subsistema Win32 central (parar = crash/BSOD imediato) ──────────
        "csrss",            // Client/Server Runtime Subsystem
        "smss",             // Session Manager Subsystem
        "wininit",          // Windows Startup Application
        "winlogon",         // Windows Logon Application
        "services",         // Service Control Manager
        "lsass",            // Local Security Authority Subsystem (TELA AZUL se parar)
        "lsm",              // Local Session Manager

        // ── Autenticação e identidade ────────────────────────────────────────
        "samss",            // Security Account Manager
        "netlogon",         // Net Logon
        "kerberos",         // Kerberos Key Distribution Center
        "seclogon",         // Secondary Logon
        "vaultsvc",         // Credential Manager

        // ── RPC / DCOM (quase todos os serviços dependem destes) ────────────
        "rpcss",            // Remote Procedure Call
        "rpcepmap",         // RPC Endpoint Mapper
        "rpceptmapper",     // alias
        "dcomlaunch",       // DCOM Server Process Launcher

        // ── Gerenciamento de energia e hardware ──────────────────────────────
        "power",            // Power Manager
        "plugplay",         // Plug and Play
        "acpi",             // ACPI Driver

        // ── Infraestrutura central do SO ─────────────────────────────────────
        "winmgmt",          // Windows Management Instrumentation (WMI)
        "eventlog",         // Windows Event Log
        "schedule",         // Task Scheduler
        "cryptsvc",         // Cryptographic Services (necessário para HTTPS e Windows Update)
        "trustedinstaller", // Windows Modules Installer
        "tiledatamodelsvc", // Tile Data Model Server

        // ── Segurança e Firewall ─────────────────────────────────────────────
        "mpssvc",           // Windows Defender Firewall
        "windefend",        // Windows Defender Antivirus
        "wscsvc",           // Security Center
        "mpsdrv",           // Windows Defender Firewall Driver
        "sens",             // System Event Notification Service
        "samsrv",           // SAM Server

        // ── Pilha de rede central (parar = sem internet/rede) ────────────────
        "nsi",              // Network Store Interface
        "dnscache",         // DNS Client
        "dhcp",             // DHCP Client
        "tcpip",            // TCP/IP Protocol Driver
        "netbt",            // NetBIOS over TCP/IP
        "afd",              // Ancillary Function Driver (Winsock)
        "tdx",              // NetIO Legacy TDI Support
        "ndis",             // NDIS System Driver
        "ndu",              // Windows Network Data Usage Monitor
        "lanmanworkstation",// Workstation
        "mup",              // Multiple UNC Provider Driver
        "nfsd",             // Network File System Driver
        "srv2",             // SMB 2.x Driver

        // ── Gerenciamento de volumes e armazenamento ─────────────────────────
        "disk",             // Disk Driver
        "volmgr",           // Volume Manager Driver
        "volsnap",          // Volume Shadow Copy Driver
        "vss",              // Volume Shadow Copy
        "partmgr",          // Partition Manager
        "storahci",         // AHCI Storage Driver
        "stornvme",         // NVMe Storage Driver
        "classpnp",         // SCSI Class System Driver

        // ── Display e sessão de desktop ──────────────────────────────────────
        "dwm",              // Desktop Window Manager (parar = tela preta)
        "uxsms",            // Desktop Window Manager Session Manager
        "sessionenv",       // Remote Desktop Configuration
        "umrdpservice",     // Remote Desktop User Mode Port Redirector

        // ── Base de filtro (necessário para Firewall e segurança de rede) ────
        "bfe",              // Base Filtering Engine

        // ── System Restore (proteção do sistema) ─────────────────────────────
        "srservice",        // System Restore Service
    };

    // Modos de inicialização WMI que indicam drivers de kernel — NUNCA mostrar
    private static readonly HashSet<string> _modosCriticos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Boot",   // Driver carregado no boot pelo bootloader
        "System", // Driver carregado durante inicialização do kernel
    };

    /// <summary>
    /// Retorna <c>true</c> se o serviço pode ser exibido e gerenciado com segurança.
    /// </summary>
    public static bool EhSeguro(ServicoWindows svc) =>
        !_criticos.Contains(svc.Nome) &&
        !_modosCriticos.Contains(svc.ModoInicio ?? "");
}
