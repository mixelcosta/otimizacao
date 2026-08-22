<#
.SYNOPSIS
  Regressao da Story 1.3 (Epico 1): valida, a partir de um clone git
  genuinamente limpo, que o alerta de software desatualizado via
  fonte oficial compila e passa nos testes -- incluindo os dois fixes
  da revisao independente (cancelamento propagado, guard de versao
  com espacos em branco).

.DESCRIPTION
  Clona o repositorio local para uma pasta temporaria, cria um
  placeholder LOCAL AO CLONE para o bug conhecido do
  Features.LifeCounter (nunca no working directory principal), entao:
  1. Compila Features.Atualizacao, Ipc e App.
  2. Roda as 3 suites de teste afetadas e confere as contagens.
  3. Confirma que VerificadorSoftware.VerificarAsync tem o catch
     dedicado de OperationCanceledException (nao engole cancelamento)
     e usa IsNullOrWhiteSpace (nao IsNullOrEmpty) nos dois guards de
     versao -- regressao dos fixes do commit 8fee95a.

.EXAMPLE
  scripts\verificar-story-1-3-clone-limpo.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$clonePath = Join-Path $env:TEMP "verificar-story-1-3-$([guid]::NewGuid().ToString('N').Substring(0,8))"

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
    $conteudo = Get-Content -Raw "src/HardwareOptimizer.Features.Atualizacao/VerificadorSoftware.cs"
    if ($conteudo -notmatch 'catch\s*\(\s*OperationCanceledException\s*\)') {
        Falhar "VerificadorSoftware.VerificarAsync nao tem o catch dedicado de OperationCanceledException -- regressao (cancelamento seria engolido como falha do provedor)."
    }
    if ($conteudo -match 'IsNullOrEmpty\(programa\.Versao\)' -or $conteudo -match 'IsNullOrEmpty\(oficial\.VersaoDisponivel\)') {
        Falhar "VerificadorSoftware.VerificarAsync voltou a usar IsNullOrEmpty nos guards de versao -- regressao (versao so-com-espacos passaria como valida)."
    }
    if ($conteudo -notmatch 'IsNullOrWhiteSpace\(programa\.Versao\)' -or $conteudo -notmatch 'IsNullOrWhiteSpace\(oficial\.VersaoDisponivel\)') {
        Falhar "VerificadorSoftware.VerificarAsync nao tem os dois guards IsNullOrWhiteSpace esperados."
    }

    Write-Host ">> 2. Compilando Ipc (arrasta Atualizacao, Drivers, LifeCounter)"
    dotnet build "src/HardwareOptimizer.Ipc/HardwareOptimizer.Ipc.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de Ipc falhou a partir do clone limpo." }

    Write-Host ">> 3. Compilando App"
    dotnet build "src/HardwareOptimizer.App/HardwareOptimizer.App.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de App falhou a partir do clone limpo." }

    $suites = @(
        @{ Path = "tests/HardwareOptimizer.Features.Atualizacao.Tests"; Esperado = 33 },
        @{ Path = "tests/HardwareOptimizer.Ipc.Tests"; Esperado = 62 },
        @{ Path = "tests/HardwareOptimizer.App.Tests"; Esperado = 97 }
    )

    foreach ($suite in $suites) {
        Write-Host ">> Testando $($suite.Path) (esperado: $($suite.Esperado))"
        $saida = dotnet test $suite.Path --nologo -v quiet 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { Falhar "$($suite.Path) teve falha de teste." }
        if ($saida -notmatch "Aprovado:\s*$($suite.Esperado),") {
            Falhar "$($suite.Path): contagem de testes aprovados nao bate com o esperado ($($suite.Esperado))."
        }
    }

    Write-Host "PASSOU: Story 1.3 validada a partir de clone limpo -- fixes de cancelamento/versao presentes, build e 192 testes verdes." -ForegroundColor Green
}
finally {
    Pop-Location
    Remove-Item $clonePath -Recurse -Force -ErrorAction SilentlyContinue
}

