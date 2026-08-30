[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDonorConfigPath,
    [Parameter(Mandatory)][string]$LoginDonorConfigPath,
    [Parameter(Mandatory)][string]$GameDatabaseName,
    [Parameter(Mandatory)][string]$LoginDatabaseName,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [uint]$TemplateCharacterId = 2,
    [uint]$FirstBotId = 20001,
    [uint]$BotAccountId = 1000000,
    [ValidateRange(100, 1000)][int]$BotCount = 100,
    [uint]$FirstContainerId = 100000,
    [uint]$FirstItemId = 16800000,
    [ValidatePattern('^[a-z0-9._-]+$')][string]$SeedVersion = 't021-seed-v1',
    [string]$MySqlExe = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe',
    [string]$MySqlDumpExe = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-DonorConfig([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Donor config does not exist: $Path" }
    $config = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $provider = $config.Connections.MySQLProvider
    if ([string]::IsNullOrWhiteSpace("$($provider.Password)")) { throw "Donor config has no database password: $Path" }
    return $provider
}

function Invoke-CapturedProcess([string]$FilePath, [string[]]$Arguments, [AllowNull()][string]$StandardInput = $null) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.RedirectStandardInput = $null -ne $StandardInput
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    try {
        $process.StartInfo = $startInfo
        if (-not $process.Start()) { throw "Could not start native process: $FilePath" }
        if ($null -ne $StandardInput) {
            $process.StandardInput.Write($StandardInput)
            $process.StandardInput.Close()
        }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            StdOut = $stdoutTask.GetAwaiter().GetResult()
            StdErr = $stderrTask.GetAwaiter().GetResult()
        }
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-MySql($Provider, [string]$Database, [string]$Query, [switch]$ColumnNames) {
    $priorMysqlPassword = [Environment]::GetEnvironmentVariable('MYSQL_PWD')
    try {
        [Environment]::SetEnvironmentVariable('MYSQL_PWD', "$($Provider.Password)")
        $arguments = @('--batch', '--raw', "--host=$($Provider.Host)", "--port=$($Provider.Port)", "--user=$($Provider.User)")
        if (-not $ColumnNames) { $arguments += '--skip-column-names' }
        if (-not [string]::IsNullOrWhiteSpace($Database)) { $arguments += "--database=$Database" }
        $result = Invoke-CapturedProcess $MySqlExe $arguments $Query
        $rows = @($result.StdOut -split '\r?\n' | Where-Object { $_ -ne '' })
        if ($result.ExitCode -ne 0) {
            throw "mysql failed with exit code $($result.ExitCode): $($result.StdErr.Trim()) $($result.StdOut.Trim())"
        }
        return $rows
    }
    finally {
        [Environment]::SetEnvironmentVariable('MYSQL_PWD', $priorMysqlPassword)
    }
}

function Export-Database($Provider, [string]$Database, [string]$DumpPath) {
    $priorMysqlPassword = [Environment]::GetEnvironmentVariable('MYSQL_PWD')
    try {
        [Environment]::SetEnvironmentVariable('MYSQL_PWD', "$($Provider.Password)")
        $arguments = @(
            '--single-transaction', '--triggers', '--skip-add-drop-table', '--skip-add-drop-trigger',
            '--hex-blob', '--set-gtid-purged=OFF', '--no-tablespaces',
            "--host=$($Provider.Host)", "--port=$($Provider.Port)", "--user=$($Provider.User)",
            "--result-file=$DumpPath", $Database
        )
        $result = Invoke-CapturedProcess $MySqlDumpExe $arguments
        if ($result.ExitCode -ne 0) {
            throw "mysqldump failed for $Database with exit code $($result.ExitCode): $($result.StdErr.Trim()) $($result.StdOut.Trim())"
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('MYSQL_PWD', $priorMysqlPassword)
    }
}

function Import-Database($Provider, [string]$Database, [string]$DumpPath) {
    $portablePath = $DumpPath.Replace('\', '/')
    [void](Invoke-MySql $Provider $Database "source $portablePath")
}

function Assert-NoDestructiveSql([string]$DumpPath) {
    $blocked = Select-String -LiteralPath $DumpPath -Pattern '^\s*(DROP|TRUNCATE|DELETE\s+FROM)\b' -CaseSensitive:$false | Select-Object -First 1
    if ($blocked) { throw "Dump contains a prohibited destructive statement at line $($blocked.LineNumber): $($blocked.Line.Trim())" }
}

function Get-Columns($Provider, [string]$Database, [string]$Table) {
    return @(Invoke-MySql $Provider $Database "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='$Table' ORDER BY ORDINAL_POSITION")
}

function Copy-OwnerTable($Provider, [string]$Database, [string]$Table, [string]$OwnerColumn) {
    $columns = Get-Columns $Provider $Database $Table
    $columnList = ($columns | ForEach-Object { "``$_``" }) -join ','
    $selectList = ($columns | ForEach-Object { if ($_ -eq $OwnerColumn) { 'm.bot_id' } else { "s.``$_``" } }) -join ','
    $sql = "INSERT INTO ``$Table`` ($columnList) SELECT $selectList FROM ``$Table`` s CROSS JOIN t021_seed_manifest m WHERE s.``$OwnerColumn``=$TemplateCharacterId"
    [void](Invoke-MySql $Provider $Database $sql)
}

if ($GameDatabaseName -notmatch '^aaemu_(t021|playerbots)_game_[a-z0-9_]*v[0-9]+$') {
    throw 'GameDatabaseName must be a new versioned aaemu_playerbots_game_*vN schema.'
}
if ($LoginDatabaseName -notmatch '^aaemu_(t021|playerbots)_login_[a-z0-9_]*v[0-9]+$') {
    throw 'LoginDatabaseName must be a new versioned aaemu_playerbots_login_*vN schema.'
}
if (-not (Test-Path -LiteralPath $MySqlExe) -or -not (Test-Path -LiteralPath $MySqlDumpExe)) {
    throw 'mysql.exe and mysqldump.exe must both exist.'
}
if (Test-Path -LiteralPath $OutputDirectory) {
    if ((Get-ChildItem -LiteralPath $OutputDirectory -Force | Measure-Object).Count -ne 0) {
        throw "OutputDirectory must be new or empty; refusing to overwrite retained evidence: $OutputDirectory"
    }
}
else {
    [void](New-Item -ItemType Directory -Path $OutputDirectory -Force:$false)
}

$gameProvider = Read-DonorConfig $GameDonorConfigPath
$loginProvider = Read-DonorConfig $LoginDonorConfigPath
$gameExists = [int](Invoke-MySql $gameProvider '' "SELECT COUNT(*) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME='$GameDatabaseName'" | Select-Object -First 1)
$loginExists = [int](Invoke-MySql $loginProvider '' "SELECT COUNT(*) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME='$LoginDatabaseName'" | Select-Object -First 1)
if ($gameExists -ne 0 -or $loginExists -ne 0) {
    throw "Destination schema already exists (game=$gameExists, login=$loginExists); create a higher version instead."
}

$templateCount = [int](Invoke-MySql $gameProvider "$($gameProvider.Database)" "SELECT COUNT(*) FROM characters WHERE id=$TemplateCharacterId AND deleted=0" | Select-Object -First 1)
if ($templateCount -ne 1) { throw "Template character $TemplateCharacterId does not exist exactly once in the donor game schema." }
$templateAccountId = [uint](Invoke-MySql $gameProvider "$($gameProvider.Database)" "SELECT account_id FROM characters WHERE id=$TemplateCharacterId AND deleted=0" | Select-Object -First 1)
if ($BotAccountId -eq $templateAccountId) { throw "Bot account $BotAccountId must not match template player account $templateAccountId." }
$templateContainerCount = [int](Invoke-MySql $gameProvider "$($gameProvider.Database)" "SELECT COUNT(*) FROM item_containers WHERE owner_id=$TemplateCharacterId" | Select-Object -First 1)
$templateItemCount = [int](Invoke-MySql $gameProvider "$($gameProvider.Database)" "SELECT COUNT(*) FROM items WHERE container_id IN (SELECT container_id FROM item_containers WHERE owner_id=$TemplateCharacterId)" | Select-Object -First 1)
if ($templateContainerCount -gt 20) { throw "Template has $templateContainerCount containers; the reserved per-bot stride is 20." }
if ($templateItemCount -gt 100) { throw "Template has $templateItemCount items; the reserved per-bot stride is 100." }
$containerStride = [Math]::Max(1, $templateContainerCount)
$itemStride = [Math]::Max(1, $templateItemCount)
$lastContainerId = [uint64]$FirstContainerId + [uint64]($BotCount - 1) * [uint64]$containerStride + [uint64]$templateContainerCount
$lastItemId = [uint64]$FirstItemId + [uint64]($BotCount - 1) * [uint64]$itemStride + [uint64]$templateItemCount
if ($FirstContainerId -lt 0x00010000 -or $lastContainerId -gt [uint]::MaxValue) {
    throw 'Generated container IDs must remain in the server uint ID-manager range.'
}
if ($FirstItemId -lt 0x01000000 -or $lastItemId -gt [uint]::MaxValue) {
    throw 'Generated item IDs must remain in the server uint ID-manager range.'
}
if (($lastItemId - 0x01000000) -ge 100000) {
    throw 'Generated item IDs must remain inside the ItemIdManager initial 100k bitset window.'
}
$containerCollisions = [int](Invoke-MySql $gameProvider "$($gameProvider.Database)" "SELECT COUNT(*) FROM item_containers WHERE container_id BETWEEN $($FirstContainerId + 1) AND $lastContainerId" | Select-Object -First 1)
$itemCollisions = [int](Invoke-MySql $gameProvider "$($gameProvider.Database)" "SELECT COUNT(*) FROM items WHERE id BETWEEN $($FirstItemId + 1) AND $lastItemId" | Select-Object -First 1)
if ($containerCollisions -ne 0 -or $itemCollisions -ne 0) {
    throw "Generated ID ranges collide with donor data (containers=$containerCollisions, items=$itemCollisions)."
}

$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$gameDump = Join-Path $OutputDirectory "$($gameProvider.Database)-$stamp.sql"
$loginDump = Join-Path $OutputDirectory "$($loginProvider.Database)-$stamp.sql"
Export-Database $gameProvider "$($gameProvider.Database)" $gameDump
Export-Database $loginProvider "$($loginProvider.Database)" $loginDump
Assert-NoDestructiveSql $gameDump
Assert-NoDestructiveSql $loginDump

[void](Invoke-MySql $gameProvider '' "CREATE DATABASE ``$GameDatabaseName`` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci")
[void](Invoke-MySql $loginProvider '' "CREATE DATABASE ``$LoginDatabaseName`` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci")
Import-Database $gameProvider $GameDatabaseName $gameDump
Import-Database $loginProvider $LoginDatabaseName $loginDump

$manifestValues = for ($offset = 0; $offset -lt $BotCount; $offset++) {
    $id = [uint]($FirstBotId + $offset)
    "($id,'ScaleBot$($offset.ToString('D3'))',$TemplateCharacterId,'$SeedVersion')"
}
$manifestSql = @"
CREATE TABLE t021_seed_manifest (
  bot_id INT UNSIGNED NOT NULL PRIMARY KEY,
  bot_name VARCHAR(128) NOT NULL,
  template_character_id INT UNSIGNED NOT NULL,
  seed_version VARCHAR(32) NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;
INSERT INTO t021_seed_manifest (bot_id,bot_name,template_character_id,seed_version) VALUES $($manifestValues -join ',');
"@
[void](Invoke-MySql $gameProvider $GameDatabaseName $manifestSql)

$characterColumns = Get-Columns $gameProvider $GameDatabaseName 'characters'
$characterColumnList = ($characterColumns | ForEach-Object { "``$_``" }) -join ','
$characterSelect = ($characterColumns | ForEach-Object {
    switch ($_ ) {
        'id' { 'm.bot_id' }
        'account_id' { "$BotAccountId" }
        'name' { 'm.bot_name' }
        'x' { "s.x + MOD(m.bot_id-$FirstBotId,10)*1.5" }
        'y' { "s.y + FLOOR((m.bot_id-$FirstBotId)/10)*1.5" }
        default { "s.``$_``" }
    }
}) -join ','
[void](Invoke-MySql $gameProvider $GameDatabaseName "INSERT INTO characters ($characterColumnList) SELECT $characterSelect FROM characters s CROSS JOIN t021_seed_manifest m WHERE s.id=$TemplateCharacterId AND s.deleted=0")

foreach ($table in @('abilities','actabilities','skills','options','appellations','completed_quests','quests','portal_book_coords','portal_visited_district')) {
    Copy-OwnerTable $gameProvider $GameDatabaseName $table 'owner'
}
Copy-OwnerTable $gameProvider $GameDatabaseName 'bot_archetype_plans' 'character_id'

$containerColumns = Get-Columns $gameProvider $GameDatabaseName 'item_containers'
$containerColumnList = ($containerColumns | ForEach-Object { "``$_``" }) -join ','
$containerSelect = ($containerColumns | ForEach-Object {
    switch ($_ ) {
        'container_id' { "$FirstContainerId + (m.bot_id-$FirstBotId)*$containerStride + (SELECT COUNT(*) FROM item_containers ranker WHERE ranker.owner_id=$TemplateCharacterId AND ranker.container_id<=s.container_id)" }
        'owner_id' { 'm.bot_id' }
        default { "s.``$_``" }
    }
}) -join ','
[void](Invoke-MySql $gameProvider $GameDatabaseName "INSERT INTO item_containers ($containerColumnList) SELECT $containerSelect FROM item_containers s CROSS JOIN t021_seed_manifest m WHERE s.owner_id=$TemplateCharacterId")

$itemColumns = Get-Columns $gameProvider $GameDatabaseName 'items'
$itemColumnList = ($itemColumns | ForEach-Object { "``$_``" }) -join ','
$sourceContainers = "SELECT container_id FROM item_containers WHERE owner_id=$TemplateCharacterId"
$itemSelect = ($itemColumns | ForEach-Object {
    switch ($_ ) {
        'id' { "$FirstItemId + (m.bot_id-$FirstBotId)*$itemStride + (SELECT COUNT(*) FROM items ranker WHERE ranker.container_id IN ($sourceContainers) AND ranker.id<=s.id)" }
        'owner' { 'm.bot_id' }
        'container_id' { "$FirstContainerId + (m.bot_id-$FirstBotId)*$containerStride + (SELECT COUNT(*) FROM item_containers ranker WHERE ranker.owner_id=$TemplateCharacterId AND ranker.container_id<=s.container_id)" }
        default { "s.``$_``" }
    }
}) -join ','
[void](Invoke-MySql $gameProvider $GameDatabaseName "INSERT INTO items ($itemColumnList) SELECT $itemSelect FROM items s CROSS JOIN t021_seed_manifest m WHERE s.container_id IN ($sourceContainers)")

$verification = Invoke-MySql $gameProvider $GameDatabaseName @"
SELECT CONCAT('manifest=',COUNT(*)) FROM t021_seed_manifest;
SELECT CONCAT('characters=',COUNT(*)) FROM characters c JOIN t021_seed_manifest m ON m.bot_id=c.id WHERE c.deleted=0;
SELECT CONCAT('bot_accounts=',COUNT(*)) FROM characters c JOIN t021_seed_manifest m ON m.bot_id=c.id WHERE c.account_id=$BotAccountId;
SELECT CONCAT('abilities=',COUNT(*)) FROM abilities a JOIN t021_seed_manifest m ON m.bot_id=a.owner;
SELECT CONCAT('skills=',COUNT(*)) FROM skills s JOIN t021_seed_manifest m ON m.bot_id=s.owner;
SELECT CONCAT('containers=',COUNT(*)) FROM item_containers c JOIN t021_seed_manifest m ON m.bot_id=c.owner_id;
SELECT CONCAT('items=',COUNT(*)) FROM items i JOIN t021_seed_manifest m ON m.bot_id=i.owner;
"@

$botIds = @(0..($BotCount - 1) | ForEach-Object { [uint]($FirstBotId + $_) })
$botIds | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'retained-bot-ids.json') -Encoding utf8
$evidence = [ordered]@{
    schemaVersion = 't021.database-seed.v2'
    createdAtUtc = [DateTime]::UtcNow.ToString('O')
    gameDatabase = $GameDatabaseName
    loginDatabase = $LoginDatabaseName
    donorGameDatabase = "$($gameProvider.Database)"
    donorLoginDatabase = "$($loginProvider.Database)"
    templateCharacterId = $TemplateCharacterId
    firstBotId = $FirstBotId
    botAccountId = $BotAccountId
    botCount = $BotCount
    seedVersion = $SeedVersion
    generatedContainerStride = $containerStride
    generatedItemStride = $itemStride
    generatedContainerIdRange = @(([uint64]$FirstContainerId + 1), $lastContainerId)
    generatedItemIdRange = @(([uint64]$FirstItemId + 1), $lastItemId)
    itemIdManagerInitialWindowValidated = $true
    positionLayout = '10x10 grid, 1.5 metre spacing from template position'
    retainedDumpFiles = @($gameDump, $loginDump)
    verification = @($verification)
    destructiveOperations = 'none; destination schemas must not pre-exist and are retained on any failure'
}
$evidence | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'seed-evidence.json') -Encoding utf8
Write-Host "Created isolated schemas $GameDatabaseName and $LoginDatabaseName with $BotCount retained bot characters."
Write-Host "Evidence: $(Join-Path $OutputDirectory 'seed-evidence.json')"
