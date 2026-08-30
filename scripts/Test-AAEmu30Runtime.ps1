[CmdletBinding()]
param(
    [string] $BaseUri = 'http://127.0.0.1:1280/api',

    [Nullable[int]] $ExpectedRuntimeCount,

    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$endpoint = [Uri]$BaseUri
$allowedHosts = @('127.0.0.1', 'localhost', '::1')
if ($endpoint.Scheme -ne 'http' -or $endpoint.Host -notin $allowedHosts) {
    throw 'The runtime smoke test only permits an HTTP loopback Web API endpoint.'
}

$base = $BaseUri.TrimEnd('/')
$status = Invoke-RestMethod -Method Get -Uri "$base/status"
$characterResponse = Invoke-WebRequest -Method Get -Uri "$base/character/list"
$characterDocument = [System.Text.Json.JsonDocument]::Parse($characterResponse.Content)
try {
    if ($characterDocument.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Array) {
        throw 'The character-list endpoint did not return a JSON array.'
    }
    $loggedCharacterCount = $characterDocument.RootElement.GetArrayLength()
}
finally {
    $characterDocument.Dispose()
}
$requestBody = @{
    character = '@system'
    arguments = 'snapshot'
} | ConvertTo-Json -Compress
$command = Invoke-RestMethod `
    -Method Post `
    -Uri "$base/commands/botmetrics" `
    -ContentType 'application/json' `
    -Body $requestBody

$commandErrors = @($command.ErrorMessages)
if ($commandErrors.Count -ne 0) {
    throw "The @system botmetrics command returned errors: $($commandErrors -join '; ')"
}
if ($command.commandCharacter -ne '@system') {
    throw "The command endpoint did not resolve the synthetic @system actor."
}

$metricsLine = @($command.Messages | Where-Object { $_ -match 'T021_METRICS\s+\{' }) | Select-Object -First 1
if (-not $metricsLine) {
    throw 'The command response did not contain a T021_METRICS payload.'
}
$jsonOffset = $metricsLine.IndexOf('{')
$metrics = $metricsLine.Substring($jsonOffset) | ConvertFrom-Json
if ($metrics.schemaVersion -ne 't021.scale-metrics.v1') {
    throw "Unexpected metrics schema '$($metrics.schemaVersion)'."
}
if ($null -ne $ExpectedRuntimeCount -and $metrics.runtimeCount -ne $ExpectedRuntimeCount) {
    throw "Expected $ExpectedRuntimeCount bot runtimes, observed $($metrics.runtimeCount)."
}

$result = [ordered]@{
    schemaVersion = 'playerbots.aaemu30-runtime-smoke.v1'
    status = 'passed'
    capturedAtUtc = [DateTime]::UtcNow.ToString('o')
    endpoint = $base
    serverStatus = $status
    loggedCharacterCount = $loggedCharacterCount
    commandCharacter = $command.commandCharacter
    commandLine = $command.commandLine
    metrics = $metrics
}

$rendered = $result | ConvertTo-Json -Depth 30
if ($OutputPath) {
    $absoluteOutput = [System.IO.Path]::GetFullPath($OutputPath)
    $parent = Split-Path -Parent $absoluteOutput
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
    Set-Content -LiteralPath $absoluteOutput -Value $rendered -Encoding utf8
}

$rendered
