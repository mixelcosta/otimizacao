using System.Runtime.Versioning;
using System.Text;
using HardwareOptimizer.Core.Contracts;

namespace HardwareOptimizer.App.Services;

[SupportedOSPlatform("windows")]
public static class ServicoRelatorio
{
    public static async Task<string> ExportarHtmlAsync(Inventario inv)
    {
        var pasta = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var arquivo = Path.Combine(pasta, $"Relatorio-Hardware-{DateTime.Now:yyyyMMdd-HHmm}.html");

        await File.WriteAllTextAsync(arquivo, GerarHtml(inv), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return arquivo;
    }

    private static string GerarHtml(Inventario inv)
    {
        var cpu = inv.Cpu;
        var placa = inv.Placa;
        var so = inv.SistemaOperacional;
        var gpu = inv.Gpu.FirstOrDefault();
        var agora = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        var totalRam = inv.Memoria.Sum(m => m.TamanhoGb ?? 0);
        var tipoRam  = inv.Memoria.FirstOrDefault(m => m.Tipo != null)?.Tipo ?? "DDR";
        var velRam   = inv.Memoria.FirstOrDefault(m => m.VelocidadeMhz > 0)?.VelocidadeMhz;
        var resumoRam = velRam > 0 ? $"{totalRam} GB {tipoRam}-{velRam}" : $"{totalRam} GB {tipoRam}";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head><meta charset=\"UTF-8\">");
        sb.AppendLine("<title>Relatório de Hardware — Otimize Builder</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("*{box-sizing:border-box;margin:0;padding:0}");
        sb.AppendLine("body{background:#050510;color:#C8C8E8;font-family:'Segoe UI',Arial,sans-serif;font-size:13px;padding:32px}");
        sb.AppendLine("h1{font-size:22px;font-weight:900;letter-spacing:4px;color:#E0E0F2;margin-bottom:4px}");
        sb.AppendLine(".sub{color:#484865;font-size:11px;letter-spacing:1px;margin-bottom:32px}");
        sb.AppendLine(".accent{color:#00C8FF}");
        sb.AppendLine(".section{margin-bottom:24px}");
        sb.AppendLine(".section-title{font-size:10px;font-weight:700;letter-spacing:3px;color:#484865;border-bottom:1px solid #1E1E3C;padding-bottom:6px;margin-bottom:12px}");
        sb.AppendLine(".card{background:#0C0C1E;border:1px solid #1E1E3C;border-radius:10px;padding:16px;margin-bottom:8px}");
        sb.AppendLine(".grid2{display:grid;grid-template-columns:1fr 1fr;gap:16px}");
        sb.AppendLine(".grid3{display:grid;grid-template-columns:1fr 1fr 1fr;gap:16px}");
        sb.AppendLine(".label{color:#484865;font-size:10px;letter-spacing:1px;font-weight:700;margin-bottom:3px}");
        sb.AppendLine(".value{color:#E0E0F2;font-size:13px}");
        sb.AppendLine(".badge{display:inline-block;border-radius:4px;padding:2px 8px;font-size:10px;font-weight:700}");
        sb.AppendLine(".badge-ok{background:#00C87018;color:#00C870;border:1px solid #00C87040}");
        sb.AppendLine(".badge-warn{background:#FFAA0018;color:#FFAA00;border:1px solid #FFAA0040}");
        sb.AppendLine(".badge-crit{background:#CC333318;color:#CC3333;border:1px solid #CC333340}");
        sb.AppendLine("table{width:100%;border-collapse:collapse}");
        sb.AppendLine("th{text-align:left;font-size:10px;font-weight:700;letter-spacing:2px;color:#484865;padding:6px 10px;border-bottom:1px solid #1E1E3C}");
        sb.AppendLine("td{padding:8px 10px;border-bottom:1px solid #0F0F1E;color:#C8C8E8;font-size:12px}");
        sb.AppendLine("tr:last-child td{border-bottom:none}");
        sb.AppendLine("</style></head><body>");

        // Header
        sb.AppendLine("<h1>OTIMIZE <span class=\"accent\">BUILDER</span></h1>");
        sb.AppendLine($"<div class=\"sub\">RELATÓRIO DE HARDWARE  ·  Gerado em {agora}</div>");

        // Sistema Operacional
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<div class=\"section-title\">SISTEMA OPERACIONAL</div>");
        sb.AppendLine("<div class=\"card grid3\">");
        Campo(sb, "NOME", so.Nome ?? "Windows");
        Campo(sb, "VERSÃO", so.Versao ?? "–");
        Campo(sb, "ARQUITETURA", so.Arquitetura ?? "–");
        sb.AppendLine("</div></div>");

        // CPU
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<div class=\"section-title\">PROCESSADOR</div>");
        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine("<div class=\"grid3\" style=\"margin-bottom:12px\">");
        Campo(sb, "MODELO", cpu.Nome);
        Campo(sb, "FABRICANTE", cpu.Fabricante ?? "–");
        Campo(sb, "SOQUETE", cpu.Soquete ?? "–");
        sb.AppendLine("</div><div class=\"grid3\">");
        var nucleos = cpu.Nucleos.HasValue && cpu.Threads.HasValue ? $"{cpu.Nucleos}C / {cpu.Threads}T" : cpu.Nucleos?.ToString() ?? "–";
        Campo(sb, "NÚCLEOS/THREADS", nucleos);
        Campo(sb, "CLOCK BASE", cpu.ClockBaseMhz.HasValue ? FormatarClock(cpu.ClockBaseMhz.Value) : "–");
        Campo(sb, "CLOCK ATUAL", cpu.ClockAtualMhz.HasValue ? FormatarClock(cpu.ClockAtualMhz.Value) : "–");
        sb.AppendLine("</div></div></div>");

        // RAM
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<div class=\"section-title\">MEMÓRIA RAM</div>");
        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine($"<div class=\"value\" style=\"margin-bottom:10px;font-size:15px;font-weight:700\">{resumoRam}</div>");
        if (inv.Memoria.Count > 0)
        {
            sb.AppendLine("<table><tr><th>SLOT</th><th>TAMANHO</th><th>TIPO</th><th>FABRICANTE</th><th>MODELO</th></tr>");
            foreach (var m in inv.Memoria)
            {
                var vel2 = m.VelocidadeConfiguradaMhz ?? m.VelocidadeMhz;
                var tipo2 = (m.Tipo ?? "DDR") + (vel2 > 0 ? $"-{vel2}" : "");
                sb.AppendLine($"<tr><td>{m.Slot ?? "–"}</td><td>{m.TamanhoGb ?? 0} GB</td><td>{tipo2}</td><td>{m.Fabricante ?? "–"}</td><td>{m.Modelo?.Trim() ?? "–"}</td></tr>");
            }
            sb.AppendLine("</table>");
        }
        sb.AppendLine("</div></div>");

        // GPU
        if (gpu != null)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("<div class=\"section-title\">PLACA DE VÍDEO</div>");
            sb.AppendLine("<div class=\"card grid3\">");
            Campo(sb, "MODELO", gpu.Nome);
            Campo(sb, "VRAM", gpu.VramMb.HasValue ? FormatarVram(gpu.VramMb.Value) : "–");
            Campo(sb, "DRIVER", gpu.VersaoDriver ?? "–");
            sb.AppendLine("</div></div>");
        }

        // Placa-mãe + BIOS
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<div class=\"section-title\">PLACA-MÃE &amp; BIOS</div>");
        sb.AppendLine("<div class=\"card grid3\">");
        Campo(sb, "FABRICANTE", placa.Fabricante);
        Campo(sb, "MODELO", placa.Modelo);
        Campo(sb, "CHIPSET", placa.Chipset ?? "–");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"card grid3\" style=\"margin-top:8px\">");
        Campo(sb, "VERSÃO BIOS", placa.VersaoBios ?? "–");
        Campo(sb, "DATA BIOS", placa.DataBios ?? "–");
        Campo(sb, "MODO", placa.Modo ?? "–");
        sb.AppendLine("</div></div>");

        // Armazenamento
        if (inv.Metricas?.Discos?.Count > 0)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("<div class=\"section-title\">ARMAZENAMENTO</div>");
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<table><tr><th>UNIDADE</th><th>TOTAL</th><th>USADO</th><th>LIVRE</th><th>USO</th></tr>");
            foreach (var d in inv.Metricas.Discos)
            {
                var cls = d.UsoPercent >= 90 ? "badge-crit" : d.UsoPercent >= 75 ? "badge-warn" : "badge-ok";
                sb.AppendLine($"<tr><td><b>{d.Letra}</b></td><td>{d.TotalGb} GB</td><td>{d.UsadoGb} GB</td><td>{d.LivreGb} GB</td><td><span class=\"badge {cls}\">{d.UsoPercent}%</span></td></tr>");
            }
            sb.AppendLine("</table></div></div>");
        }

        // S.M.A.R.T.
        if (inv.SaudeDiscos.Count > 0)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("<div class=\"section-title\">SAÚDE S.M.A.R.T.</div>");
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<table><tr><th>DISCO</th><th>VIDA ÚTIL</th><th>HORAS DE USO</th><th>TBW ESCRITO</th><th>STATUS</th></tr>");
            foreach (var s in inv.SaudeDiscos)
            {
                var cls = s.Nivel switch
                {
                    NivelSaudeDisco.Bom     => "badge-ok",
                    NivelSaudeDisco.Atencao => "badge-warn",
                    NivelSaudeDisco.Critico => "badge-crit",
                    _ => ""
                };
                var status = s.Nivel switch
                {
                    NivelSaudeDisco.Bom     => "Bom",
                    NivelSaudeDisco.Atencao => "Atenção",
                    NivelSaudeDisco.Critico => "Crítico",
                    _ => "–"
                };
                sb.AppendLine($"<tr><td>{s.Modelo}</td><td>{s.PorcentagemVidaRestante:F0}%</td><td>{(s.HorasUso > 0 ? s.HorasUso + " h" : "–")}</td><td>{(s.TbwEscritoGb > 0 ? s.TbwEscritoGb + " GB" : "–")}</td><td><span class=\"badge {cls}\">{status}</span></td></tr>");
            }
            sb.AppendLine("</table></div></div>");
        }

        // Rede
        if (inv.Rede.Count > 0)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("<div class=\"section-title\">INTERFACES DE REDE</div>");
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<table><tr><th>NOME</th><th>TIPO</th><th>MAC</th></tr>");
            foreach (var r in inv.Rede)
                sb.AppendLine($"<tr><td>{r.Nome}</td><td>{r.Tipo ?? "–"}</td><td style=\"font-family:monospace\">{r.EnderecoMac ?? "–"}</td></tr>");
            sb.AppendLine("</table></div></div>");
        }

        sb.AppendLine("<div style=\"margin-top:32px;color:#282840;font-size:11px\">Gerado pelo Otimize Builder · otimizebuilder.com</div>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void Campo(StringBuilder sb, string label, string valor)
    {
        sb.AppendLine($"<div><div class=\"label\">{label}</div><div class=\"value\">{valor}</div></div>");
    }

    private static string FormatarClock(int mhz) =>
        mhz >= 1000 ? $"{mhz / 1000.0:F2} GHz" : $"{mhz} MHz";

    private static string FormatarVram(int mb) =>
        mb >= 1024 ? $"{mb / 1024} GB" : $"{mb} MB";
}
