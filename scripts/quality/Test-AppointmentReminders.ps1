param(
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [ValidateRange(1024, 65535)][int]$DatabasePort = 55447
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runPath = Join-Path $repo ('output/verification/appointment-reminders/' + [Guid]::NewGuid().ToString('N'))
$dataPath = Join-Path $runPath 'pgdata'
New-Item -ItemType Directory -Force $runPath | Out-Null
$tracked = @('HOMEJI_TEST_DATABASE', 'ConnectionStrings__DefaultConnection', 'ASPNETCORE_ENVIRONMENT', 'BackgroundJobs__Enabled', 'Database__ApplyMigrationsOnStartup')
$previous = @{}
foreach ($name in $tracked) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$started = $false
try {
    # Disposable, fixture-only database, listening exclusively on loopback.
    & "$PostgresBin/initdb.exe" -D $dataPath -U homeji_quality --auth=trust --encoding=UTF8 --locale=C > (Join-Path $runPath 'initdb.log')
    if ($LASTEXITCODE -ne 0) { throw 'initdb failed' }
    $starter = Start-Process "$PostgresBin/pg_ctl.exe" -ArgumentList @('-D', ('"{0}"' -f $dataPath), '-l', ('"{0}"' -f (Join-Path $runPath 'postgres.log')), '-o', "`"-h 127.0.0.1 -p $DatabasePort`"", '-w', 'start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runPath 'pg-start.log') -RedirectStandardError (Join-Path $runPath 'pg-start-error.log')
    if (!$starter.WaitForExit(30000)) { throw 'Postgres startup timed out' }
    & "$PostgresBin/pg_ctl.exe" -D $dataPath status
    if ($LASTEXITCODE -ne 0) { throw 'Postgres start failed' }
    $started = $true
    & "$PostgresBin/psql.exe" -h 127.0.0.1 -p $DatabasePort -U homeji_quality -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE homeji_quality;'
    if ($LASTEXITCODE -ne 0) { throw 'Database creation failed' }
    & "$PostgresBin/psql.exe" -h 127.0.0.1 -p $DatabasePort -U homeji_quality -d homeji_quality -v ON_ERROR_STOP=1 -c 'CREATE ROLE anon; CREATE ROLE authenticated; CREATE ROLE service_role; CREATE SCHEMA auth; CREATE TABLE auth.users(id uuid PRIMARY KEY, email text, email_confirmed_at timestamptz);'
    if ($LASTEXITCODE -ne 0) { throw 'Auth stub failed' }
    $env:HOMEJI_TEST_DATABASE = "Host=127.0.0.1;Port=$DatabasePort;Database=homeji_quality;Username=homeji_quality"
    $env:ConnectionStrings__DefaultConnection = $env:HOMEJI_TEST_DATABASE
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:BackgroundJobs__Enabled = 'false'
    $env:Database__ApplyMigrationsOnStartup = 'false'
    Push-Location $repo
    try {
        dotnet test tests/Homeji.Application.UnitTests --filter 'FullyQualifiedName~ViewingAppointmentReminder' --logger 'trx;LogFileName=domain.trx' --results-directory $runPath
        if ($LASTEXITCODE -ne 0) { throw 'Domain reminder tests failed' }
        dotnet ef database update --project src/Homeji.Infrastructure --startup-project src/Homeji.Api --context ApplicationDbContext
        if ($LASTEXITCODE -ne 0) { throw 'Migration failed' }
        dotnet test tests/Homeji.Api.IntegrationTests --no-restore --filter 'FullyQualifiedName~AppointmentReminder' --logger 'trx;LogFileName=reminders.trx' --results-directory $runPath
        if ($LASTEXITCODE -ne 0) { throw 'Reminder database/API/email tests failed' }
        Write-Output "PASS: reminder domain, PostgreSQL migration, API and email tests. Evidence: $runPath"
    } finally { Pop-Location }
} finally {
    if ($started) { & "$PostgresBin/pg_ctl.exe" -D $dataPath -m fast -w stop }
    foreach ($name in $tracked) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
