<#
.SYNOPSIS
  Regressão do bug de build corrigido na Story 1.1 (Épico 1): valida, a partir
  de um clone git genuinamente limpo, que Features.Upgrade e Features.Drivers
  compilam sem erro CS1566, e que a regra do .gitignore continua correta.

.DESCRIPTION
  Clona o repositório local para uma pasta temporária, então verifica:
  1. hardware_catalog.json e whql_catalog.json estão versionados no git.
  2. git check-ignore NÃO captura as pastas Data/ dos dois projetos.
  3. git check-ignore AINDA captura data/backups/ na raiz (pasta de runtime
     do ServicoBackup) — a regra não pode ficar permissiva demais.
  4. Os dois projetos compilam sem erro, a partir do clone limpo.

.EXAMPLE
  scripts\verificar-build-clone-limpo.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$clonePath = Join-Path $env:TEMP "verificar-build-clone-limpo-$([guid]::NewGuid().ToString('N').Substring(0,8))"

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
    Write-Host ">> 1. Verificando se os catálogos estão versionados"
    $arquivos = git ls-files | Select-String -Pattern 'Data/(hardware_catalog|whql_catalog)\.json$'
    if ($arquivos.Count -ne 2) { Falhar "Esperava 2 catálogos versionados, encontrou $($arquivos.Count)." }

    Write-Host ">> 2. Verificando que Data/ dos projetos NÃO é ignorado"
    git check-ignore "src/HardwareOptimizer.Features.Upgrade/Data/hardware_catalog.json" | Out-Null
    if ($LASTEXITCODE -eq 0) { Falhar "hardware_catalog.json ainda está sendo ignorado pelo git." }
    git check-ignore "src/HardwareOptimizer.Features.Drivers/Data/whql_catalog.json" | Out-Null
    if ($LASTEXITCODE -eq 0) { Falhar "whql_catalog.json ainda está sendo ignorado pelo git." }

    Write-Host ">> 3. Verificando que data/backups/ (runtime) AINDA e ignorado"
    New-Item -ItemType Directory -Path "data/backups" -Force | Out-Null
    git check-ignore "data/backups/teste.db" | Out-Null
    if ($LASTEXITCODE -ne 0) { Falhar "data/backups/ parou de ser ignorado - regressao na regra do .gitignore." }
    Remove-Item "data" -Recurse -Force

    Write-Host ">> 4. Compilando Features.Upgrade"
    dotnet build "src/HardwareOptimizer.Features.Upgrade/HardwareOptimizer.Features.Upgrade.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de Features.Upgrade falhou a partir do clone limpo." }

    Write-Host ">> 5. Compilando Features.Drivers"
    dotnet build "src/HardwareOptimizer.Features.Drivers/HardwareOptimizer.Features.Drivers.csproj" --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Falhar "Build de Features.Drivers falhou a partir do clone limpo." }

    Write-Host "PASSOU: build limpo e regra do .gitignore corretos." -ForegroundColor Green
}
finally {
    Pop-Location
    Remove-Item $clonePath -Recurse -Force -ErrorAction SilentlyContinue
}

