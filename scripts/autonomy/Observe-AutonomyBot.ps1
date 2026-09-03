[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateRange(1, [uint32]::MaxValue)][uint32]$BotId,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$ApiBase = 'http://127.0.0.1:1280/api',
    [ValidateRange(10, 60000)][int]$SampleIntervalMilliseconds = 1000,
    [ValidateRange(1, 120)][int]$TimeoutSeconds = 10,
    [ValidateRange(0, [int]::MaxValue)][int]$MaximumSamples = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot 'AutonomyObserver.psd1'
Import-Module -Name $modulePath -Force -ErrorAction Stop

$result = Start-AutonomyObserver `
    -BotId $BotId `
    -ApiBase $ApiBase `
    -OutputPath $OutputPath `
    -SampleIntervalMilliseconds $SampleIntervalMilliseconds `
    -TimeoutSeconds $TimeoutSeconds `
    -MaximumSamples $MaximumSamples

if ($MaximumSamples -gt 0 -and (-not $result.armed -or -not $result.live)) {
    Write-Error 'The bounded observer ended before both offline arm and liveness boundaries were proven.'
    exit 2
}

$result | ConvertTo-Json -Depth 5
