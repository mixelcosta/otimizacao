<#
.SYNOPSIS
  Regressao da Story 1.4 (Epico 1): valida, a partir de um clone git
  genuinamente limpo, que o alerta de BIOS desatualizada com risco
  obrigatorio compila e passa nos testes -- incluindo os dois fixes
  da revisao independente (guard de confirmacao ausente em
  VerGuiaBios, estado de painel/guia nao resetado numa nova
  verificacao).

.DESCRIPTION
  Clona o repositorio local para uma pasta temporaria, cria um
  placeholder LOCAL AO CLONE para o bug conhecido do
  Features.LifeCounter (nunca no working directory principal), entao:
  1. Compila Features.Atualizacao, Core, Ipc e App.
  2. Roda as 4 suites de teste afetadas e confere as contagens.
  3. Confirma que VerGuiaBios tem o guard `if (!ConfirmadoBios)
     return;` e que VerificarBiosAsync reseta
     PainelConfirmacaoBiosAberto/ConfirmadoBios/GuiaBiosVisivel --
     regressao dos fixes do commit 3a58f93.

.EXAMPLE
  scripts\verificar-story-1-4-clone-limpo.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$clonePath = Join-Path $env:TEMP "verificar-story-1-4-$([guid]::NewGuid().ToString('N').Substring(0,8))"

function Falhar {
    param([string] $Mensagem)
    Write-Host "FALHOU: $Mensagem" -ForegroundColor Red
    if (Test-Path $clonePath) { Remove-Item $clonePath -Recurse -Force -ErrorAction SilentlyContinue }
    exit 1
}

Write-Host ">> Clonando $raiz para $clonePath"
git clone --quiet $raiz $clonePath
if ($LASTEXITCODE -ne 0) { Falhar "git clone falhou." }

Push-Location $clonePath
try {
    Write-Host ">> Placeholder local (so neste clone) pro bug conhecido de Features.LifeCounter"
    New-Item -ItemType Directory -Path "src/HardwareOptimizer.Features.LifeCounter/Data" -Force | Out-Null
    Set-Content -Path "src/HardwareOptimizer.Features.LifeCounter/Data/tbw_database.json" -Value '{ "discos": [] }' -Encoding UTF8

    Write-Host ">> 1. Confirmando os fixes criticos da revisao independente"
    $conteudo = Get-Content -Raw "src/HardwareOptimizer.App/ViewModels/DriversViewModel.cs"
    if ($conteudo -notmatch 'private void VerGuiaBios\(\)\s*\{\s*if \(!ConfirmadoBios\) return;') {
        Falhar "VerGuiaBios nao tem o guard 'if (!ConfirmadoBios) return;' -- regressao (guia revelado sem confirmacao de risco)."
    }
    if ($conteudo -notmatch 'PainelConfirmacaoBiosAberto = false;\s*\r?\n\s*ConfirmadoBios = false;\s*\r?\n\s*GuiaBiosVisivel = false;') {
        Falhar "VerificarBiosAsync nao reseta PainelConfirmacaoBiosAberto/ConfirmadoBios/GuiaBiosVisivel -- regressao (confirmacao obsoleta sobrevive a novo alerta)."
    }

    Write-Host ">> 2. Compilando Ipc (arrasta Atualizacao, Core, Drivers, LifeCounter)"
    dotnet build "src/HardwareOptimizer.Ipc/HardwareOptimizer.Ipc.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de Ipc falhou a partir do clone limpo." }

    Write-Host ">> 3. Compilando App"
    dotnet build "src/HardwareOptimizer.App/HardwareOptimizer.App.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de App falhou a partir do clone limpo." }

    $suites = @(
        @{ Path = "tests/HardwareOptimizer.Features.Atualizacao.Tests"; Esperado = 48 },
        @{ Path = "tests/HardwareOptimizer.Core.Tests"; Esperado = 91 },
        @{ Path = "tests/HardwareOptimizer.App.Tests"; Esperado = 109 },
        @{ Path = "tests/HardwareOptimizer.Ipc.Tests"; Esperado = 68 }
    )

    foreach ($suite in $suites) {
        Write-Host ">> Testando $($suite.Path) (esperado: $($suite.Esperado))"
        $saida = dotnet test $suite.Path --nologo -v quiet 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { Falhar "$($suite.Path) teve falha de teste." }
        if ($saida -notmatch "Aprovado:\s*$($suite.Esperado),") {
            Falhar "$($suite.Path): contagem de testes aprovados nao bate com o esperado ($($suite.Esperado))."
        }
    }

    Write-Host "PASSOU: Story 1.4 validada a partir de clone limpo -- fixes de confirmacao/estado presentes, build e 316 testes verdes." -ForegroundColor Green
}
finally {
    Pop-Location
    Remove-Item $clonePath -Recurse -Force -ErrorAction SilentlyContinue
}

