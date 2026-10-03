#Requires -Version 7.0
param(
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [int]$DatabasePort = 55439,
    [int]$ApiPort = 5189,
    [string]$SourceImportSql,
    [switch]$SkipLoadTest
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runPath = Join-Path $repo ('output\quality\local-' + [Guid]::NewGuid().ToString('N'))
$dataPath = Join-Path $runPath 'pgdata'
New-Item -ItemType Directory -Path $runPath | Out-Null
$password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$passwordFile = Join-Path $runPath 'pg-password.txt'
Set-Content -LiteralPath $passwordFile -Value $password
$trackedEnvironment = @('PGPASSWORD', 'HOMEJI_TEST_DATABASE', 'ASPNETCORE_ENVIRONMENT', 'ASPNETCORE_URLS',
    'ConnectionStrings__DefaultConnection', 'Database__ApplyMigrationsOnStartup', 'BackgroundJobs__Enabled',
    'Api__EnableOpenApi', 'RateLimiting__PublicSearch__PermitLimit')
$previousEnvironment = @{}
foreach ($name in $trackedEnvironment) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$databaseStarted = $false
$api = $null
try {
    & (Join-Path $PostgresBin 'initdb.exe') -D $dataPath -U homeji_quality --pwfile=$passwordFile --auth=scram-sha-256 --encoding=UTF8 --locale=C > (Join-Path $runPath 'initdb.log')
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize isolated PostgreSQL.' }
    $starter = Start-Process (Join-Path $PostgresBin 'pg_ctl.exe') -ArgumentList @('-D', ('"{0}"' -f $dataPath), '-l', ('"{0}"' -f (Join-Path $runPath 'postgres.log')), '-o', "`"-h 127.0.0.1 -p $DatabasePort`"", '-w', 'start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runPath 'pg-start.log') -RedirectStandardError (Join-Path $runPath 'pg-start-error.log')
    if (!$starter.WaitForExit(30000) -or $starter.ExitCode -ne 0) { throw 'Could not start isolated PostgreSQL.' }
    $databaseStarted = $true
    $env:PGPASSWORD = $password
    $connection = "Host=127.0.0.1;Port=$DatabasePort;Database=homeji_quality;Username=homeji_quality;Password=$password"
    $env:HOMEJI_TEST_DATABASE = $connection
    $env:ConnectionStrings__DefaultConnection = $connection
    & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p $DatabasePort -U homeji_quality -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE homeji_quality;'
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated database.' }
    & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p $DatabasePort -U homeji_quality -d homeji_quality -v ON_ERROR_STOP=1 -c 'CREATE ROLE anon; CREATE ROLE authenticated; CREATE ROLE service_role; CREATE SCHEMA auth; CREATE TABLE auth.users(id uuid PRIMARY KEY);'
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the local Supabase auth FK stub.' }
    Push-Location $repo
    try {
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
        dotnet ef database update --project src/Homeji.Infrastructure --startup-project src/Homeji.Api --context ApplicationDbContext
        if ($LASTEXITCODE -ne 0) { throw 'Migration failed.' }
        if ($SourceImportSql) {
            $sourceSqlPath = (Resolve-Path -LiteralPath $SourceImportSql).Path
            # The destination is always the disposable loopback database above.
            & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p $DatabasePort -U homeji_quality -d homeji_quality -v ON_ERROR_STOP=1 -f $sourceSqlPath
            if ($LASTEXITCODE -ne 0) { throw 'Source listing import failed.' }
            & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p $DatabasePort -U homeji_quality -d homeji_quality -v ON_ERROR_STOP=1 -f $sourceSqlPath
            if ($LASTEXITCODE -ne 0) { throw 'Repeated source import failed.' }
        }
        dotnet test Homeji.sln --no-restore --logger 'trx;LogFilePrefix=local' --results-directory (Join-Path $runPath 'tests') --collect 'XPlat Code Coverage' -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
        if ($LASTEXITCODE -ne 0) { throw 'Local regression/database tests failed.' }
        if ($SkipLoadTest) {
            Write-Output "Test evidence: $runPath"
            return
        }
        $env:ASPNETCORE_ENVIRONMENT = 'Production'
        $env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"
        $env:Database__ApplyMigrationsOnStartup = 'false'
        $env:BackgroundJobs__Enabled = 'false'
        $env:Api__EnableOpenApi = 'false'
        # Benchmark SQL/API throughput separately from the normal 60/min public quota.
        $env:RateLimiting__PublicSearch__PermitLimit = '10000'
        $api = Start-Process dotnet -ArgumentList @(('"{0}"' -f (Join-Path $repo 'src\Homeji.Api\bin\Debug\net9.0\Homeji.Api.dll'))) -WorkingDirectory (Join-Path $repo 'src\Homeji.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runPath 'api.log') -RedirectStandardError (Join-Path $runPath 'api-error.log')
        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            if ($api.HasExited) { throw 'Benchmark API exited; inspect api-error.log.' }
            try { $ready = (Invoke-WebRequest "http://127.0.0.1:$ApiPort/health/ready" -TimeoutSec 2).StatusCode -eq 200 } catch { $ready = $false }
            if ($ready) { break }
            Start-Sleep -Milliseconds 500
        }
        if (!$ready) { throw 'Benchmark API did not become ready.' }
        k6 run --env "BASE_URL=http://127.0.0.1:$ApiPort" --summary-export (Join-Path $runPath 'k6-summary.json') scripts/quality/nearby-load.js
        if ($LASTEXITCODE -ne 0) { throw 'Load test failed its thresholds.' }
        Write-Output "Evidence: $runPath"
    } finally { Pop-Location }
} finally {
    if ($api -and !$api.HasExited) { Stop-Process -Id $api.Id }
    if ($databaseStarted) {
        $stopper = Start-Process (Join-Path $PostgresBin 'pg_ctl.exe') -ArgumentList @('-D', ('"{0}"' -f $dataPath), '-m', 'fast', '-w', 'stop') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runPath 'pg-stop.log') -RedirectStandardError (Join-Path $runPath 'pg-stop-error.log')
        if (!$stopper.WaitForExit(30000)) { Write-Warning 'Isolated PostgreSQL shutdown timed out.' }
    }
    if (Test-Path -LiteralPath $passwordFile) { Remove-Item -LiteralPath $passwordFile }
    foreach ($name in $trackedEnvironment) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
}
