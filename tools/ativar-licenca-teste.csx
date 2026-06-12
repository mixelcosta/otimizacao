#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Extensions.Logging.Abstractions, 8.0.0"
#r "../src/HardwareOptimizer.Features.Licensing/bin/Release/net8.0/HardwareOptimizer.Features.Licensing.dll"
#r "../src/HardwareOptimizer.Core/bin/Release/net8.0/HardwareOptimizer.Core.dll"

using HardwareOptimizer.Features.Licensing;
using Microsoft.Extensions.Logging;

var log = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ServicoLicencaLocal>();
var servico = new ServicoLicencaLocal(log);
var resultado = await servico.AtivarAsync("OTIMIZE-PREM-2026-TEST-FULL");
Console.WriteLine(resultado.Sucesso ? "Licença Premium ativada!" : $"Erro: {resultado.Erro}");
