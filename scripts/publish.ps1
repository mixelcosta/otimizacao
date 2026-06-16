<#
.SYNOPSIS
  Publica o Otimize Builder como executavel standalone (.exe).
  O usuario final nao precisa instalar o .NET — basta executar OtimizeBuilder.exe.

.PARAMETER ApenasApp
  Publica somente a UI (padrao). Use -ApenasApp:$false para tambem publicar a CLI.

.EXAMPLE
  .\scripts\publish.ps1              # gera publish\OtimizeBuilder.exe
#>
[CmdletBinding()]
param(
    [switch] $ApenasApp = $true
)

$ErrorActionPreference = 'Stop'
$raiz  = Split-Path -Parent $PSScriptRoot
$saida = Join-Path $raiz 'publish'
$app   = Join-Path $raiz 'src\HardwareOptimizer.App\HardwareOptimizer.App.csproj'

Write-Host ''
Write-Host '=== Otimize Builder — Gerando EXE Standalone ===' -ForegroundColor Cyan
Write-Host "Saida: $saida\OtimizeBuilder.exe"
Write-Host ''

# Limpa saida anterior
if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
New-Item -ItemType Directory -Path $saida | Out-Null

# Publish single-file self-contained win-x64
dotnet publish $app `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $saida

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: publish falhou (codigo $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

# Renomeia para OtimizeBuilder.exe
$gerado = Join-Path $saida 'HardwareOptimizer.App.exe'
$final  = Join-Path $saida 'OtimizeBuilder.exe'
if (Test-Path $gerado) { Rename-Item $gerado $final }

$tamanhoMB = [math]::Round((Get-Item $final).Length / 1MB, 1)

Write-Host ''
Write-Host '=== Publicado com sucesso! ===' -ForegroundColor Green
Write-Host "Arquivo : $final" -ForegroundColor Green
Write-Host "Tamanho : $tamanhoMB MB"
Write-Host ''
Write-Host 'O usuario so precisa copiar OtimizeBuilder.exe e executar.'
Write-Host 'Sem instalar .NET, sem instalar nada.'
Write-Host ''
