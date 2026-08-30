[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AAEmuRoot,

    [switch] $CheckOnly,

    [ValidateSet('Auto', 'AAEmu12', 'AAEmu30')]
    [string] $Track = 'Auto',

    [switch] $AllowExperimental
)

$ErrorActionPreference = 'Stop'
$tracks = @{
    'AAEmu12' = @{
        Base = '62e3eb1d87da01194802ac886cd500134facad28'
        Patch = 'compatibility\aaemu-1.2-r208022-v2.patch'
        Status = 'supported'
    }
    'AAEmu30' = @{
        Base = '8c1c943bb2309eefffb9da2aa99a408d0acbb095'
        Patch = 'compatibility\aaemu-3.0.4.2-r336598-alpha-v3.patch'
        Status = 'server-start-validated'
    }
}
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

function Test-Track([string] $name) {
    $base = $tracks[$name].Base
    & git -C $aaemuRoot cat-file -e "$base^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }
    & git -C $aaemuRoot merge-base --is-ancestor $base HEAD
    return $LASTEXITCODE -eq 0
}

if ($Track -eq 'Auto') {
    $matches = @($tracks.Keys | Where-Object { Test-Track $_ })
    if ($matches.Count -ne 1) {
        throw "Could not identify exactly one supported AAEmu lineage. Pass -Track AAEmu12 or -Track AAEmu30 after verifying the checkout."
    }
    $Track = $matches[0]
}
elseif (-not (Test-Track $Track)) {
    throw "The selected checkout is not a descendant of the $Track tested base $($tracks[$Track].Base)."
}

if ($tracks[$Track].Status -ne 'supported' -and -not $AllowExperimental) {
    throw "$Track is $($tracks[$Track].Status) but still awaits complete matching-client runtime acceptance. Re-run with -AllowExperimental only for isolated 3.0 development."
}

$patchPath = Join-Path $moduleRoot $tracks[$Track].Patch
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
    Write-Host "ArcheAge PlayerBots validation passed; track: $Track; state: $state; status: $($tracks[$Track].Status)."
    exit 0
}

if ($canApply) {
    $trackedChanges = @(& git -C $aaemuRoot status --porcelain=v1 --untracked-files=no)
    if ($LASTEXITCODE -ne 0 -or $trackedChanges.Count -ne 0) {
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

Write-Host "ArcheAge PlayerBots is installed for $Track. Rebuild AAEmu and apply the SQL updater before starting the game server."
