<#
.SYNOPSIS
  Regressao da Story 1.5 (Epico 1, ultima historia): valida, a partir de um
  clone git genuinamente limpo, que o diagnostico de causa-raiz via Event Log
  compila e passa nos testes -- incluindo os fixes da revisao independente
  (ReverseDirection na leitura, filtro de severidade WHEA, elemento nulo no
  array de drivers).

.DESCRIPTION
  Clona o repositorio local para uma pasta temporaria, cria um placeholder
  LOCAL AO CLONE para o bug conhecido do Features.LifeCounter (nunca no
  working directory principal), entao:
  1. Compila Features.Atualizacao, Agent, Core, Ipc e App.
  2. Roda as 4 suites de teste afetadas e confere as contagens.
  3. Confirma que EventLogQuery usa ReverseDirection=true e que a consulta
     WHEA filtra por Level (1 ou 2) -- regressao dos fixes do commit 8a46615.

.EXAMPLE
  scripts\verificar-story-1-5-clone-limpo.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$clonePath = Join-Path $env:TEMP "verificar-story-1-5-$([guid]::NewGuid().ToString('N').Substring(0,8))"

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
    $conteudo = Get-Content -Raw "src/HardwareOptimizer.Agent/EventLog/LeitorEventLog.cs"
    if ($conteudo -notmatch 'ReverseDirection\s*=\s*true') {
        Falhar "EventLogQuery nao tem ReverseDirection=true -- regressao (cap de eventos manteria os mais antigos, nao os mais recentes)."
    }
    if ($conteudo -notmatch "Level=1 or Level=2") {
        Falhar "Consulta WHEA nao tem filtro de severidade Level=1/2 -- regressao (eventos informativos poluiriam a correlacao)."
    }

    Write-Host ">> 2. Compilando Ipc (arrasta Atualizacao, Agent, Core, Drivers, LifeCounter)"
    dotnet build "src/HardwareOptimizer.Ipc/HardwareOptimizer.Ipc.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de Ipc falhou a partir do clone limpo." }

    Write-Host ">> 3. Compilando App"
    dotnet build "src/HardwareOptimizer.App/HardwareOptimizer.App.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de App falhou a partir do clone limpo." }

    $suites = @(
        @{ Path = "tests/HardwareOptimizer.Features.Atualizacao.Tests"; Esperado = 63 },
        @{ Path = "tests/HardwareOptimizer.Agent.Tests"; Esperado = 187 },
        @{ Path = "tests/HardwareOptimizer.App.Tests"; Esperado = 121 },
        @{ Path = "tests/HardwareOptimizer.Ipc.Tests"; Esperado = 75 }
    )

    foreach ($suite in $suites) {
        Write-Host ">> Testando $($suite.Path) (esperado: $($suite.Esperado))"
        $saida = dotnet test $suite.Path --nologo -v quiet 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { Falhar "$($suite.Path) teve falha de teste." }
        if ($saida -notmatch "Aprovado:\s*$($suite.Esperado),") {
            Falhar "$($suite.Path): contagem de testes aprovados nao bate com o esperado ($($suite.Esperado))."
        }
    }

    Write-Host "PASSOU: Story 1.5 validada a partir de clone limpo -- fixes de leitura/correlacao presentes, build e 446 testes verdes." -ForegroundColor Green
}
finally {
    Pop-Location
    Remove-Item $clonePath -Recurse -Force -ErrorAction SilentlyContinue
}

