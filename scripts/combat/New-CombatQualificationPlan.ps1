[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
    throw "Plan input does not exist: $InputPath"
}
if (Test-Path -LiteralPath $OutputPath) {
    throw "Refusing to overwrite retained plan output: $OutputPath"
}

Import-Module (Join-Path $PSScriptRoot 'CombatQualification.psm1') -Force
$inputObject = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json -Depth 100
$plan = New-T044QualificationPlan -InputObject $inputObject
$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -LiteralPath $parent)) {
    [void](New-Item -ItemType Directory -Path $parent)
}
$plan | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Output "Wrote deterministic T-044 plan: $OutputPath"
