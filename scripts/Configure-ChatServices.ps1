[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$TurnHost,
    [Parameter(Mandatory)][System.Net.IPAddress]$PublicIp,
    [ValidateNotNullOrEmpty()][string]$GiphyApiKey = $env:GIPHY_API_KEY,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'output/voice-deployment' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$localPath = Join-Path $repositoryRoot 'src/Homeji.Api/appsettings.Local.json'
$settings = if (Test-Path -LiteralPath $localPath) { Get-Content -LiteralPath $localPath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
$secret = $settings.VoiceCalls.TurnSecret
if (-not $secret) { $secret = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
if (-not $settings.VoiceCalls) { $settings.VoiceCalls = @{} }
$settings.VoiceCalls.Provider = 'Coturn'
$settings.VoiceCalls.TurnUrls = @("turn:${TurnHost}:3478?transport=udp", "turn:${TurnHost}:3478?transport=tcp")
$settings.VoiceCalls.TurnSecret = $secret
if ($GiphyApiKey) { $settings.Giphy = @{ ApiKey = $GiphyApiKey } }
$settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $localPath -Encoding utf8
$turnConfiguration = @"
listening-port=3478
external-ip=$PublicIp
realm=$TurnHost
server-name=$TurnHost
fingerprint
use-auth-secret
static-auth-secret=$secret
min-port=49160
max-port=49260
user-quota=4
total-quota=200
max-bps=128000
stale-nonce=600
no-cli
no-tls
no-dtls
no-multicast-peers
no-loopback-peers
no-tcp-relay
denied-peer-ip=0.0.0.0-0.255.255.255
denied-peer-ip=10.0.0.0-10.255.255.255
denied-peer-ip=100.64.0.0-100.127.255.255
denied-peer-ip=127.0.0.0-127.255.255.255
denied-peer-ip=169.254.0.0-169.254.255.255
denied-peer-ip=172.16.0.0-172.31.255.255
denied-peer-ip=192.168.0.0-192.168.255.255
denied-peer-ip=::1
denied-peer-ip=fc00::-fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff
denied-peer-ip=fe80::-febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff
log-file=stdout
simple-log
"@
Set-Content -LiteralPath (Join-Path $OutputDirectory 'turnserver.conf') -Value $turnConfiguration -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'deploy/voice/compose.yaml') -Destination (Join-Path $OutputDirectory 'compose.yaml')
$productionSettings = @{ VoiceCalls__Provider = 'Coturn'; VoiceCalls__TurnUrls__0 = $settings.VoiceCalls.TurnUrls[0]; VoiceCalls__TurnUrls__1 = $settings.VoiceCalls.TurnUrls[1]; VoiceCalls__TurnSecret = $secret }
if ($GiphyApiKey) { $productionSettings.Giphy__ApiKey = $GiphyApiKey }
$productionSettings | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'backend-environment.private.json') -Encoding utf8
Write-Host "Saved local API configuration and private deployment files to $OutputDirectory. No server has been deployed. Keep these files private."
