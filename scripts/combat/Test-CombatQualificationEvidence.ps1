[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$EvidencePath,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

foreach ($path in @($PlanPath, $EvidencePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Input does not exist: $path" }
}
if (Test-Path -LiteralPath $OutputPath) { throw "Refusing to overwrite retained result: $OutputPath" }

Import-Module (Join-Path $PSScriptRoot 'CombatQualification.psm1') -Force
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json -Depth 100
$evidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json -Depth 100
$basePath = Split-Path -Parent (Resolve-Path -LiteralPath $EvidencePath).Path
$result = Test-T044QualificationEvidence -Plan $plan -Evidence $evidence -EvidenceBasePath $basePath

$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -LiteralPath $parent)) {
    [void](New-Item -ItemType Directory -Path $parent)
}
$result | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Output "T-044 verdict: $($result.verdict)"
foreach ($reason in @($result.incompleteReasons)) { Write-Output "INCOMPLETE: $reason" }
foreach ($failure in @($result.failures)) { Write-Output "FAIL: $failure" }

if ($result.verdict -eq 'PASS') { exit 0 }
if ($result.verdict -eq 'FAIL') { exit 1 }
exit 2
