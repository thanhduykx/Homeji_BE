[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$')][string]$AppName,
    [ValidateNotNullOrEmpty()][string]$CredentialApiKey = $env:METERED_TURN_CREDENTIAL_API_KEY,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $CredentialApiKey) { throw 'Set METERED_TURN_CREDENTIAL_API_KEY to the credential-scoped key. Do not use the account Secret Key.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'output/voice-deployment' }
$credentialUri = 'https://{0}.metered.live/api/v1/turn/credentials?apiKey={1}' -f $AppName, [Uri]::EscapeDataString($CredentialApiKey)
try {
    $iceServers = Invoke-RestMethod -Uri $credentialUri -Method Get -TimeoutSec 15 -MaximumRedirection 0
} catch {
    throw 'Metered rejected the credential request or could not be reached. Local configuration has not been changed.'
}
$relays = @($iceServers | Where-Object { @($_.urls | Where-Object { $_ -match '^turns?:(?:[a-z0-9-]+\.)*relay\.metered\.(?:ca|live):' }).Count -gt 0 -and $_.username -and $_.credential })
if ($relays.Count -eq 0) { throw 'Metered did not return usable TURN credentials. Local configuration has not been changed.' }
$localPath = Join-Path $repositoryRoot 'src/Homeji.Api/appsettings.Local.json'
$settings = if (Test-Path -LiteralPath $localPath) { Get-Content -LiteralPath $localPath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
if (-not $settings.VoiceCalls) { $settings.VoiceCalls = @{} }
$settings.VoiceCalls.Provider = 'Metered'
$settings.VoiceCalls.Metered = @{ AppName = $AppName; ApiKey = $CredentialApiKey }
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($OutputDirectory)) | Out-Null
$settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $localPath -Encoding utf8
@{ VoiceCalls__Provider = 'Metered'; VoiceCalls__Metered__AppName = $AppName; VoiceCalls__Metered__ApiKey = $CredentialApiKey } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'metered-environment.private.json') -Encoding utf8
@{ iceServers = $iceServers; checkedAt = [DateTimeOffset]::UtcNow.ToString('O') } |
    ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'turn-ice.private.json') -Encoding utf8
Write-Host "Metered returned TURN credentials. Local configuration and private deployment files were saved to $OutputDirectory. Relay traffic has not been tested yet."
