#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$configPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/Homeji.Api/appsettings.Local.json'))
$settings = if (Test-Path -LiteralPath $configPath) {
    Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
} else { @{} }

function Read-SecretValue([string]$Prompt) {
    $secureValue = Read-Host $Prompt -AsSecureString
    return ConvertFrom-SecureString -SecureString $secureValue -AsPlainText
}

$fromEmail = (Read-Host 'Email sender da xac minh trong Brevo').Trim()
if (-not [Net.Mail.MailAddress]::TryCreate($fromEmail, [ref]$null)) {
    throw 'Dia chi email sender khong hop le.'
}
$apiKey = Read-SecretValue 'Brevo API key (nhap an; khong phai SMTP key)'
if ([string]::IsNullOrWhiteSpace($apiKey) -or $apiKey.ToCharArray().Where({ [char]::IsControl($_) }).Count -gt 0) {
    throw 'API key khong hop le.'
}

# Read-only checks; this script does not send email or alter the Brevo account.
try {
    $headers = @{ 'api-key' = $apiKey; accept = 'application/json' }
    $null = Invoke-RestMethod -Method Get -Uri 'https://api.brevo.com/v3/account' -Headers $headers -TimeoutSec 30
    $senders = Invoke-RestMethod -Method Get -Uri 'https://api.brevo.com/v3/senders' -Headers $headers -TimeoutSec 30
} catch {
    throw 'Khong xac minh duoc Brevo API key/quyen truy cap. Chua ghi cau hinh. Kiem tra key, IP allowlist va ket noi.'
}
if (-not ($senders.senders | Where-Object { $_.email -ieq $fromEmail -and $_.active -eq $true })) {
    throw 'Sender chua duoc Brevo xac minh/kich hoat. Chua ghi cau hinh.'
}

if (-not $settings.Contains('Supabase')) { $settings.Supabase = @{} }
$serviceKey = $settings.Supabase.ServiceRoleKey
if ([string]::IsNullOrWhiteSpace($serviceKey)) {
    $serviceKey = Read-SecretValue 'Supabase service_role/secret key (nhap an; chi dung backend)'
}
if ([string]::IsNullOrWhiteSpace($serviceKey)) { throw 'Thieu Supabase service role key. Chua ghi cau hinh.' }

if (-not $settings.Contains('Email')) { $settings.Email = @{} }
$settings.Email.Provider = 'Brevo'
$settings.Email.Brevo = @{
    ApiKey = $apiKey
    FromEmail = $fromEmail
    FromName = 'Homeji'
    TimeoutSeconds = 30
}
$settings.Supabase.ServiceRoleKey = $serviceKey

# Stage beside the ignored local config so the replacement stays on the same volume.
$temporaryPath = Join-Path ([IO.Path]::GetDirectoryName($configPath)) ('.env.brevo-' + [Guid]::NewGuid().ToString('N'))
try {
    $json = $settings | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($temporaryPath, $json, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $configPath -Force
} finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    $apiKey = $null
    $serviceKey = $null
    $headers = $null
    $settings = $null
    $json = $null
}
Write-Host 'Da luu cau hinh Brevo vao appsettings.Local.json (Git ignored). Chua gui email.'
Write-Host 'Restart backend local. Production can nhap cung cac bien moi truong tren Render theo docs/brevo-setup.md.'
