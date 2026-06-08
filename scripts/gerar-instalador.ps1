<#
.SYNOPSIS
  Gera o instalador .exe (Inno Setup) do Agente de Otimização de Hardware.

.DESCRIPTION
  1) Publica os binários self-contained (CLI + UI, win-x64) com scripts\publish.ps1.
  2) Compila installer\otimizacao.iss com o Inno Setup (ISCC), produzindo
     artifacts\installer\OtimizacaoHardware-Setup-<versão>.exe.

  Pré-requisito: Inno Setup 6  ->  winget install JRSoftware.InnoSetup

.EXAMPLE
  scripts\gerar-instalador.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User')

$raiz = Split-Path -Parent $PSScriptRoot
$iss  = Join-Path $raiz 'installer\otimizacao.iss'

Write-Host '>> Publicando binários self-contained (CLI + UI, win-x64)...'
& (Join-Path $PSScriptRoot 'publish.ps1') -Rids 'win-x64' -ComUI

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}
if (-not $iscc) {
    throw 'Inno Setup (ISCC.exe) não encontrado. Instale com: winget install JRSoftware.InnoSetup'
}

Write-Host ">> Compilando o instalador: $iscc"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador (ISCC retornou $LASTEXITCODE)." }

$saida = Join-Path $raiz 'artifacts\installer'
Write-Host ''
Write-Host "Instalador gerado em: $saida"
Get-ChildItem $saida -Filter *.exe -ErrorAction SilentlyContinue |
    ForEach-Object { '  {0}  ({1:N1} MB)' -f $_.Name, ($_.Length / 1MB) }
