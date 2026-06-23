#Requires -Version 5.1
<#
.SYNOPSIS
    Assina o executável do Otimize Builder com um certificado EV via signtool.exe.

.DESCRIPTION
    Usa o thumbprint do certificado EV lido da variável de ambiente OTIMIZE_CERT_THUMBPRINT.
    Requer o Windows SDK (signtool.exe) instalado e o certificado no store do usuário.

.PARAMETER Executavel
    Caminho do .exe a ser assinado. Padrão: artefato de publicação em publish\OtimizeBuilder.exe

.PARAMETER TimestampServer
    URL do servidor de timestamp. Padrão: http://timestamp.digicert.com

.EXAMPLE
    # Em CI:
    $env:OTIMIZE_CERT_THUMBPRINT = "<thumbprint-do-certificado-ev>"
    .\scripts\assinar-binario.ps1

    # Local (sobrescreve executável):
    .\scripts\assinar-binario.ps1 -Executavel "publish\OtimizeBuilder.exe"

.NOTES
    Para adquirir o certificado EV:
    1. Comprar EV Code Signing Certificate em DigiCert, Sectigo ou GlobalSign
    2. Instalar o hardware token USB ou usar Azure Key Vault (para CI headless)
    3. Instalar o certificado no Windows Certificate Store (CurrentUser\My)
    4. Obter o Thumbprint: (Get-ChildItem Cert:\CurrentUser\My | Where Subject -like '*OtimizeBuilder*').Thumbprint
    5. Setar a variável: $env:OTIMIZE_CERT_THUMBPRINT = "<thumbprint>"

    Para CI/CD (GitHub Actions), adicionar o thumbprint como secret:
      OTIMIZE_CERT_THUMBPRINT: ${{ secrets.EV_CERT_THUMBPRINT }}
#>
param(
    [string] $Executavel = "publish\OtimizeBuilder.exe",
    [string] $TimestampServer = "http://timestamp.digicert.com"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Validações ---

$thumbprint = $env:OTIMIZE_CERT_THUMBPRINT
if ([string]::IsNullOrWhiteSpace($thumbprint)) {
    Write-Error "Variável de ambiente OTIMIZE_CERT_THUMBPRINT não definida."
    exit 1
}

if (-not (Test-Path $Executavel)) {
    Write-Error "Executável não encontrado: $Executavel — rode scripts\publish.sh ou dotnet publish antes."
    exit 1
}

# Localiza signtool.exe (Windows SDK)
$signtool = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*x64*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $signtool) {
    Write-Error "signtool.exe não encontrado. Instale o Windows SDK: https://developer.microsoft.com/windows/downloads/windows-sdk/"
    exit 1
}

Write-Host "Assinando: $Executavel"
Write-Host "Certificado (thumbprint): $($thumbprint.Substring(0,8))..."
Write-Host "signtool: $signtool"

# --- Assinatura ---
& $signtool sign `
    /sha1 $thumbprint `
    /fd SHA256 `
    /tr $TimestampServer `
    /td SHA256 `
    /d "Otimize Builder" `
    /du "https://otimizebuilder.com" `
    $Executavel

if ($LASTEXITCODE -ne 0) {
    Write-Error "Assinatura falhou (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# --- Verificação ---
& $signtool verify /pa /v $Executavel
if ($LASTEXITCODE -ne 0) {
    Write-Error "Verificação da assinatura falhou."
    exit $LASTEXITCODE
}

Write-Host "`nAssinatura concluída com sucesso." -ForegroundColor Green
