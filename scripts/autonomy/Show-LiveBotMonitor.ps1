[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateRange(1, [uint32]::MaxValue)][uint32]$BotId,
    [Parameter(Mandatory)][string]$LogPath,
    [string]$ApiBase = 'http://127.0.0.1:1280/api',
    [string]$ServerLogPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
$Host.UI.RawUI.WindowTitle = "BOT $BotId AI THOUGHT DASHBOARD"

$logDirectory = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDirectory) -and -not (Test-Path -LiteralPath $logDirectory)) {
    $null = New-Item -ItemType Directory -Path $logDirectory
}

$lastThoughtSignature = ''
$apiDown = $false
$body = @{ character = '@system'; arguments = "$BotId" } | ConvertTo-Json -Compress
$rotationBody = @{ character = '@system'; arguments = "$BotId show" } | ConvertTo-Json -Compress
$knownQuestIds = [System.Collections.Generic.HashSet[uint32]]::new()

function Write-DashboardLine {
    param([string]$Text, [ConsoleColor]$Color)

    Write-Host $Text -ForegroundColor $Color
}

function Write-DecisionLog {
    param([string]$Text)

    Add-Content -LiteralPath $LogPath -Value $Text -Encoding utf8
}

function Get-DebugLine {
    param(
        [object[]]$Messages,
        [string]$Label
    )

    $line = $Messages |
        Where-Object { $_ -match ("^\[botdebug\]\s+{0}" -f [regex]::Escape($Label)) } |
        Select-Object -First 1
    if ($null -eq $line) {
        return "$Label unavailable"
    }

    return ([string]$line -replace '^\[botdebug\]\s+', '')
}

function Get-ThoughtSignature {
    param([string[]]$Lines)

    return (($Lines -join ' | ') `
        -replace '\d{4}-\d{2}-\d{2}T[^, ]+', '<time>' `
        -replace 'distance=\d+(\.\d+)?', 'distance=<live>' `
        -replace 'remaining=\d+', 'remaining=<live>' `
        -replace 'progress=\d+/\d+', 'progress=<live>')
}

function Update-KnownQuestIds {
    param([string]$Lifecycle)

    if ($Lifecycle -match 'quest=(\d+)' -and [uint32]$Matches[1] -gt 0) {
        $null = $script:knownQuestIds.Add([uint32]$Matches[1])
    }

    if ([string]::IsNullOrWhiteSpace($ServerLogPath) -or
        -not (Test-Path -LiteralPath $ServerLogPath)) {
        return
    }

    foreach ($line in (Get-Content -LiteralPath $ServerLogPath -Tail 12000 -ErrorAction SilentlyContinue)) {
        $questId = 0
        if ($line -match ("BOT id={0} .*quest=(\d+)" -f $BotId)) {
            $questId = [uint32]$Matches[1]
        }
        elseif ($line -match ("Owner .*\({0}\).*Quest:?\s*(\d+)" -f $BotId)) {
            $questId = [uint32]$Matches[1]
        }
        elseif ($line -match ("Quest:?\s*(\d+).*Player .*\({0}\)" -f $BotId)) {
            $questId = [uint32]$Matches[1]
        }

        if ($questId -gt 0) {
            $null = $script:knownQuestIds.Add($questId)
        }
    }
}

function Get-SelectedQuestId {
    param([string]$Lifecycle)

    if ($Lifecycle -match 'quest=(\d+)' -and [uint32]$Matches[1] -gt 0) {
        return [uint32]$Matches[1]
    }
    return 0
}

function Write-QuestRows {
    param([uint32[]]$QuestIds, [uint32]$SelectedQuestId)

    if ($QuestIds.Count -eq 0) {
        Write-DashboardLine -Text 'No accepted quests observed yet.' -Color DarkGray
        return
    }

    foreach ($questId in $QuestIds) {
        $isSelected = $questId -eq $SelectedQuestId
        $prefix = if ($isSelected) { 'P1 CURRENT' } else { 'OBSERVED  ' }
        $color = if ($isSelected) { [ConsoleColor]::Green } else { [ConsoleColor]::Yellow }
        Write-DashboardLine -Text ("{0} | Q{1}" -f $prefix, $questId) -Color $color
    }
}

Write-DecisionLog -Text ("[{0}] MONITOR START bot={1} mode=thought-dashboard" -f (Get-Date -Format 'o'), $BotId)

while ($true) {
    try {
        $debug = Invoke-RestMethod -Uri "$ApiBase/commands/botdebug" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 2
        $rotation = Invoke-RestMethod -Uri "$ApiBase/commands/botrotation" -Method Post -ContentType 'application/json' -Body $rotationBody -TimeoutSec 2

        $identity = Get-DebugLine -Messages $debug.Messages -Label '=== Bot'
        $questIntake = Get-DebugLine -Messages $debug.Messages -Label 'Quest intake:'
        $questLifecycle = Get-DebugLine -Messages $debug.Messages -Label 'Quest lifecycle:'
        $decisionTarget = Get-DebugLine -Messages $debug.Messages -Label 'Decision target:'
        $target = Get-DebugLine -Messages $debug.Messages -Label 'Target:'
        $combat = Get-DebugLine -Messages $debug.Messages -Label 'State:'
        $resources = Get-DebugLine -Messages $debug.Messages -Label 'HP:'
        $position = Get-DebugLine -Messages $debug.Messages -Label 'Position:'
        $route = Get-DebugLine -Messages $debug.Messages -Label 'Travel route:'
        $navigation = Get-DebugLine -Messages $debug.Messages -Label 'Navigation decision:'
        $runtime = Get-DebugLine -Messages $debug.Messages -Label 'Runtime metrics:'
        $rotationText = (($rotation.Messages | ForEach-Object {
            [string]$_ -replace '^\[botrotation\]\s+', ''
        }) -join ' | ')
        if ([string]::IsNullOrWhiteSpace($rotationText)) {
            $rotationText = 'Rotation decision: unavailable'
        }

        Update-KnownQuestIds -Lifecycle $questLifecycle
        $selectedQuestId = Get-SelectedQuestId -Lifecycle $questLifecycle
        $questIds = @($knownQuestIds | Sort-Object)

        Clear-Host
        Write-DashboardLine -Text ("BOT {0} AI DECISION BOARD    refreshed {1}" -f $BotId, (Get-Date -Format 'HH:mm:ss')) -Color Cyan
        Write-DashboardLine -Text $identity -Color White
        Write-DashboardLine -Text '' -Color White
        Write-DashboardLine -Text 'QUESTS PICKED UP / PRIORITY' -Color Magenta
        Write-QuestRows -QuestIds $questIds -SelectedQuestId $selectedQuestId
        Write-DashboardLine -Text 'Policy: keep current work; otherwise prefer <=500m, distance, ready status, main story, quest ID' -Color DarkCyan
        Write-DashboardLine -Text '' -Color White
        Write-DashboardLine -Text 'CURRENT THOUGHT - QUEST' -Color Magenta
        Write-DashboardLine -Text $questLifecycle -Color Yellow
        Write-DashboardLine -Text $questIntake -Color DarkYellow
        Write-DashboardLine -Text '' -Color White
        Write-DashboardLine -Text 'CURRENT THOUGHT - COMBAT' -Color Magenta
        Write-DashboardLine -Text $decisionTarget -Color Green
        Write-DashboardLine -Text $target -Color Green
        Write-DashboardLine -Text $combat -Color Green
        Write-DashboardLine -Text $rotationText -Color Yellow
        Write-DashboardLine -Text '' -Color White
        Write-DashboardLine -Text 'CURRENT THOUGHT - NAVIGATION' -Color Magenta
        Write-DashboardLine -Text $route -Color Cyan
        Write-DashboardLine -Text $navigation -Color Cyan
        Write-DashboardLine -Text $position -Color DarkCyan
        Write-DashboardLine -Text '' -Color White
        Write-DashboardLine -Text 'HEALTH / EXECUTION' -Color Magenta
        Write-DashboardLine -Text $resources -Color Green
        Write-DashboardLine -Text $runtime -Color DarkGray
        Write-DashboardLine -Text ("Decision log: {0}" -f $LogPath) -Color DarkGray

        $thoughtLines = @(
            $questIntake,
            $questLifecycle,
            $decisionTarget,
            $target,
            $combat,
            $navigation,
            $rotationText
        )
        $thoughtSignature = Get-ThoughtSignature -Lines $thoughtLines
        if ($thoughtSignature -ne $lastThoughtSignature) {
            Write-DecisionLog -Text ("[{0}] THOUGHT bot={1}`n  {2}" -f (
                Get-Date -Format 'o'),
                $BotId,
                ($thoughtLines -join "`n  "))
            $lastThoughtSignature = $thoughtSignature
        }

        if ($apiDown) {
            Write-DecisionLog -Text ("[{0}] GAME API RECONNECTED" -f (Get-Date -Format 'o'))
            $apiDown = $false
        }
    }
    catch {
        Clear-Host
        Write-DashboardLine -Text ("BOT {0} AI THOUGHT DASHBOARD" -f $BotId) -Color Cyan
        Write-DashboardLine -Text 'GAME API OFFLINE - waiting for graceful restart' -Color DarkYellow
        if (-not $apiDown) {
            Write-DecisionLog -Text ("[{0}] GAME API OFFLINE" -f (Get-Date -Format 'o'))
            $apiDown = $true
        }
    }

    Start-Sleep -Milliseconds 1000
}
