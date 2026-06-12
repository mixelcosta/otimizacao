using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HardwareOptimizer.Core.Common;
using HardwareOptimizer.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareOptimizer.Agent.Sensors;

/// <summary>
/// Lê temperatura da GPU AMD via ADL2 (atiadlxx.dll) sem necessidade de elevação.
/// Usado quando o LibreHardwareMonitor não consegue inicializar ADL pelo próprio processo.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LeitorSensoresAdl : ILeitorSensores, IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr AllocCallbackDelegate(int size);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Main_Control_Create(
        AllocCallbackDelegate callback, int enumConnectedAdapters, out IntPtr context);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Main_Control_Destroy(IntPtr context);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, ref int numAdapters);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_OverdriveN_Temperature_Get(
        IntPtr context, int adapterIndex, int temperatureType, ref int temperature);

    private const int AdlOk = 0;
    private const int AdlTempGpu = 1;

    private readonly ILogger _log;
    // Mantém referência forte ao delegate para evitar GC durante chamadas ADL.
    private readonly AllocCallbackDelegate _allocCallback = size => Marshal.AllocHGlobal(size);
    private IntPtr _context = IntPtr.Zero;
    private bool _inicializado;
    private bool _adlIndisponivel;

    public LeitorSensoresAdl(ILogger? logger = null) => _log = logger ?? NullLogger.Instance;

    public SistemaOperacionalTipo Tipo => SistemaOperacionalTipo.Windows;

    public Task<LeituraSensores> LerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_adlIndisponivel)
            return Task.FromResult(new LeituraSensores());

        var sensores = new List<Sensor>();
        try
        {
            if (!_inicializado)
                TentarInicializar();

            if (!_inicializado || _context == IntPtr.Zero)
                return Task.FromResult(new LeituraSensores());

            int numAdapters = 0;
            if (ADL2_Adapter_NumberOfAdapters_Get(_context, ref numAdapters) != AdlOk || numAdapters <= 0)
                return Task.FromResult(new LeituraSensores());

            for (int i = 0; i < numAdapters; i++)
            {
                int rawTemp = 0;
                if (ADL2_OverdriveN_Temperature_Get(_context, i, AdlTempGpu, ref rawTemp) != AdlOk)
                    continue;

                if (rawTemp <= 0)
                    continue;

                // ADL retorna temperatura em milligraus Celsius (80000 = 80°C) para RDNA2/3.
                // Versões mais antigas retornam graus diretamente. Detecta pelo valor.
                double celsius = rawTemp >= 1000 ? rawTemp / 1000.0 : (double)rawTemp;

                if (celsius is > 0 and < 150)
                {
                    sensores.Add(new Sensor
                    {
                        Nome = "[GPU] AMD / GPU Temperature",
                        Tipo = TipoSensor.Temperatura,
                        Valor = Math.Round(celsius, 1),
                        Unidade = "°C",
                    });
                    _log.LogDebug("ADL2: GPU temp = {Temp}°C (adapter {Idx}, raw={Raw})", celsius, i, rawTemp);
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _log.LogDebug("ADL2 não disponível neste sistema: {Msg}", ex.Message);
            _adlIndisponivel = true;
        }
        catch (Exception ex) when (ex is SEHException or AccessViolationException)
        {
            _log.LogWarning("ADL2 retornou exceção nativa — desabilitando leitor ADL. {Msg}", ex.Message);
            _adlIndisponivel = true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Erro ao ler temperatura GPU via ADL2.");
        }

        return Task.FromResult(new LeituraSensores { Sensores = sensores });
    }

    private void TentarInicializar()
    {
        try
        {
            int result = ADL2_Main_Control_Create(_allocCallback, 1, out _context);
            _inicializado = result == AdlOk && _context != IntPtr.Zero;
            if (_inicializado)
                _log.LogDebug("ADL2 inicializado (context=0x{Ctx:X}).", _context);
            else
                _log.LogDebug("ADL2_Main_Control_Create retornou {Result} — GPU não detectada via ADL.", result);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            _log.LogDebug("atiadlxx.dll não encontrada: {Msg}", ex.Message);
            _adlIndisponivel = true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao inicializar ADL2.");
            _adlIndisponivel = true;
        }
    }

    public void Dispose()
    {
        if (_inicializado && _context != IntPtr.Zero)
        {
            try { ADL2_Main_Control_Destroy(_context); } catch { /* ignora */ }
            _context = IntPtr.Zero;
            _inicializado = false;
        }
    }
}
