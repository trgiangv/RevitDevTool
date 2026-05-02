#requires -RunAsAdministrator
<#
.SYNOPSIS
    Generate self-signed code signing certificate for RevitDevTool

.DESCRIPTION
    Creates a self-signed certificate for code signing, exports PFX (private) and CER (public).
    The PFX base64 encoded string should be added to GitHub Secrets as SIGN_CERT_BASE64.

.EXAMPLE
    .\scripts\generate-cert.ps1

.OUTPUTS
    RevitDevTool.pfx - Private key (keep secret)
    RevitDevTool.cer - Public certificate (can be shared)
    cert.txt - Base64 encoded PFX for GitHub Secrets
#>

$ErrorActionPreference = "Stop"

# ================================
# CONFIG
# ================================
$certName = "RevitDevTool"
$publisher = "Inspexel"
$passwordPlain = Read-Host -Prompt "Enter password for PFX export" -AsSecureString

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$pfxPath = Join-Path $rootDir "RevitDevTool.pfx"
$cerPath = Join-Path $rootDir "RevitDevTool.cer"
$txtPath = Join-Path $rootDir "cert.txt"

# ================================
# CREATE CERT
# ================================
Write-Host "Creating self-signed certificate..." -ForegroundColor Cyan

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=$certName, O=$publisher, C=VN" `
    -KeyUsage DigitalSignature `
    -FriendlyName "$certName Code Signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(5)

Write-Host "Certificate created: $($cert.Thumbprint)" -ForegroundColor Green

# ================================
# EXPORT PFX
# ================================
Write-Host "Exporting PFX (private key)..." -ForegroundColor Cyan

Export-PfxCertificate `
    -Cert $cert `
    -FilePath $pfxPath `
    -Password $passwordPlain

Write-Host "PFX exported to: $pfxPath" -ForegroundColor Green

# ================================
# EXPORT CER (PUBLIC)
# ================================
Write-Host "Exporting CER (public certificate)..." -ForegroundColor Cyan

Export-Certificate `
    -Cert $cert `
    -FilePath $cerPath

Write-Host "CER exported to: $cerPath" -ForegroundColor Green

# ================================
# ENCODE FOR GITHUB
# ================================
Write-Host "Encoding PFX for GitHub Secrets..." -ForegroundColor Cyan

$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath))
$base64 | Out-File -FilePath $txtPath -Encoding ASCII

Write-Host "Base64 encoded PFX saved to: $txtPath" -ForegroundColor Green

# ================================
# VERIFY
# ================================
Write-Host "`n=== Certificate Details ===" -ForegroundColor Yellow
$cert | Format-List Subject, Thumbprint, NotBefore, NotAfter, FriendlyName

Write-Host "`n=== Files Created ===" -ForegroundColor Yellow
Write-Host "PFX (Private): $pfxPath" -ForegroundColor Red
Write-Host "CER (Public):  $cerPath" -ForegroundColor Green
Write-Host "TXT (GitHub):  $txtPath" -ForegroundColor Cyan

Write-Host "`n=== Next Steps ===" -ForegroundColor Yellow
Write-Host "1. Add content of cert.txt to GitHub Secret: SIGN_CERT_BASE64"
Write-Host "2. Add your password to GitHub Secret: SIGN_CERT_PASSWORD"
Write-Host "3. Keep RevitDevTool.pfx SAFE - do NOT commit it"
Write-Host "4. RevitDevTool.cer can be committed (public certificate)"

# Cleanup cert from store (we have the files now)
Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force
Write-Host "`nCertificate removed from personal store (files retained)." -ForegroundColor Gray
