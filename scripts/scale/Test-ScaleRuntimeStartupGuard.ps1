[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'ScaleRuntimeStartupGuard.psm1') -Force

$script:AssertionCount = 0

function Assert-StartupEvidence {
    param(
        [Parameter(Mandatory)][bool]$Expected,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$ExpectedDatabase,
        [Parameter(Mandatory)][AllowNull()][object]$Content,
        [int]$LoginHttpPort = 1237
    )

    $patterns = New-ScaleLoginStartupPatterns `
        -LoginDatabaseName $ExpectedDatabase `
        -LoginHttpPort $LoginHttpPort
    $actual = Test-ScaleRuntimeStartupEvidence -Content $Content -RequiredPatterns $patterns
    $script:AssertionCount++
    if ($actual -ne $Expected) {
        throw "$Label expected $Expected but received $actual."
    }
}

function Assert-GameStartupEvidence {
    param(
        [Parameter(Mandatory)][bool]$Expected,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$ExpectedDatabase,
        [Parameter(Mandatory)][AllowNull()][object]$Content,
        [int]$GameWebApiPort = 1280
    )

    $patterns = New-ScaleGameStartupPatterns `
        -GameDatabaseName $ExpectedDatabase `
        -GameWebApiPort $GameWebApiPort
    $actual = Test-ScaleRuntimeStartupEvidence -Content $Content -RequiredPatterns $patterns
    $script:AssertionCount++
    if ($actual -ne $Expected) {
        throw "$Label expected $Expected but received $actual."
    }
}

function Assert-LegacyArrayBindingFailure([object[]]$Content) {
    function Invoke-LegacyStringPredicate {
        [CmdletBinding()]
        param([Parameter(Mandatory)][string]$Content)

        return $true
    }

    try {
        [void](Invoke-LegacyStringPredicate -Content $Content)
    }
    catch {
        if ($_.Exception.Message -notmatch 'transform.*System\.String|convert.*System\.String') {
            throw "Legacy array fixture produced an unexpected failure: $($_.Exception.Message)"
        }
        $script:AssertionCount++
        return
    }
    throw 'Legacy array fixture did not reproduce the T-050 string parameter-conversion failure.'
}

function Join-StartupLog([string[]]$Lines) {
    return $Lines -join "`r`n"
}

$database = 'aaemu_playerbots_login_public_alpha_v1'
$selectedPrefix = '12:34:56 [INFO] LoginService - Selected Login database schema: '
$internalLine = '12:34:57 [INFO] InternalNetwork - InternalNetwork started'
$loopbackLine = '12:34:58 [INFO] Microsoft.Hosting.Lifetime - Now listening on: http://127.0.0.1:1237'

$exact = Join-StartupLog @(
    $selectedPrefix + $database
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $true 'exact selected schema and loopback startup' $database $exact

$genericConnection = Join-StartupLog @(
    '12:34:56 [INFO] MySqlInitializer - MySQL connection established successfully'
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'generic connection line' $database $genericConnection

$hardCodedUpdaterPrefix = Join-StartupLog @(
    "12:34:56 [INFO] MySqlDatabaseUpdater - database aaemu_login selected $database"
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'hard-coded updater prefix' $database $hardCodedUpdaterPrefix

$donorSchema = Join-StartupLog @(
    $selectedPrefix + 'aaemu_login'
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'donor schema' $database $donorSchema

$substringCollision = Join-StartupLog @(
    $selectedPrefix + $database + '_shadow'
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'schema substring collision' $database $substringCollision

$missingSelectedLine = Join-StartupLog @(
    '12:34:56 [INFO] LoginService - Starting daemon: AAEmu.Login'
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'missing selected-schema line' $database $missingSelectedLine

$wrongSchema = Join-StartupLog @(
    $selectedPrefix + 'aaemu_playerbots_login_public_alpha_v2'
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'wrong selected schema' $database $wrongSchema

$wrongLogger = Join-StartupLog @(
    '12:34:56 [INFO] MySqlDatabaseUpdater - Selected Login database schema: ' + $database
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'selected schema from wrong logger' $database $wrongLogger

$metacharDatabase = 'aaemu.playerbots+login[retry](v1)?^$|\exact'
$metacharExact = Join-StartupLog @(
    $selectedPrefix + $metacharDatabase
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $true 'regex metacharacters are literal' $metacharDatabase $metacharExact

$metacharCollision = Join-StartupLog @(
    $selectedPrefix + $metacharDatabase + '.copy'
    $internalLine
    $loopbackLine
)
Assert-StartupEvidence $false 'regex metacharacter substring collision' $metacharDatabase $metacharCollision

$missingInternalNetwork = Join-StartupLog @(
    $selectedPrefix + $database
    $loopbackLine
)
Assert-StartupEvidence $false 'missing internal-network startup' $database $missingInternalNetwork

$nonLoopbackHttp = Join-StartupLog @(
    $selectedPrefix + $database
    $internalLine
    '12:34:58 [INFO] Microsoft.Hosting.Lifetime - Now listening on: http://0.0.0.0:1237'
)
Assert-StartupEvidence $false 'non-loopback HTTP listener' $database $nonLoopbackHttp

$wrongLoopbackPort = Join-StartupLog @(
    $selectedPrefix + $database
    $internalLine
    '12:34:58 [INFO] Microsoft.Hosting.Lifetime - Now listening on: http://127.0.0.1:1238'
)
Assert-StartupEvidence $false 'wrong loopback HTTP port' $database $wrongLoopbackPort

$gameDatabase = 'aaemu_playerbots_game_public_alpha_v1'
$gameSelectedPrefix = '19:29:09 [INFO] GameService - Selected Game database schema: '
$gameNetworkLine = '19:30:45 [INFO] GameNetwork - Network started'
$streamNetworkLine = '19:30:45 [INFO] StreamNetwork - StreamNetwork started'
$serverStartedLine = '19:30:45 [INFO] GameService - Server started! Took 00:01:32.2618974'
$webApiLine = '19:30:45 [INFO] WebApiService - WebApi server started on 127.0.0.1:1280'

$gameExact = Join-StartupLog @(
    $gameSelectedPrefix + $gameDatabase
    $gameNetworkLine
    $streamNetworkLine
    $serverStartedLine
    $webApiLine
)
Assert-GameStartupEvidence $true 'exact Game schema and loopback startup' $gameDatabase $gameExact

$gameGenericConnection = Join-StartupLog @(
    '19:29:09 [INFO] MySqlInitializer - MySQL connection established successfully'
    $gameNetworkLine
    $streamNetworkLine
    $serverStartedLine
    $webApiLine
)
Assert-GameStartupEvidence $false 'Game generic connection line' $gameDatabase $gameGenericConnection

$gameUpdaterPrefix = Join-StartupLog @(
    "19:29:09 [INFO] MySqlDatabaseUpdater - database aaemu_game selected $gameDatabase"
    $gameNetworkLine
    $streamNetworkLine
    $serverStartedLine
    $webApiLine
)
Assert-GameStartupEvidence $false 'Game updater prefix' $gameDatabase $gameUpdaterPrefix

$gameDonorSchema = $gameExact.Replace($gameSelectedPrefix + $gameDatabase, $gameSelectedPrefix + 'aaemu_game')
Assert-GameStartupEvidence $false 'Game donor schema' $gameDatabase $gameDonorSchema

$gameSubstringCollision = $gameExact.Replace(
    $gameSelectedPrefix + $gameDatabase,
    $gameSelectedPrefix + $gameDatabase + '_shadow')
Assert-GameStartupEvidence $false 'Game schema substring collision' $gameDatabase $gameSubstringCollision

$gameMissingSelectedLine = Join-StartupLog @(
    '19:29:09 [INFO] GameService - Starting daemon: AAEmu.Game'
    $gameNetworkLine
    $streamNetworkLine
    $serverStartedLine
    $webApiLine
)
Assert-GameStartupEvidence $false 'missing Game selected-schema line' $gameDatabase $gameMissingSelectedLine

$gameWrongSchema = $gameExact.Replace(
    $gameSelectedPrefix + $gameDatabase,
    $gameSelectedPrefix + 'aaemu_playerbots_game_public_alpha_v2')
Assert-GameStartupEvidence $false 'wrong Game schema' $gameDatabase $gameWrongSchema

$gameWrongLogger = $gameExact.Replace(
    $gameSelectedPrefix + $gameDatabase,
    '19:29:09 [INFO] MySqlDatabaseUpdater - Selected Game database schema: ' + $gameDatabase)
Assert-GameStartupEvidence $false 'Game schema from wrong logger' $gameDatabase $gameWrongLogger

$gameEmbeddedLogger = $gameExact.Replace(
    $gameSelectedPrefix + $gameDatabase,
    '19:29:09 [INFO] OtherLogger - GameService - Selected Game database schema: ' + $gameDatabase)
Assert-GameStartupEvidence $false 'embedded GameService text from wrong logger' $gameDatabase $gameEmbeddedLogger

$gameMetacharDatabase = 'aaemu.playerbots+game[retry](v1)?^$|\exact'
$gameMetacharExact = $gameExact.Replace(
    $gameSelectedPrefix + $gameDatabase,
    $gameSelectedPrefix + $gameMetacharDatabase)
Assert-GameStartupEvidence $true 'Game regex metacharacters are literal' $gameMetacharDatabase $gameMetacharExact

$gameMetacharCollision = $gameMetacharExact.Replace(
    $gameSelectedPrefix + $gameMetacharDatabase,
    $gameSelectedPrefix + $gameMetacharDatabase + '.copy')
Assert-GameStartupEvidence $false 'Game regex metacharacter substring collision' $gameMetacharDatabase $gameMetacharCollision

Assert-GameStartupEvidence $false 'missing GameNetwork startup' $gameDatabase ($gameExact.Replace($gameNetworkLine, ''))
Assert-GameStartupEvidence $false 'missing StreamNetwork startup' $gameDatabase ($gameExact.Replace($streamNetworkLine, ''))
Assert-GameStartupEvidence $false 'missing Game server-start marker' $gameDatabase ($gameExact.Replace($serverStartedLine, ''))
Assert-GameStartupEvidence $false 'non-loopback Game Web API' $gameDatabase ($gameExact.Replace('127.0.0.1:1280', '0.0.0.0:1280'))
Assert-GameStartupEvidence $false 'wrong Game Web API port' $gameDatabase ($gameExact.Replace('127.0.0.1:1280', '127.0.0.1:1281'))

# T-050 retained a 20,255,151-byte Game log and a string parameter-conversion
# failure. These observed startup lines plus a same-scale deterministic prefix
# reproduce its accidental multi-value boundary without depending on mutable logs.
$largeBuilder = [Text.StringBuilder]::new(21 * 1024 * 1024)
[void]$largeBuilder.Append([string]::new('x', 20 * 1024 * 1024))
[void]$largeBuilder.Append("`r`n")
[void]$largeBuilder.Append($gameExact)
[string]$largeGrowingLog = $largeBuilder.ToString()
Assert-GameStartupEvidence $true 'large growing Game log scalar' $gameDatabase $largeGrowingLog

$splitAt = [int]($largeGrowingLog.Length / 2)
[object[]]$accidentalArray = @(
    $largeGrowingLog.Substring(0, $splitAt)
    $largeGrowingLog.Substring($splitAt)
)
Assert-LegacyArrayBindingFailure $accidentalArray
Assert-GameStartupEvidence $false 'accidental Game log array fails closed' $gameDatabase $accidentalArray

Write-Output "T-051 deterministic startup-guard fixtures: PASS ($script:AssertionCount assertions, no runtime)"
