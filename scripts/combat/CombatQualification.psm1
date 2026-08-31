Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:SchemaVersion = 't044.combat-qualification.v1'
$script:PlanSchemaVersion = 't044.combat-plan.v1'
$script:RequiredCohorts = @(1, 5, 10, 25, 50, 100)

function Get-T044Property {
    param($InputObject, [Parameter(Mandatory)][string]$Name)

    if ($null -eq $InputObject) { return $null }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-T044NestedProperty {
    param($InputObject, [Parameter(Mandatory)][string[]]$Path)

    $value = $InputObject
    foreach ($name in $Path) {
        $value = Get-T044Property $value $name
        if ($null -eq $value) { return $null }
    }
    return $value
}

function Add-T044Reason {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Reasons,
        [Parameter(Mandatory)][string]$Reason
    )

    if (-not $Reasons.Contains($Reason)) { $Reasons.Add($Reason) }
}

function Test-T044Hex {
    param([string]$Value, [int]$Length)
    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value -match "\A[0-9a-fA-F]{$Length}\z"
}

function Get-T044Sha256 {
    param([byte[]]$Bytes)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function ConvertTo-T044Timestamp {
    param($Value)

    if ($null -eq $Value) { return $null }
    try {
        if ($Value -is [DateTimeOffset]) { return [DateTimeOffset]$Value }
        if ($Value -is [DateTime]) { return [DateTimeOffset]::new([DateTime]$Value) }
        return [DateTimeOffset]::Parse(
            [string]$Value,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind)
    }
    catch {
        return $null
    }
}

function Get-T044PlanFingerprint {
    param($Plan)
    $copy = $Plan | ConvertTo-Json -Depth 100 | ConvertFrom-Json -Depth 100
    if ($null -ne $copy.PSObject.Properties['planFingerprintSha256']) {
        $copy.PSObject.Properties.Remove('planFingerprintSha256')
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($copy | ConvertTo-Json -Depth 100 -Compress))
    return Get-T044Sha256 $bytes
}

function Read-T044LogSegment {
    param($Segment, [string]$BasePath)

    try {
        if ($null -eq $Segment) { throw 'log segment is absent' }
        $startOffset = Get-T044Property $Segment 'startOffset'
        $endOffset = Get-T044Property $Segment 'endOffset'
        $expectedHash = "$(Get-T044Property $Segment 'segmentSha256')".ToLowerInvariant()
        if ($null -eq $startOffset -or $null -eq $endOffset -or
            [long]$startOffset -lt 0 -or [long]$endOffset -le [long]$startOffset) {
            throw 'log offsets are missing or invalid'
        }
        if (-not (Test-T044Hex $expectedHash 64)) { throw 'log segment SHA-256 is missing or invalid' }

        $inlineText = Get-T044Property $Segment 'inlineFixtureText'
        if ($null -ne $inlineText) {
            if ((Get-T044Property $Segment 'provenance') -ne 'deterministic-synthetic-fixture') {
                throw 'inline log text is permitted only for deterministic synthetic fixtures'
            }
            $allBytes = [Text.Encoding]::UTF8.GetBytes([string]$inlineText)
        }
        else {
            $path = "$(Get-T044Property $Segment 'path')"
            if ([string]::IsNullOrWhiteSpace($path)) { throw 'physical log path is absent' }
            if (-not [IO.Path]::IsPathRooted($path)) {
                if ([string]::IsNullOrWhiteSpace($BasePath)) { throw 'relative log path has no evidence base path' }
                $path = Join-Path $BasePath $path
            }
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "log file does not exist: $path" }
            $allBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $path).Path)
        }

        if ([long]$endOffset -gt $allBytes.LongLength) {
            throw "log end offset $endOffset exceeds retained length $($allBytes.LongLength)"
        }
        $length = [int]([long]$endOffset - [long]$startOffset)
        $bytes = [byte[]]::new($length)
        [Array]::Copy($allBytes, [long]$startOffset, $bytes, 0, $length)
        $actualHash = Get-T044Sha256 $bytes
        if ($actualHash -ne $expectedHash) {
            throw "stale or mismatched log offsets: expected segment $expectedHash, observed $actualHash"
        }
        return [pscustomobject]@{
            complete = $true
            text = [Text.Encoding]::UTF8.GetString($bytes)
            startOffset = [long]$startOffset
            endOffset = [long]$endOffset
            sha256 = $actualHash
            reason = $null
        }
    }
    catch {
        return [pscustomobject]@{
            complete = $false
            text = ''
            startOffset = $null
            endOffset = $null
            sha256 = $null
            reason = $_.Exception.Message
        }
    }
}

function Get-T044Metric {
    param($Metrics, [Parameter(Mandatory)][string[]]$Path)
    $value = Get-T044NestedProperty $Metrics $Path
    if ($null -eq $value) { return $null }
    try { return [long]$value } catch { return $null }
}

function Get-T044MetricDelta {
    param($Start, $End, [Parameter(Mandatory)][string[]]$Path)
    $startValue = Get-T044Metric $Start $Path
    $endValue = Get-T044Metric $End $Path
    if ($null -eq $startValue -or $null -eq $endValue -or $endValue -lt $startValue) { return $null }
    return $endValue - $startValue
}

function Get-T044CommandKey {
    param($Stimulus)
    $command = "$(Get-T044Property $Stimulus 'command')".Trim().TrimStart('/').ToLowerInvariant()
    $arguments = "$(Get-T044Property $Stimulus 'arguments')".Trim()
    return "$command|$arguments"
}

function Test-T044StimulusSet {
    param(
        $Stimuli,
        [string[]]$RequiredKeys,
        [string]$Label,
        [System.Collections.Generic.List[string]]$Incomplete
    )

    $actual = @($Stimuli | ForEach-Object { Get-T044CommandKey $_ })
    if (($actual -join "`n") -ne (@($RequiredKeys) -join "`n")) {
        Add-T044Reason $Incomplete "$Label command stimuli are missing, extra, or out of declared order."
    }
    foreach ($stimulus in @($Stimuli)) {
        $ok = Get-T044Property $stimulus 'ok'
        $errors = @(Get-T044Property $stimulus 'errors')
        if ($ok -ne $true -or $errors.Count -gt 0) {
            Add-T044Reason $Incomplete "$Label contains an unsuccessful or ambiguous command response for '$(Get-T044CommandKey $stimulus)'."
        }
    }
}

function Test-T044ResourceSamples {
    param($Samples, [string]$Label, [System.Collections.Generic.List[string]]$Incomplete)

    $items = @($Samples)
    if ($items.Count -lt 2) {
        Add-T044Reason $Incomplete "$Label requires at least two resource samples."
        return
    }
    foreach ($sample in $items) {
        $captured = Get-T044Property $sample 'capturedAtUtc'
        $cpu = Get-T044Property $sample 'cpuPercent'
        $working = Get-T044Property $sample 'workingSetBytes'
        $private = Get-T044Property $sample 'privateMemoryBytes'
        $timestamp = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse("$captured", [ref]$timestamp) -or
            $null -eq $cpu -or [double]$cpu -lt 0 -or
            $null -eq $working -or [long]$working -le 0 -or
            $null -eq $private -or [long]$private -le 0) {
            Add-T044Reason $Incomplete "$Label has a malformed resource sample."
            return
        }
    }
}

function Test-T044Cleanup {
    param(
        $Cleanup,
        [uint[]]$ExpectedBotIds,
        [Nullable[uint]]$ExpectedDeadTarget,
        $ExpectedStimuli,
        [string]$Label,
        [System.Collections.Generic.List[string]]$Incomplete
    )

    if ($null -eq $Cleanup) {
        Add-T044Reason $Incomplete "$Label cleanup evidence is absent."
        return
    }
    $removed = @((Get-T044Property $Cleanup 'removedBotIds') | ForEach-Object { [uint]$_ } | Sort-Object -Unique)
    $expected = @($ExpectedBotIds | Sort-Object -Unique)
    if (($removed -join ',') -ne ($expected -join ',')) {
        Add-T044Reason $Incomplete "$Label cleanup does not identify exactly the cohort bots."
    }
    if ((Get-T044Property $Cleanup 'postPopulation') -ne 0 -or
        (Get-T044Property $Cleanup 'postRuntimeCount') -ne 0) {
        Add-T044Reason $Incomplete "$Label cleanup did not prove zero bots and zero runtimes."
    }
    Test-T044StimulusSet (Get-T044Property $Cleanup 'stimuli') `
        @($ExpectedStimuli | ForEach-Object { Get-T044CommandKey $_ }) "$Label cleanup" $Incomplete
    if ($null -ne $ExpectedDeadTarget) {
        $deadTargets = @((Get-T044Property $Cleanup 'deadTargetObjectIds') | ForEach-Object { [uint]$_ })
        if ($deadTargets -notcontains [uint]$ExpectedDeadTarget) {
            Add-T044Reason $Incomplete "$Label cleanup did not prove target object $([uint]$ExpectedDeadTarget) dead."
        }
    }
}

function Test-T044HealthChecks {
    param($Checks, [uint[]]$ExpectedBotIds, [string]$Label, [System.Collections.Generic.List[string]]$Incomplete)

    $items = @($Checks)
    $ids = @($items | ForEach-Object { [uint](Get-T044Property $_ 'botId') } | Sort-Object -Unique)
    $expected = @($ExpectedBotIds | Sort-Object -Unique)
    if (($ids -join ',') -ne ($expected -join ',')) {
        Add-T044Reason $Incomplete "$Label health evidence does not cover exactly the cohort bots."
        return
    }
    if (@($items | Where-Object { (Get-T044Property $_ 'isDead') -ne $false }).Count -gt 0) {
        Add-T044Reason $Incomplete "$Label has absent or ambiguous bot mortality evidence."
    }
}

function Test-T044CohortPhase {
    param(
        $Phase,
        [uint[]]$ExpectedBotIds,
        [uint]$TargetObjectId,
        [ValidateSet('control','combat')][string]$Kind,
        [int]$ExpectedTimeoutSeconds,
        $ExpectedStimuli,
        $ExpectedCleanupStimuli,
        [string]$Label,
        [string]$BasePath,
        [System.Collections.Generic.List[string]]$Incomplete,
        [System.Collections.Generic.List[string]]$Failures
    )

    if ($null -eq $Phase) {
        Add-T044Reason $Incomplete "$Label evidence is absent."
        return [pscustomobject]@{ label = $Label; complete = $false }
    }

    $phaseIds = @((Get-T044Property $Phase 'botIds') | ForEach-Object { [uint]$_ } | Sort-Object)
    $expected = @($ExpectedBotIds | Sort-Object)
    if (($phaseIds -join ',') -ne ($expected -join ',')) {
        Add-T044Reason $Incomplete "$Label bot identities do not match the declared cohort."
    }
    if ((Get-T044Property $Phase 'populationStart') -ne $expected.Count -or
        (Get-T044Property $Phase 'populationEnd') -ne $expected.Count) {
        Add-T044Reason $Incomplete "$Label did not retain exact population $($expected.Count)."
    }
    $timeoutSeconds = Get-T044Property $Phase 'timeoutSeconds'
    if ($null -eq $timeoutSeconds -or [int]$timeoutSeconds -ne $ExpectedTimeoutSeconds) {
        Add-T044Reason $Incomplete "$Label does not use the supplied bounded timeout $ExpectedTimeoutSeconds seconds."
    }

    $metricsStart = Get-T044Property $Phase 'metricsStart'
    $metricsEnd = Get-T044Property $Phase 'metricsEnd'
    foreach ($metrics in @($metricsStart, $metricsEnd)) {
        if ((Get-T044Metric $metrics @('runtimeCount')) -ne $expected.Count -or
            (Get-T044Metric $metrics @('bots','bots')) -ne $expected.Count) {
            Add-T044Reason $Incomplete "$Label metrics do not prove exact population $($expected.Count)."
            break
        }
    }

    Test-T044ResourceSamples (Get-T044Property $Phase 'resourceSamples') $Label $Incomplete
    Test-T044HealthChecks (Get-T044Property $Phase 'healthChecks') $ExpectedBotIds $Label $Incomplete

    Test-T044StimulusSet (Get-T044Property $Phase 'stimuli') `
        @($ExpectedStimuli | ForEach-Object { Get-T044CommandKey $_ }) $Label $Incomplete

    $segment = Read-T044LogSegment (Get-T044Property $Phase 'log') $BasePath
    if (-not $segment.complete) {
        Add-T044Reason $Incomplete "$Label log evidence is incomplete: $($segment.reason)."
    }
    $text = $segment.text
    if ($text -match 'ev=tick_error') { Add-T044Reason $Failures "$Label recorded a bot tick error." }

    $casts = Get-T044MetricDelta $metricsStart $metricsEnd @('bots','castAttempts')
    $kills = Get-T044MetricDelta $metricsStart $metricsEnd @('bots','creditedKills')
    $recoveries = (Get-T044MetricDelta $metricsStart $metricsEnd @('bots','stuckNudges'))
    $teleports = (Get-T044MetricDelta $metricsStart $metricsEnd @('bots','stuckTeleports'))
    if ($null -eq $casts -or $null -eq $kills -or $null -eq $recoveries -or $null -eq $teleports) {
        Add-T044Reason $Incomplete "$Label metrics counters are missing, decreasing, or malformed."
    }
    elseif ($Kind -eq 'control') {
        if ($casts -ne 0 -or $kills -ne 0 -or ($recoveries + $teleports) -ne 0 -or
            $text -match 'ev=(target_lost|target_found|kill_credit)' -or $text -match 'to=(Combat|Searching)') {
            Add-T044Reason $Failures "$Label Idle control recorded combat, kill, search, or recovery activity."
        }
    }
    else {
        if ($casts -le 0) { Add-T044Reason $Failures "$Label recorded no combat cast attempts." }
        if ($kills -le 0 -or $text -notmatch "ev=kill_credit[^\r\n]*target=$TargetObjectId(?:\s|$)") {
            Add-T044Reason $Failures "$Label did not prove a credited mortal kill of target object $TargetObjectId."
        }
        foreach ($id in $ExpectedBotIds) {
            if ($text -notmatch "BOT id=$id ev=transition from=[A-Za-z]+ to=Combat") {
                Add-T044Reason $Incomplete "$Label lacks a server-authoritative Combat transition for bot $id."
            }
            if ($text -notmatch "BOT id=$id ev=transition from=[A-Za-z]+ to=Idle") {
                Add-T044Reason $Incomplete "$Label lacks a server-authoritative Idle release for bot $id."
            }
        }
    }

    Test-T044Cleanup (Get-T044Property $Phase 'cleanup') $ExpectedBotIds `
        $(if ($Kind -eq 'combat') { [Nullable[uint]]$TargetObjectId } else { [Nullable[uint]]$null }) `
        $ExpectedCleanupStimuli $Label $Incomplete

    return [pscustomobject]@{
        label = $Label
        complete = $segment.complete
        casts = $casts
        creditedKills = $kills
        recoveries = if ($null -eq $recoveries -or $null -eq $teleports) { $null } else { $recoveries + $teleports }
        logStartOffset = $segment.startOffset
        logEndOffset = $segment.endOffset
        logSegmentSha256 = $segment.sha256
    }
}

function Test-T044StealthPhase {
    param(
        $Phase,
        [ValidateSet('reacquire','release')][string]$Kind,
        [uint]$AttackerBotId,
        [uint]$TargetObjectId,
        [uint]$BuffId,
        [double]$MaximumRadius,
        [double]$TimeoutSeconds,
        $ExpectedStimuli,
        $ExpectedCleanupStimuli,
        [string]$BasePath,
        [System.Collections.Generic.List[string]]$Incomplete,
        [System.Collections.Generic.List[string]]$Failures
    )

    $label = "stealth-$Kind"
    if ($null -eq $Phase) {
        Add-T044Reason $Incomplete "$label evidence is absent."
        return [pscustomobject]@{ label = $label; complete = $false }
    }
    if ((Get-T044Property $Phase 'populationStart') -ne 1 -or (Get-T044Property $Phase 'populationEnd') -ne 1) {
        Add-T044Reason $Incomplete "$label did not retain the exact one-bot population."
    }
    if ((Get-T044Property $Phase 'timeoutSeconds') -ne $TimeoutSeconds) {
        Add-T044Reason $Incomplete "$label timeout does not match the supplied bounded timeout."
    }

    Test-T044StimulusSet (Get-T044Property $Phase 'stimuli') `
        @($ExpectedStimuli | ForEach-Object { Get-T044CommandKey $_ }) $label $Incomplete
    $apply = @((Get-T044Property $Phase 'stimuli') | Where-Object {
        (Get-T044CommandKey $_) -eq "botbuffnpc|$AttackerBotId $TargetObjectId $BuffId"
    }) | Select-Object -First 1
    $remove = @((Get-T044Property $Phase 'stimuli') | Where-Object {
        (Get-T044CommandKey $_) -eq "botbuffnpc|$AttackerBotId $TargetObjectId -$BuffId"
    }) | Select-Object -First 1
    if ((@((Get-T044Property $apply 'messages')) -join ' ') -notmatch 'stealth=True') {
        Add-T044Reason $Incomplete "$label did not retain server confirmation that buff $BuffId is stealth."
    }
    if ((@((Get-T044Property $remove 'messages')) -join ' ') -notmatch "Removed buff $BuffId") {
        Add-T044Reason $Incomplete "$label did not retain server confirmation that buff $BuffId was removed."
    }
    Test-T044ResourceSamples (Get-T044Property $Phase 'resourceSamples') $label $Incomplete
    Test-T044HealthChecks (Get-T044Property $Phase 'healthChecks') @($AttackerBotId) $label $Incomplete
    $metricsStart = Get-T044Property $Phase 'metricsStart'
    $metricsEnd = Get-T044Property $Phase 'metricsEnd'
    foreach ($metrics in @($metricsStart, $metricsEnd)) {
        if ((Get-T044Metric $metrics @('runtimeCount')) -ne 1 -or
            (Get-T044Metric $metrics @('bots','bots')) -ne 1) {
            Add-T044Reason $Incomplete "$label metrics do not prove the exact one-bot population."
            break
        }
    }
    $casts = Get-T044MetricDelta $metricsStart $metricsEnd @('bots','castAttempts')
    $kills = Get-T044MetricDelta $metricsStart $metricsEnd @('bots','creditedKills')
    $nudges = Get-T044MetricDelta $metricsStart $metricsEnd @('bots','stuckNudges')
    $teleports = Get-T044MetricDelta $metricsStart $metricsEnd @('bots','stuckTeleports')
    if ($null -eq $casts -or $null -eq $kills -or $null -eq $nudges -or $null -eq $teleports) {
        Add-T044Reason $Incomplete "$label metrics counters are missing, decreasing, or malformed."
    }
    else {
        if ($casts -le 0) { Add-T044Reason $Failures "$label recorded no combat cast attempts." }
        if ($kills -le 0) { Add-T044Reason $Failures "$label recorded no credited cleanup kill." }
    }

    $samples = @((Get-T044Property $Phase 'searchSamples'))
    if ($samples.Count -eq 0) { Add-T044Reason $Incomplete "$label has no botdebug search samples." }
    foreach ($sample in $samples) {
        $radius = Get-T044Property $sample 'radiusMeters'
        $elapsed = Get-T044Property $sample 'elapsedSeconds'
        $active = Get-T044Property $sample 'active'
        if ($null -eq $radius -or $null -eq $elapsed -or $active -ne $true) {
            Add-T044Reason $Incomplete "$label contains a malformed botdebug search sample."
            continue
        }
        if ([double]$radius -lt 0 -or [double]$radius -gt $MaximumRadius) {
            Add-T044Reason $Failures "$label exceeded the supplied search radius $MaximumRadius m."
        }
        if ([double]$elapsed -lt 0 -or [double]$elapsed -gt $TimeoutSeconds) {
            Add-T044Reason $Failures "$label exceeded the supplied search timeout $TimeoutSeconds s."
        }
    }

    $segment = Read-T044LogSegment (Get-T044Property $Phase 'log') $BasePath
    if (-not $segment.complete) { Add-T044Reason $Incomplete "$label log evidence is incomplete: $($segment.reason)." }
    $text = $segment.text
    foreach ($pattern in @(
        "BOT id=$AttackerBotId ev=target_lost reason=stealth",
        "BOT id=$AttackerBotId ev=transition from=Combat to=Searching"
    )) {
        if ($text -notmatch [regex]::Escape($pattern)) { Add-T044Reason $Incomplete "$label lacks authoritative event '$pattern'." }
    }
    if ($Kind -eq 'reacquire') {
        if ($text -notmatch "BOT id=$AttackerBotId ev=target_found target=$TargetObjectId" -or
            $text -notmatch "BOT id=$AttackerBotId ev=transition from=Searching to=Combat") {
            Add-T044Reason $Failures "$label did not prove in-radius reacquisition of the supplied target."
        }
    }
    else {
        if ($text -notmatch "BOT id=$AttackerBotId ev=search_give_up" -or
            $text -notmatch "BOT id=$AttackerBotId ev=transition from=Searching to=Idle") {
            Add-T044Reason $Failures "$label did not prove timeout release to Idle."
        }
        if ($text -match "ev=target_found") { Add-T044Reason $Failures "$label reacquired a target during the release cohort." }
    }
    if ($text -notmatch "ev=kill_credit[^\r\n]*target=$TargetObjectId(?:\s|$)") {
        Add-T044Reason $Incomplete "$label cleanup kill of supplied target $TargetObjectId is absent."
    }

    Test-T044Cleanup (Get-T044Property $Phase 'cleanup') @($AttackerBotId) `
        ([Nullable[uint]]$TargetObjectId) $ExpectedCleanupStimuli $label $Incomplete
    return [pscustomobject]@{
        label = $label
        complete = $segment.complete
        casts = $casts
        creditedKills = $kills
        recoveries = if ($null -eq $nudges -or $null -eq $teleports) { $null } else { $nudges + $teleports }
        logStartOffset = $segment.startOffset
        logEndOffset = $segment.endOffset
        logSegmentSha256 = $segment.sha256
    }
}

function Test-T044IdentityAndFixtures {
    param(
        $Plan,
        [System.Collections.Generic.List[string]]$Incomplete
    )

    $identity = Get-T044Property $Plan 'identity'
    foreach ($field in @('moduleSourceCommit','aaemuHostBaseCommit')) {
        if (-not (Test-T044Hex "$(Get-T044Property $identity $field)" 40)) {
            Add-T044Reason $Incomplete "Source identity field '$field' is missing or invalid."
        }
    }
    if ("$(Get-T044Property $identity 'aaemuHostBaseCommit')" -ne '62e3eb1d87da01194802ac886cd500134facad28') {
        Add-T044Reason $Incomplete 'AAEmu host identity is not the pinned 1.2 r208022 commit.'
    }
    foreach ($field in @('executableSha256','configurationSha256')) {
        if (-not (Test-T044Hex "$(Get-T044Property $identity $field)" 64)) {
            Add-T044Reason $Incomplete "Build identity field '$field' is missing or invalid."
        }
    }
    if ((Get-T044Property $identity 'sourceDirty') -ne $false) {
        Add-T044Reason $Incomplete 'Source identity is dirty or ambiguous.'
    }
    $processStart = ConvertTo-T044Timestamp (Get-T044Property $identity 'processStartUtc')
    if ([int](Get-T044Property $identity 'processId') -le 0 -or
        $null -eq $processStart) {
        Add-T044Reason $Incomplete 'Initial process identity is absent or invalid.'
    }

    $database = Get-T044Property $Plan 'database'
    $databaseName = "$(Get-T044Property $database 'name')"
    if ((Get-T044Property $database 'isolated') -ne $true -or
        $databaseName -notmatch '\Aaaemu_playerbots_[a-z0-9_]+_v[1-9][0-9]*\z' -or
        -not (Test-T044Hex "$(Get-T044Property $database 'provenanceSha256')" 64)) {
        Add-T044Reason $Incomplete 'Database identity is absent, non-isolated, ambiguous, or lacks provenance.'
    }

    $stealth = Get-T044Property $Plan 'stealth'
    if ((Get-T044Property $stealth 'buffTemplateVerified') -ne $true -or
        (Get-T044Property $stealth 'buffIsStealth') -ne $true -or
        [uint](Get-T044Property $stealth 'buffId') -eq 0) {
        Add-T044Reason $Incomplete 'Stealth buff identity is absent, unknown, or not verified as a stealth buff.'
    }
}

function Test-T044Restart {
    param(
        $Restart,
        $Identity,
        [string]$BasePath,
        [System.Collections.Generic.List[string]]$Incomplete
    )

    if ($null -eq $Restart) {
        Add-T044Reason $Incomplete 'Cleanup/restart evidence is absent.'
        return [pscustomobject]@{ complete = $false }
    }
    $priorPid = Get-T044Property $Restart 'priorProcessId'
    $newPid = Get-T044Property $Restart 'newProcessId'
    $priorStart = ConvertTo-T044Timestamp (Get-T044Property $Restart 'priorProcessStartUtc')
    $newStart = ConvertTo-T044Timestamp (Get-T044Property $Restart 'newProcessStartUtc')
    $identityStart = ConvertTo-T044Timestamp (Get-T044Property $Identity 'processStartUtc')
    if ((Get-T044Property $Restart 'gracefulStopRequested') -ne $true -or
        (Get-T044Property $Restart 'priorProcessExited') -ne $true -or
        (Get-T044Property $Restart 'startupReady') -ne $true -or
        $null -eq $priorPid -or $null -eq $newPid -or [int]$priorPid -le 0 -or [int]$newPid -le 0 -or
        [int]$priorPid -eq [int]$newPid -or
        $null -eq $priorStart -or $null -eq $newStart -or $newStart -le $priorStart) {
        Add-T044Reason $Incomplete 'Restart did not prove graceful prior exit and a distinct ready process.'
    }
    if ($null -eq $identityStart -or
        [int]$priorPid -ne [int](Get-T044Property $Identity 'processId') -or
        $priorStart -ne $identityStart) {
        Add-T044Reason $Incomplete 'Restart prior-process identity does not match the initially qualified process.'
    }
    if ("$(Get-T044Property $Restart 'executableSha256')" -ne "$(Get-T044Property $Identity 'executableSha256')" -or
        "$(Get-T044Property $Restart 'moduleSourceCommit')" -ne "$(Get-T044Property $Identity 'moduleSourceCommit')") {
        Add-T044Reason $Incomplete 'Restart build/source identity does not match the qualified run.'
    }
    $postMetrics = Get-T044Property $Restart 'postRestartMetrics'
    if ((Get-T044Metric $postMetrics @('runtimeCount')) -ne 0 -or
        (Get-T044Metric $postMetrics @('bots','bots')) -ne 0) {
        Add-T044Reason $Incomplete 'Restart did not prove zero bots and zero runtimes.'
    }

    $shutdown = Read-T044LogSegment (Get-T044Property $Restart 'shutdownLog') $BasePath
    $startup = Read-T044LogSegment (Get-T044Property $Restart 'startupLog') $BasePath
    if (-not $shutdown.complete) { Add-T044Reason $Incomplete "Shutdown log evidence is incomplete: $($shutdown.reason)." }
    elseif ($shutdown.text -notmatch 'BOT ev=shutdown_cleanup remaining_bots=0 remaining_runtimes=0') {
        Add-T044Reason $Incomplete 'Shutdown log does not prove zero retained bots and runtimes.'
    }
    if (-not $startup.complete -or [string]::IsNullOrWhiteSpace($startup.text)) {
        Add-T044Reason $Incomplete "Startup log evidence is incomplete: $($startup.reason)."
    }
    return [pscustomobject]@{
        complete = $shutdown.complete -and $startup.complete
        shutdownLogStartOffset = $shutdown.startOffset
        shutdownLogEndOffset = $shutdown.endOffset
        startupLogStartOffset = $startup.startOffset
        startupLogEndOffset = $startup.endOffset
    }
}

function Test-T044QualificationEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)]$Evidence,
        [string]$EvidenceBasePath = ''
    )

    $incomplete = [System.Collections.Generic.List[string]]::new()
    $failures = [System.Collections.Generic.List[string]]::new()
    $cohortSummaries = [System.Collections.Generic.List[object]]::new()
    $stealthSummaries = [System.Collections.Generic.List[object]]::new()

    try {
        if ((Get-T044Property $Plan 'schemaVersion') -ne $script:PlanSchemaVersion) {
            Add-T044Reason $incomplete "Unsupported plan schema; expected $script:PlanSchemaVersion."
        }
        if ((Get-T044Property $Evidence 'schemaVersion') -ne $script:SchemaVersion) {
            Add-T044Reason $incomplete "Unsupported evidence schema; expected $script:SchemaVersion."
        }
        if ((Get-T044Property $Plan 'gateDefinitionVersion') -ne 1 -or
            (Get-T044Property $Evidence 'gateDefinitionVersion') -ne 1) {
            Add-T044Reason $incomplete 'Gate definition version is missing or unsupported.'
        }
        $planFingerprint = "$(Get-T044Property $Plan 'planFingerprintSha256')"
        if (-not (Test-T044Hex $planFingerprint 64) -or
            $planFingerprint -ne (Get-T044PlanFingerprint $Plan) -or
            $planFingerprint -ne "$(Get-T044Property $Evidence 'planFingerprintSha256')") {
            Add-T044Reason $incomplete 'Plan fingerprint is missing, stale, altered, or not bound to the evidence.'
        }
        if ("$(Get-T044Property $Plan 'runId')" -ne "$(Get-T044Property $Evidence 'runId')" -or
            [string]::IsNullOrWhiteSpace("$(Get-T044Property $Plan 'runId')")) {
            Add-T044Reason $incomplete 'Plan/evidence run identity is missing or mismatched.'
        }
        Test-T044IdentityAndFixtures $Plan $incomplete

        $planCohorts = @(Get-T044Property $Plan 'cohorts')
        $evidenceCohorts = @(Get-T044Property $Evidence 'cohorts')
        $planSizes = @($planCohorts | ForEach-Object { [int](Get-T044Property $_ 'size') } | Sort-Object)
        $evidenceSizes = @($evidenceCohorts | ForEach-Object { [int](Get-T044Property $_ 'size') } | Sort-Object)
        if (($planSizes -join ',') -ne ($script:RequiredCohorts -join ',') -or
            ($evidenceSizes -join ',') -ne ($script:RequiredCohorts -join ',')) {
            Add-T044Reason $incomplete 'Evidence must contain exactly the 1/5/10/25/50/100 cohorts.'
        }

        foreach ($size in $script:RequiredCohorts) {
            $planCohort = @($planCohorts | Where-Object { (Get-T044Property $_ 'size') -eq $size }) | Select-Object -First 1
            $evidenceCohort = @($evidenceCohorts | Where-Object { (Get-T044Property $_ 'size') -eq $size }) | Select-Object -First 1
            if ($null -eq $planCohort -or $null -eq $evidenceCohort) { continue }
            $botIds = @((Get-T044Property $planCohort 'botIds') | ForEach-Object { [uint]$_ })
            $target = Get-T044Property $planCohort 'target'
            $targetId = [uint](Get-T044Property $target 'objectId')
            if ($botIds.Count -ne $size -or @($botIds | Sort-Object -Unique).Count -ne $size -or $targetId -eq 0 -or
                [uint](Get-T044Property $target 'templateId') -eq 0 -or
                (Get-T044Property $target 'identitySource') -ne 'supplied-runtime-fixture') {
                Add-T044Reason $incomplete "Cohort $size has absent or ambiguous bot/target fixture identities."
                continue
            }
            $control = Test-T044CohortPhase (Get-T044Property $evidenceCohort 'control') $botIds $targetId control `
                ([int](Get-T044NestedProperty $planCohort @('control','timeoutSeconds'))) `
                (Get-T044NestedProperty $planCohort @('control','stimuli')) `
                (Get-T044NestedProperty $planCohort @('control','cleanupStimuli')) `
                "cohort-$size-control" $EvidenceBasePath $incomplete $failures
            $combat = Test-T044CohortPhase (Get-T044Property $evidenceCohort 'combat') $botIds $targetId combat `
                ([int](Get-T044NestedProperty $planCohort @('combat','timeoutSeconds'))) `
                (Get-T044NestedProperty $planCohort @('combat','stimuli')) `
                (Get-T044NestedProperty $planCohort @('combat','cleanupStimuli')) `
                "cohort-$size-combat" $EvidenceBasePath $incomplete $failures
            $cohortSummaries.Add([pscustomobject]@{ size = $size; control = $control; combat = $combat })
        }

        $stealthPlan = Get-T044Property $Plan 'stealth'
        $stealthEvidence = Get-T044Property $Evidence 'stealth'
        $attacker = [uint](Get-T044Property $stealthPlan 'attackerBotId')
        $buffId = [uint](Get-T044Property $stealthPlan 'buffId')
        $maximumRadius = [double](Get-T044Property $stealthPlan 'maximumSearchRadiusMeters')
        $timeout = [double](Get-T044Property $stealthPlan 'searchTimeoutSeconds')
        if ($attacker -eq 0 -or $maximumRadius -le 0 -or $maximumRadius -gt 30 -or $timeout -le 50 -or $timeout -gt 60) {
            Add-T044Reason $incomplete 'Stealth attacker, radius, or timeout boundary is absent or invalid.'
        }
        foreach ($kind in @('reacquire','release')) {
            $target = Get-T044Property $stealthPlan ("${kind}Target")
            $targetId = [uint](Get-T044Property $target 'objectId')
            if ($targetId -eq 0 -or [uint](Get-T044Property $target 'templateId') -eq 0 -or
                (Get-T044Property $target 'identitySource') -ne 'supplied-runtime-fixture') {
                Add-T044Reason $incomplete "Stealth-$kind target identity is absent or ambiguous."
                continue
            }
            $summary = Test-T044StealthPhase (Get-T044Property $stealthEvidence $kind) $kind $attacker $targetId `
                $buffId $maximumRadius $timeout (Get-T044NestedProperty $stealthPlan @($kind,'stimuli')) `
                (Get-T044NestedProperty $stealthPlan @($kind,'cleanupStimuli')) $EvidenceBasePath $incomplete $failures
            $stealthSummaries.Add($summary)
        }

        $restartSummary = Test-T044Restart (Get-T044Property $Evidence 'restart') (Get-T044Property $Plan 'identity') `
            $EvidenceBasePath $incomplete
    }
    catch {
        Add-T044Reason $incomplete "Malformed or partial evidence raised: $($_.Exception.Message)"
        $restartSummary = [pscustomobject]@{ complete = $false }
    }

    $verdict = if ($incomplete.Count -gt 0) { 'INCOMPLETE' } elseif ($failures.Count -gt 0) { 'FAIL' } else { 'PASS' }
    return [pscustomobject]@{
        schemaVersion = 't044.combat-result.v1'
        gateDefinitionVersion = 1
        runId = Get-T044Property $Plan 'runId'
        verdict = $verdict
        incompleteReasons = @($incomplete)
        failures = @($failures)
        cohorts = @($cohortSummaries)
        stealth = @($stealthSummaries)
        restart = $restartSummary
        rawLogsRetainedExternally = $true
    }
}

function New-T044QualificationPlan {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$InputObject)

    $reasons = [System.Collections.Generic.List[string]]::new()
    if ([string]::IsNullOrWhiteSpace("$(Get-T044Property $InputObject 'runId')")) {
        Add-T044Reason $reasons 'A non-empty immutable run ID is required.'
    }
    if ((Get-T044Property $InputObject 'provenance') -notin @('physical-aaemu12','deterministic-synthetic-fixture')) {
        Add-T044Reason $reasons 'Provenance must be physical-aaemu12 or deterministic-synthetic-fixture.'
    }
    $botIds = @((Get-T044Property $InputObject 'botIds') | ForEach-Object { [uint]$_ })
    if ($botIds.Count -ne 100 -or @($botIds | Sort-Object -Unique).Count -ne 100 -or @($botIds | Where-Object { $_ -eq 0 }).Count -gt 0) {
        Add-T044Reason $reasons 'Exactly 100 unique, non-zero supplied bot IDs are required.'
    }
    $targets = Get-T044Property $InputObject 'targets'
    $combatTargets = @(Get-T044Property $targets 'combat')
    $combatSizes = @($combatTargets | ForEach-Object { [int](Get-T044Property $_ 'cohortSize') } | Sort-Object)
    if (($combatSizes -join ',') -ne ($script:RequiredCohorts -join ',')) {
        Add-T044Reason $reasons 'Exactly one supplied combat target is required for every 1/5/10/25/50/100 cohort.'
    }
    $allTargets = @($combatTargets + @(Get-T044Property $targets 'stealthReacquire') + @(Get-T044Property $targets 'stealthRelease'))
    foreach ($target in $allTargets) {
        if ($null -eq $target -or [uint](Get-T044Property $target 'objectId') -eq 0 -or
            [uint](Get-T044Property $target 'templateId') -eq 0 -or
            (Get-T044Property $target 'identitySource') -ne 'supplied-runtime-fixture') {
            Add-T044Reason $reasons 'Every target must supply non-zero object/template IDs with supplied-runtime-fixture identity provenance.'
            break
        }
    }
    $targetIds = @($allTargets | ForEach-Object { [uint](Get-T044Property $_ 'objectId') })
    if (@($targetIds | Sort-Object -Unique).Count -ne 8) {
        Add-T044Reason $reasons 'All six combat targets and both stealth targets must have distinct supplied object IDs.'
    }
    $timeouts = Get-T044Property $InputObject 'timeouts'
    foreach ($name in @('controlSeconds','combatSeconds','cleanupSeconds')) {
        if ([int](Get-T044Property $timeouts $name) -le 0) { Add-T044Reason $reasons "Timeout '$name' must be positive." }
    }
    Test-T044IdentityAndFixtures $InputObject $reasons
    $stealthInput = Get-T044Property $InputObject 'stealth'
    $attacker = [uint](Get-T044Property $stealthInput 'attackerBotId')
    if ($botIds -notcontains $attacker) { Add-T044Reason $reasons 'The supplied stealth attacker must be one of the 100 retained bot IDs.' }
    if ([double](Get-T044Property $stealthInput 'maximumSearchRadiusMeters') -le 0 -or
        [double](Get-T044Property $stealthInput 'maximumSearchRadiusMeters') -gt 30 -or
        [double](Get-T044Property $stealthInput 'searchTimeoutSeconds') -le 50 -or
        [double](Get-T044Property $stealthInput 'searchTimeoutSeconds') -gt 60) {
        Add-T044Reason $reasons 'Stealth search radius must be at most 30 m and timeout must be greater than 50 and at most 60 seconds.'
    }
    if ($reasons.Count -gt 0) {
        throw "Qualification plan input is incomplete: $($reasons -join ' ')"
    }

    $cohorts = foreach ($size in $script:RequiredCohorts) {
        $ids = @($botIds[0..($size - 1)])
        $target = @($combatTargets | Where-Object { (Get-T044Property $_ 'cohortSize') -eq $size })[0]
        $spawnStimuli = @($ids | ForEach-Object { [pscustomobject]@{ command = 'addbot'; arguments = "$_" } })
        $idleStimuli = @($ids | ForEach-Object { [pscustomobject]@{ command = 'botstate'; arguments = "$_ idle" } })
        $debugStimuli = @($ids | ForEach-Object { [pscustomobject]@{ command = 'botdebug'; arguments = "$_" } })
        $controlStimuli = @($spawnStimuli) + @($idleStimuli) + @(
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'reset' },
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'snapshot' },
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'snapshot' }
        ) + @($debugStimuli)
        $combatStimuli = @($spawnStimuli) + @($idleStimuli) + @(
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'reset' },
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'snapshot' }
        ) + @($ids | ForEach-Object {
            [pscustomobject]@{ command = 'botattackobject'; arguments = "$_ $(Get-T044Property $target 'objectId')" }
        }) + @([pscustomobject]@{ command = 'botmetrics'; arguments = 'snapshot' }) + @($debugStimuli)
        $cleanupStimuli = @($ids | ForEach-Object { [pscustomobject]@{ command = 'removebot'; arguments = "$_" } })
        [pscustomobject]@{
            size = $size
            botIds = $ids
            target = $target
            control = [pscustomobject]@{
                expectedPopulation = $size
                timeoutSeconds = [int](Get-T044Property $timeouts 'controlSeconds')
                stimuli = $controlStimuli
                cleanupStimuli = $cleanupStimuli
                requiredEvidence = @('metrics-start','metrics-end','resource-samples','health-checks','log-start-offset','log-end-offset','log-segment-sha256','cleanup')
            }
            combat = [pscustomobject]@{
                expectedPopulation = $size
                timeoutSeconds = [int](Get-T044Property $timeouts 'combatSeconds')
                stimuli = $combatStimuli
                cleanupStimuli = $cleanupStimuli
                requiredEvidence = @('combat-transitions','casts','kill-credit','deaths','recovery','metrics-start','metrics-end','resource-samples','log-start-offset','log-end-offset','log-segment-sha256','cleanup')
            }
        }
    }

    $buffId = [uint](Get-T044Property $stealthInput 'buffId')
    $stealthPhases = [ordered]@{}
    foreach ($kind in @('reacquire','release')) {
            $target = if ($kind -eq 'reacquire') { Get-T044Property $targets 'stealthReacquire' } else { Get-T044Property $targets 'stealthRelease' }
            $targetId = [uint](Get-T044Property $target 'objectId')
        $phaseStimuli = @(
            [pscustomobject]@{ command = 'addbot'; arguments = "$attacker" },
            [pscustomobject]@{ command = 'botstate'; arguments = "$attacker idle" },
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'reset' },
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'snapshot' },
            [pscustomobject]@{ command = 'botbuffnpc'; arguments = "$attacker $targetId $buffId" },
            [pscustomobject]@{ command = 'botattackobject'; arguments = "$attacker $targetId" },
            [pscustomobject]@{ command = 'botdebug'; arguments = "$attacker" }
        )
        if ($kind -eq 'release') {
            $phaseStimuli += [pscustomobject]@{ command = 'botdebug'; arguments = "$attacker" }
        }
        $phaseStimuli += @(
            [pscustomobject]@{ command = 'botbuffnpc'; arguments = "$attacker $targetId -$buffId" },
            [pscustomobject]@{ command = 'botdebug'; arguments = "$attacker" }
        )
        if ($kind -eq 'release') {
            $phaseStimuli += [pscustomobject]@{ command = 'botattackobject'; arguments = "$attacker $targetId" }
        }
        $phaseStimuli += @(
            [pscustomobject]@{ command = 'botmetrics'; arguments = 'snapshot' },
            [pscustomobject]@{ command = 'botdebug'; arguments = "$attacker" }
        )
        $stealthPhases[$kind] = [pscustomobject]@{
            expectedPopulation = 1
            timeoutSeconds = [int](Get-T044Property $stealthInput 'searchTimeoutSeconds')
            stimuli = @($phaseStimuli)
            cleanupStimuli = @([pscustomobject]@{ command = 'removebot'; arguments = "$attacker" })
            requiredEvidence = @('target-loss','search-transition','botdebug-search-samples','bounded-radius','reacquisition-or-timeout-release','target-cleanup','bot-cleanup','log-offsets')
        }
    }

    $plan = [pscustomobject]@{
        schemaVersion = $script:PlanSchemaVersion
        gateDefinitionVersion = 1
        runId = "$(Get-T044Property $InputObject 'runId')"
        provenance = "$(Get-T044Property $InputObject 'provenance')"
        identity = Get-T044Property $InputObject 'identity'
        database = Get-T044Property $InputObject 'database'
        cohorts = @($cohorts)
        stealth = [pscustomobject]@{
            attackerBotId = $attacker
            buffId = $buffId
            buffTemplateVerified = Get-T044Property $stealthInput 'buffTemplateVerified'
            buffIsStealth = Get-T044Property $stealthInput 'buffIsStealth'
            maximumSearchRadiusMeters = [double](Get-T044Property $stealthInput 'maximumSearchRadiusMeters')
            searchTimeoutSeconds = [double](Get-T044Property $stealthInput 'searchTimeoutSeconds')
            reacquireTarget = Get-T044Property $targets 'stealthReacquire'
            releaseTarget = Get-T044Property $targets 'stealthRelease'
            reacquire = $stealthPhases.reacquire
            release = $stealthPhases.release
        }
        restartRequiredEvidence = @('graceful-stop-request','prior-process-exit','shutdown-cleanup-log-offsets','distinct-process-start','same-build-source','startup-log-offsets','zero-post-restart-population')
    }
    $plan | Add-Member -NotePropertyName planFingerprintSha256 -NotePropertyValue (Get-T044PlanFingerprint $plan)
    return $plan
}

Export-ModuleMember -Function New-T044QualificationPlan, Test-T044QualificationEvidence
