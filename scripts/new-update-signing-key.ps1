<#
.SYNOPSIS
    Creates the ECDSA P-256 key pair that signs the update feeds (0051, 0054).

.DESCRIPTION
    Writes the public key to Update:PublicKey in source/DevTools.Daemon/appsettings.json.
    Stores the private key in the secret UPDATE_SIGNING_KEY of the GitHub environment 'release',
    so only the approved release job can read it. Removes a repository secret with the same name.
    GitHub never shows a secret value again. Use -BackupPath to write one copy of the private key,
    put it in a password manager, then delete the file.
    A new key makes earlier installs refuse new feeds, so run it one time, before the first 4.x release.

.EXAMPLE
    .\scripts\new-update-signing-key.ps1 -BackupPath $HOME\Desktop\update-signing-key.pem
#>
param(
    [string] $Repository = 'trgiangv/RevitDevTool',
    [string] $Environment = 'release',
    [string] $BackupPath,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$secretName = 'UPDATE_SIGNING_KEY'
$settingsPath = Join-Path $PSScriptRoot '..\source\DevTools.Daemon\appsettings.json'
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json

if ($settings.Update.PublicKey -and -not $Force) {
    throw 'Update:PublicKey is already set. Clients of earlier releases trust it. Use -Force only to replace a key that was never released.'
}
if ($BackupPath) {
    $BackupPath = [System.IO.Path]::GetFullPath($BackupPath)
    if (Test-Path $BackupPath) { throw "$BackupPath exists. Choose a new file." }
    if (-not (Test-Path (Split-Path $BackupPath -Parent))) { throw "The folder of $BackupPath does not exist." }
}

gh api --method PUT "repos/$Repository/environments/$Environment" --silent
if ($LASTEXITCODE -ne 0) { throw "Could not create the environment '$Environment'." }

$key = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    $privateKey = $key.ExportECPrivateKeyPem()
    if ($BackupPath) {
        Set-Content -Path $BackupPath -Value $privateKey -Encoding ascii -NoNewline
    }

    $privateKey | gh secret set $secretName --env $Environment --repo $Repository
    if ($LASTEXITCODE -ne 0) { throw 'gh secret set failed.' }

    $settings.Update.PublicKey = $key.ExportSubjectPublicKeyInfoPem()
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding utf8NoBOM
}
finally {
    $key.Dispose()
}

$repositorySecrets = gh secret list --repo $Repository --json name --jq '.[].name'
if ($repositorySecrets -contains $secretName) {
    gh secret delete $secretName --repo $Repository
    if ($LASTEXITCODE -ne 0) { throw "Could not delete the repository secret $secretName." }
}

Write-Host "$secretName is set in the '$Environment' environment of $Repository."
if ($BackupPath) { Write-Host "Backup: $BackupPath. Put it in a password manager, then delete the file." }
Write-Host "Commit the public key in $settingsPath."
