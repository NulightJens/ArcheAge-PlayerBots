[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RuntimeRoot,
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$BotIdsPath,
    [Parameter(Mandatory)][string]$DatabaseName,
    [Parameter(Mandatory)][int]$GameProcessId,
    [Parameter(Mandatory)][string]$ServerLogPath,
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'runs'),
    [string]$ApiBase = 'http://127.0.0.1:1280',
    [string]$Actor = '@system',
    [string]$MySqlExe = 'mysql.exe',
    [string]$DbHost = '127.0.0.1',
    [int]$DbPort = 3306,
    [string]$DbUser = 'root',
    [string]$DbPasswordEnvironmentVariable = 'AAEMU_PLAYERBOTS_DB_PASSWORD',
    [string]$BudgetPolicyPath = '',
    [int]$WarmupSeconds = 120,
    [int]$SteadySeconds = 300,
    [int]$RecoverySeconds = 60,
    [int]$SampleIntervalSeconds = 1,
    [switch]$SafetyAcknowledged
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[System.Net.ServicePointManager]::Expect100Continue = $false

$script:RunStartedAtUtc = [DateTime]::UtcNow
$script:IncompleteReasons = [System.Collections.Generic.List[string]]::new()
$script:ApiCalls = [System.Collections.Generic.List[object]]::new()
$script:SpawnedBotIds = [System.Collections.Generic.List[uint]]::new()

function Add-Incomplete([string]$Reason) {
    if (-not $script:IncompleteReasons.Contains($Reason)) { $script:IncompleteReasons.Add($Reason) }
    Write-Warning $Reason
}

function Resolve-ExistingPath([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Label does not exist: $Path" }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Assert-OperatorSafety {
    if (-not $SafetyAcknowledged) {
        throw 'Live measurement requires -SafetyAcknowledged after verifying runtime ownership, an isolated versioned database, and loopback-only command API access.'
    }
}

function Invoke-BotCommand([string]$Command, [string]$Arguments) {
    $body = ConvertTo-Json -Compress @{ character = $Actor; arguments = $Arguments }
    $uri = "$ApiBase/api/commands/$Command"
    try {
        $response = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json' -Body $body -TimeoutSec 30
        $messages = @($response.Messages)
        $errors = @($response.ErrorMessages)
        $call = [pscustomobject]@{
            capturedAtUtc = [DateTime]::UtcNow.ToString('O')
            command = "/$Command $Arguments".TrimEnd()
            http = 200
            ok = ($errors.Count -eq 0)
            messages = $messages
            errors = $errors
        }
    }
    catch {
        $call = [pscustomobject]@{
            capturedAtUtc = [DateTime]::UtcNow.ToString('O')
            command = "/$Command $Arguments".TrimEnd()
            http = 0
            ok = $false
            messages = @()
            errors = @($_.Exception.Message)
        }
    }
    $script:ApiCalls.Add($call)
    return $call
}

function Get-LiveMetrics {
    $call = Invoke-BotCommand 'botmetrics' 'snapshot'
    if (-not $call.ok) { throw "botmetrics snapshot failed: $($call.errors -join '; ')" }
    $marker = 'T021_METRICS '
    $line = @($call.messages | Where-Object { "$($_)".Contains($marker, [StringComparison]::Ordinal) }) | Select-Object -First 1
    if (-not $line) { throw 'botmetrics response did not contain a T021_METRICS document.' }
    $markerIndex = $line.IndexOf($marker, [StringComparison]::Ordinal)
    $metrics = ($line.Substring($markerIndex + $marker.Length) | ConvertFrom-Json)
    if ($metrics.schemaVersion -ne 't021.scale-metrics.v1' -or $metrics.provenance -ne 'live-server') {
        throw "unsupported or non-live metrics provenance: schema=$($metrics.schemaVersion), provenance=$($metrics.provenance)"
    }
    return $metrics
}

function Reset-LiveMetrics {
    $call = Invoke-BotCommand 'botmetrics' 'reset'
    if (-not $call.ok -or -not (@($call.messages) -match 'T021_METRICS_RESET ')) {
        throw "botmetrics reset failed: $($call.errors -join '; ')"
    }
}

function Wait-ExactBotCount([int]$Expected, [int]$TimeoutSeconds = 120) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $snapshot = Get-LiveMetrics
        if ([int]$snapshot.runtimeCount -eq $Expected -and [int]$snapshot.bots.bots -eq $Expected) { return $snapshot }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "bot count did not reach exact value $Expected (runtimeCount=$($snapshot.runtimeCount), hostBots=$($snapshot.bots.bots))."
}

function Get-DatabaseStatus {
    $secret = [Environment]::GetEnvironmentVariable($DbPasswordEnvironmentVariable)
    if ([string]::IsNullOrWhiteSpace($secret)) {
        throw "database password environment variable '$DbPasswordEnvironmentVariable' is not set"
    }
    $query = "SHOW GLOBAL STATUS WHERE Variable_name IN ('Questions','Queries','Com_select','Com_insert','Com_update','Com_delete','Slow_queries','Bytes_received','Bytes_sent','Threads_running')"
    $priorMysqlPassword = [Environment]::GetEnvironmentVariable('MYSQL_PWD')
    try {
        [Environment]::SetEnvironmentVariable('MYSQL_PWD', $secret)
        $rows = @(& $MySqlExe --batch --skip-column-names --host=$DbHost --port=$DbPort --user=$DbUser --execute=$query 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "mysql status query failed with exit code ${LASTEXITCODE}: $($rows -join ' ')" }
    }
    finally {
        [Environment]::SetEnvironmentVariable('MYSQL_PWD', $priorMysqlPassword)
    }
    $status = [ordered]@{}
    foreach ($row in $rows) {
        $parts = "$row" -split "`t", 2
        if ($parts.Count -eq 2) { $status[$parts[0]] = [double]$parts[1] }
    }
    return [pscustomobject]$status
}

function Get-DatabaseRates($Start, $End, [double]$Seconds) {
    $result = [ordered]@{}
    foreach ($name in @('Questions','Queries','Com_select','Com_insert','Com_update','Com_delete','Slow_queries','Bytes_received','Bytes_sent')) {
        $startValue = [double]$Start.$name
        $endValue = [double]$End.$name
        $result[($name.Substring(0,1).ToLowerInvariant() + $name.Substring(1) + 'PerSecond')] =
            [Math]::Max(0, $endValue - $startValue) / [Math]::Max(0.001, $Seconds)
    }
    $result['threadsRunningAtEnd'] = [double]$End.Threads_running
    $result['scope'] = 'mysql-global-status'
    return [pscustomobject]$result
}

function Get-ProcessSample([int]$Id, $Previous) {
    $process = Get-Process -Id $Id -ErrorAction Stop
    $now = [DateTime]::UtcNow
    $cpuTicks = $process.TotalProcessorTime.Ticks
    $cpuPercent = $null
    if ($null -ne $Previous) {
        $elapsed = ($now - [DateTime]$Previous.capturedAtUtc).TotalSeconds
        if ($elapsed -gt 0) {
            $cpuPercent = [Math]::Max(0d, ($cpuTicks - [long]$Previous.cpuTicks) / [TimeSpan]::TicksPerSecond / $elapsed / [Environment]::ProcessorCount * 100)
        }
    }
    return [pscustomobject]@{
        capturedAtUtc = $now.ToString('O')
        cpuTicks = $cpuTicks
        cpuPercent = $cpuPercent
        workingSetBytes = [long]$process.WorkingSet64
        privateMemoryBytes = [long]$process.PrivateMemorySize64
        threadCount = [int]$process.Threads.Count
        handleCount = [int]$process.HandleCount
    }
}

function Get-Percentile([double[]]$Values, [double]$Quantile) {
    $clean = @($Values | Where-Object { $null -ne $_ } | Sort-Object)
    if ($clean.Count -eq 0) { return $null }
    $index = [Math]::Min($clean.Count - 1, [Math]::Max(0, [Math]::Ceiling($clean.Count * $Quantile) - 1))
    return [double]$clean[$index]
}

function Get-ProcessSummary($Samples) {
    $cpu = @($Samples | ForEach-Object { if ($null -ne $_.cpuPercent) { [double]$_.cpuPercent } })
    $working = @($Samples | ForEach-Object { [double]$_.workingSetBytes })
    $private = @($Samples | ForEach-Object { [double]$_.privateMemoryBytes })
    return [pscustomobject]@{
        sampleCount = $Samples.Count
        cpuMeanPercent = if ($cpu.Count) { ($cpu | Measure-Object -Average).Average } else { $null }
        cpuP95Percent = Get-Percentile $cpu 0.95
        cpuMaxPercent = if ($cpu.Count) { ($cpu | Measure-Object -Maximum).Maximum } else { $null }
        workingSetP95Bytes = Get-Percentile $working 0.95
        workingSetMaxBytes = if ($working.Count) { ($working | Measure-Object -Maximum).Maximum } else { $null }
        privateMemoryP95Bytes = Get-Percentile $private 0.95
        privateMemoryMaxBytes = if ($private.Count) { ($private | Measure-Object -Maximum).Maximum } else { $null }
    }
}

function Measure-Window([string]$Name, [int]$ExpectedBots, [int]$DurationSeconds) {
    Reset-LiveMetrics
    $startMetrics = Get-LiveMetrics
    if ([int]$startMetrics.runtimeCount -ne $ExpectedBots) { throw "$Name started with $($startMetrics.runtimeCount) bots; expected $ExpectedBots" }
    $dbStart = Get-DatabaseStatus
    $samples = [System.Collections.Generic.List[object]]::new()
    $previous = $null
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $DurationSeconds) {
        $sample = Get-ProcessSample $GameProcessId $previous
        $samples.Add($sample)
        $previous = $sample
        $remaining = $DurationSeconds - $clock.Elapsed.TotalSeconds
        if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int]([Math]::Min($SampleIntervalSeconds, $remaining) * 1000)) }
    }
    $clock.Stop()
    $dbEnd = Get-DatabaseStatus
    $endMetrics = Get-LiveMetrics
    $exact = [int]$endMetrics.runtimeCount -eq $ExpectedBots -and [int]$endMetrics.bots.bots -eq $ExpectedBots
    if (-not $exact) { Add-Incomplete "$Name did not retain exact bot count $ExpectedBots." }
    return [pscustomobject]@{
        name = $Name
        expectedBots = $ExpectedBots
        exactBotCount = $exact
        durationSeconds = $clock.Elapsed.TotalSeconds
        metrics = $endMetrics
        process = Get-ProcessSummary $samples
        processSamples = @($samples)
        database = Get-DatabaseRates $dbStart $dbEnd $clock.Elapsed.TotalSeconds
    }
}

function Measure-LoadStage([int]$Target, [uint[]]$Ids, [int]$PriorTarget) {
    $spawnSnapshot = $null
    if ($Target -gt $PriorTarget) {
        Reset-LiveMetrics
        foreach ($id in $Ids[$PriorTarget..($Target - 1)]) {
            $spawn = Invoke-BotCommand 'addbot' "$id"
            if (-not $spawn.ok) { throw "addbot $id failed: $($spawn.errors -join '; ')" }
            $script:SpawnedBotIds.Add($id)
            $state = Invoke-BotCommand 'botstate' "$id grind"
            if (-not $state.ok) { throw "botstate $id grind failed: $($state.errors -join '; ')" }
        }
        [void](Wait-ExactBotCount $Target)
        $spawnSnapshot = Get-LiveMetrics
        if ([long]$spawnSnapshot.bots.spawn.count -ne ($Target - $PriorTarget)) {
            Add-Incomplete "load-$Target spawn timing count was $($spawnSnapshot.bots.spawn.count), expected $($Target - $PriorTarget)."
        }
    }
    Start-Sleep -Seconds $WarmupSeconds
    $steady = Measure-Window "load-$Target" $Target $SteadySeconds
    return [pscustomobject]@{ load = $Target; spawn = $spawnSnapshot; steady = $steady }
}

function Get-HardwareIdentity {
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
    $computer = Get-CimInstance Win32_ComputerSystem
    $os = Get-CimInstance Win32_OperatingSystem
    return [pscustomobject]@{
        machine = $env:COMPUTERNAME
        cpu = $cpu.Name
        logicalProcessors = [int]$cpu.NumberOfLogicalProcessors
        physicalMemoryBytes = [long]$computer.TotalPhysicalMemory
        operatingSystem = $os.Caption
        osVersion = $os.Version
        dotnetRuntime = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    }
}

function Get-BuildIdentity([string]$Source, [string]$Runtime, [int]$ProcessId) {
    $commit = (& git -C $Source rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'could not resolve source commit' }
    $dirtyLines = @(& git -C $Source status --porcelain=v1)
    $process = Get-Process -Id $ProcessId -ErrorAction Stop
    $exePath = $process.Path
    if (-not $exePath.StartsWith($Runtime, [StringComparison]::OrdinalIgnoreCase)) {
        throw "game PID $ProcessId is outside runtime root: $exePath"
    }
    return [pscustomobject]@{
        sourceCommit = $commit
        sourceDirty = ($dirtyLines.Count -gt 0)
        sourceDirtyPaths = $dirtyLines
        processId = $ProcessId
        processStartTimeUtc = $process.StartTime.ToUniversalTime().ToString('O')
        executablePath = $exePath
        executableSha256 = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
        executableVersion = (Get-Item -LiteralPath $exePath).VersionInfo.FileVersion
    }
}

function Read-BudgetPolicy([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $resolved = Resolve-ExistingPath $Path 'Budget policy'
    $policy = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
    if ($policy.schemaVersion -ne 't021.scale-budget.v1' -or $policy.provenance -ne 'baseline-plus-desired-server-target') {
        throw 'Budget policy must use t021.scale-budget.v1 and baseline-plus-desired-server-target provenance.'
    }
    if ([string]::IsNullOrWhiteSpace("$($policy.approvedBy)") -or $null -eq $policy.desiredServerTickTargetMs) {
        throw 'Budget policy requires approvedBy and desiredServerTickTargetMs.'
    }
    foreach ($name in @('serverWorkP95Ms','serverWorkP99Ms','serverWorkMaxMs','cpuP95Percent','workingSetMaxBytes','allocatedBytesPerSecond','gen2CollectionsPerMinute','databaseQueriesPerSecond','skippedTicksPerMinute','runtimeOverlapsPerMinute','recoveryServerWorkP95Ms')) {
        if ($null -eq $policy.limits.$name) { throw "Budget policy limit '$name' is missing; partial policies cannot pass." }
    }
    return $policy
}

function Test-StageBudget($Stage, $Policy) {
    if ($null -eq $Policy) { return [pscustomobject]@{ status = 'INCOMPLETE'; breaches = @('approved baseline-derived budget policy absent') } }
    $window = $Stage.steady
    $m = $window.metrics
    $seconds = [Math]::Max(0.001, [double]$window.durationSeconds)
    $checks = [ordered]@{
        serverWorkP95Ms = [double]$m.server.work.p95Ms
        serverWorkP99Ms = [double]$m.server.work.p99Ms
        serverWorkMaxMs = [double]$m.server.work.maxMs
        cpuP95Percent = [double]$window.process.cpuP95Percent
        workingSetMaxBytes = [double]$window.process.workingSetMaxBytes
        allocatedBytesPerSecond = [double]$m.bots.allocatedBytes / $seconds
        gen2CollectionsPerMinute = [double]$m.bots.gen2Collections / $seconds * 60
        databaseQueriesPerSecond = [double]$window.database.queriesPerSecond
        skippedTicksPerMinute = [double]$m.bots.skippedTicks / $seconds * 60
        runtimeOverlapsPerMinute = [double]$m.bots.runtimeOverlaps / $seconds * 60
    }
    $breaches = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $checks.GetEnumerator()) {
        if ([double]$entry.Value -gt [double]$Policy.limits.($entry.Key)) {
            $breaches.Add("$($entry.Key)=$($entry.Value) > $($Policy.limits.($entry.Key))")
        }
    }
    if ([long]$m.bots.tickErrors -ne 0) { $breaches.Add("tickErrors=$($m.bots.tickErrors)") }
    return [pscustomobject]@{ status = if ($breaches.Count -eq 0) { 'PASS' } else { 'FAIL' }; measurements = [pscustomobject]$checks; breaches = @($breaches) }
}

function Test-RecoveryBudget($Recovery, $Policy) {
    if ($null -eq $Policy -or $null -eq $Recovery) {
        return [pscustomobject]@{ status = 'INCOMPLETE'; breaches = @('recovery measurement or approved policy absent') }
    }
    $breaches = [System.Collections.Generic.List[string]]::new()
    if (-not $Recovery.exactBotCount) { $breaches.Add('recovery did not retain exact zero-bot state') }
    if ([double]$Recovery.metrics.server.work.p95Ms -gt [double]$Policy.limits.recoveryServerWorkP95Ms) {
        $breaches.Add("serverWorkP95Ms=$($Recovery.metrics.server.work.p95Ms) > $($Policy.limits.recoveryServerWorkP95Ms)")
    }
    if ([long]$Recovery.metrics.bots.tickErrors -ne 0) { $breaches.Add("tickErrors=$($Recovery.metrics.bots.tickErrors)") }
    return [pscustomobject]@{ status = if ($breaches.Count -eq 0) { 'PASS' } else { 'FAIL' }; breaches = @($breaches) }
}

function New-BudgetTemplate($Baseline, [string]$RunId) {
    $seconds = [Math]::Max(0.001, [double]$Baseline.steady.durationSeconds)
    return [ordered]@{
        schemaVersion = 't021.scale-budget.v1'
        status = 'REQUIRES_OPERATOR_INPUT'
        provenance = 'baseline-plus-desired-server-target'
        approvedBy = $null
        approvedAtUtc = $null
        desiredServerTickTargetMs = $null
        baseline = [ordered]@{
            sourceRunId = $RunId
            expectedBots = 0
            serverWorkP95Ms = $Baseline.steady.metrics.server.work.p95Ms
            serverWorkP99Ms = $Baseline.steady.metrics.server.work.p99Ms
            serverWorkMaxMs = $Baseline.steady.metrics.server.work.maxMs
            cpuP95Percent = $Baseline.steady.process.cpuP95Percent
            workingSetMaxBytes = $Baseline.steady.process.workingSetMaxBytes
            allocatedBytesPerSecond = [double]$Baseline.steady.metrics.bots.allocatedBytes / $seconds
            gen2CollectionsPerMinute = [double]$Baseline.steady.metrics.bots.gen2Collections / $seconds * 60
            databaseQueriesPerSecond = $Baseline.steady.database.queriesPerSecond
        }
        limits = [ordered]@{
            serverWorkP95Ms = $null; serverWorkP99Ms = $null; serverWorkMaxMs = $null
            cpuP95Percent = $null; workingSetMaxBytes = $null; allocatedBytesPerSecond = $null
            gen2CollectionsPerMinute = $null; databaseQueriesPerSecond = $null
            skippedTicksPerMinute = $null; runtimeOverlapsPerMinute = $null
            recoveryServerWorkP95Ms = $null
        }
        rationale = 'Fill limits from this retained no-bot baseline plus the explicitly desired whole-server tick target. Do not infer them from bot-host timing.'
    }
}

function Write-MarkdownReport($Result, [string]$Path) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# PlayerBots scale/resource run $($Result.runId)")
    $lines.Add('')
    $lines.Add("- Verdict: **$($Result.verdict)**")
    $lines.Add("- Provenance: live-server ladder; no simulator capacity claim")
    $lines.Add("- Commit: ``$($Result.build.sourceCommit)``")
    $lines.Add("- Database: ``$($Result.databaseName)`` (isolated/versioned preflight)")
    $lines.Add("- Scenario: same retained world; 0/10/50/100 bots; $SteadySeconds s steady windows")
    $lines.Add('')
    $lines.Add('| Bots | Exact | server p50/p95/p99/max ms | CPU p95 % | WS/private max MiB | alloc MiB/s | GC 0/1/2 | DB queries/s | scans/s | decisions/s | configured/governor/observed % | Budget |')
    $lines.Add('|---:|:---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|')
    foreach ($stage in $Result.stages) {
        $w = $stage.steady; $m = $w.metrics; $s = [Math]::Max(.001,[double]$w.durationSeconds)
        $tick = '{0:N2}/{1:N2}/{2:N2}/{3:N2}' -f $m.server.work.p50Ms,$m.server.work.p95Ms,$m.server.work.p99Ms,$m.server.work.maxMs
        $gc = "$($m.bots.gen0Collections)/$($m.bots.gen1Collections)/$($m.bots.gen2Collections)"
        $activity = '{0:N1}/{1:N1}/{2:N1}' -f $m.config.activityPercent,$m.bots.governorEffectivePercent,$m.bots.effectiveActivityPercent
        $memory = '{0:N1}/{1:N1}' -f ([double]$w.process.workingSetMaxBytes/1MB),([double]$w.process.privateMemoryMaxBytes/1MB)
        $allocated = [double]$m.bots.allocatedBytes/$s/1MB
        $lines.Add("| $($stage.load) | $($w.exactBotCount) | $tick | $([double]$w.process.cpuP95Percent) | $memory | $([Math]::Round($allocated,3)) | $gc | $([Math]::Round([double]$w.database.queriesPerSecond,2)) | $([Math]::Round([double]$m.bots.worldScans/$s,2)) | $([Math]::Round([double]$m.bots.decisionSteps/$s,2)) | $activity | $($stage.budget.status) |")
    }
    $lines.Add('')
    $lines.Add('| Bots | host p50/p95/p99/max ms | brain/mover per s | cadence C/M/I/R/inactive | scans/path per s | decisions/invalid per s | casts ok/attempt | kills observed/credited | stuck nudge/teleport | spawn p95/max ms |')
    $lines.Add('|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
    foreach ($stage in $Result.stages) {
        $w = $stage.steady; $m = $w.metrics; $s = [Math]::Max(.001,[double]$w.durationSeconds)
        $hostTiming = '{0:N2}/{1:N2}/{2:N2}/{3:N2}' -f $m.bots.hostTick.p50Ms,$m.bots.hostTick.p95Ms,$m.bots.hostTick.p99Ms,$m.bots.hostTick.maxMs
        $work = '{0:N2}/{1:N2}' -f ([double]$m.bots.brainSteps/$s),([double]$m.bots.moverSteps/$s)
        $cadence = '{0:N2}/{1:N2}/{2:N2}/{3:N2}/{4:N2}' -f ([double]$m.bots.combatBrainSteps/$s),([double]$m.bots.movingBrainSteps/$s),([double]$m.bots.idleBrainSteps/$s),([double]$m.bots.restingBrainSteps/$s),([double]$m.bots.inactiveCadenceBrainSteps/$s)
        $scan = '{0:N2}/{1:N2}' -f ([double]$m.bots.worldScans/$s),([double]$m.bots.pathRequests/$s)
        $decision = '{0:N2}/{1:N2}' -f ([double]$m.bots.decisionSteps/$s),([double]$m.bots.invalidTargets/$s)
        $spawnCost = if ($null -eq $stage.spawn) { 'n/a' } else { "$($stage.spawn.bots.spawn.p95Ms)/$($stage.spawn.bots.spawn.maxMs)" }
        $lines.Add("| $($stage.load) | $hostTiming | $work | $cadence | $scan | $decision | $($m.bots.castSuccesses)/$($m.bots.castAttempts) | $($m.bots.observedKills)/$($m.bots.creditedKills) | $($m.bots.stuckNudges)/$($m.bots.stuckTeleports) | $spawnCost |")
    }
    $lines.Add('')
    $lines.Add("Highest honestly demonstrated stable population: $($Result.highestStablePopulation)")
    $lines.Add("First measured bottleneck: $($Result.firstBottleneck)")
    if ($null -ne $Result.recovery) {
        $lines.Add("Recovery: exact=$($Result.recovery.exactBotCount), duration=$([Math]::Round([double]$Result.recovery.durationSeconds,1)) s, server p95=$($Result.recovery.metrics.server.work.p95Ms) ms, budget=$($Result.recoveryBudget.status).")
    }
    if ($null -ne $Result.despawn) {
        $lines.Add("Despawn: count=$($Result.despawn.bots.despawn.count), p95=$($Result.despawn.bots.despawn.p95Ms) ms, max=$($Result.despawn.bots.despawn.maxMs) ms, failures=$($Result.despawn.bots.despawnFailures).")
    }
    $lines.Add('')
    $lines.Add('Shutdown cleanup is pending until a graceful Ctrl+C stop is finalized with `Finalize-ScaleGate.ps1`; therefore this measurement command never emits PASS by itself.')
    if ($Result.incompleteReasons.Count -gt 0) {
        $lines.Add('')
        $lines.Add('## Incomplete reasons')
        $lines.Add('')
        foreach ($reason in $Result.incompleteReasons) { $lines.Add("- $reason") }
    }
    $lines | Set-Content -LiteralPath $Path -Encoding utf8
}

Assert-OperatorSafety
$runtime = Resolve-ExistingPath $RuntimeRoot 'Runtime root'
$source = Resolve-ExistingPath $SourceRoot 'Source root'
$idsFile = Resolve-ExistingPath $BotIdsPath 'Bot IDs file'
$serverLog = Resolve-ExistingPath $ServerLogPath 'Server log'
$serverLogStartOffset = [long](Get-Item -LiteralPath $serverLog).Length
if ($DatabaseName -notmatch '^aaemu_(t021|playerbots)_[a-z0-9_]*v[0-9]+$') {
    throw "DatabaseName '$DatabaseName' is not an isolated versioned PlayerBots schema (expected aaemu_playerbots_*vN)."
}
$localConfigPath = Join-Path $runtime 'AAEmu.Game\bin\Debug\net10.0\Config.Local.json'
$localConfig = Get-Content -LiteralPath (Resolve-ExistingPath $localConfigPath 'Runtime Config.Local.json') -Raw | ConvertFrom-Json
if ("$($localConfig.Connections.MySQLProvider.Database)" -ne $DatabaseName) {
    throw 'Runtime Config.Local.json must explicitly select the supplied isolated versioned database.'
}
if ("$($localConfig.WebApiNetwork.Host)" -notin @('127.0.0.1','localhost')) {
    throw 'Runtime Config.Local.json must explicitly bind WebApiNetwork.Host to loopback.'
}
$botIds = @((Get-Content -LiteralPath $idsFile -Raw | ConvertFrom-Json) | ForEach-Object { [uint]$_ })
if ($botIds.Count -lt 100 -or @($botIds | Select-Object -Unique).Count -ne $botIds.Count) {
    throw 'Bot IDs file must contain at least 100 unique retained character IDs.'
}
$botIds = [uint[]]$botIds[0..99]
$budget = Read-BudgetPolicy $BudgetPolicyPath

$runId = '{0}-{1}' -f ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')), ([Guid]::NewGuid().ToString('N').Substring(0,8))
$runDirectory = Join-Path $OutputRoot $runId
if (Test-Path -LiteralPath $runDirectory) { throw "refusing to overwrite existing run directory $runDirectory" }
[void](New-Item -ItemType Directory -Path $runDirectory -Force:$false)

$hardware = Get-HardwareIdentity
$build = Get-BuildIdentity $source $runtime $GameProcessId
$initial = Get-LiveMetrics
if ([int]$initial.runtimeCount -ne 0 -or [int]$initial.bots.bots -ne 0) {
    throw "scale ladder requires an untouched no-bot start; observed runtimeCount=$($initial.runtimeCount), hostBots=$($initial.bots.bots)"
}

$stages = [System.Collections.Generic.List[object]]::new()
try {
    foreach ($load in @(0,10,50,100)) {
        $prior = if ($load -eq 0) { 0 } elseif ($load -eq 10) { 0 } elseif ($load -eq 50) { 10 } else { 50 }
        $stage = Measure-LoadStage $load $botIds $prior
        $stages.Add($stage)
    }

}
catch {
    Add-Incomplete $_.Exception.Message
}

$despawn = $null
$recovery = $null
try {
    Reset-LiveMetrics
    foreach ($id in @($script:SpawnedBotIds)) {
        $remove = Invoke-BotCommand 'removebot' "$id"
        if (-not $remove.ok) { Add-Incomplete "removebot $id failed during scale-down recovery." }
    }
    [void](Wait-ExactBotCount 0)
    $despawn = Get-LiveMetrics
    if ([long]$despawn.bots.despawn.count -ne $script:SpawnedBotIds.Count) {
        Add-Incomplete "despawn timing count was $($despawn.bots.despawn.count), expected $($script:SpawnedBotIds.Count)."
    }
    $recovery = Measure-Window 'recovery-0' 0 $RecoverySeconds
}
catch {
    Add-Incomplete "scale-down recovery failed: $($_.Exception.Message)"
}

foreach ($stage in $stages) { $stage | Add-Member -NotePropertyName budget -NotePropertyValue (Test-StageBudget $stage $budget) }
$recoveryBudget = Test-RecoveryBudget $recovery $budget
$budgetTemplate = if ($stages.Count -gt 0) { New-BudgetTemplate $stages[0] $runId } else { Get-Content -LiteralPath (Join-Path $PSScriptRoot 'budget-template.example.json') -Raw | ConvertFrom-Json }
$budgetTemplatePath = Join-Path $runDirectory 'budget-template.json'
$budgetTemplate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $budgetTemplatePath -Encoding utf8

$highest = $null
$firstBottleneck = 'not determined without a complete approved budget evaluation'
if ($null -ne $budget -and $stages.Count -eq 4) {
    foreach ($stage in $stages) {
        if ($stage.budget.status -eq 'PASS') { $highest = $stage.load }
        elseif ($firstBottleneck -like 'not determined*') {
            $firstBottleneck = "load $($stage.load): $($stage.budget.breaches -join '; ')"
        }
    }
    if ($firstBottleneck -like 'not determined*') { $firstBottleneck = 'none within the measured 100-bot ladder' }
    if ($recoveryBudget.status -eq 'FAIL' -and $firstBottleneck -eq 'none within the measured 100-bot ladder') {
        $firstBottleneck = "scale-down recovery: $($recoveryBudget.breaches -join '; ')"
        $highest = $null
    }
}
if ($null -eq $budget) { Add-Incomplete 'approved baseline-derived budget policy absent; budget-template.json requires review' }
if ($stages.Count -ne 4) { Add-Incomplete "only $($stages.Count) of 4 load stages completed" }
Add-Incomplete 'graceful shutdown cleanup evidence pending finalization'

$result = [ordered]@{
    schemaVersion = 't021.scale-run.v1'
    runId = $runId
    verdict = 'INCOMPLETE'
    provenance = 'live-server'
    startedAtUtc = $script:RunStartedAtUtc.ToString('O')
    completedAtUtc = [DateTime]::UtcNow.ToString('O')
    runtimeRoot = $runtime
    sourceRoot = $source
    databaseName = $DatabaseName
    serverLog = [ordered]@{ path = $serverLog; startOffset = $serverLogStartOffset }
    hardware = $hardware
    build = $build
    scenario = [ordered]@{ loads = @(0,10,50,100); warmupSeconds = $WarmupSeconds; steadySeconds = $SteadySeconds; recoverySeconds = $RecoverySeconds; botIds = $botIds }
    budgetPolicy = $budget
    stages = @($stages)
    despawn = $despawn
    recovery = $recovery
    recoveryBudget = $recoveryBudget
    shutdownCleanup = [ordered]@{ status = 'PENDING'; remainingBots = $null; remainingRuntimes = $null; evidenceLine = $null }
    highestStablePopulation = $highest
    firstBottleneck = $firstBottleneck
    incompleteReasons = @($script:IncompleteReasons)
    apiCalls = @($script:ApiCalls)
}
$rawPath = Join-Path $runDirectory 'result.json'
$result | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $rawPath -Encoding utf8
Write-MarkdownReport ([pscustomobject]$result) (Join-Path $runDirectory 'report.md')
Write-Host "PlayerBots measurement retained at $runDirectory"
Write-Host 'Verdict: INCOMPLETE (graceful shutdown finalization and any missing approved budget are mandatory).'
exit 2
