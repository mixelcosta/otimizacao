using System.Globalization;
using HardwareOptimizer.Agent.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Execution.Windows;

/// <summary>
/// Implementação real de <see cref="IEstadoSistema"/> para Windows. Interpreta os
/// alvos simbólicos do catálogo (<c>registro:*</c>, <c>powercfg:*</c>,
/// <c>servico:*</c>) e os traduz em operações concretas de registro, plano de
/// energia e serviços — preservando a semântica ler/escrever/restaurar, de modo
/// que o <see cref="ExecutorControlado"/> e o rollback funcionem sem alteração.
///
/// O acesso ao registro e a processos é abstraído (<see cref="IAcessoRegistro"/>,
/// <see cref="IExecutorProcesso"/>), tornando toda a lógica testável fora do
/// Windows com fakes. Os adaptadores reais só são criados sob Windows elevado.
/// </summary>
public sealed class EstadoSistemaWindows : IEstadoSistema
{
    // GUIDs oficiais do Windows.
    internal const string GuidAltoDesempenho = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    internal const string SubgrupoUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
    internal const string ConfigUsbSuspensao = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

    private const string MultimediaSystemProfile =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string GraphicsDrivers = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    private readonly IAcessoRegistro _registro;
    private readonly IExecutorProcesso _processo;
    private readonly ILogger _log;

    public EstadoSistemaWindows(IAcessoRegistro registro, IExecutorProcesso processo, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(processo);

        _registro = registro;
        _processo = processo;
        _log = logger ?? NullLogger.Instance;
    }

    public string? Ler(string alvo) => Mapear(alvo).Ler();

    public void Escrever(string alvo, string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        _log.LogDebug("Windows: aplicando '{Alvo}' = '{Valor}'.", alvo, valor);
        Mapear(alvo).Escrever(valor);
    }

    public void Restaurar(string alvo, string? valorAnterior)
    {
        _log.LogDebug("Windows: restaurando '{Alvo}' = '{Valor}'.", alvo, valorAnterior ?? "(remover)");
        Mapear(alvo).Restaurar(valorAnterior);
    }

    /// <summary>
    /// Seleciona o estado de execução do ambiente: o estado real do Windows quando
    /// estamos sob Windows E a execução real foi explicitamente habilitada
    /// (<c>HWOPT_EXECUCAO_REAL=1</c>); caso contrário, o simulado (dry-run), que é
    /// o padrão seguro do projeto.
    /// </summary>
    public static IEstadoSistema Selecionar(ILogger? logger = null)
    {
        var log = logger ?? NullLogger.Instance;
        if (OperatingSystem.IsWindows() && ExecucaoRealHabilitada())
        {
            log.LogWarning(
                "Execução REAL no Windows habilitada (HWOPT_EXECUCAO_REAL): as ações aprovadas alterarão o sistema.");
            return new EstadoSistemaWindows(new AcessoRegistroWindows(), new ExecutorProcesso(), log);
        }

        log.LogInformation("Execução em modo SIMULADO (dry-run): nenhuma alteração real será feita.");
        return new EstadoSistemaSimulado();
    }

    internal static bool ExecucaoRealHabilitada()
    {
        var valor = Environment.GetEnvironmentVariable("HWOPT_EXECUCAO_REAL");
        return valor is "1" || string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase);
    }

    private IAlvoWindows Mapear(string alvo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alvo);

        var separador = alvo.IndexOf(':', StringComparison.Ordinal);
        if (separador <= 0 || separador == alvo.Length - 1)
        {
            throw NaoMapeado(alvo);
        }

        var tipo = alvo[..separador];
        var chave = alvo[(separador + 1)..];

        return tipo switch
        {
            "registro" => MapearRegistro(chave),
            "powercfg" => MapearPowercfg(chave),
            "servico" => new AlvoServico(_processo, chave, _log),
            "feature" => new AlvoFeatureWindows(_processo, chave, _log),
            _ => throw NaoMapeado(alvo),
        };
    }

    private IAlvoWindows MapearRegistro(string nome) => nome switch
    {
        "VisualFXSetting" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", nome, TraduzirVisualFx),
        "SystemResponsiveness" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, MultimediaSystemProfile, nome, TraduzirNumero),
        "NetworkThrottlingIndex" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, MultimediaSystemProfile, nome, TraduzirNumero),
        "TdrDelay" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, GraphicsDrivers, nome, TraduzirNumero),
        "HwSchMode" => new AlvoRegistroDword(
            _registro, ColmeiaRegistro.LocalMachine, GraphicsDrivers, nome, TraduzirNumero),
        _ => throw NaoMapeado("registro:" + nome),
    };

    private IAlvoWindows MapearPowercfg(string chave) => chave switch
    {
        "plano_ativo" => new AlvoPlanoEnergia(_processo),
        "usb_suspensao_seletiva" => new AlvoUsbSuspensao(_processo),
        _ => throw NaoMapeado("powercfg:" + chave),
    };

    // Valores numéricos do comando podem vir em decimal ("20") ou hexadecimal
    // ("ffffffff"); a leitura do registro devolve sempre decimal (round-trip).
    internal static uint TraduzirNumero(string valor)
    {
        if (uint.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalValor))
        {
            return decimalValor;
        }

        if (uint.TryParse(valor, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValor))
        {
            return hexValor;
        }

        throw new FormatException($"Valor numérico inválido para o registro: '{valor}'.");
    }

    internal static uint TraduzirVisualFx(string valor) => valor.ToUpperInvariant() switch
    {
        "DESEMPENHO" => 2,  // "Ajustar para obter um melhor desempenho"
        "APARENCIA" => 1,   // "Ajustar para obter uma melhor aparência"
        "AUTOMATICO" or "PADRAO" => 0,
        _ => TraduzirNumero(valor),
    };

    private static InvalidOperationException FalhaProcesso(string comando, ResultadoProcesso resultado) =>
        new($"Comando '{comando}' falhou (código {resultado.CodigoSaida}). {resultado.SaidaErro}".Trim());

    private static NotSupportedException NaoMapeado(string alvo) =>
        new($"Alvo do estado do sistema não mapeado para Windows: '{alvo}'.");

    /// <summary>Recurso Opcional do Windows via DISM (enable-feature / disable-feature).</summary>
    private sealed class AlvoFeatureWindows : IAlvoWindows
    {
        private readonly IExecutorProcesso _processo;
        private readonly string _feature;
        private readonly ILogger _log;

        public AlvoFeatureWindows(IExecutorProcesso processo, string feature, ILogger log)
        {
            _processo = processo;
            _feature  = feature;
            _log      = log;
        }

        public string? Ler()
        {
            var resultado = _processo.Executar(
                "dism.exe",
                new[] { "/online", "/get-featureinfo", $"/featurename:{_feature}" });
            return resultado.Sucesso ? ExtrairEstado(resultado.SaidaPadrao) : null;
        }

        public void Escrever(string valor)
        {
            var (acao, args) = ResolverAcao(valor);
            var resultado = _processo.Executar("dism.exe", args);
            if (!resultado.Sucesso)
                throw FalhaProcesso($"dism.exe {acao} {_feature}", resultado);
            _log.LogInformation("Feature '{Feature}': {Acao} concluído.", _feature, acao);
        }

        public void Restaurar(string? valorAnterior)
        {
            if (!string.IsNullOrWhiteSpace(valorAnterior))
                Escrever(valorAnterior);
        }

        private (string acao, string[] args) ResolverAcao(string valor) =>
            valor.Equals("Enabled", StringComparison.OrdinalIgnoreCase)
                ? ("enable", new[] { "/online", "/enable-feature", $"/featurename:{_feature}", "/all", "/norestart" })
                : ("disable", new[] { "/online", "/disable-feature", $"/featurename:{_feature}", "/norestart" });

        // Extrai o valor da linha "State : Enabled" ou "Estado : Habilitado" (independente do idioma).
        internal static string? ExtrairEstado(string saida)
        {
            foreach (var linha in saida.Split('\n'))
            {
                var idx = linha.IndexOf(':', StringComparison.Ordinal);
                if (idx < 0) continue;
                var chave = linha[..idx].Trim();
                if (chave.Equals("State", StringComparison.OrdinalIgnoreCase)
                    || chave.Equals("Estado", StringComparison.OrdinalIgnoreCase))
                    return linha[(idx + 1)..].Trim();
            }
            return null;
        }
    }

    /// <summary>Estratégia de um alvo concreto (registro, plano, serviço).</summary>
    private interface IAlvoWindows
    {
        string? Ler();
        void Escrever(string valor);
        void Restaurar(string? valorAnterior);
    }

    /// <summary>Valor DWORD do registro, com tradução simbólica → numérica.</summary>
    private sealed class AlvoRegistroDword : IAlvoWindows
    {
        private readonly IAcessoRegistro _registro;
        private readonly ColmeiaRegistro _colmeia;
        private readonly string _subchave;
        private readonly string _nome;
        private readonly Func<string, uint> _traduzir;

        public AlvoRegistroDword(
            IAcessoRegistro registro, ColmeiaRegistro colmeia, string subchave, string nome, Func<string, uint> traduzir)
        {
            _registro = registro;
            _colmeia = colmeia;
            _subchave = subchave;
            _nome = nome;
            _traduzir = traduzir;
        }

        public string? Ler() =>
            _registro.LerDword(_colmeia, _subchave, _nome)?.ToString(CultureInfo.InvariantCulture);

        public void Escrever(string valor) =>
            _registro.EscreverDword(_colmeia, _subchave, _nome, _traduzir(valor));

        public void Restaurar(string? valorAnterior)
        {
            if (valorAnterior is null)
            {
                _registro.RemoverValor(_colmeia, _subchave, _nome);
            }
            else
            {
                _registro.EscreverDword(_colmeia, _subchave, _nome, _traduzir(valorAnterior));
            }
        }
    }

    /// <summary>Plano de energia ativo (powercfg /getactivescheme · /setactive).</summary>
    private sealed class AlvoPlanoEnergia : IAlvoWindows
    {
        private readonly IExecutorProcesso _processo;

        public AlvoPlanoEnergia(IExecutorProcesso processo) => _processo = processo;

        public string? Ler()
        {
            var resultado = _processo.Executar("powercfg", new[] { "/getactivescheme" });
            return ExtrairGuid(resultado.SaidaPadrao);
        }

        public void Escrever(string valor) => Aplicar(
            valor.Equals("ALTO_DESEMPENHO", StringComparison.OrdinalIgnoreCase) ? GuidAltoDesempenho : valor);

        public void Restaurar(string? valorAnterior)
        {
            if (!string.IsNullOrWhiteSpace(valorAnterior))
            {
                Aplicar(valorAnterior);
            }
        }

        private void Aplicar(string guid)
        {
            var resultado = _processo.Executar("powercfg", new[] { "/setactive", guid });
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso("powercfg /setactive", resultado);
            }
        }

        // Locale-independente: o primeiro token no formato GUID "D" é o plano ativo.
        internal static string? ExtrairGuid(string saida)
        {
            var separadores = new[] { ' ', '\t', '\r', '\n', ':', '(', ')' };
            foreach (var token in saida.Split(separadores, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Guid.TryParseExact(token, "D", out var guid))
                {
                    return guid.ToString("D", CultureInfo.InvariantCulture);
                }
            }

            return null;
        }
    }

    /// <summary>Suspensão seletiva de USB (índice 0/1 no esquema atual).</summary>
    private sealed class AlvoUsbSuspensao : IAlvoWindows
    {
        private readonly IExecutorProcesso _processo;

        public AlvoUsbSuspensao(IExecutorProcesso processo) => _processo = processo;

        public string? Ler()
        {
            var resultado = _processo.Executar(
                "powercfg", new[] { "/query", "SCHEME_CURRENT", SubgrupoUsb, ConfigUsbSuspensao });
            return ExtrairIndice(resultado.SaidaPadrao);
        }

        public void Escrever(string valor) => Aplicar(valor.ToUpperInvariant() switch
        {
            "DESABILITADO" => 0u,
            "HABILITADO" => 1u,
            _ => TraduzirNumero(valor),
        });

        public void Restaurar(string? valorAnterior)
        {
            if (!string.IsNullOrWhiteSpace(valorAnterior))
            {
                Aplicar(TraduzirNumero(valorAnterior));
            }
        }

        private void Aplicar(uint indice)
        {
            var texto = indice.ToString(CultureInfo.InvariantCulture);
            Exec("/setacvalueindex", "SCHEME_CURRENT", SubgrupoUsb, ConfigUsbSuspensao, texto);
            Exec("/setdcvalueindex", "SCHEME_CURRENT", SubgrupoUsb, ConfigUsbSuspensao, texto);
            Exec("/setactive", "SCHEME_CURRENT");
        }

        private void Exec(params string[] argumentos)
        {
            var resultado = _processo.Executar("powercfg", argumentos);
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso("powercfg " + string.Join(' ', argumentos), resultado);
            }
        }

        // Pega o primeiro "0x..." da saída (índice CA/AC), independente do idioma.
        internal static string? ExtrairIndice(string saida)
        {
            var inicio = saida.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            if (inicio < 0)
            {
                return null;
            }

            var fim = inicio + 2;
            while (fim < saida.Length && Uri.IsHexDigit(saida[fim]))
            {
                fim++;
            }

            var hex = saida[(inicio + 2)..fim];
            return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var valor)
                ? valor.ToString(CultureInfo.InvariantCulture)
                : null;
        }
    }

    /// <summary>Modo de início de um serviço (sc.exe qc · config · stop).</summary>
    private sealed class AlvoServico : IAlvoWindows
    {
        private readonly IExecutorProcesso _processo;
        private readonly string _servico;
        private readonly ILogger _log;

        public AlvoServico(IExecutorProcesso processo, string servico, ILogger log)
        {
            _processo = processo;
            _servico = servico;
            _log = log;
        }

        public string? Ler()
        {
            var resultado = _processo.Executar("sc", new[] { "qc", _servico });
            return resultado.Sucesso ? InterpretarStartType(resultado.SaidaPadrao) : null;
        }

        public void Escrever(string valor)
        {
            var modo = NormalizarModo(valor);
            var resultado = _processo.Executar("sc", new[] { "config", _servico, "start=", modo });
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso($"sc config {_servico}", resultado);
            }

            if (modo == "disabled")
            {
                // Melhor esforço: para o serviço agora. O rollback restaura o tipo
                // de início (o estado em execução é retomado no próximo boot).
                var parada = _processo.Executar("sc", new[] { "stop", _servico });
                if (!parada.Sucesso)
                {
                    _log.LogDebug(
                        "sc stop {Servico} retornou {Codigo} (o serviço já pode estar parado).",
                        _servico, parada.CodigoSaida);
                }
            }
        }

        public void Restaurar(string? valorAnterior)
        {
            var modo = string.IsNullOrWhiteSpace(valorAnterior) ? "demand" : NormalizarModo(valorAnterior);
            var resultado = _processo.Executar("sc", new[] { "config", _servico, "start=", modo });
            if (!resultado.Sucesso)
            {
                throw FalhaProcesso($"sc config {_servico} (restauração)", resultado);
            }
        }

        // Mapeia a linha "START_TYPE : N XXX_START" para o vocabulário do sc config.
        internal static string InterpretarStartType(string saida)
        {
            foreach (var linha in saida.Split('\n'))
            {
                if (linha.IndexOf("START_TYPE", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var texto = linha.ToUpperInvariant();
                if (texto.Contains("DISABLED", StringComparison.Ordinal)) return "disabled";
                if (texto.Contains("DELAYED", StringComparison.Ordinal)) return "delayed-auto";
                if (texto.Contains("AUTO_START", StringComparison.Ordinal)) return "auto";
                if (texto.Contains("DEMAND_START", StringComparison.Ordinal)) return "demand";
                if (texto.Contains("SYSTEM_START", StringComparison.Ordinal)) return "system";
                if (texto.Contains("BOOT_START", StringComparison.Ordinal)) return "boot";
            }

            return "demand"; // padrão conservador quando não foi possível interpretar
        }

        internal static string NormalizarModo(string valor) => valor.Trim().ToUpperInvariant() switch
        {
            "DISABLED" => "disabled",
            "AUTO" or "AUTOMATIC" or "AUTO_START" => "auto",
            "DELAYED-AUTO" or "DELAYED" => "delayed-auto",
            "DEMAND" or "MANUAL" or "DEMAND_START" => "demand",
            "SYSTEM" or "SYSTEM_START" => "system",
            "BOOT" or "BOOT_START" => "boot",
            _ => "demand",
        };
    }
}
