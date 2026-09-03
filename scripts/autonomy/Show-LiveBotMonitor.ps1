[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateRange(1, [uint32]::MaxValue)][uint32]$BotId,
    [Parameter(Mandatory)][string]$LogPath,
    [string]$ApiBase = 'http://127.0.0.1:1280/api',
    [string]$ServerLogPath = 'D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\AAEmu.Game\bin\Debug\net10.0\Logs\Server.log'
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
$questBoard = @()
$lastQuestRefresh = [datetime]::MinValue

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

    if (-not (Test-Path -LiteralPath $ServerLogPath)) {
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

function Get-QuestBoard {
    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($questId in ($script:knownQuestIds | Sort-Object)) {
        try {
            $statusBody = @{
                character = '@system'
                arguments = "status $BotId $questId"
            } | ConvertTo-Json -Compress
            $status = Invoke-RestMethod -Uri "$ApiBase/commands/botquest" -Method Post `
                -ContentType 'application/json' -Body $statusBody -TimeoutSec 2
            $lines = @($status.Messages | ForEach-Object {
                ([string]$_ -replace '^\[botquest\]\s+', '') `
                    -replace ("^Bot '.*' quest {0}:\s*" -f $questId), ''
            })
            $summary = if ($lines.Count -gt 0) { $lines[0] } else { 'status unavailable' }
            $detail = if ($lines.Count -gt 1) { ($lines[1..($lines.Count - 1)] -join ' | ') } else { '' }
            $rows.Add([pscustomobject]@{
                QuestId = $questId
                Summary = $summary
                Detail = $detail
            })
        }
        catch {
            $rows.Add([pscustomobject]@{
                QuestId = $questId
                Summary = 'status unavailable'
                Detail = ''
            })
        }
    }
    return @($rows)
}

function Get-SelectedQuestId {
    param([string]$Lifecycle)

    if ($Lifecycle -match 'quest=(\d+)' -and [uint32]$Matches[1] -gt 0) {
        return [uint32]$Matches[1]
    }
    return 0
}

function Write-QuestRows {
    param([object[]]$Rows, [uint32]$SelectedQuestId)

    if ($Rows.Count -eq 0) {
        Write-DashboardLine -Text 'No accepted quests observed yet.' -Color DarkGray
        return
    }

    foreach ($row in $Rows) {
        $isSelected = $row.QuestId -eq $SelectedQuestId
        $isCompleted = $row.Summary -match 'lifecycle=completed'
        $prefix = if ($isCompleted) { 'DONE      ' } elseif ($isSelected) { 'P1 CURRENT' } else { 'QUEUED    ' }
        $color = if ($isCompleted) { [ConsoleColor]::DarkGray } `
            elseif ($isSelected) { [ConsoleColor]::Green } `
            else { [ConsoleColor]::Yellow }
        Write-DashboardLine -Text ("{0} | Q{1} | {2}" -f $prefix, $row.QuestId, $row.Summary) -Color $color
        if (-not [string]::IsNullOrWhiteSpace($row.Detail)) {
            Write-DashboardLine -Text ("             {0}" -f $row.Detail) -Color DarkYellow
        }
    }
}

Write-DecisionLog -Text ("[{0}] MONITOR START bot={1} mode=thought-dashboard" -f (Get-Date -Format 'o'), $BotId)

while ($true) {
    try {
        $debug = Invoke-RestMethod -Uri "$ApiBase/commands/botdebug" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 2
        $rotation = Invoke-RestMethod -Uri "$ApiBase/commands/botrotation" -Method Post -ContentType 'application/json' -Body $rotationBody -TimeoutSec 2

        $identity = Get-DebugLine -Messages $debug.Messages -Label '=== Bot'
        $life = Get-DebugLine -Messages $debug.Messages -Label 'Life:'
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
        if (((Get-Date) - $lastQuestRefresh).TotalSeconds -ge 3) {
            $questBoard = @(Get-QuestBoard)
            $lastQuestRefresh = Get-Date
        }
        $selectedQuestId = Get-SelectedQuestId -Lifecycle $questLifecycle

        Clear-Host
        Write-DashboardLine -Text ("BOT {0} AI DECISION BOARD    refreshed {1}" -f $BotId, (Get-Date -Format 'HH:mm:ss')) -Color Cyan
        Write-DashboardLine -Text $identity -Color White
        Write-DashboardLine -Text '' -Color White
        Write-DashboardLine -Text 'QUESTS PICKED UP / PRIORITY' -Color Magenta
        Write-QuestRows -Rows $questBoard -SelectedQuestId $selectedQuestId
        Write-DashboardLine -Text 'Policy: safety > finish sticky current quest > nearby <=500m by distance > regional by distance' -Color DarkCyan
        Write-DashboardLine -Text 'Tie-break: ready-to-turn-in > main story > quest id' -Color DarkCyan
        Write-DashboardLine -Text '' -Color White
        Write-DashboardLine -Text 'CURRENT THOUGHT - QUEST' -Color Magenta
        Write-DashboardLine -Text $questLifecycle -Color Yellow
        Write-DashboardLine -Text $questIntake -Color DarkYellow
        Write-DashboardLine -Text $life -Color DarkYellow
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
            $life,
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
