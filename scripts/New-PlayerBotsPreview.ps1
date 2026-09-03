[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $Ref = 'HEAD'
)

$ErrorActionPreference = 'Stop'
$moduleRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

$status = @(& git -C $moduleRoot status --porcelain=v1)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the PlayerBots Git worktree.'
}
if ($status.Count -ne 0) {
    throw 'Refusing to package a dirty PlayerBots worktree. Commit the intended preview first.'
}

$commit = (& git -C $moduleRoot rev-parse "$Ref^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
    throw "Could not resolve '$Ref' to one Git commit."
}

$moduleManifestPath = Join-Path $moduleRoot 'playerbots.module.json'
$moduleManifest = Get-Content -LiteralPath $moduleManifestPath -Raw | ConvertFrom-Json
$version = [string]$moduleManifest.version
if ($version -notmatch '^[0-9A-Za-z][0-9A-Za-z.+-]*$') {
    throw "Module version '$version' is not safe for an artifact name."
}

foreach ($track in $moduleManifest.hostTracks) {
    $patchPath = Join-Path $moduleRoot ([string]$track.compatibilityPatch).Replace('/', '\')
    if (-not (Test-Path -LiteralPath $patchPath -PathType Leaf)) {
        throw "Missing compatibility patch '$($track.compatibilityPatch)'."
    }
    $actualPatchHash = (Get-FileHash -LiteralPath $patchPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualPatchHash -ne ([string]$track.compatibilityPatchSha256).ToLowerInvariant()) {
        throw "Compatibility patch hash mismatch for '$($track.id)'."
    }
}

$migrationPath = Join-Path $moduleRoot ([string]$moduleManifest.install.databaseMigration).Replace('/', '\')
if (-not (Test-Path -LiteralPath $migrationPath -PathType Leaf)) {
    throw "Missing database migration '$($moduleManifest.install.databaseMigration)'."
}
$actualMigrationHash = (Get-FileHash -LiteralPath $migrationPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualMigrationHash -ne ([string]$moduleManifest.install.databaseMigrationSha256).ToLowerInvariant()) {
    throw 'Database migration hash does not match playerbots.module.json.'
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$shortCommit = $commit.Substring(0, 7)
$artifactBase = "archeage-playerbots-$version-$shortCommit"
$archivePath = Join-Path $outputRoot "$artifactBase.zip"
$previewManifestPath = Join-Path $outputRoot "$artifactBase.manifest.json"
$checksumPath = Join-Path $outputRoot "$artifactBase.sha256"

foreach ($target in @($archivePath, $previewManifestPath, $checksumPath)) {
    if (Test-Path -LiteralPath $target) {
        throw "Refusing to overwrite existing preview artifact '$target'."
    }
}

& git -C $moduleRoot archive --format=zip '--prefix=archeage-playerbots/' "--output=$archivePath" $commit
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw 'Git could not create the preview archive.'
}

$requiredEntries = @(
    'archeage-playerbots/README.md',
    'archeage-playerbots/LICENSE.GPL',
    'archeage-playerbots/playerbots.module.json',
    'archeage-playerbots/scripts/Install-PlayerBots.ps1',
    'archeage-playerbots/scripts/install-playerbots.sh',
    'archeage-playerbots/compatibility/aaemu-1.2-r208022-v4.patch',
    'archeage-playerbots/compatibility/aaemu-3.0.4.2-r336598-alpha-v4.patch',
    'archeage-playerbots/sql/2026-08-25_aaemu_game_bot_archetype_plans.sql',
    'archeage-playerbots/src/AAEmu.Game/Scripts/Commands/BotQuestCommand.cs'
)

$zip = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entryNames = @($zip.Entries | ForEach-Object FullName)
    foreach ($requiredEntry in $requiredEntries) {
        if ($requiredEntry -notin $entryNames) {
            throw "Preview archive is missing required entry '$requiredEntry'."
        }
    }
    if ($entryNames | Where-Object { $_ -match '(^|/)\.git(/|$)' }) {
        throw 'Preview archive unexpectedly contains Git internals.'
    }
    $entryCount = $entryNames.Count
}
finally {
    $zip.Dispose()
}

$archiveInfo = Get-Item -LiteralPath $archivePath
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$previewManifest = [ordered]@{
    schemaVersion = 1
    name = [string]$moduleManifest.name
    version = $version
    commit = $commit
    createdUtc = [DateTime]::UtcNow.ToString('O')
    archive = [ordered]@{
        file = $archiveInfo.Name
        sha256 = $archiveHash
        bytes = $archiveInfo.Length
        entries = $entryCount
        extractAs = 'modules/archeage-playerbots'
    }
    hostTracks = @($moduleManifest.hostTracks | ForEach-Object {
        [ordered]@{
            id = [string]$_.id
            track = [string]$_.track
            status = [string]$_.status
            testedBaseCommit = [string]$_.testedBaseCommit
            compatibilityPatch = [string]$_.compatibilityPatch
            compatibilityPatchSha256 = ([string]$_.compatibilityPatchSha256).ToLowerInvariant()
        }
    })
    databaseMigration = [ordered]@{
        path = [string]$moduleManifest.install.databaseMigration
        sha256 = $actualMigrationHash
    }
}

$json = $previewManifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($previewManifestPath, $json + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($checksumPath, "$archiveHash *$($archiveInfo.Name)" + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Archive = $archivePath
    Manifest = $previewManifestPath
    Checksum = $checksumPath
    Commit = $commit
    Sha256 = $archiveHash
    Bytes = $archiveInfo.Length
    Entries = $entryCount
}
