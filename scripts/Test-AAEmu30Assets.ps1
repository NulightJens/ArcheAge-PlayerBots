[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AAEmuRoot,

    [Parameter(Mandatory = $true)]
    [string] $ClientRoot,

    [Parameter(Mandatory = $true)]
    [string] $ProvenancePath
)

$ErrorActionPreference = 'Stop'
$expectedBase = '8c1c943bb2309eefffb9da2aa99a408d0acbb095'
$expectedTrack = 'ArcheAge 3.0.4.2 r336598'
$knownAAEmu12Hashes = @(
    'ed2921ff434af5279ada601bc95ddc132121eb40edf623ee77b9b1c72fe32025',
    '784d362434a2a0fd0a29fbc1bbb7f771d9ffbe2c8568339ac54d6fdcf44d2ed7'
)

$aaemuRoot = (Resolve-Path -LiteralPath $AAEmuRoot).Path
$clientRoot = (Resolve-Path -LiteralPath $ClientRoot).Path
$provenancePath = (Resolve-Path -LiteralPath $ProvenancePath).Path
$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json

if ($provenance.track -ne $expectedTrack) {
    throw "Asset provenance track must be exactly '$expectedTrack'."
}

if ([string]::IsNullOrWhiteSpace($provenance.sourceUrl)) {
    throw 'Asset provenance must record a non-empty sourceUrl.'
}

& git -C $aaemuRoot cat-file -e "$expectedBase`^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "The selected AAEmu checkout does not contain tested base $expectedBase."
}

& git -C $aaemuRoot merge-base --is-ancestor $expectedBase HEAD
if ($LASTEXITCODE -ne 0) {
    throw "The selected AAEmu checkout is not a descendant of tested base $expectedBase."
}

$assets = @(
    @{
        Name = 'game_pak'
        Path = Join-Path $clientRoot 'game_pak'
        HashProperty = 'gamePakSha256'
        MinimumBytes = 1GB
        SQLite = $false
    },
    @{
        Name = 'compact.sqlite3'
        Path = Join-Path $aaemuRoot 'AAEmu.Game\Data\compact.sqlite3'
        HashProperty = 'compactSha256'
        MinimumBytes = 1MB
        SQLite = $true
    },
    @{
        Name = 'compact.server.table.sqlite3'
        Path = Join-Path $aaemuRoot 'AAEmu.Game\Data\compact.server.table.sqlite3'
        HashProperty = 'serverTableSha256'
        MinimumBytes = 1MB
        SQLite = $true
    }
)

$results = foreach ($asset in $assets) {
    if (-not (Test-Path -LiteralPath $asset.Path -PathType Leaf)) {
        throw "Missing required $($asset.Name) at '$($asset.Path)'."
    }

    $file = Get-Item -LiteralPath $asset.Path
    if ($file.Length -lt $asset.MinimumBytes) {
        throw "$($asset.Name) is unexpectedly small: $($file.Length) bytes."
    }

    if ($asset.SQLite) {
        $stream = [System.IO.File]::OpenRead($file.FullName)
        try {
            $headerBytes = [byte[]]::new(16)
            if ($stream.Read($headerBytes, 0, $headerBytes.Length) -ne $headerBytes.Length) {
                throw "$($asset.Name) does not contain a complete SQLite header."
            }
            $header = [System.Text.Encoding]::ASCII.GetString($headerBytes)
            if ($header -ne "SQLite format 3`0") {
                throw "$($asset.Name) is not a SQLite 3 database."
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    if ($knownAAEmu12Hashes -contains $actualHash) {
        throw "$($asset.Name) matches a known ArcheAge 1.2 asset and cannot be accepted for 3.0."
    }

    $expectedHash = [string]$provenance.($asset.HashProperty)
    if ($expectedHash -notmatch '^[0-9a-fA-F]{64}$') {
        throw "Asset provenance property '$($asset.HashProperty)' must contain a complete SHA-256 hash."
    }

    if ($actualHash -ne $expectedHash.ToLowerInvariant()) {
        throw "$($asset.Name) SHA-256 does not match the recorded provenance."
    }

    [pscustomobject]@{
        name = $asset.Name
        path = $file.FullName
        bytes = $file.Length
        sha256 = $actualHash
    }
}

[pscustomobject]@{
    status = 'passed'
    track = $expectedTrack
    aaemuBase = $expectedBase
    aaemuRoot = $aaemuRoot
    clientRoot = $clientRoot
    provenancePath = $provenancePath
    assets = @($results)
} | ConvertTo-Json -Depth 4
