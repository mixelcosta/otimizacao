using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using HardwareOptimizer.Agent.Drivers;
using HardwareOptimizer.Agent.Smart;
using HardwareOptimizer.Agent.Startup;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace HardwareOptimizer.Agent.Collector;

/// <summary>
/// Leitor de inventário para Windows (plataforma prioritária do MVP). Usa
/// PowerShell + CIM/WMI em modo somente leitura (Get-CimInstance). É defensivo:
/// qualquer falha de uma consulta resulta em coleta parcial, nunca em exceção.
/// A validação real ocorre em máquinas Windows (Fase 1 do roadmap).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorWindows : ILeitorPlataforma
{
    private readonly ILogger _log;

    public LeitorWindows(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public async Task<Inventario> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log.LogDebug("Lendo inventário do Windows via PowerShell/CIM + WMI (paralelo).");

        // Tarefas lentas (WMI/System.Management + filesystem) rodam em paralelo
        // enquanto as consultas CIM/PowerShell executam sequencialmente no thread atual.
        var saudeTask    = Task.Run(() => ColetarSaudeDiscos(),          cancellationToken);
        var driversTask  = Task.Run(() => ColetarDrivers(),              cancellationToken);
        var tempTask     = Task.Run(() => ColetarArquivosTemporarios(),  cancellationToken);
        var programasTask = Task.Run(() => ColetarProgramasInstalados(), cancellationToken);

        // Coleta de hardware via PowerShell/CIM (sequencial)
        var placa    = LerPlaca();
        var cpu      = LerCpu();
        var memoria  = LerMemoria();
        var gpu      = LerGpu();
        var so       = LerSistemaOperacional();
        var rede     = LerRede();
        var idents   = LerIdentificadores();
        var startup  = ColetarEntradasStartup();
        var servicos = ColetarServicos();
        var processos = ColetarProcessos();
        var metricas = ColetarMetricas();

        await Task.WhenAll(saudeTask, driversTask, tempTask, programasTask).ConfigureAwait(false);

        var inventario = new Inventario
        {
            Placa                = placa,
            Cpu                  = cpu,
            Memoria              = memoria,
            Gpu                  = gpu,
            SistemaOperacional   = so,
            Rede                 = rede,
            EntradasStartup      = startup,
            Servicos             = servicos,
            Processos            = processos,
            SaudeDiscos          = saudeTask.Result,
            Drivers              = driversTask.Result,
            Metricas             = metricas,
            ProgramasInstalados  = programasTask.Result,
            ArquivosTemporarios  = tempTask.Result,
            Identificadores      = idents,
            ColetadoEm           = DateTimeOffset.UtcNow,
        };

        if (inventario.Placa.Fabricante == "Desconhecido")
        {
            _log.LogWarning(
                "Coleta Windows parcial: consultas CIM/PowerShell não retornaram dados "
                + "(PowerShell ausente, sem permissão ou execução bloqueada).");
        }

        _log.LogInformation(
            "Coleta concluída: CPU '{Cpu}', {Mem} módulo(s) RAM, {Gpu} GPU(s), "
            + "{Discos} disco(s), {Drivers} driver(s), {Programas} programa(s), "
            + "{Startup} entrada(s) startup, {Servicos} serviço(s), {Processos} processo(s).",
            inventario.Cpu.Nome,
            inventario.Memoria.Count,
            inventario.Gpu.Count,
            inventario.SaudeDiscos.Count,
            inventario.Drivers.Count,
            inventario.ProgramasInstalados.Count,
            inventario.EntradasStartup.Count,
            inventario.Servicos.Count,
            inventario.Processos.Count);

        return inventario;
    }

    private static PlacaMae LerPlaca()
    {
        var board = PrimeiroItem("Win32_BaseBoard", "Manufacturer,Product,SerialNumber");
        var bios = PrimeiroItem("Win32_BIOS", "SMBIOSBIOSVersion,ReleaseDate");
        var modelo = Texto(board, "Product") ?? "Desconhecido";
        var (chipset, busSpecs) = LerChipsetEBus(modelo);

        return new PlacaMae
        {
            Fabricante = Texto(board, "Manufacturer") ?? "Desconhecido",
            Modelo = modelo,
            VersaoBios = Texto(bios, "SMBIOSBIOSVersion"),
            DataBios = NormalizadorData.Normalizar(Texto(bios, "ReleaseDate")),
            Modo = LerTexto("$env:firmware_type") is { Length: > 0 } modo ? modo : null,
            SecureBoot = LerSecureBoot(),
            Chipset = chipset,
            BusSpecs = busSpecs,
        };
    }

    private static (string? Chipset, string? BusSpecs) LerChipsetEBus(string modeloPlaca)
    {
        // Busca via Win32_PnPEntity — dispositivos de sistema AMD (VEN_1022) e Intel (VEN_8086)
        // excluindo CPU/memória/periféricos para isolar o chipset.
        const string cmd =
            "Get-CimInstance Win32_PnPEntity | " +
            "Where-Object {$_.DeviceID -match '^PCI\\\\VEN_(1022|8086)' -and $_.PNPClass -eq 'System' " +
            "-and $_.Name -notmatch 'Processor|Core|Memory|IOMMU|PSP|Crypt|Audio|USB|GPIO|I2C|SPI|UART|SMU|NTB|Dual'} | " +
            "Select-Object Name -First 5 | ConvertTo-Json -Compress";

        string? chipset = null;
        var saida = LerTexto(cmd);
        if (!string.IsNullOrWhiteSpace(saida))
        {
            try
            {
                var doc = JsonDocument.Parse(saida);
                var itens = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList()
                    : new List<JsonElement> { doc.RootElement.Clone() };

                // Prefere nomes que contenham identificadores de chipset conhecidos
                string[] conhecidos = ["X570","X670","B550","B650","X470","B450","X370","B350",
                                       "Z790","Z690","Z590","Z490","Z390","H770","B760","H670","B560"];
                foreach (var item in itens)
                {
                    var nome = Texto(item, "Name");
                    if (nome != null && conhecidos.Any(k => nome.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        chipset = nome;
                        break;
                    }
                }

                // Fallback: primeiro nome retornado
                chipset ??= Texto(itens.FirstOrDefault(), "Name");
            }
            catch { }
        }

        // Se WMI não retornou, tenta derivar pelo nome do modelo da placa-mãe
        chipset ??= DerivChipsetDoModelo(modeloPlaca);

        var busSpecs = chipset != null ? DerivarBusSpecs(chipset) : DerivarBusSpecs(modeloPlaca);
        return (chipset, busSpecs);
    }

    private static string? DerivChipsetDoModelo(string modelo)
    {
        string[] tokens = ["X670","B650","X570","B550","X470","B450","X370","B350",
                           "Z790","Z690","Z590","Z490","Z390","H770","B760","H670","B560","H570"];
        foreach (var t in tokens)
            if (modelo.Contains(t, StringComparison.OrdinalIgnoreCase))
                return t;
        return null;
    }

    private static string? DerivarBusSpecs(string? nome)
    {
        if (nome == null) return null;
        // PCIe 5.0 — AM5 / Intel Raptor Lake+
        if (ContainsAny(nome, "X670","B650","Z790","H770","B760"))
            return "PCI-Express 5.0 (32.0 GT/s)";
        // PCIe 4.0 — AM4 high-end / Alder Lake
        if (ContainsAny(nome, "X570","B550","Z690","Z590","H570","B560"))
            return "PCI-Express 4.0 (16.0 GT/s)";
        // PCIe 3.0 — AM4 mainstream / LGA1151
        if (ContainsAny(nome, "X470","B450","X370","B350","Z490","Z390","H470","B460","Z370","H370","B365"))
            return "PCI-Express 3.0 (8.0 GT/s)";
        return null;
    }

    private static bool ContainsAny(string texto, params string[] tokens) =>
        tokens.Any(t => texto.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static Processador LerCpu()
    {
        var cpu = PrimeiroItem("Win32_Processor",
            "Name,NumberOfCores,NumberOfLogicalProcessors,Manufacturer," +
            "MaxClockSpeed,CurrentClockSpeed,SocketDesignation,L2CacheSize,L3CacheSize");

        var fabricanteRaw = Texto(cpu, "Manufacturer");
        var fabricante = fabricanteRaw switch
        {
            { } f when f.Contains("AMD",   StringComparison.OrdinalIgnoreCase) => "AMD",
            { } f when f.Contains("Intel", StringComparison.OrdinalIgnoreCase) => "Intel",
            { } f when f.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase) => "Qualcomm",
            _ => fabricanteRaw,
        };

        return new Processador
        {
            Nome       = Texto(cpu, "Name") ?? "Desconhecido",
            Nucleos    = Inteiro(cpu, "NumberOfCores"),
            Threads    = Inteiro(cpu, "NumberOfLogicalProcessors"),
            Fabricante = fabricante,
            Soquete    = Texto(cpu, "SocketDesignation"),
            ClockBaseMhz   = Inteiro(cpu, "MaxClockSpeed"),
            ClockAtualMhz  = Inteiro(cpu, "CurrentClockSpeed"),
            L2CacheKb  = Inteiro(cpu, "L2CacheSize"),
            L3CacheKb  = Inteiro(cpu, "L3CacheSize"),
        };
    }

    private static IReadOnlyList<ModuloMemoria> LerMemoria()
    {
        var modulos = new List<ModuloMemoria>();
        foreach (var item in Itens("Win32_PhysicalMemory",
            "Capacity,Speed,ConfiguredClockSpeed,Manufacturer,PartNumber," +
            "DeviceLocator,SMBIOSMemoryType,FormFactor,ConfiguredVoltage"))
        {
            int? gb = null;
            var capacidade = NumeroLong(item, "Capacity");
            if (capacidade.HasValue && capacidade.Value > 0)
                gb = (int)Math.Round(capacidade.Value / 1024.0 / 1024.0 / 1024.0);

            var tensaoMv = Inteiro(item, "ConfiguredVoltage");
            var tensao = tensaoMv is > 0 ? $"{tensaoMv.Value / 1000.0:F2}V" : null;

            modulos.Add(new ModuloMemoria
            {
                TamanhoGb = gb,
                VelocidadeMhz = Inteiro(item, "Speed"),
                VelocidadeConfiguradaMhz = Inteiro(item, "ConfiguredClockSpeed"),
                Fabricante = DecodificarFabricanteRam(Texto(item, "Manufacturer")),
                Modelo = Texto(item, "PartNumber"),
                Slot = Texto(item, "DeviceLocator"),
                Tipo = DecodificarTipoMemoria(Inteiro(item, "SMBIOSMemoryType")),
                FormFactor = DecodificarFormFactor(Inteiro(item, "FormFactor")),
                Tensao = tensao,
            });
        }

        return modulos;
    }

    private static string? DecodificarFormFactor(int? ff) => ff switch
    {
        8  => "DIMM",
        12 => "SO-DIMM",
        13 => "SO-DIMM",
        _  => null,
    };

    private static string? DecodificarFabricanteRam(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trim = raw.Trim();
        // Se já é nome legível (contém letra não-hex), devolve direto
        if (trim.Any(c => c is > 'F' and <= 'Z' or > 'f' and <= 'z' or ' '))
            return trim;
        return trim.ToUpperInvariant().Replace(" ", "") switch
        {
            "CE"     or "04CE" or "80CE"     => "Samsung",
            "AD"     or "80AD" or "04AD"     => "SK Hynix",
            "2C"     or "802C" or "04F1"     => "Micron",
            "9E"     or "029E"               => "Corsair",
            "61"     or "04CB" or "04CD"
                    or "0198"                => "Kingston",
            "5105"                           => "Qimonda",
            "C1"     or "04C1"               => "Infineon",
            "51"     or "0151"               => "Qimonda",
            "98"     or "0198"               => "Crucial",
            _ => trim,
        };
    }

    private static string? DecodificarTipoMemoria(int? tipo) => tipo switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _  => null,
    };

    private static IReadOnlyList<PlacaVideo> LerGpu()
    {
        var gpus = new List<PlacaVideo>();
        var pcie = LerPcieGpu();
        bool primeiraGpu = true;

        foreach (var item in Itens("Win32_VideoController",
            "Name,DriverVersion,DriverDate,AdapterRAM," +
            "CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate"))
        {
            var nome = Texto(item, "Name");
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var vramBytes = NumeroLong(item, "AdapterRAM");
                var vramMb = vramBytes is > 0 ? (int)(vramBytes.Value / 1024 / 1024) : (int?)null;

                var resH = Inteiro(item, "CurrentHorizontalResolution");
                var resV = Inteiro(item, "CurrentVerticalResolution");
                var resolucao = resH is > 0 && resV is > 0 ? $"{resH}×{resV}" : null;

                var gpu = new PlacaVideo
                {
                    Nome             = nome,
                    VersaoDriver     = Texto(item, "DriverVersion"),
                    DataDriver       = NormalizadorData.Normalizar(Texto(item, "DriverDate")),
                    VramMb           = vramMb,
                    Resolucao        = resolucao,
                    TaxaAtualizacaoHz = Inteiro(item, "CurrentRefreshRate"),
                };

                if (primeiraGpu && pcie.HasValue)
                {
                    gpu = gpu with
                    {
                        LinkWidthAtual  = pcie.Value.WidthAtual,
                        LinkWidthMax    = pcie.Value.WidthMax,
                        LinkSpeedAtual  = pcie.Value.SpeedAtual,
                        LinkSpeedMax    = pcie.Value.SpeedMax,
                    };
                    primeiraGpu = false;
                }

                gpus.Add(gpu);
            }
        }

        return gpus;
    }

    private static (string? WidthAtual, string? WidthMax, string? SpeedAtual, string? SpeedMax)? LerPcieGpu()
    {
        // DEVPKEY_PciDevice: {4340A6C5-93FA-4706-972C-7B648008A5A7}
        //   6 = CurrentLinkWidth, 7 = CurrentLinkSpeed, 8 = MaxLinkWidth, 9 = MaxLinkSpeed
        // Valores de velocidade: 1=2.5, 2=5.0, 3=8.0, 4=16.0, 5=32.0 GT/s
        const string cmd =
            "$id=(Get-PnpDevice -Class Display -Status OK -ErrorAction SilentlyContinue|" +
            "Select-Object -First 1 -ExpandProperty InstanceId);" +
            "if($id){$p=Get-PnpDeviceProperty -InstanceId $id -ErrorAction SilentlyContinue;" +
            "$cs=($p|Where-Object{$_.KeyName -eq '{4340A6C5-93FA-4706-972C-7B648008A5A7} 7'}).Data;" +
            "$cw=($p|Where-Object{$_.KeyName -eq '{4340A6C5-93FA-4706-972C-7B648008A5A7} 6'}).Data;" +
            "$ms=($p|Where-Object{$_.KeyName -eq '{4340A6C5-93FA-4706-972C-7B648008A5A7} 9'}).Data;" +
            "$mw=($p|Where-Object{$_.KeyName -eq '{4340A6C5-93FA-4706-972C-7B648008A5A7} 8'}).Data;" +
            "[PSCustomObject]@{cs=$cs;cw=$cw;ms=$ms;mw=$mw}|ConvertTo-Json -Compress}";

        var saida = LerTexto(cmd);
        if (string.IsNullOrWhiteSpace(saida)) return null;

        try
        {
            var doc = JsonDocument.Parse(saida);
            var r = doc.RootElement;
            var wa = r.TryGetProperty("cw", out var cw) && cw.ValueKind == JsonValueKind.Number ? cw.GetInt32() : 0;
            var sa = r.TryGetProperty("cs", out var cs) && cs.ValueKind == JsonValueKind.Number ? cs.GetInt32() : 0;
            var wm = r.TryGetProperty("mw", out var mw) && mw.ValueKind == JsonValueKind.Number ? mw.GetInt32() : 0;
            var sm = r.TryGetProperty("ms", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetInt32() : 0;

            if (wa == 0 && sa == 0) return null;

            return (
                wa > 0 ? $"x{wa}" : null,
                wm > 0 ? $"x{wm}" : null,
                DecodificarVelocidadePcie(sa),
                DecodificarVelocidadePcie(sm)
            );
        }
        catch { return null; }
    }

    private static string? DecodificarVelocidadePcie(int codigo) => codigo switch
    {
        1 => "2.5 GT/s",
        2 => "5.0 GT/s",
        3 => "8.0 GT/s",
        4 => "16.0 GT/s",
        5 => "32.0 GT/s",
        6 => "64.0 GT/s",
        _ => null,
    };

    private static SistemaOperacionalInfo LerSistemaOperacional()
    {
        var os = PrimeiroItem("Win32_OperatingSystem", "Caption,Version");
        return new SistemaOperacionalInfo
        {
            Tipo = SistemaOperacionalTipo.Windows,
            Nome = Texto(os, "Caption") ?? "Windows",
            Versao = Texto(os, "Version") ?? Environment.OSVersion.VersionString,
            Arquitetura = RuntimeInformation.OSArchitecture.ToString(),
        };
    }

    private static IReadOnlyList<InterfaceRede> LerRede()
    {
        var interfaces = new List<InterfaceRede>();
        foreach (var item in Itens(
            "Win32_NetworkAdapter -Filter 'PhysicalAdapter=True'", "NetConnectionID,MACAddress"))
        {
            var nome = Texto(item, "NetConnectionID");
            if (!string.IsNullOrWhiteSpace(nome))
            {
                interfaces.Add(new InterfaceRede { Nome = nome, EnderecoMac = Texto(item, "MACAddress") });
            }
        }

        return interfaces;
    }

    private static IdentificadoresSensiveis LerIdentificadores()
    {
        var board = PrimeiroItem("Win32_BaseBoard", "SerialNumber");
        var produto = PrimeiroItem("Win32_ComputerSystemProduct", "UUID");
        return new IdentificadoresSensiveis
        {
            NumeroSerie = Texto(board, "SerialNumber"),
            UuidPlaca = Texto(produto, "UUID"),
            NomeMaquina = SeguroOuNulo(() => Environment.MachineName),
            NomeUsuario = SeguroOuNulo(() => Environment.UserName),
            ChaveProdutoWindows = null, // exige leitura adicional; sensível, omitido por padrão.
        };
    }

    private static bool? LerSecureBoot()
    {
        var saida = LerTexto("try { Confirm-SecureBootUEFI } catch { '' }");
        return bool.TryParse(saida, out var valor) ? valor : null;
    }

    // ---- SMART / Drivers / Métricas / Programas / Temp ----------------------------------

    private IReadOnlyList<SaudeDisco> ColetarSaudeDiscos()
    {
        try
        {
            var leitor = new LeitorSmart(NullLogger<LeitorSmart>.Instance);
            return leitor.LerDados()
                .Select(b => new SaudeDisco
                {
                    Modelo                  = b.Modelo,
                    Letra                   = "–",
                    TbwEscritoGb            = b.TbwEscritoGb,
                    TbwFabricanteGb         = 0,
                    HorasUso                = b.HorasUso,
                    PorcentagemVidaRestante = 100,
                    TemErrosNaoCorrigiveis  = b.TemErrosNaoCorrigiveis,
                    SetoresComProblema      = b.SetoresPendentes,
                    Nivel                   = ClassificarNivelSaude(b),
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao coletar dados S.M.A.R.T.");
            return Array.Empty<SaudeDisco>();
        }
    }

    private static NivelSaudeDisco ClassificarNivelSaude(DadosSmartBrutos b)
    {
        if (b.TemErrosNaoCorrigiveis || b.SetoresPendentes > 0) return NivelSaudeDisco.Critico;
        if (b.HorasUso > 30_000) return NivelSaudeDisco.Atencao;
        return NivelSaudeDisco.Bom;
    }

    private IReadOnlyList<InfoDriver> ColetarDrivers()
    {
        try
        {
            var coletor = new ColetorHwid(NullLogger<ColetorHwid>.Instance);
            return coletor.Coletar();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao coletar informações de drivers.");
            return Array.Empty<InfoDriver>();
        }
    }

    private static MetricasSistema ColetarMetricas()
    {
        var cpuItem  = PrimeiroItem("Win32_Processor", "LoadPercentage");
        var cpuUso   = Inteiro(cpuItem, "LoadPercentage") ?? 0;

        var osItem   = PrimeiroItem("Win32_OperatingSystem", "TotalVisibleMemorySize,FreePhysicalMemory");
        var totalKb  = NumeroLong(osItem, "TotalVisibleMemorySize") ?? 0;
        var livreKb  = NumeroLong(osItem, "FreePhysicalMemory")     ?? 0;

        var discos = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => new MetricaDisco
            {
                Letra   = d.Name.TrimEnd('\\'),
                TotalGb = d.TotalSize            / 1024 / 1024 / 1024,
                LivreGb = d.AvailableFreeSpace   / 1024 / 1024 / 1024,
                UsadoGb = (d.TotalSize - d.AvailableFreeSpace) / 1024 / 1024 / 1024,
            })
            .ToList<MetricaDisco>();

        return new MetricasSistema
        {
            CpuUsoPercent = cpuUso,
            RamTotalMb    = totalKb / 1024,
            RamLivreMb    = livreKb / 1024,
            Discos        = discos,
        };
    }

    private static IReadOnlyList<ProgramaInstalado> ColetarProgramasInstalados()
    {
        var programas = new Dictionary<string, ProgramaInstalado>(StringComparer.OrdinalIgnoreCase);

        RegistryKey[] raizes =
        [
            Registry.LocalMachine,
            Registry.CurrentUser,
        ];

        string[] subcaminhos =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];

        foreach (var raiz in raizes)
        {
            foreach (var subcaminho in subcaminhos)
            {
                try
                {
                    using var chave = raiz.OpenSubKey(subcaminho, writable: false);
                    if (chave is null) continue;

                    foreach (var subNome in chave.GetSubKeyNames())
                    {
                        try
                        {
                            using var sub = chave.OpenSubKey(subNome, writable: false);
                            if (sub is null) continue;

                            var nome = sub.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(nome)) continue;
                            nome = nome.Trim();

                            // Ignora atualizações do sistema
                            if (nome.StartsWith("KB", StringComparison.OrdinalIgnoreCase) && nome.Length < 15) continue;
                            if (nome.Contains("Hotfix",          StringComparison.OrdinalIgnoreCase)) continue;
                            if (nome.Contains("Security Update", StringComparison.OrdinalIgnoreCase)) continue;
                            if (nome.Contains("Update for Windows", StringComparison.OrdinalIgnoreCase)) continue;

                            if (programas.ContainsKey(nome)) continue;

                            int? tamanhoMb = null;
                            if (sub.GetValue("EstimatedSize") is int kb && kb > 0)
                                tamanhoMb = kb / 1024;

                            programas[nome] = new ProgramaInstalado
                            {
                                Nome                 = nome,
                                Versao               = sub.GetValue("DisplayVersion") as string,
                                Fabricante           = sub.GetValue("Publisher") as string,
                                DataInstalacao       = sub.GetValue("InstallDate") as string,
                                TamanhoMb            = tamanhoMb,
                                Bloatware            = ClassificarBloatware(nome),
                                UninstallString      = sub.GetValue("UninstallString") as string,
                                QuietUninstallString = sub.GetValue("QuietUninstallString") as string,
                            };
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        return programas.Values.OrderBy(p => p.Nome).ToList();
    }

    private static bool ClassificarBloatware(string nome)
    {
        string[] padroes =
        [
            "toolbar", "search assistant", "browser guard", "browser protector",
            "pc cleaner", "registry cleaner", "registry optimizer", "system optimizer",
            "pc optimizer", "speed booster", "driver booster", "driver easy",
            "driver genius", "coupon", "savings", "deals", "discount",
            "ask toolbar", "babylon", "conduit", "sweetpacks",
            "mcafee security scan", "norton security scan", "norton online backup",
            "wildtangent", "cyberlink", "roxio", "trial",
        ];
        var lower = nome.ToLowerInvariant();
        return padroes.Any(p => lower.Contains(p));
    }

    private static AnaliseTemp ColetarArquivosTemporarios()
    {
        var vistos  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pastas  = new List<PastaTemp>();

        var caminhos = new[]
        {
            Environment.GetEnvironmentVariable("TEMP"),
            Environment.GetEnvironmentVariable("TMP"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            @"C:\Windows\Temp",
        };

        foreach (var caminho in caminhos)
        {
            if (string.IsNullOrEmpty(caminho) || !vistos.Add(caminho)) continue;
            try
            {
                var di = new DirectoryInfo(caminho);
                if (!di.Exists) continue;

                long bytes    = 0;
                int  contagem = 0;

                foreach (var fi in di.EnumerateFiles("*", SearchOption.AllDirectories).Take(10_000))
                {
                    try { bytes += fi.Length; contagem++; } catch { }
                }

                pastas.Add(new PastaTemp
                {
                    Caminho           = caminho,
                    TamanhoMb         = bytes / 1024 / 1024,
                    QuantidadeArquivos = contagem,
                });
            }
            catch { }
        }

        return new AnaliseTemp
        {
            TamanhoTotalMb    = pastas.Sum(p => p.TamanhoMb),
            QuantidadeArquivos = pastas.Sum(p => p.QuantidadeArquivos),
            Pastas            = pastas,
        };
    }

    // ---- Startup / Serviços / Processos --------------------------------------------------

    private IReadOnlyList<InicializacaoEntrada> ColetarEntradasStartup()
    {
        try
        {
            var verificador = new VerificadorInicializacao(NullLogger<VerificadorInicializacao>.Instance);
            return verificador.Varrer();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao coletar entradas de inicialização.");
            return Array.Empty<InicializacaoEntrada>();
        }
    }

    private static IReadOnlyList<ServicoSistema> ColetarServicos()
    {
        var servicos = new List<ServicoSistema>();
        foreach (var item in Itens("Win32_Service", "Name,DisplayName,State,StartMode"))
        {
            var nome = Texto(item, "Name");
            if (string.IsNullOrWhiteSpace(nome)) continue;

            var state     = Texto(item, "State")     ?? "";
            var startMode = Texto(item, "StartMode") ?? "";

            servicos.Add(new ServicoSistema
            {
                Nome         = nome,
                NomeExibicao = Texto(item, "DisplayName"),
                Status = state switch
                {
                    "Running" => StatusServico.Rodando,
                    "Stopped" => StatusServico.Parado,
                    _         => StatusServico.Outro,
                },
                TipoInicio = startMode switch
                {
                    "Auto"     => TipoInicioServico.Automatico,
                    "Manual"   => TipoInicioServico.Manual,
                    "Disabled" => TipoInicioServico.Desabilitado,
                    _          => TipoInicioServico.Outro,
                },
            });
        }
        return servicos;
    }

    private static IReadOnlyList<ProcessoSistema> ColetarProcessos()
    {
        try
        {
            return Process.GetProcesses()
                .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0L; } })
                .Take(150)
                .Select(p => new ProcessoSistema
                {
                    Nome      = p.ProcessName,
                    Pid       = p.Id,
                    MemoriaKb = p.WorkingSet64 / 1024,
                })
                .ToList();
        }
        catch
        {
            return Array.Empty<ProcessoSistema>();
        }
    }

    // ---- Infraestrutura CIM/PowerShell ----------------------------------------------------

    private static JsonElement? PrimeiroItem(string classe, string propriedades) =>
        Itens(classe, propriedades).FirstOrDefault() is { ValueKind: not JsonValueKind.Undefined } e ? e : null;

    private static IEnumerable<JsonElement> Itens(string classe, string propriedades)
    {
        var saida = LerTexto(
            $"Get-CimInstance -ClassName {classe} | Select-Object {propriedades} | ConvertTo-Json -Compress -Depth 3");
        if (string.IsNullOrWhiteSpace(saida))
        {
            yield break;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(saida);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    yield return item.Clone();
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                yield return doc.RootElement.Clone();
            }
        }
    }

    private static string? Texto(JsonElement? elemento, string propriedade)
    {
        if (elemento is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var texto = prop.GetString();
            return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        }

        return null;
    }

    private static int? Inteiro(JsonElement? elemento, string propriedade)
    {
        if (elemento is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var prop) &&
            prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var valor))
        {
            return valor;
        }

        return null;
    }

    private static long? NumeroLong(JsonElement? elemento, string propriedade)
    {
        if (elemento is not { } e || e.ValueKind != JsonValueKind.Object) return null;
        if (!e.TryGetProperty(propriedade, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var v)) return v;
        if (prop.ValueKind == JsonValueKind.String &&
            long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            return s;
        return null;
    }

    private static string? LerTexto(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{comando}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(psi);
            if (processo is null)
            {
                return null;
            }

            var saida = processo.StandardOutput.ReadToEnd();
            if (!processo.WaitForExit(20_000))
            {
                return null;
            }

            return saida.Trim();
        }
        catch (Win32Exception)
        {
            return null; // PowerShell ausente.
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static T? SeguroOuNulo<T>(Func<T> acao)
        where T : class
    {
        try
        {
            return acao();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
