[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResultPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ShutdownErrorLogPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Percentile([double[]]$Values, [double]$Quantile) {
    $clean = @($Values | Sort-Object)
    if ($clean.Count -eq 0) { return $null }
    $index = [Math]::Min($clean.Count - 1, [Math]::Max(0, [Math]::Ceiling($clean.Count * $Quantile) - 1))
    return [double]$clean[$index]
}

function Get-CpuSummary($Samples, [int]$LogicalProcessors) {
    $samplesArray = @($Samples)
    if ($samplesArray.Count -lt 2 -or $LogicalProcessors -le 0) { throw 'CPU reanalysis requires at least two raw samples and a positive logical processor count.' }
    $values = [System.Collections.Generic.List[double]]::new()
    for ($i = 1; $i -lt $samplesArray.Count; $i++) {
        $prior = $samplesArray[$i - 1]
        $current = $samplesArray[$i]
        $elapsed = ([DateTimeOffset]::Parse("$($current.capturedAtUtc)") - [DateTimeOffset]::Parse("$($prior.capturedAtUtc)")).TotalSeconds
        if ($elapsed -le 0) { continue }
        $cpu = ([long]$current.cpuTicks - [long]$prior.cpuTicks) / [TimeSpan]::TicksPerSecond / $elapsed / $LogicalProcessors * 100
        $values.Add([Math]::Max(0d, $cpu))
    }
    if ($values.Count -eq 0) { throw 'Raw CPU samples contained no positive-duration intervals.' }
    $wall = ([DateTimeOffset]::Parse("$($samplesArray[-1].capturedAtUtc)") - [DateTimeOffset]::Parse("$($samplesArray[0].capturedAtUtc)")).TotalSeconds
    $mean = ([long]$samplesArray[-1].cpuTicks - [long]$samplesArray[0].cpuTicks) / [TimeSpan]::TicksPerSecond / $wall / $LogicalProcessors * 100
    return [pscustomobject]@{
        sampleCount = $samplesArray.Count
        intervalCount = $values.Count
        meanPercent = [Math]::Max(0d, $mean)
        p95Percent = Get-Percentile $values.ToArray() 0.95
        maxPercent = ($values | Measure-Object -Maximum).Maximum
        provenance = 'recomputed-from-retained-cumulative-process-cpu-ticks'
    }
}

$resolvedResult = (Resolve-Path -LiteralPath $ResultPath).Path
$run = Get-Content -LiteralPath $resolvedResult -Raw | ConvertFrom-Json
if ($run.schemaVersion -ne 't021.scale-run.v1' -or $run.provenance -ne 'live-server') {
    throw 'Analysis requires a t021.scale-run.v1 live-server result.'
}
if (Test-Path -LiteralPath $OutputDirectory) {
    if (@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0) { throw "Refusing to overwrite analysis output: $OutputDirectory" }
}
else {
    [void](New-Item -ItemType Directory -Path $OutputDirectory)
}

$logicalProcessors = [int]$run.hardware.logicalProcessors
$stageAnalyses = [System.Collections.Generic.List[object]]::new()
foreach ($stage in @($run.stages)) {
    $window = $stage.steady
    $metrics = $window.metrics
    $bots = $metrics.bots
    $seconds = [Math]::Max(0.001, [double]$window.durationSeconds)
    $cpu = Get-CpuSummary $window.processSamples $logicalProcessors
    $spawn = if ($null -eq $stage.spawn) { $null } else { $stage.spawn.bots.spawn }
    $stageAnalyses.Add([pscustomobject][ordered]@{
        load = [int]$stage.load
        exactBotCount = [bool]$window.exactBotCount
        durationSeconds = [double]$window.durationSeconds
        serverWorkMs = $metrics.server.work
        serverIntervalMs = $metrics.server.interval
        botHostMs = $bots.hostTick
        cpu = $cpu
        memory = [ordered]@{
            workingSetP95Bytes = [long]$window.process.workingSetP95Bytes
            workingSetMaxBytes = [long]$window.process.workingSetMaxBytes
            privateP95Bytes = [long]$window.process.privateMemoryP95Bytes
            privateMaxBytes = [long]$window.process.privateMemoryMaxBytes
        }
        allocationAndGc = [ordered]@{
            allocatedBytesPerSecond = [double]$bots.allocatedBytes / $seconds
            gen0PerMinute = [double]$bots.gen0Collections / $seconds * 60
            gen1PerMinute = [double]$bots.gen1Collections / $seconds * 60
            gen2PerMinute = [double]$bots.gen2Collections / $seconds * 60
        }
        database = $window.database
        cadencePerSecond = [ordered]@{
            brain = [double]$bots.brainSteps / $seconds
            combat = [double]$bots.combatBrainSteps / $seconds
            moving = [double]$bots.movingBrainSteps / $seconds
            idle = [double]$bots.idleBrainSteps / $seconds
            resting = [double]$bots.restingBrainSteps / $seconds
            inactive = [double]$bots.inactiveCadenceBrainSteps / $seconds
        }
        activity = [ordered]@{
            configuredPercent = [int]$bots.configuredActivityPercent
            governorPercent = [int]$bots.governorEffectivePercent
            observedPercent = [double]$bots.effectiveActivityPercent
            activeSteps = [long]$bots.activeBrainSteps
            inactiveSteps = [long]$bots.inactiveBrainSteps
        }
        workPerSecond = [ordered]@{
            worldScans = [double]$bots.worldScans / $seconds
            npcScans = [double]$bots.npcScans / $seconds
            realPlayerScans = [double]$bots.realPlayerScans / $seconds
            enemyCountScans = [double]$bots.enemyCountScans / $seconds
            searchScans = [double]$bots.searchScans / $seconds
            pathRequests = [double]$bots.pathRequests / $seconds
            decisions = [double]$bots.decisionSteps / $seconds
            castAttempts = [double]$bots.castAttempts / $seconds
        }
        outcomes = [ordered]@{
            observedKills = [long]$bots.observedKills
            creditedKills = [long]$bots.creditedKills
            invalidTargets = [long]$bots.invalidTargets
            stuckNudges = [long]$bots.stuckNudges
            stuckTeleports = [long]$bots.stuckTeleports
            tickErrors = [long]$bots.tickErrors
            runtimeOverlaps = [long]$bots.runtimeOverlaps
        }
        transitionSpawn = $spawn
    })
}

$loads = @($stageAnalyses | ForEach-Object { $_.load })
$allExact = $stageAnalyses.Count -eq 4 -and ($loads -join ',') -eq '0,10,50,100' -and @($stageAnalyses | Where-Object { -not $_.exactBotCount }).Count -eq 0
$baseline = $stageAnalyses[0]
$hundred = $stageAnalyses[3]
$dbSlopePerBot = ([double]$hundred.database.queriesPerSecond - [double]$baseline.database.queriesPerSecond) / 100
$allocationSlopePerBot = ([double]$hundred.allocationAndGc.allocatedBytesPerSecond - [double]$baseline.allocationAndGc.allocatedBytesPerSecond) / 100
$p99Delta = [double]$hundred.serverWorkMs.p99Ms - [double]$baseline.serverWorkMs.p99Ms
$p99Percent = if ([double]$baseline.serverWorkMs.p99Ms -gt 0) { $p99Delta / [double]$baseline.serverWorkMs.p99Ms * 100 } else { $null }
$shutdownErrors = @()
if (-not [string]::IsNullOrWhiteSpace($ShutdownErrorLogPath)) {
    $resolvedErrorLog = (Resolve-Path -LiteralPath $ShutdownErrorLogPath).Path
    $shutdownErrors = @(Get-Content -LiteralPath $resolvedErrorLog | Where-Object { $_ -match '^\d\d:\d\d:\d\d \[ERROR\]' })
}

$analysis = [ordered]@{
    schemaVersion = 't021.scale-analysis.v1'
    createdAtUtc = [DateTime]::UtcNow.ToString('O')
    input = [ordered]@{
        resultPath = $resolvedResult
        resultSha256 = (Get-FileHash -LiteralPath $resolvedResult -Algorithm SHA256).Hash.ToLowerInvariant()
        runId = $run.runId
        measuredSourceCommit = $run.build.sourceCommit
        databaseName = $run.databaseName
    }
    hardware = $run.hardware
    build = $run.build
    scenario = $run.scenario
    gateVerdict = $run.verdict
    measurementCompleteness = if ($allExact -and $null -ne $run.recovery -and $run.recovery.exactBotCount -and $run.shutdownCleanup.status -eq 'PASS') { 'PASS' } else { 'FAIL' }
    highestExactMeasuredPopulation = if ($allExact) { 100 } else { $null }
    highestBudgetQualifiedStablePopulation = $null
    budgetQualification = 'not determined: an approved baseline-plus-desired-server-target policy was not supplied'
    stages = @($stageAnalyses)
    lifecycle = [ordered]@{
        despawn = $run.despawn.bots.despawn
        despawnFailures = [long]$run.despawn.bots.despawnFailures
        recoveryExact = [bool]$run.recovery.exactBotCount
        recoveryDurationSeconds = [double]$run.recovery.durationSeconds
        recoveryServerWorkMs = $run.recovery.metrics.server.work
        recoveryWorkingSetMaxBytes = [long]$run.recovery.process.workingSetMaxBytes
        shutdownCleanup = $run.shutdownCleanup
        nonBotShutdownErrors = $shutdownErrors
    }
    measuredSlopes = [ordered]@{
        databaseQueriesPerSecondPerBot_0To100 = $dbSlopePerBot
        allocatedBytesPerSecondPerBot_0To100 = $allocationSlopePerBot
        serverWorkP99DeltaMs_0To100 = $p99Delta
        serverWorkP99IncreasePercent_0To100 = $p99Percent
        botHostP99MsAt100 = [double]$hundred.botHostMs.p99Ms
    }
    firstMeasuredCostSlope = "Database query rate increased by $([Math]::Round($dbSlopePerBot,3)) queries/s per bot from 0 to 100; allocations increased by $([Math]::Round($allocationSlopePerBot / 1MB,3)) MiB/s per bot."
    firstObservedLatencyConstraint = "At 100 bots whole-server work p99 increased by $([Math]::Round($p99Delta,2)) ms ($([Math]::Round($p99Percent,1))%) over baseline while bot-host p99 was $([Math]::Round([double]$hundred.botHostMs.p99Ms,2)) ms; the tail constraint is not attributable to bot-host timing alone."
    cpuCorrection = [ordered]@{
        reason = 'The measured collector used Math.Max with an integer zero, truncating normalized CPU below one percent. Values here are deterministically recomputed from retained cumulative process CPU ticks and timestamps.'
        collectorFixedAfterRun = $true
    }
}

$jsonPath = Join-Path $OutputDirectory 'analysis.json'
$markdownPath = Join-Path $OutputDirectory 'analysis.md'
$analysis | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# PlayerBots retained analysis $($run.runId)")
$lines.Add('')
$lines.Add("- Gate verdict: **$($run.verdict)** (budget policy absent)")
$lines.Add("- Measurement completeness: **$($analysis.measurementCompleteness)**")
$lines.Add("- Highest exact measured load: **$($analysis.highestExactMeasuredPopulation) bots**")
$lines.Add('- Highest budget-qualified stable load: **not determined**')
$lines.Add("- Commit/executable: ``$($run.build.sourceCommit)`` / ``$($run.build.executableSha256)``")
$lines.Add("- Database: ``$($run.databaseName)``")
$lines.Add("- Hardware: $($run.hardware.cpu); $($run.hardware.logicalProcessors) logical processors; $([Math]::Round($run.hardware.physicalMemoryBytes / 1GB,1)) GiB RAM; $($run.hardware.operatingSystem) $($run.hardware.osVersion)")
$lines.Add("- Scenario: retained 0/10/50/100 ladder; $($run.scenario.warmupSeconds) s warmup and $($run.scenario.steadySeconds) s steady per load; $($run.scenario.recoverySeconds) s recovery")
$lines.Add('')
$lines.Add('| Bots | server work p50/p95/p99/max ms | host p95/p99/max ms | CPU mean/p95/max % | WS max MiB | alloc MiB/s | GC0/1/2 per min | DB queries/s | NPC scans/s | decisions/s |')
$lines.Add('|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach ($stage in $stageAnalyses) {
    $lines.Add(('| {0} | {1:N1}/{2:N1}/{3:N1}/{4:N1} | {5:N1}/{6:N1}/{7:N1} | {8:N3}/{9:N3}/{10:N3} | {11:N1} | {12:N2} | {13:N1}/{14:N1}/{15:N1} | {16:N2} | {17:N2} | {18:N2} |' -f
        $stage.load,$stage.serverWorkMs.p50Ms,$stage.serverWorkMs.p95Ms,$stage.serverWorkMs.p99Ms,$stage.serverWorkMs.maxMs,
        $stage.botHostMs.p95Ms,$stage.botHostMs.p99Ms,$stage.botHostMs.maxMs,
        $stage.cpu.meanPercent,$stage.cpu.p95Percent,$stage.cpu.maxPercent,
        ($stage.memory.workingSetMaxBytes / 1MB),($stage.allocationAndGc.allocatedBytesPerSecond / 1MB),
        $stage.allocationAndGc.gen0PerMinute,$stage.allocationAndGc.gen1PerMinute,$stage.allocationAndGc.gen2PerMinute,
        $stage.database.queriesPerSecond,$stage.workPerSecond.npcScans,$stage.workPerSecond.decisions))
}
$lines.Add('')
$lines.Add('| Bots | activity cfg/gov/observed % | cadence combat/moving/idle/rest/inactive per s | scans NPC/real/enemy/search per s | paths/s | casts/s | kills observed/credited | invalid | stuck nudge/teleport |')
$lines.Add('|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach ($stage in $stageAnalyses) {
    $sourceStage = @($run.stages | Where-Object { [int]$_.load -eq $stage.load })[0]
    $sourceBots = $sourceStage.steady.metrics.bots
    $lines.Add(('| {0} | {1}/{2}/{3:N1} | {4:N2}/{5:N2}/{6:N2}/{7:N2}/{8:N2} | {9:N2}/{10:N2}/{11:N2}/{12:N2} | {13:N2} | {14:N2} | {15}/{16} | {17} | {18}/{19} |' -f
        $stage.load,$stage.activity.configuredPercent,$stage.activity.governorPercent,$stage.activity.observedPercent,
        $stage.cadencePerSecond.combat,$stage.cadencePerSecond.moving,$stage.cadencePerSecond.idle,$stage.cadencePerSecond.resting,$stage.cadencePerSecond.inactive,
        $stage.workPerSecond.npcScans,$stage.workPerSecond.realPlayerScans,$stage.workPerSecond.enemyCountScans,$stage.workPerSecond.searchScans,
        $stage.workPerSecond.pathRequests,$stage.workPerSecond.castAttempts,
        $sourceBots.observedKills,$sourceBots.creditedKills,$sourceBots.invalidTargets,$sourceBots.stuckNudges,$sourceBots.stuckTeleports))
}
$lines.Add('')
$lines.Add('| Transition to bots | spawn count | p50/p95/p99/max ms | failures |')
$lines.Add('|---:|---:|---:|---:|')
foreach ($stage in @($stageAnalyses | Where-Object { $null -ne $_.transitionSpawn })) {
    $sourceStage = @($run.stages | Where-Object { [int]$_.load -eq $stage.load })[0]
    $spawnFailures = $sourceStage.spawn.bots.spawnFailures
    $lines.Add(('| {0} | {1} | {2:N1}/{3:N1}/{4:N1}/{5:N1} | {6} |' -f $stage.load,$stage.transitionSpawn.count,$stage.transitionSpawn.p50Ms,$stage.transitionSpawn.p95Ms,$stage.transitionSpawn.p99Ms,$stage.transitionSpawn.maxMs,$spawnFailures))
}
$lines.Add('')
$lines.Add("First measured cost slope: $($analysis.firstMeasuredCostSlope)")
$lines.Add('')
$lines.Add("First observed latency constraint: $($analysis.firstObservedLatencyConstraint)")
$lines.Add('')
$lines.Add("Lifecycle: despawn count=$($run.despawn.bots.despawn.count), p95=$($run.despawn.bots.despawn.p95Ms) ms, max=$($run.despawn.bots.despawn.maxMs) ms, failures=$($run.despawn.bots.despawnFailures); recovery exact=$($run.recovery.exactBotCount); shutdown cleanup=$($run.shutdownCleanup.status).")
if ($shutdownErrors.Count -gt 0) {
    $lines.Add('')
    $lines.Add("Non-bot shutdown error retained separately: $($shutdownErrors -join '; ')")
}
$lines.Add('')
$lines.Add("CPU correction: $($analysis.cpuCorrection.reason)")
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8
Write-Host "Retained PlayerBots reanalysis at $OutputDirectory"
