<#
.SYNOPSIS
  Publica binários distribuíveis do Agente de Otimização de Hardware.
  Gera executáveis self-contained (não exigem .NET instalado na máquina alvo).

.PARAMETER Rids
  Runtime Identifiers a publicar. Padrão: win-x64.

.PARAMETER ComUI
  Também publica a UI Avalonia (desktop), além da CLI.

.EXAMPLE
  scripts\publish.ps1                          # CLI para win-x64
.EXAMPLE
  scripts\publish.ps1 -Rids win-x64,linux-x64  # CLI para vários RIDs
.EXAMPLE
  scripts\publish.ps1 -ComUI                   # também publica a UI
#>
[CmdletBinding()]
param(
    [string[]] $Rids = @('win-x64'),
    [switch] $ComUI
)

$ErrorActionPreference = 'Stop'
$raiz  = Split-Path -Parent $PSScriptRoot
$saida = Join-Path $raiz 'artifacts'
$cli   = Join-Path $raiz 'src\HardwareOptimizer.Cli\HardwareOptimizer.Cli.csproj'
$app   = Join-Path $raiz 'src\HardwareOptimizer.App\HardwareOptimizer.App.csproj'

function Publicar {
    param([string] $Proj, [string] $Rid, [string] $Nome)

    Write-Host ">> Publicando $Nome para $Rid"
    dotnet publish $Proj -c Release -r $Rid --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o (Join-Path $saida "$Nome-$Rid")
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar $Nome ($Rid)." }
}

foreach ($rid in $Rids) {
    Publicar -Proj $cli -Rid $rid -Nome 'hwopt-cli'
    if ($ComUI) { Publicar -Proj $app -Rid $rid -Nome 'hwopt-ui' }
}

Write-Host ''
Write-Host "Artefatos gerados em: $saida"
Get-ChildItem $saida -ErrorAction SilentlyContinue | Select-Object Name
