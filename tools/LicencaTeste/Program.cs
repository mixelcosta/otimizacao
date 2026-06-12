using HardwareOptimizer.Features.Licensing;
using Microsoft.Extensions.Logging;

var log = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ServicoLicencaLocal>();
var servico = new ServicoLicencaLocal(log);

Console.WriteLine($"Status atual: {servico.TipoAtual}");

var resultado = await servico.AtivarAsync("OTIMIZE-PREM-2026-TEST-FULL");

if (resultado.Sucesso)
{
    Console.WriteLine("Licenca Premium ativada com sucesso!");
    Console.WriteLine($"Tipo: {resultado.NovoTipo}");
    Console.WriteLine($"Arquivo: %AppData%\\OtimizeBuilder\\license.dat");
}
else
{
    Console.WriteLine($"Erro: {resultado.Erro}");
}
