[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AAEmuRoot,

    [switch] $CheckOnly
)

$ErrorActionPreference = 'Stop'
$supportedBase = '62e3eb1d87da01194802ac886cd500134facad28'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$moduleRoot = (Resolve-Path -LiteralPath $moduleRoot).Path
$aaemuRoot = (Resolve-Path -LiteralPath $AAEmuRoot).Path
$expectedModuleRoot = [System.IO.Path]::GetFullPath((Join-Path $aaemuRoot 'modules\archeage-playerbots')).TrimEnd('\')

if ($moduleRoot.TrimEnd('\') -ne $expectedModuleRoot) {
    throw "Clone this repository at '$expectedModuleRoot' before running the installer."
}

foreach ($required in @('AAEmu.Game\AAEmu.Game.csproj', 'AAEmu.UnitTests\AAEmu.UnitTests.csproj', '.git')) {
    if (-not (Test-Path -LiteralPath (Join-Path $aaemuRoot $required))) {
        throw "The selected path is not a complete AAEmu checkout: missing $required"
    }
}

& git -C $aaemuRoot cat-file -e "$supportedBase^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "The supported AAEmu base commit is not present. Fetch AAEmu history and retry."
}

& git -C $aaemuRoot merge-base --is-ancestor $supportedBase HEAD
if ($LASTEXITCODE -ne 0) {
    throw "This module currently supports AAEmu 1.2 descendants of $supportedBase."
}

$patchPath = Join-Path $moduleRoot 'compatibility\aaemu-1.2-r208022.patch'
$sqlSource = Join-Path $moduleRoot 'sql\2026-08-25_aaemu_game_bot_archetype_plans.sql'
$sqlDestination = Join-Path $aaemuRoot 'SQL\updates\2026-08-25_aaemu_game_bot_archetype_plans.sql'

& git -C $aaemuRoot apply --reverse --check $patchPath 2>$null
$alreadyPatched = $LASTEXITCODE -eq 0
& git -C $aaemuRoot apply --check $patchPath 2>$null
$canApply = $LASTEXITCODE -eq 0

if (-not $alreadyPatched -and -not $canApply) {
    throw 'The compatibility patch does not apply cleanly. Use the tested AAEmu base or inspect local host changes.'
}

if (Test-Path -LiteralPath $sqlDestination) {
    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sqlSource).Hash
    $destinationHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sqlDestination).Hash
    if ($sourceHash -ne $destinationHash) {
        throw "A different migration already exists at '$sqlDestination'."
    }
}

if ($CheckOnly) {
    $state = if ($alreadyPatched) { 'installed' } else { 'ready' }
    Write-Host "ArcheAge PlayerBots validation passed; state: $state."
    exit 0
}

if ($canApply) {
    & git -C $aaemuRoot diff --quiet --no-ext-diff
    if ($LASTEXITCODE -ne 0) {
        throw 'AAEmu has tracked local changes. Commit or move those changes before installation.'
    }

    & git -C $aaemuRoot apply $patchPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The compatibility patch failed to apply.'
    }
}

if (-not (Test-Path -LiteralPath $sqlDestination)) {
    Copy-Item -LiteralPath $sqlSource -Destination $sqlDestination
}

Write-Host 'ArcheAge PlayerBots is installed. Rebuild AAEmu and apply the SQL updater before starting the game server.'
