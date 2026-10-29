[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateScript({ [Guid]::TryParse($_, [ref][Guid]::Empty) })][string]$KeyId,
    [ValidateNotNullOrEmpty()][string]$ApiToken = $env:CLOUDFLARE_TURN_API_TOKEN,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $ApiToken) { throw 'Set CLOUDFLARE_TURN_API_TOKEN before configuring Cloudflare TURN.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'output/voice-deployment' }
# Test credentials before activating this provider; never save unverified administrative tokens.
$credentialUri = 'https://rtc.live.cloudflare.com/v1/turn/keys/{0}/credentials/generate-ice-servers' -f [Uri]::EscapeDataString($KeyId)
try {
    $credentials = Invoke-RestMethod -Uri $credentialUri -Method Post -Headers @{ Authorization = "Bearer $ApiToken" } -ContentType 'application/json' -Body '{"ttl":7200}' -TimeoutSec 15
} catch {
    throw 'Cloudflare rejected the credential request or could not be reached. Local configuration has not been changed.'
}
$relays = @($credentials.iceServers | Where-Object { @($_.urls | Where-Object { $_ -match '^turns?:turn\.cloudflare\.com:' }).Count -gt 0 -and $_.username -and $_.credential })
if ($relays.Count -eq 0) { throw 'Cloudflare did not return usable TURN credentials. Local configuration has not been changed.' }
$localPath = Join-Path $repositoryRoot 'src/Homeji.Api/appsettings.Local.json'
$settings = if (Test-Path -LiteralPath $localPath) { Get-Content -LiteralPath $localPath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
if (-not $settings.VoiceCalls) { $settings.VoiceCalls = @{} }
$settings.VoiceCalls.Provider = 'Cloudflare'
$settings.VoiceCalls.Cloudflare = @{ KeyId = $KeyId; ApiToken = $ApiToken }
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($OutputDirectory)) | Out-Null
$settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $localPath -Encoding utf8
@{ VoiceCalls__Provider = 'Cloudflare'; VoiceCalls__Cloudflare__KeyId = $KeyId; VoiceCalls__Cloudflare__ApiToken = $ApiToken } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'cloudflare-environment.private.json') -Encoding utf8
@{ iceServers = $credentials.iceServers; issuedAt = [DateTimeOffset]::UtcNow.ToString('O'); ttl = 7200 } |
    ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'turn-ice.private.json') -Encoding utf8
Write-Host "Cloudflare issued temporary TURN credentials. Local configuration and private deployment files were saved to $OutputDirectory. Relay traffic has not been tested yet."
