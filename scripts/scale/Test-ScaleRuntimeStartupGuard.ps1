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
        [Parameter(Mandatory)][string]$Content,
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

Write-Output "T-048 deterministic startup-guard fixtures: PASS ($script:AssertionCount assertions, no runtime)"
