[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'CombatQualification.psm1') -Force

function Assert-Equal($Actual, $Expected, [string]$Label) {
    if ($Actual -ne $Expected) { throw "$Label expected '$Expected', observed '$Actual'." }
}

function Copy-Object($Value) {
    return ($Value | ConvertTo-Json -Depth 100 | ConvertFrom-Json -Depth 100)
}

function New-Segment([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    return [pscustomobject]@{
        provenance = 'deterministic-synthetic-fixture'
        startOffset = 0
        endOffset = $bytes.Length
        segmentSha256 = $hash
        inlineFixtureText = $Text
    }
}

function New-Metrics([int]$Population, [long]$Casts = 0, [long]$Kills = 0, [long]$Nudges = 0, [long]$Teleports = 0) {
    return [pscustomobject]@{
        runtimeCount = $Population
        bots = [pscustomobject]@{
            bots = $Population
            castAttempts = $Casts
            creditedKills = $Kills
            stuckNudges = $Nudges
            stuckTeleports = $Teleports
        }
    }
}

function New-Resources {
    return @(
        [pscustomobject]@{ capturedAtUtc = '2026-08-31T18:00:00Z'; cpuPercent = 2.5; workingSetBytes = 100000000; privateMemoryBytes = 80000000 },
        [pscustomobject]@{ capturedAtUtc = '2026-08-31T18:00:01Z'; cpuPercent = 3.5; workingSetBytes = 101000000; privateMemoryBytes = 81000000 }
    )
}

function New-Stimuli($Definitions) {
    return @($Definitions | ForEach-Object {
        $message = if ($_.command -eq 'botbuffnpc' -and $_.arguments -match '\s-(\d+)$') {
            "Removed buff $($Matches[1]) from deterministic NPC fixture."
        }
        elseif ($_.command -eq 'botbuffnpc') {
            'Applied deterministic NPC buff (stealth=True).'
        }
        else { 'deterministic fixture response' }
        [pscustomobject]@{
            command = $_.command
            arguments = $_.arguments
            ok = $true
            messages = @($message)
            errors = @()
        }
    })
}

function New-Health([uint[]]$BotIds) {
    return @($BotIds | ForEach-Object { [pscustomobject]@{ botId = $_; isDead = $false; recoveryCount = 0 } })
}

function New-PlanInput {
    $combatTargets = for ($index = 0; $index -lt 6; $index++) {
        [pscustomobject]@{
            cohortSize = @(1, 5, 10, 25, 50, 100)[$index]
            objectId = 9001 + $index
            templateId = 7001 + $index
            identitySource = 'supplied-runtime-fixture'
        }
    }
    return [pscustomobject]@{
        runId = 't044-fixture-pass-v1'
        provenance = 'deterministic-synthetic-fixture'
        identity = [pscustomobject]@{
            moduleSourceCommit = ('a' * 40)
            aaemuHostBaseCommit = '62e3eb1d87da01194802ac886cd500134facad28'
            executableSha256 = ('b' * 64)
            configurationSha256 = ('c' * 64)
            sourceDirty = $false
            processId = 12001
            processStartUtc = '2026-08-31T17:00:00Z'
        }
        database = [pscustomobject]@{
            name = 'aaemu_playerbots_t044_fixture_v1'
            isolated = $true
            provenanceSha256 = ('d' * 64)
        }
        botIds = @(1001..1100)
        targets = [pscustomobject]@{
            combat = @($combatTargets)
            stealthReacquire = [pscustomobject]@{ objectId = 9101; templateId = 7101; identitySource = 'supplied-runtime-fixture' }
            stealthRelease = [pscustomobject]@{ objectId = 9102; templateId = 7102; identitySource = 'supplied-runtime-fixture' }
        }
        timeouts = [pscustomobject]@{ controlSeconds = 30; combatSeconds = 120; cleanupSeconds = 60 }
        stealth = [pscustomobject]@{
            attackerBotId = 1001
            buffId = 599
            buffTemplateVerified = $true
            buffIsStealth = $true
            maximumSearchRadiusMeters = 30
            searchTimeoutSeconds = 55
        }
    }
}

function New-CohortLog([uint[]]$BotIds, [uint]$TargetObjectId) {
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($id in $BotIds) { $lines.Add("BOT id=$id ev=transition from=Idle to=Combat") }
    $lines.Add("BOT id=$($BotIds[0]) ev=kill_credit killer=$($BotIds[0]) target=$TargetObjectId target_type=7000 old_count=0 new_count=1")
    foreach ($id in $BotIds) { $lines.Add("BOT id=$id ev=transition from=Combat to=Idle") }
    return ($lines -join "`n") + "`n"
}

function New-Cleanup([uint[]]$BotIds, [Nullable[uint]]$DeadTarget, $StimulusDefinitions) {
    return [pscustomobject]@{
        stimuli = New-Stimuli $StimulusDefinitions
        removedBotIds = @($BotIds)
        postPopulation = 0
        postRuntimeCount = 0
        deadTargetObjectIds = if ($null -ne $DeadTarget) { @([uint]$DeadTarget) } else { @() }
    }
}

function New-PassEvidence($Plan) {
    $cohorts = foreach ($cohort in $Plan.cohorts) {
        $ids = @($cohort.botIds | ForEach-Object { [uint]$_ })
        $size = [int]$cohort.size
        $targetId = [uint]$cohort.target.objectId
        [pscustomobject]@{
            size = $size
            control = [pscustomobject]@{
                botIds = $ids
                populationStart = $size
                populationEnd = $size
                timeoutSeconds = [int]$cohort.control.timeoutSeconds
                stimuli = New-Stimuli $cohort.control.stimuli
                metricsStart = New-Metrics $size
                metricsEnd = New-Metrics $size
                resourceSamples = New-Resources
                healthChecks = New-Health $ids
                log = New-Segment "T044 control cohort=$size state=Idle`n"
                cleanup = New-Cleanup $ids $null $cohort.control.cleanupStimuli
            }
            combat = [pscustomobject]@{
                botIds = $ids
                populationStart = $size
                populationEnd = $size
                timeoutSeconds = [int]$cohort.combat.timeoutSeconds
                stimuli = New-Stimuli $cohort.combat.stimuli
                metricsStart = New-Metrics $size
                metricsEnd = New-Metrics $size ($size * 2) 1
                resourceSamples = New-Resources
                healthChecks = New-Health $ids
                log = New-Segment (New-CohortLog $ids $targetId)
                cleanup = New-Cleanup $ids ([Nullable[uint]]$targetId) $cohort.combat.cleanupStimuli
            }
        }
    }

    $attacker = [uint]$Plan.stealth.attackerBotId
    $buff = [uint]$Plan.stealth.buffId
    $reacquireTarget = [uint]$Plan.stealth.reacquireTarget.objectId
    $releaseTarget = [uint]$Plan.stealth.releaseTarget.objectId
    $reacquireLog = @(
        "BOT id=$attacker ev=transition from=Idle to=Combat",
        "BOT id=$attacker ev=transition from=Combat to=Searching",
        "BOT id=$attacker ev=target_lost reason=stealth pos=<0, 0, 0>",
        "BOT id=$attacker ev=transition from=Searching to=Combat",
        "BOT id=$attacker ev=target_found target=$reacquireTarget",
        "BOT id=$attacker ev=kill_credit killer=$attacker target=$reacquireTarget target_type=7101 old_count=0 new_count=1",
        "BOT id=$attacker ev=transition from=Combat to=Idle"
    ) -join "`n"
    $releaseLog = @(
        "BOT id=$attacker ev=transition from=Idle to=Combat",
        "BOT id=$attacker ev=transition from=Combat to=Searching",
        "BOT id=$attacker ev=target_lost reason=stealth pos=<0, 0, 0>",
        "BOT id=$attacker ev=search_give_up",
        "BOT id=$attacker ev=transition from=Searching to=Idle",
        "BOT id=$attacker ev=transition from=Idle to=Combat",
        "BOT id=$attacker ev=kill_credit killer=$attacker target=$releaseTarget target_type=7102 old_count=0 new_count=1",
        "BOT id=$attacker ev=transition from=Combat to=Idle"
    ) -join "`n"

    return [pscustomobject]@{
        schemaVersion = 't044.combat-qualification.v1'
        gateDefinitionVersion = 1
        runId = $Plan.runId
        planFingerprintSha256 = $Plan.planFingerprintSha256
        cohorts = @($cohorts)
        stealth = [pscustomobject]@{
            reacquire = [pscustomobject]@{
                populationStart = 1
                populationEnd = 1
                timeoutSeconds = [double]$Plan.stealth.searchTimeoutSeconds
                stimuli = New-Stimuli $Plan.stealth.reacquire.stimuli
                resourceSamples = New-Resources
                healthChecks = New-Health @($attacker)
                metricsStart = New-Metrics 1
                metricsEnd = New-Metrics 1 3 1
                searchSamples = @([pscustomobject]@{ active = $true; elapsedSeconds = 10; radiusMeters = 16 })
                log = New-Segment ($reacquireLog + "`n")
                cleanup = New-Cleanup @($attacker) ([Nullable[uint]]$reacquireTarget) $Plan.stealth.reacquire.cleanupStimuli
            }
            release = [pscustomobject]@{
                populationStart = 1
                populationEnd = 1
                timeoutSeconds = [double]$Plan.stealth.searchTimeoutSeconds
                stimuli = New-Stimuli $Plan.stealth.release.stimuli
                resourceSamples = New-Resources
                healthChecks = New-Health @($attacker)
                metricsStart = New-Metrics 1
                metricsEnd = New-Metrics 1 3 1
                searchSamples = @(
                    [pscustomobject]@{ active = $true; elapsedSeconds = 10; radiusMeters = 16 },
                    [pscustomobject]@{ active = $true; elapsedSeconds = 50; radiusMeters = 30 }
                )
                log = New-Segment ($releaseLog + "`n")
                cleanup = New-Cleanup @($attacker) ([Nullable[uint]]$releaseTarget) $Plan.stealth.release.cleanupStimuli
            }
        }
        restart = [pscustomobject]@{
            gracefulStopRequested = $true
            priorProcessExited = $true
            startupReady = $true
            priorProcessId = 12001
            newProcessId = 12002
            priorProcessStartUtc = '2026-08-31T17:00:00Z'
            newProcessStartUtc = '2026-08-31T19:00:00Z'
            executableSha256 = $Plan.identity.executableSha256
            moduleSourceCommit = $Plan.identity.moduleSourceCommit
            postRestartMetrics = New-Metrics 0
            shutdownLog = New-Segment "BOT ev=shutdown_cleanup remaining_bots=0 remaining_runtimes=0`n"
            startupLog = New-Segment "AAEmu.Game ready against isolated T-044 database`n"
        }
    }
}

$plan = New-T044QualificationPlan -InputObject (New-PlanInput)
Assert-Equal (($plan.cohorts.size | Sort-Object) -join ',') '1,5,10,25,50,100' 'explicit cohort ladder'
Assert-Equal @($plan.cohorts | Where-Object size -eq 100)[0].botIds.Count 100 '100-bot supplied identity count'

$passEvidence = New-PassEvidence $plan
$pass = Test-T044QualificationEvidence -Plan $plan -Evidence $passEvidence
Assert-Equal $pass.verdict 'PASS' 'complete deterministic fixture'

$idleActivity = Copy-Object $passEvidence
$idleActivity.cohorts[0].control.metricsEnd.bots.castAttempts = 1
Assert-Equal (Test-T044QualificationEvidence $plan $idleActivity).verdict 'FAIL' 'matched Idle control activity'

$mortalFailure = Copy-Object $passEvidence
$mortalFailure.cohorts[1].combat.metricsEnd.bots.creditedKills = 0
Assert-Equal (Test-T044QualificationEvidence $plan $mortalFailure).verdict 'FAIL' 'mortal combat without credited kill'

$lostMissing = Copy-Object $passEvidence
$lostMissing.stealth.reacquire.log = New-Segment ($lostMissing.stealth.reacquire.log.inlineFixtureText -replace 'BOT id=1001 ev=target_lost[^\r\n]*\r?\n', '')
Assert-Equal (Test-T044QualificationEvidence $plan $lostMissing).verdict 'INCOMPLETE' 'missing stealth-loss evidence'

$reacquireFailure = Copy-Object $passEvidence
$reacquireFailure.stealth.reacquire.log = New-Segment ($reacquireFailure.stealth.reacquire.log.inlineFixtureText -replace 'BOT id=1001 ev=target_found[^\r\n]*\r?\n', '')
Assert-Equal (Test-T044QualificationEvidence $plan $reacquireFailure).verdict 'FAIL' 'missing in-radius reacquisition'

$radiusFailure = Copy-Object $passEvidence
$radiusFailure.stealth.release.searchSamples[1].radiusMeters = 30.01
Assert-Equal (Test-T044QualificationEvidence $plan $radiusFailure).verdict 'FAIL' 'radius-bounded release'

$staleOffset = Copy-Object $passEvidence
$staleOffset.cohorts[2].combat.log.segmentSha256 = ('0' * 64)
Assert-Equal (Test-T044QualificationEvidence $plan $staleOffset).verdict 'INCOMPLETE' 'stale log offsets'

$alteredPlan = Copy-Object $plan
$alteredPlan.cohorts[0].target.objectId = 9999
Assert-Equal (Test-T044QualificationEvidence $alteredPlan $passEvidence).verdict 'INCOMPLETE' 'altered plan fingerprint'

$cleanupGap = Copy-Object $passEvidence
$cleanupGap.cohorts[3].combat.cleanup.postRuntimeCount = 1
Assert-Equal (Test-T044QualificationEvidence $plan $cleanupGap).verdict 'INCOMPLETE' 'cleanup gap'

$restartGap = Copy-Object $passEvidence
$restartGap.restart.priorProcessExited = $false
Assert-Equal (Test-T044QualificationEvidence $plan $restartGap).verdict 'INCOMPLETE' 'restart gap'

$partial = [pscustomobject]@{ schemaVersion = 't044.combat-qualification.v1'; gateDefinitionVersion = 1; runId = $plan.runId }
Assert-Equal (Test-T044QualificationEvidence $plan $partial).verdict 'INCOMPLETE' 'malformed partial evidence'

Write-Output 'T-044 deterministic harness fixtures: PASS (11 verdict scenarios, no sleeps)'
