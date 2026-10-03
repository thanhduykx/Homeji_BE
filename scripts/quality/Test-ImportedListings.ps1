#Requires -Version 7.0
# Read the actual imported catalog through a loopback API; no data mutations.
param(
    [Parameter(Mandatory = $true)][string]$Snapshot,
    [int]$ApiPort = 5192
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$source = Get-Content -LiteralPath $Snapshot -Raw | ConvertFrom-Json
if ($source.records.Count -lt 1) { throw 'Snapshot must contain source listings.' }
$connection = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection')
if (!$connection) {
    $localSettings = Get-Content -LiteralPath (Join-Path $repo 'src\Homeji.Api\appsettings.Local.json') -Raw | ConvertFrom-Json
    $connection = $localSettings.ConnectionStrings.DefaultConnection
}
if (!$connection) { throw 'A database connection is required; do not put credentials in command arguments.' }
$runPath = Join-Path $repo ('output\quality\imported-api-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runPath | Out-Null
$names = @('ConnectionStrings__DefaultConnection', 'ASPNETCORE_ENVIRONMENT', 'ASPNETCORE_URLS',
    'Database__ApplyMigrationsOnStartup', 'BackgroundJobs__Enabled', 'Api__EnableOpenApi')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$api = $null
try {
    $env:ConnectionStrings__DefaultConnection = $connection
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"
    $env:Database__ApplyMigrationsOnStartup = 'false'
    $env:BackgroundJobs__Enabled = 'false'
    $env:Api__EnableOpenApi = 'false'
    $api = Start-Process dotnet -ArgumentList @(('"{0}"' -f (Join-Path $repo 'src\Homeji.Api\bin\Debug\net9.0\Homeji.Api.dll'))) -WorkingDirectory (Join-Path $repo 'src\Homeji.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runPath 'api.log') -RedirectStandardError (Join-Path $runPath 'api-error.log')
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($api.HasExited) { throw 'Verification API exited; inspect api-error.log.' }
        try { $ready = (Invoke-WebRequest "http://127.0.0.1:$ApiPort/health/ready" -TimeoutSec 5).StatusCode -eq 200 } catch { $ready = $false }
        if ($ready) { break }
        Start-Sleep -Milliseconds 300
    }
    if (!$ready) { throw 'Verification API did not become ready.' }
    $items = Invoke-RestMethod "http://127.0.0.1:$ApiPort/api/rental-source-listings?pageSize=50" -TimeoutSec 20
    $verified = 0
    foreach ($expected in $source.records) {
        $actual = @($items | Where-Object { $_.sourceId -eq $expected.source_id -and $_.source -eq $expected.source })
        if ($actual.Count -ne 1 -or $actual[0].district -ne $expected.district -or $actual[0].sourceUrl -ne $expected.source_url -or $actual[0].price -ne $expected.price -or $actual[0].area -ne $expected.area -or ($actual[0].imageUrls -join '|') -ne ($expected.image_urls -join '|')) {
            throw ('API catalog differs from snapshot for source id ' + $expected.source_id)
        }
        $verified++
    }
    $detail = Invoke-RestMethod "http://127.0.0.1:$ApiPort/api/rental-source-listings/$($items[0].id)" -TimeoutSec 20
    if ($detail.sourceUrl -ne $items[0].sourceUrl) { throw 'Detail provenance differs from list.' }
    $evidence = [pscustomobject]@{VerifiedListings=$verified;VerifiedImages=($source.records | ForEach-Object {$_.image_urls.Count} | Measure-Object -Sum).Sum;Endpoint='/api/rental-source-listings';ReadOnly=$true}
    $evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runPath 'verification.json') -Encoding utf8
    $evidence | ConvertTo-Json
    Write-Output "Evidence: $runPath"
} finally {
    if ($api -and !$api.HasExited) { Stop-Process -Id $api.Id }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
