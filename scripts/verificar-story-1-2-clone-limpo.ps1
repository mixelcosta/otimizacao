<#
.SYNOPSIS
  Regressao da Story 1.2 (Epico 1): valida, a partir de um clone git
  genuinamente limpo, que o fluxo de varredura/aprovacao/rollback de
  driver compila e passa nos testes -- incluindo o fix critico do
  rollback (pnputil precisa da flag /subdirs).

.DESCRIPTION
  Clona o repositorio local para uma pasta temporaria, cria um
  placeholder LOCAL AO CLONE para o bug conhecido do
  Features.LifeCounter (nunca no working directory principal), entao:
  1. Compila Features.Atualizacao, Features.Drivers, Ipc e App.
  2. Roda as 4 suites de teste afetadas e confere as contagens.
  3. Confirma que RestaurarBackupAsync usa a flag /subdirs no comando
     pnputil (regressao do bug critico corrigido no commit 02e601a).

.EXAMPLE
  scripts\verificar-story-1-2-clone-limpo.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$clonePath = Join-Path $env:TEMP "verificar-story-1-2-$([guid]::NewGuid().ToString('N').Substring(0,8))"

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
    Set-Content -Path "src/HardwareOptimizer.Features.LifeCounter/Data/tbw_database.json" -Value "[]" -Encoding UTF8

    Write-Host ">> 1. Confirmando o fix critico do rollback (flag /subdirs)"
    $conteudo = Get-Content -Raw "src/HardwareOptimizer.Features.Drivers/AtualizadorDrivers.cs"
    if ($conteudo -notmatch '/subdirs') {
        Falhar "RestaurarBackupAsync nao tem a flag /subdirs -- regressao do bug critico (rollback restaura nada silenciosamente)."
    }

    Write-Host ">> 2. Compilando Ipc (arrasta Atualizacao, Drivers, LifeCounter)"
    dotnet build "src/HardwareOptimizer.Ipc/HardwareOptimizer.Ipc.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de Ipc falhou a partir do clone limpo." }

    Write-Host ">> 3. Compilando App"
    dotnet build "src/HardwareOptimizer.App/HardwareOptimizer.App.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de App falhou a partir do clone limpo." }

    $suites = @(
        @{ Path = "tests/HardwareOptimizer.Features.Atualizacao.Tests"; Esperado = 8 },
        @{ Path = "tests/HardwareOptimizer.Features.Drivers.Tests"; Esperado = 18 },
        @{ Path = "tests/HardwareOptimizer.Ipc.Tests"; Esperado = 57 },
        @{ Path = "tests/HardwareOptimizer.App.Tests"; Esperado = 91 }
    )

    foreach ($suite in $suites) {
        Write-Host ">> Testando $($suite.Path) (esperado: $($suite.Esperado))"
        $saida = dotnet test $suite.Path --nologo -v quiet 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { Falhar "$($suite.Path) teve falha de teste." }
        if ($saida -notmatch "Aprovado:\s*$($suite.Esperado),") {
            Falhar "$($suite.Path): contagem de testes aprovados nao bate com o esperado ($($suite.Esperado))."
        }
    }

    Write-Host "PASSOU: Story 1.2 validada a partir de clone limpo -- fix do rollback presente, build e 174 testes verdes." -ForegroundColor Green
}
finally {
    Pop-Location
    Remove-Item $clonePath -Recurse -Force -ErrorAction SilentlyContinue
}

