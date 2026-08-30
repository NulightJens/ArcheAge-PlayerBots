[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDonorConfigPath,
    [Parameter(Mandatory)][string]$LoginDonorConfigPath,
    [Parameter(Mandatory)][string]$GameOutputConfigPath,
    [Parameter(Mandatory)][string]$LoginOutputConfigPath,
    [Parameter(Mandatory)][string]$GameDatabaseName,
    [Parameter(Mandatory)][string]$LoginDatabaseName,
    [int]$LoginInternalPort = 1234,
    [int]$LoginClientPort = 1237,
    [int]$GameClientPort = 1239,
    [int]$GameStreamPort = 1250,
    [int]$GameWebApiPort = 1280
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Config([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Donor config does not exist: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Add-OrSet($Object, [string]$Name, $Value) {
    if ($Object.PSObject.Properties.Name -contains $Name) { $Object.$Name = $Value }
    else { $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value }
}

function New-NetworkOverride([string]$BindHost, [int]$Port) {
    return [pscustomobject]@{ Host = $BindHost; Port = $Port }
}

if ($GameDatabaseName -notmatch '^aaemu_(t021|playerbots)_game_[a-z0-9_]*v[0-9]+$' -or
    $LoginDatabaseName -notmatch '^aaemu_(t021|playerbots)_login_[a-z0-9_]*v[0-9]+$') {
    throw 'Both database names must be isolated, versioned PlayerBots schemas.'
}
foreach ($destination in @($GameOutputConfigPath, $LoginOutputConfigPath)) {
    if (Test-Path -LiteralPath $destination) { throw "Refusing to overwrite existing runtime config: $destination" }
    $parent = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $parent)) { throw "Runtime config parent does not exist: $parent" }
}

$game = Read-Config $GameDonorConfigPath
$login = Read-Config $LoginDonorConfigPath
$game.Connections.MySQLProvider.Database = $GameDatabaseName
$login.Connections.MySQLProvider.Database = $LoginDatabaseName

Add-OrSet $game 'Network' (New-NetworkOverride '127.0.0.1' $GameClientPort)
Add-OrSet $game 'StreamNetwork' (New-NetworkOverride '127.0.0.1' $GameStreamPort)
Add-OrSet $game 'WebApiNetwork' (New-NetworkOverride '127.0.0.1' $GameWebApiPort)
Add-OrSet $game 'LoginNetwork' (New-NetworkOverride '127.0.0.1' $LoginInternalPort)
Add-OrSet $login 'InternalNetwork' (New-NetworkOverride '127.0.0.1' $LoginInternalPort)
Add-OrSet $login 'Network' (New-NetworkOverride '127.0.0.1' $LoginClientPort)
foreach ($server in @($login.GameServers)) {
    Add-OrSet $server 'Host' '127.0.0.1'
    Add-OrSet $server 'Port' $GameClientPort
}

$game | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $GameOutputConfigPath -Encoding utf8
$login | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $LoginOutputConfigPath -Encoding utf8

Write-Host "Created task-local runtime configs for $GameDatabaseName and $LoginDatabaseName."
Write-Host "All AAEmu listeners are bound to 127.0.0.1; secrets were copied but not displayed."
