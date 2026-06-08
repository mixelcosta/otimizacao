<#
.SYNOPSIS
  Regenera docs/CODIGO_FONTE_COMPLETO.md embutindo todo o código-fonte atual.

.DESCRIPTION
  Mantém o documento sempre em sincronia com o código. Preserva a narrativa
  (Parte I) até o marcador "<!-- CODIGO-GERADO-ABAIXO -->" e, abaixo dele,
  regenera o código de todos os projetos em ordem de dependência (Core → Agent →
  Cerebro → Ipc → App → Cli → testes), além de Directory.Build.props, do .sln e
  dos schemas JSON. Cada arquivo entra com o caminho relativo e o conteúdo
  integral em blocos de quatro crases (UTF-8 sem BOM).

  Rode após qualquer mudança de código:  scripts\gerar-doc-codigo.ps1

.PARAMETER Verificar
  Não escreve nada; apenas confirma se o documento está em dia com o código
  (sai com código 1 se estiver desatualizado). Útil em CI.

.EXAMPLE
  scripts\gerar-doc-codigo.ps1
.EXAMPLE
  scripts\gerar-doc-codigo.ps1 -Verificar
#>
[CmdletBinding()]
param([switch] $Verificar)

$ErrorActionPreference = 'Stop'
$raiz     = Split-Path -Parent $PSScriptRoot
$doc      = Join-Path $raiz 'docs\CODIGO_FONTE_COMPLETO.md'
$marcador = '<!-- CODIGO-GERADO-ABAIXO -->'
$utf8     = [System.Text.UTF8Encoding]::new($false)

if (-not (Test-Path $doc)) { throw "Documento não encontrado: $doc" }

# Preserva o cabeçalho (narrativa) até o fim da linha do marcador.
$texto = [System.IO.File]::ReadAllText($doc, $utf8)
$idx = $texto.IndexOf($marcador, [System.StringComparison]::Ordinal)
if ($idx -lt 0) { throw "Marcador '$marcador' não encontrado em $doc." }
$fimLinha = $texto.IndexOf("`n", $idx)
if ($fimLinha -lt 0) { $cabecalho = $texto } else { $cabecalho = $texto.Substring(0, $fimLinha) }
$cabecalho = $cabecalho.TrimEnd()

# ---- Geração do corpo (Parte II) ----
$sb = [System.Text.StringBuilder]::new()
function Add-Linha([string] $t = '') { [void]$sb.AppendLine($t) }

function Get-Linguagem([string] $ext) {
    switch ($ext.ToLowerInvariant()) {
        '.cs'     { 'csharp'; break }
        '.csproj' { 'xml';    break }
        '.props'  { 'xml';    break }
        '.axaml'  { 'xml';    break }
        '.json'   { 'json';   break }
        default   { 'text' }
    }
}

function Add-Arquivo([string] $full) {
    $rel = $full.Substring($raiz.Length + 1).Replace('\', '/')
    Add-Linha ('### `' + $rel + '`')
    Add-Linha ''
    Add-Linha ('````' + (Get-Linguagem ([System.IO.Path]::GetExtension($full))))
    Add-Linha ([System.IO.File]::ReadAllText($full, $utf8).TrimEnd())
    Add-Linha '````'
    Add-Linha ''
}

Add-Linha ''
Add-Linha "> _Código gerado automaticamente por ``scripts/gerar-doc-codigo.ps1`` a partir do repositório. Não edite manualmente abaixo deste ponto._"
Add-Linha ''
Add-Linha '## Configuração de build (raiz)'
Add-Linha ''
Add-Arquivo (Join-Path $raiz 'Directory.Build.props')
Add-Arquivo (Join-Path $raiz 'HardwareOptimizer.sln')

$ordem = @(
    'src\HardwareOptimizer.Core',  'src\HardwareOptimizer.Agent', 'src\HardwareOptimizer.Cerebro',
    'src\HardwareOptimizer.Ipc',   'src\HardwareOptimizer.App',   'src\HardwareOptimizer.Cli',
    'tests\HardwareOptimizer.Core.Tests',    'tests\HardwareOptimizer.Agent.Tests',
    'tests\HardwareOptimizer.Cerebro.Tests', 'tests\HardwareOptimizer.Ipc.Tests',
    'tests\HardwareOptimizer.App.Tests'
)
foreach ($projeto in $ordem) {
    $base = Join-Path $raiz $projeto
    if (-not (Test-Path $base)) { continue }
    Add-Linha ''
    Add-Linha ('## ' + (Split-Path $projeto -Leaf))
    Add-Linha ''
    Get-ChildItem $base -Recurse -Include *.cs, *.axaml, *.csproj |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        Sort-Object @{ Expression = { $_.Extension -ne '.csproj' } }, FullName |
        ForEach-Object { Add-Arquivo $_.FullName }
}

$schemas = Join-Path $raiz 'schemas'
if (Test-Path $schemas) {
    Add-Linha ''
    Add-Linha '## Apêndice — Schemas (contratos JSON)'
    Add-Linha ''
    Get-ChildItem $schemas -Recurse -Include *.json | Sort-Object FullName | ForEach-Object { Add-Arquivo $_.FullName }
}

$conteudo = $cabecalho + "`r`n`r`n" + $sb.ToString().TrimEnd() + "`r`n"

if ($Verificar) {
    $atual = if (Test-Path $doc) { [System.IO.File]::ReadAllText($doc, $utf8) } else { '' }
    if ($atual.Replace("`r`n", "`n").TrimEnd() -ne $conteudo.Replace("`r`n", "`n").TrimEnd()) {
        Write-Error "CODIGO_FONTE_COMPLETO.md está desatualizado. Rode: scripts\gerar-doc-codigo.ps1"
        exit 1
    }
    Write-Host "OK: documento em dia com o código."
    exit 0
}

[System.IO.File]::WriteAllText($doc, $conteudo, $utf8)
$blocos = ([regex]::Matches($sb.ToString(), '(?m)^### ')).Count
Write-Host "Documento regenerado: $doc"
Write-Host ("Arquivos embutidos: {0}" -f $blocos)
Write-Host ("Tamanho: {0:N0} KB" -f ((Get-Item $doc).Length / 1KB))
