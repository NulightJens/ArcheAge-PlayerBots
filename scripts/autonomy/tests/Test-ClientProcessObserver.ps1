[CmdletBinding()]
param(
    [string]$ArtifactsDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testRoot = $PSScriptRoot
$autonomyRoot = Split-Path -Parent $testRoot
$observerPath = Join-Path $autonomyRoot 'Observe-ClientProcessAbsence.ps1'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$assertionCount = 0
$zeroHash = '0' * 64

if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $runName = 'client-observer-{0}-{1}-{2}' -f `
        [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ'),
        $PID,
        [Guid]::NewGuid().ToString('N')
    $ArtifactsDirectory = Join-Path (Join-Path $autonomyRoot '.test-runs') $runName
}
$ArtifactsDirectory = [System.IO.Path]::GetFullPath($ArtifactsDirectory)
if (Test-Path -LiteralPath $ArtifactsDirectory) {
    throw "ArtifactsDirectory must be a fresh path so prior attempts remain retained: $ArtifactsDirectory"
}
[void][System.IO.Directory]::CreateDirectory($ArtifactsDirectory)

$currentProcess = [System.Diagnostics.Process]::GetCurrentProcess()
$shellExecutable = $currentProcess.MainModule.FileName
$shellProcessName = $currentProcess.ProcessName

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
    $script:assertionCount++
}

function Write-NewTextFile {
    param([string]$Path, [string]$Content)

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $bytes = $utf8NoBom.GetBytes($Content)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
}

function Read-JsonFile {
    param([string]$Path)
    return Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json
}

function Get-RoundtripTimestampText {
    param([object]$Value)

    if ($Value -is [DateTime]) {
        return ([DateTime]$Value).ToString('o')
    }
    return [string]$Value
}

function ConvertTo-RoundtripDateTime {
    param([object]$Value)

    if ($Value -is [DateTime]) {
        return [DateTime]$Value
    }
    return [DateTime]::Parse(
        [string]$Value,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind)
}

function Get-Sha256HexFromBytes {
    param([byte[]]$Bytes)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $algorithm.ComputeHash($Bytes)
    }
    finally {
        $algorithm.Dispose()
    }
    return -join @($hashBytes | ForEach-Object { $_.ToString('x2') })
}

function Get-Sha256HexFromFile {
    param([string]$Path)

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $algorithm.ComputeHash($stream)
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
    return -join @($hashBytes | ForEach-Object { $_.ToString('x2') })
}

function New-CaseDirectory {
    param([string]$Name)

    $path = Join-Path $ArtifactsDirectory $Name
    if (Test-Path -LiteralPath $path) {
        throw "Test case path unexpectedly exists: $path"
    }
    [void][System.IO.Directory]::CreateDirectory($path)
    return $path
}

function Start-ObserverProcess {
    param(
        [string]$CaseDirectory,
        [string]$OutputDirectory,
        [string]$SentinelPath,
        [int]$IntervalMilliseconds,
        [string]$ObservedProcessName
    )

    $stdoutPath = Join-Path $CaseDirectory ('stdout-{0}.txt' -f [Guid]::NewGuid().ToString('N'))
    $stderrPath = Join-Path $CaseDirectory ('stderr-{0}.txt' -f [Guid]::NewGuid().ToString('N'))
    $arguments = @(
        '-NoLogo',
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $observerPath,
        '-OutputPath',
        $OutputDirectory,
        '-StopSentinel',
        $SentinelPath,
        '-SampleIntervalMilliseconds',
        [string]$IntervalMilliseconds,
        '-ProcessName',
        $ObservedProcessName
    )
    $process = Start-Process `
        -FilePath $shellExecutable `
        -ArgumentList $arguments `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    return [pscustomobject]@{
        Process = $process
        OutputDirectory = $OutputDirectory
        SentinelPath = $SentinelPath
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
    }
}

function Start-InjectedQueryFailureProcess {
    param(
        [string]$CaseDirectory,
        [string]$OutputDirectory,
        [string]$SentinelPath,
        [string]$ObservedProcessName
    )

    $wrapperPath = Join-Path $CaseDirectory 'inject-query-failure.ps1'
    $quotedObserver = "'$($observerPath.Replace("'", "''"))'"
    $quotedOutput = "'$($OutputDirectory.Replace("'", "''"))'"
    $quotedSentinel = "'$($SentinelPath.Replace("'", "''"))'"
    $quotedProcessName = "'$($ObservedProcessName.Replace("'", "''"))'"
    $errorPath = Join-Path $OutputDirectory 'error.json'
    $quotedErrorPath = "'$($errorPath.Replace("'", "''"))'"
    $wrapper = @"
Set-StrictMode -Version Latest
`$ErrorActionPreference = 'Stop'
function global:Get-Process { throw 'deterministic-injected-query-failure' }
. $quotedObserver -OutputPath $quotedOutput -StopSentinel $quotedSentinel -SampleIntervalMilliseconds 40 -ProcessName $quotedProcessName
if (Test-Path -LiteralPath $quotedErrorPath -PathType Leaf) { exit 2 }
exit 0
"@
    Write-NewTextFile -Path $wrapperPath -Content $wrapper

    $stdoutPath = Join-Path $CaseDirectory 'stdout-query-failure.txt'
    $stderrPath = Join-Path $CaseDirectory 'stderr-query-failure.txt'
    $arguments = @(
        '-NoLogo',
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $wrapperPath
    )
    $process = Start-Process `
        -FilePath $shellExecutable `
        -ArgumentList $arguments `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    return [pscustomobject]@{
        Process = $process
        OutputDirectory = $OutputDirectory
        SentinelPath = $SentinelPath
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
    }
}

function Get-ProcessFailureText {
    param([object]$Run)

    if (Test-Path -LiteralPath $Run.StderrPath -PathType Leaf) {
        return Get-Content -LiteralPath $Run.StderrPath -Raw
    }
    return '<no stderr retained>'
}

function Wait-ForProcessExit {
    param([object]$Run, [int]$TimeoutSeconds = 15)

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $Run.Process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 10
        $Run.Process.Refresh()
    }
    Assert-True -Condition $Run.Process.HasExited -Message "observer exited within $TimeoutSeconds seconds after cooperative/refusal boundary"
    $Run.Process.WaitForExit()
    $Run.Process.Refresh()
    return [int]$Run.Process.ExitCode
}

function Wait-ForReadyAndAssertOrdering {
    param([object]$Run, [int]$TimeoutSeconds = 15)

    $readyPath = Join-Path $Run.OutputDirectory 'ready.json'
    $firstRawPath = Join-Path (Join-Path $Run.OutputDirectory 'raw') '000000.json'
    $ledgerPath = Join-Path $Run.OutputDirectory 'samples.jsonl'
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $readyPath -PathType Leaf) {
            Assert-True -Condition (Test-Path -LiteralPath $firstRawPath -PathType Leaf) -Message 'ready never appears before the first atomic raw snapshot'
            Assert-True -Condition (Test-Path -LiteralPath $ledgerPath -PathType Leaf) -Message 'ready never appears before the ledger'
            $lines = @(Get-Content -LiteralPath $ledgerPath -ErrorAction Stop | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            Assert-True -Condition ($lines.Count -ge 1) -Message 'ready never appears before the first derived ledger row'
            return
        }
        if ($Run.Process.HasExited) {
            throw "Observer exited before readiness. exit=$($Run.Process.ExitCode) stderr=$(Get-ProcessFailureText -Run $Run)"
        }
        Start-Sleep -Milliseconds 2
        $Run.Process.Refresh()
    }
    throw "Timed out waiting for observer readiness. stderr=$(Get-ProcessFailureText -Run $Run)"
}

function Wait-ForLedgerSamples {
    param([object]$Run, [int]$MinimumSamples, [int]$TimeoutSeconds = 15)

    $ledgerPath = Join-Path $Run.OutputDirectory 'samples.jsonl'
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $ledgerPath -PathType Leaf) {
            $lines = @(Get-Content -LiteralPath $ledgerPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            if ($lines.Count -ge $MinimumSamples) {
                return
            }
        }
        if ($Run.Process.HasExited) {
            throw "Observer exited before $MinimumSamples samples. exit=$($Run.Process.ExitCode) stderr=$(Get-ProcessFailureText -Run $Run)"
        }
        Start-Sleep -Milliseconds 5
        $Run.Process.Refresh()
    }
    throw "Timed out waiting for $MinimumSamples ledger samples."
}

function Request-CooperativeStop {
    param([object]$Run)
    Write-NewTextFile -Path $Run.SentinelPath -Content 'stop'
}

function Invoke-ExpectedRefusal {
    param(
        [string]$Name,
        [string]$OutputDirectory,
        [string]$SentinelPath,
        [int]$IntervalMilliseconds,
        [string]$ObservedProcessName
    )

    $caseDirectory = Split-Path -Parent $SentinelPath
    $run = Start-ObserverProcess `
        -CaseDirectory $caseDirectory `
        -OutputDirectory $OutputDirectory `
        -SentinelPath $SentinelPath `
        -IntervalMilliseconds $IntervalMilliseconds `
        -ObservedProcessName $ObservedProcessName

    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    while (-not $run.Process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 10
        $run.Process.Refresh()
    }
    if (-not $run.Process.HasExited) {
        if (-not (Test-Path -LiteralPath $SentinelPath)) {
            Write-NewTextFile -Path $SentinelPath -Content 'test-fallback-cooperative-stop'
        }
        [void](Wait-ForProcessExit -Run $run -TimeoutSeconds 15)
        throw "Expected refusal '$Name' instead entered the observation loop."
    }

    $run.Process.WaitForExit()
    $run.Process.Refresh()
    Assert-True -Condition ($run.Process.ExitCode -ne 0) -Message "$Name exits nonzero"
    return $run
}

function Resolve-RawPath {
    param([string]$OutputDirectory, [string]$RelativePath)

    $path = $OutputDirectory
    foreach ($segment in $RelativePath.Split('/')) {
        $path = Join-Path $path $segment
    }
    return $path
}

Assert-True -Condition (Test-Path -LiteralPath $observerPath -PathType Leaf) -Message 'observer entry point exists'
Assert-True -Condition ($shellProcessName -match '^(powershell|pwsh)$') -Message 'test host is Windows PowerShell or pwsh'

$tokens = $null
$parseErrors = $null
$observerAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $observerPath,
    [ref]$tokens,
    [ref]$parseErrors)
Assert-True -Condition ($parseErrors.Count -eq 0) -Message 'observer parses without syntax errors'
$forbiddenCommandName = 'Get' + '-FileHash'
$commandAsts = @($observerAst.FindAll({
    param($node)
    return $node -is [System.Management.Automation.Language.CommandAst]
}, $true))
$forbiddenCommands = @($commandAsts | Where-Object { $_.GetCommandName() -eq $forbiddenCommandName })
Assert-True -Condition ($forbiddenCommands.Count -eq 0) -Message 'observer has no unavailable hash-command dependency'
$observerSource = [System.IO.File]::ReadAllText($observerPath)
Assert-True -Condition ($observerSource.Contains('[System.Security.Cryptography.SHA256]::Create()')) -Message 'observer uses framework SHA-256'

$processNameParameter = @($observerAst.ParamBlock.Parameters | Where-Object {
    $_.Name.VariablePath.UserPath -eq 'ProcessName'
})
Assert-True -Condition ($processNameParameter.Count -eq 1) -Message 'observer declares exactly one process-name parameter'
$processNameDefault = $processNameParameter[0].DefaultValue.Extent.Text
foreach ($safeDefault in @('archeage', 'archeageclient', 'game_pak', 'cryengine')) {
    Assert-True -Condition ($processNameDefault -match [regex]::Escape($safeDefault)) -Message "safe client-only default includes $safeDefault"
}
Assert-True -Condition ($processNameDefault -notmatch '(?i)powershell|pwsh|login|mysql') -Message 'safe defaults do not include server, database, or shell processes'

$zeroCase = New-CaseDirectory -Name 'zero-match-and-chain'
$zeroOutput = Join-Path $zeroCase 'output'
$zeroSentinel = Join-Path $zeroCase 'stop.sentinel'
$missingName = 'pbobservermissing{0}' -f [Guid]::NewGuid().ToString('N').Substring(0, 12)
$zeroRun = Start-ObserverProcess `
    -CaseDirectory $zeroCase `
    -OutputDirectory $zeroOutput `
    -SentinelPath $zeroSentinel `
    -IntervalMilliseconds 40 `
    -ObservedProcessName $missingName
Wait-ForReadyAndAssertOrdering -Run $zeroRun
Wait-ForLedgerSamples -Run $zeroRun -MinimumSamples 3
Request-CooperativeStop -Run $zeroRun
$zeroExitCode = Wait-ForProcessExit -Run $zeroRun
Assert-True -Condition ($zeroExitCode -eq 0) -Message 'cooperative sentinel exits zero'
Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $zeroOutput 'error.json'))) -Message 'successful run has no error record'

$zeroLedgerPath = Join-Path $zeroOutput 'samples.jsonl'
$zeroLedgerLines = @(Get-Content -LiteralPath $zeroLedgerPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$zeroRows = @($zeroLedgerLines | ForEach-Object { $_ | ConvertFrom-Json })
Assert-True -Condition ($zeroRows.Count -ge 3) -Message 'observer records multiple samples'
$rawFiles = @(Get-ChildItem -LiteralPath (Join-Path $zeroOutput 'raw') -File -Filter '*.json')
Assert-True -Condition ($rawFiles.Count -eq $zeroRows.Count) -Message 'each derived row has one atomic raw snapshot'

$expectedPreviousHash = $zeroHash
$capturedTimes = @()
for ($index = 0; $index -lt $zeroRows.Count; $index++) {
    $row = $zeroRows[$index]
    Assert-True -Condition ([int]$row.sequence -eq $index) -Message "sample $index sequence is monotonic"
    Assert-True -Condition ([int]$row.processCount -eq 0) -Message "sample $index is a deterministic zero match"
    Assert-True -Condition (@($row.processIdentities).Count -eq 0) -Message "sample $index has no process identities"
    Assert-True -Condition ([string]$row.previousRowHash -eq $expectedPreviousHash) -Message "sample $index previous-row hash chains"

    $rawPath = Resolve-RawPath -OutputDirectory $zeroOutput -RelativePath ([string]$row.rawRelativePath)
    Assert-True -Condition (Test-Path -LiteralPath $rawPath -PathType Leaf) -Message "sample $index raw file exists"
    $rawInfo = Get-Item -LiteralPath $rawPath
    Assert-True -Condition ([int64]$row.rawLength -eq [int64]$rawInfo.Length) -Message "sample $index raw length matches exact bytes"
    $actualRawHash = Get-Sha256HexFromFile -Path $rawPath
    Assert-True -Condition ([string]$row.rawSha256 -eq $actualRawHash) -Message "sample $index raw framework SHA-256 matches"

    $raw = Read-JsonFile -Path $rawPath
    Assert-True -Condition ([int]$raw.sequence -eq $index) -Message "sample $index raw sequence matches ledger"
    Assert-True -Condition ((Get-RoundtripTimestampText -Value $raw.capturedAtUtc) -eq (Get-RoundtripTimestampText -Value $row.capturedAtUtc)) -Message "sample $index raw timestamp matches ledger"
    Assert-True -Condition ([int]$raw.processCount -eq [int]$row.processCount) -Message "sample $index raw count matches ledger"

    $recomputedIdentities = @(
        @($row.processIdentities) | ForEach-Object {
            [ordered]@{
                name = [string]$_.name
                processId = [int64]$_.processId
            }
        }
    )
    $rowMaterial = [ordered]@{
        schemaVersion = [string]$row.schemaVersion
        capturedAtUtc = (Get-RoundtripTimestampText -Value $row.capturedAtUtc)
        sequence = [int]$row.sequence
        processCount = [int]$row.processCount
        processIdentities = @($recomputedIdentities)
        rawRelativePath = [string]$row.rawRelativePath
        rawLength = [int64]$row.rawLength
        rawSha256 = [string]$row.rawSha256
        previousRowHash = [string]$row.previousRowHash
    }
    $rowMaterialBytes = $utf8NoBom.GetBytes(($rowMaterial | ConvertTo-Json -Depth 10 -Compress))
    $recomputedRowHash = Get-Sha256HexFromBytes -Bytes $rowMaterialBytes
    Assert-True -Condition ([string]$row.currentRowHash -eq $recomputedRowHash) -Message "sample $index current-row hash recomputes"
    $expectedPreviousHash = $recomputedRowHash
    $capturedTimes += ConvertTo-RoundtripDateTime -Value $row.capturedAtUtc
}

$ready = Read-JsonFile -Path (Join-Path $zeroOutput 'ready.json')
Assert-True -Condition ([int]$ready.sequence -eq 0) -Message 'ready marker identifies first sequence'
Assert-True -Condition ([string]$ready.rawRelativePath -eq [string]$zeroRows[0].rawRelativePath) -Message 'ready marker identifies first raw snapshot'
Assert-True -Condition ([string]$ready.firstRowHash -eq [string]$zeroRows[0].currentRowHash) -Message 'ready marker identifies flushed first row'
$readyPublishedAt = ConvertTo-RoundtripDateTime -Value $ready.publishedAtUtc
Assert-True -Condition ($readyPublishedAt -ge $capturedTimes[0]) -Message 'ready publication timestamp follows first capture'

$maximumGap = 0.0
for ($index = 1; $index -lt $capturedTimes.Count; $index++) {
    $gap = ($capturedTimes[$index] - $capturedTimes[$index - 1]).TotalMilliseconds
    if ($gap -gt $maximumGap) {
        $maximumGap = $gap
    }
}
$maximumGap = [Math]::Round($maximumGap, 3)
$zeroSummary = Read-JsonFile -Path (Join-Path $zeroOutput 'summary.json')
Assert-True -Condition ([string]$zeroSummary.exit -eq 'cooperative-sentinel') -Message 'summary records cooperative stop'
Assert-True -Condition ([int]$zeroSummary.sampleCount -eq $zeroRows.Count) -Message 'summary sample count matches ledger'
Assert-True -Condition ((Get-RoundtripTimestampText -Value $zeroSummary.firstSampleAtUtc) -eq (Get-RoundtripTimestampText -Value $zeroRows[0].capturedAtUtc)) -Message 'summary first timestamp matches ledger'
Assert-True -Condition ((Get-RoundtripTimestampText -Value $zeroSummary.lastSampleAtUtc) -eq (Get-RoundtripTimestampText -Value $zeroRows[-1].capturedAtUtc)) -Message 'summary last timestamp matches ledger'
Assert-True -Condition ([double]$zeroSummary.maximumAdjacentGapMilliseconds -eq $maximumGap) -Message 'summary maximum adjacent gap recomputes'
Assert-True -Condition ([int]$zeroSummary.nonzeroSampleCount -eq 0) -Message 'zero-match summary has zero nonzero samples'
Assert-True -Condition ([int]$zeroSummary.errorCount -eq 0) -Message 'successful summary has zero errors'
Assert-True -Condition ([string]$zeroSummary.terminalChainHash -eq [string]$zeroRows[-1].currentRowHash) -Message 'summary terminal hash matches ledger chain'

$shellCase = New-CaseDirectory -Name 'current-shell-detection'
$shellOutput = Join-Path $shellCase 'output'
$shellSentinel = Join-Path $shellCase 'stop.sentinel'
$shellRun = Start-ObserverProcess `
    -CaseDirectory $shellCase `
    -OutputDirectory $shellOutput `
    -SentinelPath $shellSentinel `
    -IntervalMilliseconds 40 `
    -ObservedProcessName $shellProcessName
Wait-ForReadyAndAssertOrdering -Run $shellRun
Request-CooperativeStop -Run $shellRun
$shellExitCode = Wait-ForProcessExit -Run $shellRun
Assert-True -Condition ($shellExitCode -eq 0) -Message 'current-shell observer stops cooperatively'
$shellRows = @(Get-Content -LiteralPath (Join-Path $shellOutput 'samples.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
Assert-True -Condition ($shellRows.Count -ge 1) -Message 'current-shell observer retains a sample'
Assert-True -Condition ([int]$shellRows[0].processCount -ge 1) -Message 'current shell produces a nonzero process count'
$observedProcessIds = @($shellRows[0].processIdentities | ForEach-Object { [int64]$_.processId })
Assert-True -Condition ($observedProcessIds -contains [int64]$PID) -Message 'current test shell PID appears in process identities'
$shellSummary = Read-JsonFile -Path (Join-Path $shellOutput 'summary.json')
Assert-True -Condition ([int]$shellSummary.nonzeroSampleCount -ge 1) -Message 'summary counts nonzero shell samples'

$queryFailureCase = New-CaseDirectory -Name 'fail-closed-query-error'
$queryFailureOutput = Join-Path $queryFailureCase 'output'
$queryFailureSentinel = Join-Path $queryFailureCase 'stop.sentinel'
$queryFailureRun = Start-InjectedQueryFailureProcess `
    -CaseDirectory $queryFailureCase `
    -OutputDirectory $queryFailureOutput `
    -SentinelPath $queryFailureSentinel `
    -ObservedProcessName $missingName
[void](Wait-ForProcessExit -Run $queryFailureRun)
$queryFailureErrorPath = Join-Path $queryFailureOutput 'error.json'
Assert-True -Condition (Test-Path -LiteralPath $queryFailureErrorPath -PathType Leaf) -Message 'unexpected process-query failure retains a concise error record'
$queryFailureError = Read-JsonFile -Path $queryFailureErrorPath
Assert-True -Condition ([int]$queryFailureError.sequence -eq 0) -Message 'query failure error record identifies first sequence'
Assert-True -Condition ([string]$queryFailureError.message -match 'deterministic-injected-query-failure') -Message 'query failure error record retains the concise cause'
Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $queryFailureOutput 'ready.json'))) -Message 'query failure does not claim readiness'
Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $queryFailureOutput 'summary.json'))) -Message 'query failure does not claim success'
$queryFailureRawFiles = @(Get-ChildItem -LiteralPath (Join-Path $queryFailureOutput 'raw') -File)
Assert-True -Condition ($queryFailureRawFiles.Count -eq 0) -Message 'query failure retains no false raw sample'

$existingDirectoryCase = New-CaseDirectory -Name 'refuse-existing-directory'
$existingDirectoryOutput = Join-Path $existingDirectoryCase 'output'
[void][System.IO.Directory]::CreateDirectory($existingDirectoryOutput)
$existingMarker = Join-Path $existingDirectoryOutput 'retained.marker'
Write-NewTextFile -Path $existingMarker -Content 'retain-me'
$existingDirectoryRun = Invoke-ExpectedRefusal `
    -Name 'existing output directory' `
    -OutputDirectory $existingDirectoryOutput `
    -SentinelPath (Join-Path $existingDirectoryCase 'stop.sentinel') `
    -IntervalMilliseconds 40 `
    -ObservedProcessName $missingName
Assert-True -Condition ((Get-Content -LiteralPath $existingMarker -Raw) -eq 'retain-me') -Message 'existing output contents are not overwritten'
Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $existingDirectoryOutput 'ready.json'))) -Message 'existing output refusal does not claim readiness'
Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $existingDirectoryOutput 'summary.json'))) -Message 'existing output refusal does not claim success'

$existingFileCase = New-CaseDirectory -Name 'refuse-existing-file'
$existingFileOutput = Join-Path $existingFileCase 'output.file'
Write-NewTextFile -Path $existingFileOutput -Content 'retain-file'
[void](Invoke-ExpectedRefusal `
    -Name 'existing output file' `
    -OutputDirectory $existingFileOutput `
    -SentinelPath (Join-Path $existingFileCase 'stop.sentinel') `
    -IntervalMilliseconds 40 `
    -ObservedProcessName $missingName)
Assert-True -Condition ((Get-Content -LiteralPath $existingFileOutput -Raw) -eq 'retain-file') -Message 'existing output file is not overwritten'

$sentinelCase = New-CaseDirectory -Name 'refuse-existing-sentinel'
$preexistingSentinel = Join-Path $sentinelCase 'stop.sentinel'
Write-NewTextFile -Path $preexistingSentinel -Content 'retain-sentinel'
$sentinelOutput = Join-Path $sentinelCase 'output'
[void](Invoke-ExpectedRefusal `
    -Name 'pre-existing sentinel' `
    -OutputDirectory $sentinelOutput `
    -SentinelPath $preexistingSentinel `
    -IntervalMilliseconds 40 `
    -ObservedProcessName $missingName)
Assert-True -Condition (-not (Test-Path -LiteralPath $sentinelOutput)) -Message 'pre-existing sentinel refusal creates no output'

$intervalCase = New-CaseDirectory -Name 'refuse-invalid-interval'
$intervalOutput = Join-Path $intervalCase 'output'
[void](Invoke-ExpectedRefusal `
    -Name 'invalid interval' `
    -OutputDirectory $intervalOutput `
    -SentinelPath (Join-Path $intervalCase 'stop.sentinel') `
    -IntervalMilliseconds 0 `
    -ObservedProcessName $missingName)
Assert-True -Condition (-not (Test-Path -LiteralPath $intervalOutput)) -Message 'invalid interval creates no output'

$nameCase = New-CaseDirectory -Name 'refuse-invalid-name'
$nameOutput = Join-Path $nameCase 'output'
[void](Invoke-ExpectedRefusal `
    -Name 'invalid wildcard process name' `
    -OutputDirectory $nameOutput `
    -SentinelPath (Join-Path $nameCase 'stop.sentinel') `
    -IntervalMilliseconds 40 `
    -ObservedProcessName '*')
Assert-True -Condition (-not (Test-Path -LiteralPath $nameOutput)) -Message 'invalid process name creates no output'

$missingParentCase = New-CaseDirectory -Name 'refuse-missing-parent'
$missingParentOutput = Join-Path (Join-Path $missingParentCase 'absent-parent') 'output'
[void](Invoke-ExpectedRefusal `
    -Name 'missing output parent' `
    -OutputDirectory $missingParentOutput `
    -SentinelPath (Join-Path $missingParentCase 'stop.sentinel') `
    -IntervalMilliseconds 40 `
    -ObservedProcessName $missingName)
Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $missingParentCase 'absent-parent'))) -Message 'missing parent refusal does not create ambiguous ancestors'

$result = [ordered]@{
    schemaVersion = 'playerbots.client-process-observer-test-result.v1'
    shellExecutable = $shellExecutable
    shellProcessName = $shellProcessName
    assertionCount = $assertionCount
    zeroMatchSampleCount = $zeroRows.Count
    shellSampleCount = $shellRows.Count
    artifactsDirectory = $ArtifactsDirectory
}
Write-Output ($result | ConvertTo-Json -Depth 5 -Compress)
