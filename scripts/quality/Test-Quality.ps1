#Requires -Version 7.0
param([switch]$ServerAnalysis)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputPath = Join-Path $repo 'output\quality\sonar'
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
Push-Location $repo
try {
    if ($ServerAnalysis) {
        foreach ($name in @('SONAR_TOKEN', 'SONAR_PROJECT_KEY', 'SONAR_HOST_URL')) {
            if (![Environment]::GetEnvironmentVariable($name)) { throw "$name is required for server analysis." }
        }
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
        $arguments = @('sonarscanner', 'begin', "/k:$env:SONAR_PROJECT_KEY", "/d:sonar.host.url=$env:SONAR_HOST_URL",
            "/d:sonar.token=$env:SONAR_TOKEN",
            '/d:sonar.cs.opencover.reportsPaths=output/quality/sonar/**/coverage.opencover.xml',
            '/d:sonar.exclusions=**/appsettings.Local.json,output/**,**/bin/**,**/obj/**',
            '/d:sonar.coverage.exclusions=**/Migrations/**', '/d:sonar.qualitygate.wait=true')
        if ($env:SONAR_ORGANIZATION) { $arguments += "/o:$env:SONAR_ORGANIZATION" }
        if ($env:SONAR_HOST_URL -match 'sonarcloud\.io' -and !$env:SONAR_ORGANIZATION) {
            throw 'SONAR_ORGANIZATION is required for SonarQube Cloud.'
        }
        # .NET scanner needs the explicit property; source the value from the environment,
        # never echo argument arrays or enable verbose/transcript logging.
        & dotnet $arguments
        if ($LASTEXITCODE -ne 0) { throw 'Sonar begin failed.' }
        dotnet build Homeji.sln --no-incremental
        if ($LASTEXITCODE -ne 0) { throw 'Sonar build failed.' }
        dotnet test Homeji.sln --no-build --logger 'trx;LogFilePrefix=sonar' --results-directory $outputPath --collect 'XPlat Code Coverage' -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
        if ($LASTEXITCODE -ne 0) { throw 'Sonar tests failed.' }
        dotnet sonarscanner end "/d:sonar.token=$env:SONAR_TOKEN"
        if ($LASTEXITCODE -ne 0) { throw 'Server analysis or quality gate failed.' }
    } else {
        dotnet restore Homeji.sln -p:EnableSonarAnalyzers=true -p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath=obj/sonar.packages.lock.json
        if ($LASTEXITCODE -ne 0) { throw 'Local Sonar restore failed.' }
        try {
            dotnet build Homeji.sln --no-restore --no-incremental -p:EnableSonarAnalyzers=true -p:TreatWarningsAsErrors=false -p:ErrorLog=obj/sonar.sarif -v:quiet > (Join-Path $outputPath 'analyzers.log')
            if ($LASTEXITCODE -ne 0) { throw 'Local Sonar build failed.' }
            foreach ($project in Get-ChildItem src,tests -Directory) {
                $report = Join-Path $project.FullName 'obj\sonar.sarif'
                if (Test-Path -LiteralPath $report) { Copy-Item -LiteralPath $report -Destination (Join-Path $outputPath ($project.Name + '.sarif')) }
            }
        } finally {
            dotnet restore Homeji.sln
            if ($LASTEXITCODE -ne 0) { throw 'Normal restore failed after local Sonar analysis.' }
        }
        # The normal warnings-as-errors build is independent of Sonar's build settings.
        dotnet build Homeji.sln --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Normal build failed.' }
        dotnet test Homeji.sln --no-build --logger 'trx;LogFilePrefix=sonar-local' --results-directory $outputPath --collect 'XPlat Code Coverage' -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
        if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
    }
    dotnet list Homeji.sln package --vulnerable --include-transitive --format json > (Join-Path $outputPath 'dependency-audit.json')
    if ($LASTEXITCODE -ne 0) { throw 'Dependency audit failed.' }
    Write-Output "Evidence: $outputPath"
} finally { Pop-Location }
