[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\AAEmu.ClientDriver.csproj'
$fixture = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\fixtures\world-loaded.log'

& dotnet build $project --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Client driver build failed.'
}

$driver = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\bin\Debug\net10.0\AAEmu.ClientDriver.dll'
$status = (& dotnet $driver status --log $fixture --ignore-process | ConvertFrom-Json)
if ($status.schemaVersion -ne 1 -or $status.state -ne 'world_loaded') {
    throw "Unexpected fixture status: schema=$($status.schemaVersion), state=$($status.state)"
}
if ($status.log.milestones.worldAuthorized -ne '17:00:06' -or
    $status.log.milestones.worldLoaded -ne '17:00:28') {
    throw 'The client lifecycle milestone parser did not preserve expected log times.'
}

$portProbe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$portProbe.Start()
$port = ([System.Net.IPEndPoint]$portProbe.LocalEndpoint).Port
$portProbe.Stop()

$server = Start-Process -FilePath 'dotnet' -ArgumentList @(
    $driver,
    'serve',
    '--log', $fixture,
    '--ignore-process',
    '--port', $port,
    '--once',
    '--idle-timeout-ms', '10000'
) -WindowStyle Hidden -PassThru

$apiStatus = $null
for ($attempt = 0; $attempt -lt 20 -and $null -eq $apiStatus; $attempt++) {
    try {
        $apiStatus = Invoke-RestMethod -Uri "http://127.0.0.1:$port/v1/status" -TimeoutSec 1
    }
    catch {
        Start-Sleep -Milliseconds 100
    }
}

$null = $server.WaitForExit(12000)
if (-not $server.HasExited) {
    throw 'Client driver did not honor its one-request/idle-timeout exit contract.'
}
if ($null -eq $apiStatus -or $apiStatus.state -ne 'world_loaded') {
    throw 'Loopback client status API did not return the expected fixture state.'
}

Write-Host 'AAEmu.ClientDriver validation passed: build, fixture parser, and one-request loopback API.'
