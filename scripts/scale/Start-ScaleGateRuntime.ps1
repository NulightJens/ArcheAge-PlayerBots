[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$RuntimeRoot,
    [Parameter(Mandatory)][string]$GameDatabaseName,
    [Parameter(Mandatory)][string]$LoginDatabaseName,
    [Parameter(Mandatory)][string]$EvidencePath,
    [int]$StartupTimeoutSeconds = 180,
    [switch]$SafetyAcknowledged
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ExistingPath([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Label does not exist: $Path" }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Wait-ForLog(
    [System.Diagnostics.Process]$Process,
    [string]$LogPath,
    [string[]]$RequiredPatterns,
    [int]$TimeoutSeconds,
    [string]$Label
) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $Process.Refresh()
        if ($Process.HasExited) { throw "$Label exited during startup with code $($Process.ExitCode)." }
        if (Test-Path -LiteralPath $LogPath) {
            $content = Get-Content -LiteralPath $LogPath -Raw
            $missing = @($RequiredPatterns | Where-Object { $content -notmatch $_ })
            if ($missing.Count -eq 0) { return }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$Label did not emit all required startup evidence within $TimeoutSeconds seconds."
}

function Stop-Gracefully([System.Diagnostics.Process]$Process) {
    $Process.Refresh()
    if ($Process.HasExited) { return }
    & (Join-Path $PSScriptRoot 'Stop-ScaleGateRuntime.ps1') -ProcessId $Process.Id -TimeoutSeconds 180
    if ($LASTEXITCODE -ne 0) {
        throw "Graceful stop failed for process $($Process.Id); it was deliberately left running."
    }
}

$source = Resolve-ExistingPath $SourceRoot 'Source root'
$runtime = Resolve-ExistingPath $RuntimeRoot 'Runtime root'
if (-not $SafetyAcknowledged) {
    throw 'Starting a live runtime requires -SafetyAcknowledged after verifying isolated databases, loopback ports, and runtime ownership.'
}

if ($GameDatabaseName -notmatch '^aaemu_(t021|playerbots)_game_[a-z0-9_]*v[0-9]+$' -or
    $LoginDatabaseName -notmatch '^aaemu_(t021|playerbots)_login_[a-z0-9_]*v[0-9]+$') {
    throw 'Both database names must be isolated, versioned PlayerBots schemas.'
}
if (Test-Path -LiteralPath $EvidencePath) { throw "Refusing to overwrite startup evidence: $EvidencePath" }
$evidenceParent = Split-Path -Parent $EvidencePath
if (-not (Test-Path -LiteralPath $evidenceParent)) { throw "Evidence parent does not exist: $evidenceParent" }

$gameExe = Resolve-ExistingPath (Join-Path $source 'AAEmu.Game\bin\Debug\net10.0\AAEmu.Game.exe') 'Game executable'
$loginExe = Resolve-ExistingPath (Join-Path $source 'AAEmu.Login\bin\Debug\net10.0\AAEmu.Login.exe') 'Login executable'
$gameConfigPath = Resolve-ExistingPath (Join-Path $source 'AAEmu.Game\bin\Debug\net10.0\Config.Local.json') 'Game local config'
$loginConfigPath = Resolve-ExistingPath (Join-Path $source 'AAEmu.Login\bin\Debug\net10.0\Config.Local.json') 'Login local config'
$gameDataRoot = Join-Path (Split-Path -Parent $gameExe) 'Data'
[void](Resolve-ExistingPath (Join-Path $gameDataRoot 'compact.sqlite3') 'Task-local compact data')
[void](Resolve-ExistingPath (Join-Path $gameDataRoot 'Chronicle') 'Task-local Chronicle data')

$gameConfig = Get-Content -LiteralPath $gameConfigPath -Raw | ConvertFrom-Json
$loginConfig = Get-Content -LiteralPath $loginConfigPath -Raw | ConvertFrom-Json
if ("$($gameConfig.Connections.MySQLProvider.Database)" -ne $GameDatabaseName -or
    "$($loginConfig.Connections.MySQLProvider.Database)" -ne $LoginDatabaseName) {
    throw 'Task-local configs do not select the supplied isolated databases.'
}
$hosts = @(
    $gameConfig.Network.Host, $gameConfig.StreamNetwork.Host, $gameConfig.WebApiNetwork.Host,
    $gameConfig.LoginNetwork.Host, $loginConfig.InternalNetwork.Host, $loginConfig.Network.Host
)
if (@($hosts | Where-Object { "$_" -notin @('127.0.0.1', 'localhost') }).Count -ne 0) {
    throw 'Every task-local network listener must be bound to loopback.'
}

$ports = @(
    [int]$gameConfig.Network.Port, [int]$gameConfig.StreamNetwork.Port,
    [int]$gameConfig.WebApiNetwork.Port, [int]$gameConfig.LoginNetwork.Port,
    [int]$loginConfig.InternalNetwork.Port, [int]$loginConfig.Network.Port
) | Sort-Object -Unique
$listeners = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object LocalPort -in $ports)
if ($listeners.Count -ne 0) { throw "Required task-local ports are in use: $($listeners.LocalPort -join ', ')." }
$aaemuProcesses = @(Get-CimInstance Win32_Process | Where-Object Name -in @('AAEmu.Game.exe', 'AAEmu.Login.exe'))
if ($aaemuProcesses.Count -ne 0) { throw 'An AAEmu Game or Login process is already running.' }

$gameLog = Join-Path (Split-Path -Parent $gameExe) 'Logs\Server.log'
$loginLog = Join-Path (Split-Path -Parent $loginExe) 'Logs\Server.log'
$startupDeletedLogs = @(
    @(
        $gameLog, (Join-Path (Split-Path -Parent $gameExe) 'Logs\Error.log'),
        $loginLog, (Join-Path (Split-Path -Parent $loginExe) 'Logs\Error.log')
    ) | Where-Object { Test-Path -LiteralPath $_ }
)
if ($startupDeletedLogs.Count -ne 0) {
    throw "NLog would delete existing files at startup. Preserve them first: $($startupDeletedLogs -join '; ')"
}

$loginEnvironment = @{
    'Connections__MySQLProvider__Database' = $LoginDatabaseName
    'InternalNetwork__Host' = '127.0.0.1'
    'InternalNetwork__Port' = "$($loginConfig.InternalNetwork.Port)"
    'Network__Host' = '127.0.0.1'
    'Network__Port' = "$($loginConfig.Network.Port)"
}
$gameEnvironment = @{
    'Connections__MySQLProvider__Database' = $GameDatabaseName
    'LoginNetwork__Host' = '127.0.0.1'
    'LoginNetwork__Port' = "$($gameConfig.LoginNetwork.Port)"
    'Network__Host' = '127.0.0.1'
    'Network__Port' = "$($gameConfig.Network.Port)"
    'StreamNetwork__Host' = '127.0.0.1'
    'StreamNetwork__Port' = "$($gameConfig.StreamNetwork.Port)"
    'WebApiNetwork__Host' = '127.0.0.1'
    'WebApiNetwork__Port' = "$($gameConfig.WebApiNetwork.Port)"
}

$loginProcess = $null
$gameProcess = $null
try {
    $loginProcess = Start-Process -FilePath $loginExe -WorkingDirectory (Split-Path -Parent $loginExe) -WindowStyle Hidden -Environment $loginEnvironment -PassThru
    Wait-ForLog $loginProcess $loginLog @(
        [regex]::Escape("database $LoginDatabaseName"),
        'InternalNetwork started',
        [regex]::Escape("Now listening on: http://127.0.0.1:$($loginConfig.Network.Port)")
    ) $StartupTimeoutSeconds 'Login'

    # Several legacy managers resolve Data paths from the process working directory.
    # Keep evidence under RuntimeRoot, but run each executable from its own task-local bin directory.
    $gameProcess = Start-Process -FilePath $gameExe -WorkingDirectory (Split-Path -Parent $gameExe) -WindowStyle Hidden -Environment $gameEnvironment -PassThru
    Wait-ForLog $gameProcess $gameLog @(
        [regex]::Escape("database $GameDatabaseName"),
        'Server started!'
    ) $StartupTimeoutSeconds 'Game'

    $evidence = [ordered]@{
        schemaVersion = 't021.runtime-start.v1'
        startedAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceRoot = $source
        runtimeRoot = $runtime
        sourceCommit = (& git -C $source rev-parse HEAD).Trim()
        gameDatabase = $GameDatabaseName
        loginDatabase = $LoginDatabaseName
        loopbackPorts = $ports
        loginProcessId = $loginProcess.Id
        gameProcessId = $gameProcess.Id
        loginLog = $loginLog
        gameLog = $gameLog
        configurationPrecedence = 'per-process environment variables override user secrets and task-local JSON'
    }
    $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
    Write-Host "PlayerBots Login $($loginProcess.Id) and Game $($gameProcess.Id) started on isolated databases."
}
catch {
    if ($null -ne $gameProcess) { Stop-Gracefully $gameProcess }
    if ($null -ne $loginProcess) { Stop-Gracefully $loginProcess }
    throw
}
