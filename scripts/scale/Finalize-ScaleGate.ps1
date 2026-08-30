[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$ServerLogPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$run = (Get-Content -LiteralPath (Join-Path $RunDirectory 'result.json') -Raw | ConvertFrom-Json)
$finalJsonPath = Join-Path $RunDirectory 'result.final.json'
$finalMarkdownPath = Join-Path $RunDirectory 'final.md'
if ((Test-Path -LiteralPath $finalJsonPath) -or (Test-Path -LiteralPath $finalMarkdownPath)) {
    throw 'Final evidence already exists; refusing to overwrite immutable finalization artifacts.'
}
if ($run.schemaVersion -ne 't021.scale-run.v1' -or $run.provenance -ne 'live-server') {
    throw 'Only a t021.scale-run.v1 live-server result can be finalized.'
}
if ("$($run.databaseName)" -notmatch '^aaemu_(t021|playerbots)_[a-z0-9_]*v[0-9]+$') {
    throw 'Run database is not an isolated versioned PlayerBots schema.'
}
$stillRunning = Get-Process -Id ([int]$run.build.processId) -ErrorAction SilentlyContinue
if ($null -ne $stillRunning -and $stillRunning.StartTime.ToUniversalTime().ToString('O') -eq "$($run.build.processStartTimeUtc)") {
    throw 'The measured game process is still running; finalize only after a confirmed graceful exit.'
}
$resolvedLog = (Resolve-Path -LiteralPath $ServerLogPath).Path
if (-not $resolvedLog.Equals("$($run.serverLog.path)", [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ServerLogPath does not match the log captured by the measurement run.'
}
function Read-LogSegment([string]$Path, [long]$Offset) {
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        if ($stream.Length -lt $Offset) { throw "Log segment is shorter than required offset $Offset`: $Path" }
        [void]$stream.Seek($Offset, [System.IO.SeekOrigin]::Begin)
        $reader = [System.IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

$offset = [long]$run.serverLog.startOffset
$currentLength = [long](Get-Item -LiteralPath $resolvedLog).Length
$logSegments = [System.Collections.Generic.List[object]]::new()
if ($currentLength -ge $offset) {
    $logDelta = Read-LogSegment $resolvedLog $offset
    $logSegments.Add([pscustomobject]@{ path = $resolvedLog; offset = $offset })
}
else {
    $directory = Split-Path -Parent $resolvedLog
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedLog)
    $extension = [System.IO.Path]::GetExtension($resolvedLog)
    $archiveCandidates = @(
        Get-ChildItem -LiteralPath $directory -Filter "$baseName*$extension" -File |
            Where-Object {
                -not $_.FullName.Equals($resolvedLog, [StringComparison]::OrdinalIgnoreCase) -and
                $_.Length -ge $offset
            }
    )
    if ($archiveCandidates.Count -ne 1) {
        throw "Server log rolled over, but exactly one archive containing the retained start offset was not provable (candidates=$($archiveCandidates.Count))."
    }
    $archivePath = $archiveCandidates[0].FullName
    $archiveDelta = Read-LogSegment $archivePath $offset
    $currentDelta = Read-LogSegment $resolvedLog 0
    $logDelta = $archiveDelta + [Environment]::NewLine + $currentDelta
    $logSegments.Add([pscustomobject]@{ path = (Resolve-Path -LiteralPath $archivePath).Path; offset = $offset })
    $logSegments.Add([pscustomobject]@{ path = $resolvedLog; offset = 0 })
}
$cleanupLines = @(($logDelta -split "`r?`n") | Where-Object { $_ -match 'BOT ev=shutdown_cleanup remaining_bots=(\d+) remaining_runtimes=(\d+)' })
$cleanup = $cleanupLines | Select-Object -Last 1
if (-not $cleanup) { throw 'No BOT ev=shutdown_cleanup evidence exists; the server must be stopped gracefully with Ctrl+C.' }
$match = [regex]::Match($cleanup, 'remaining_bots=(\d+) remaining_runtimes=(\d+)')
$remainingBots = [int]$match.Groups[1].Value
$remainingRuntimes = [int]$match.Groups[2].Value
$run.shutdownCleanup.status = if ($remainingBots -eq 0 -and $remainingRuntimes -eq 0) { 'PASS' } else { 'FAIL' }
$run.shutdownCleanup.remainingBots = $remainingBots
$run.shutdownCleanup.remainingRuntimes = $remainingRuntimes
$run.shutdownCleanup.evidenceLine = $cleanup
$run.serverLog | Add-Member -NotePropertyName finalizationSegments -NotePropertyValue @($logSegments) -Force

$reasons = @($run.incompleteReasons | Where-Object { $_ -ne 'graceful shutdown cleanup evidence pending finalization' })
$loads = @($run.stages | ForEach-Object { [int]$_.load })
$allExact = @($run.stages).Count -eq 4 -and ($loads -join ',') -eq '0,10,50,100' -and @($run.stages | Where-Object { -not $_.steady.exactBotCount }).Count -eq 0
$budgetStatuses = @($run.stages | ForEach-Object { $_.budget.status })
if (-not $allExact) { $reasons += '0/10/50/100 ladder was not exact and complete' }
if ($budgetStatuses -contains 'INCOMPLETE') { $reasons += 'approved baseline-derived budget evaluation is incomplete' }
if ($run.recoveryBudget.status -eq 'INCOMPLETE') { $reasons += 'recovery budget evaluation is incomplete' }
if ($null -eq $run.budgetPolicy -or $run.budgetPolicy.provenance -ne 'baseline-plus-desired-server-target' -or [string]::IsNullOrWhiteSpace("$($run.budgetPolicy.approvedBy)")) {
    $reasons += 'approved baseline-plus-target budget provenance is absent'
}
foreach ($stage in @($run.stages)) {
    if ($stage.steady.metrics.schemaVersion -ne 't021.scale-metrics.v1' -or $stage.steady.metrics.provenance -ne 'live-server') {
        $reasons += "load $($stage.load) lacks live in-process metrics provenance"
    }
    if ([long]$stage.steady.metrics.server.work.count -le 0) { $reasons += "load $($stage.load) lacks whole-server tick samples" }
    if ([int]$stage.steady.process.sampleCount -le 1) { $reasons += "load $($stage.load) lacks process samples" }
    if ($stage.steady.database.scope -ne 'mysql-global-status') { $reasons += "load $($stage.load) lacks database status provenance" }
}
if ($null -eq $run.recovery -or -not $run.recovery.exactBotCount) { $reasons += 'exact zero-bot recovery evidence is absent' }

if ($reasons.Count -gt 0) {
    $run.verdict = 'INCOMPLETE'
}
elseif ($budgetStatuses -contains 'FAIL' -or $run.recoveryBudget.status -eq 'FAIL' -or $run.shutdownCleanup.status -eq 'FAIL') {
    $run.verdict = 'FAIL'
}
else {
    $run.verdict = 'PASS'
}
if ($run.recoveryBudget.status -eq 'FAIL') {
    $run.highestStablePopulation = $null
    $run.firstBottleneck = "scale-down recovery: $($run.recoveryBudget.breaches -join '; ')"
}
if ($run.shutdownCleanup.status -eq 'FAIL') {
    $run.highestStablePopulation = $null
    $run.firstBottleneck = "shutdown cleanup retained bots=$remainingBots runtimes=$remainingRuntimes"
}
$run.incompleteReasons = @($reasons | Select-Object -Unique)
$run | Add-Member -NotePropertyName finalizedAtUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('O')) -Force
$run | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $finalJsonPath -Encoding utf8

$summary = @(
    '# PlayerBots finalized verdict',
    '',
    "- Run: ``$($run.runId)``",
    "- Verdict: **$($run.verdict)**",
    "- Shutdown cleanup: $($run.shutdownCleanup.status) (bots=$remainingBots, runtimes=$remainingRuntimes)",
    "- Highest honestly demonstrated stable population: $($run.highestStablePopulation)",
    "- First measured bottleneck: $($run.firstBottleneck)"
)
if ($run.incompleteReasons.Count -gt 0) {
    $summary += @('', '## Incomplete reasons', '')
    $summary += @($run.incompleteReasons | ForEach-Object { "- $_" })
}
$summary | Set-Content -LiteralPath $finalMarkdownPath -Encoding utf8
Write-Host "Verdict: $($run.verdict)"
if ($run.verdict -eq 'PASS') { exit 0 }
if ($run.verdict -eq 'FAIL') { exit 1 }
exit 2
