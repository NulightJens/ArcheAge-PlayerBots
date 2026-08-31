[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $PlanPath -PathType Leaf)) { throw "Plan does not exist: $PlanPath" }
if (Test-Path -LiteralPath $OutputPath) { throw "Refusing to overwrite retained evidence template: $OutputPath" }

$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json -Depth 100
if ($plan.schemaVersion -ne 't044.combat-plan.v1') { throw 'Unsupported T-044 plan schema.' }

function New-StimulusEvidence($Stimuli) {
    return @($Stimuli | ForEach-Object {
        [pscustomobject]@{ command = $_.command; arguments = $_.arguments; ok = $false; messages = @(); errors = @('not-run') }
    })
}

function New-Phase($Definition, $BotIds) {
    return [pscustomobject]@{
        botIds = @($BotIds)
        populationStart = $null
        populationEnd = $null
        timeoutSeconds = $Definition.timeoutSeconds
        stimuli = New-StimulusEvidence $Definition.stimuli
        metricsStart = $null
        metricsEnd = $null
        resourceSamples = @()
        healthChecks = @()
        log = [pscustomobject]@{ path = ''; startOffset = $null; endOffset = $null; segmentSha256 = '' }
        cleanup = [pscustomobject]@{
            stimuli = New-StimulusEvidence $Definition.cleanupStimuli
            removedBotIds = @()
            postPopulation = $null
            postRuntimeCount = $null
            deadTargetObjectIds = @()
        }
    }
}

$cohorts = foreach ($cohort in $plan.cohorts) {
    [pscustomobject]@{
        size = $cohort.size
        control = New-Phase $cohort.control $cohort.botIds
        combat = New-Phase $cohort.combat $cohort.botIds
    }
}

function New-StealthPhase($Definition, [uint]$AttackerBotId) {
    $phase = New-Phase $Definition @($AttackerBotId)
    $phase.PSObject.Properties.Remove('botIds')
    $phase | Add-Member -NotePropertyName searchSamples -NotePropertyValue @()
    return $phase
}

$evidence = [pscustomobject]@{
    schemaVersion = 't044.combat-qualification.v1'
    gateDefinitionVersion = 1
    runId = $plan.runId
    planFingerprintSha256 = $plan.planFingerprintSha256
    cohorts = @($cohorts)
    stealth = [pscustomobject]@{
        reacquire = New-StealthPhase $plan.stealth.reacquire $plan.stealth.attackerBotId
        release = New-StealthPhase $plan.stealth.release $plan.stealth.attackerBotId
    }
    restart = [pscustomobject]@{
        gracefulStopRequested = $false
        priorProcessExited = $false
        startupReady = $false
        priorProcessId = $null
        newProcessId = $null
        priorProcessStartUtc = $null
        newProcessStartUtc = $null
        executableSha256 = ''
        moduleSourceCommit = ''
        postRestartMetrics = $null
        shutdownLog = [pscustomobject]@{ path = ''; startOffset = $null; endOffset = $null; segmentSha256 = '' }
        startupLog = [pscustomobject]@{ path = ''; startOffset = $null; endOffset = $null; segmentSha256 = '' }
    }
}

$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -LiteralPath $parent)) {
    [void](New-Item -ItemType Directory -Path $parent)
}
$evidence | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Output "Wrote fail-closed T-044 evidence template: $OutputPath"
