[CmdletBinding()]
param(
    [string]$ArtifactsDirectory = '',
    [string]$RetainedFixturePath = 'D:\Codex-Labs\evidence\T-075\one-bot-autonomy-v5\iteration-1\observer\raw\000000-botdebug.response.bin'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testRoot = $PSScriptRoot
$autonomyRoot = Split-Path -Parent $testRoot
$modulePath = Join-Path $autonomyRoot 'AutonomyObserver.psd1'
$moduleSourcePath = Join-Path $autonomyRoot 'AutonomyObserver.psm1'
$entryPointPath = Join-Path $autonomyRoot 'Observe-AutonomyBot.ps1'
if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $ArtifactsDirectory = Join-Path $autonomyRoot ".test-runs/run-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ'))-$PID"
}
$ArtifactsDirectory = [System.IO.Path]::GetFullPath($ArtifactsDirectory)
if (Test-Path -LiteralPath $ArtifactsDirectory) {
    throw "ArtifactsDirectory must be a new path so prior attempts are retained: $ArtifactsDirectory"
}
[void](New-Item -ItemType Directory -Path $ArtifactsDirectory -ErrorAction Stop)

$utf8 = [System.Text.UTF8Encoding]::new($false)
$assertionCount = 0
$expectedFixtureLength = 165
$expectedFixtureSha256 = 'f1d865e388eca68afd064d5bbc89fcad18577e97a806c2afb9e3a77e1646bf98'

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
    $script:assertionCount++
}

function Write-NewTextFile {
    param([string]$Path, [string]$Content)

    $stream = [System.IO.FileStream]::new(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $writer = [System.IO.StreamWriter]::new($stream, $utf8)
        try {
            $writer.Write($Content)
            $writer.Flush()
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function ConvertTo-TestBytes {
    param([string]$Text)
    return $utf8.GetBytes($Text)
}

function Read-TestJson {
    param([string]$Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 30 -DateKind String
}

function Start-HttpFixtureServer {
    param(
        [string]$Name,
        [string[]]$Bodies,
        [int[]]$StatusCodes
    )

    if ($Bodies.Count -ne $StatusCodes.Count -or $Bodies.Count -eq 0) {
        throw 'Fixture server requires one status code for every response body.'
    }

    $serverRoot = Join-Path $ArtifactsDirectory $Name
    [void](New-Item -ItemType Directory -Path $serverRoot -ErrorAction Stop)
    $readyPath = Join-Path $serverRoot 'ready.marker'

    $probe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $probe.Start()
    $port = ([System.Net.IPEndPoint]$probe.LocalEndpoint).Port
    $probe.Stop()

    $job = Start-Job -ArgumentList $port, $Bodies, $StatusCodes, $readyPath, $serverRoot -ScriptBlock {
        param($Port, $ResponseBodies, $ResponseStatusCodes, $ReadyPath, $RequestRoot)

        Set-StrictMode -Version Latest
        $ErrorActionPreference = 'Stop'
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, [int]$Port)
        try {
            $listener.Start()
            $readyStream = [System.IO.File]::Open(
                $ReadyPath,
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None)
            $readyStream.Dispose()

            for ($index = 0; $index -lt $ResponseBodies.Count; $index++) {
                $deadline = [DateTime]::UtcNow.AddSeconds(10)
                while (-not $listener.Pending()) {
                    if ([DateTime]::UtcNow -ge $deadline) {
                        throw "Timed out waiting for request $index."
                    }
                    Start-Sleep -Milliseconds 10
                }

                $tcpClient = $listener.AcceptTcpClient()
                try {
                    $stream = $tcpClient.GetStream()
                    $reader = [System.IO.StreamReader]::new(
                        $stream,
                        [System.Text.Encoding]::ASCII,
                        $false,
                        1024,
                        $true)
                    try {
                        $requestLine = $reader.ReadLine()
                        if ([string]::IsNullOrEmpty($requestLine)) {
                            throw 'Fixture server received an empty request line.'
                        }
                        $contentLength = 0
                        while ($true) {
                            $header = $reader.ReadLine()
                            if ($null -eq $header) {
                                throw 'Fixture server received truncated headers.'
                            }
                            if ($header.Length -eq 0) {
                                break
                            }
                            if ($header -match '^Content-Length:\s*(?<length>\d+)\s*$') {
                                $contentLength = [int]$Matches['length']
                            }
                        }

                        $requestBody = ''
                        if ($contentLength -gt 0) {
                            $buffer = [char[]]::new($contentLength)
                            $offset = 0
                            while ($offset -lt $contentLength) {
                                $read = $reader.ReadBlock($buffer, $offset, $contentLength - $offset)
                                if ($read -le 0) {
                                    throw 'Fixture server received a truncated request body.'
                                }
                                $offset += $read
                            }
                            $requestBody = [string]::new($buffer)
                        }

                        $requestText = "$requestLine`n$requestBody"
                        $requestPath = Join-Path $RequestRoot ("request-{0:D2}.txt" -f $index)
                        $requestBytes = [System.Text.UTF8Encoding]::new($false).GetBytes($requestText)
                        $requestStream = [System.IO.File]::Open(
                            $requestPath,
                            [System.IO.FileMode]::CreateNew,
                            [System.IO.FileAccess]::Write,
                            [System.IO.FileShare]::None)
                        try {
                            $requestStream.Write($requestBytes, 0, $requestBytes.Count)
                        }
                        finally {
                            $requestStream.Dispose()
                        }

                        $bodyBytes = [System.Text.UTF8Encoding]::new($false).GetBytes([string]$ResponseBodies[$index])
                        $statusCode = [int]$ResponseStatusCodes[$index]
                        $reason = if ($statusCode -eq 200) { 'OK' } else { 'Service Unavailable' }
                        $headers = "HTTP/1.1 $statusCode $reason`r`nContent-Type: application/json`r`nContent-Length: $($bodyBytes.Count)`r`nConnection: close`r`n`r`n"
                        $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($headers)
                        $stream.Write($headerBytes, 0, $headerBytes.Count)
                        $stream.Write($bodyBytes, 0, $bodyBytes.Count)
                        $stream.Flush()
                    }
                    finally {
                        $reader.Dispose()
                    }
                }
                finally {
                    $tcpClient.Dispose()
                }
            }
        }
        finally {
            $listener.Stop()
        }
    }

    $ready = $false
    for ($attempt = 0; $attempt -lt 500; $attempt++) {
        if (Test-Path -LiteralPath $readyPath -PathType Leaf) {
            $ready = $true
            break
        }
        if ($job.State -in @('Failed', 'Stopped', 'Completed')) {
            break
        }
        Start-Sleep -Milliseconds 10
    }
    if (-not $ready) {
        throw "Fixture server '$Name' did not become ready; state=$($job.State)."
    }

    return [pscustomobject]@{
        Job = $job
        Port = $port
        Root = $serverRoot
    }
}

function Assert-ServerCompleted {
    param([object]$Server, [string]$Name)

    [void](Wait-Job -Job $Server.Job -Timeout 15)
    Assert-True -Condition ($Server.Job.State -ceq 'Completed') -Message "$Name fixture server must complete"
    $reason = $Server.Job.ChildJobs[0].JobStateInfo.Reason
    Assert-True -Condition ($null -eq $reason) -Message "$Name fixture server must not retain an error"
}

Import-Module -Name $modulePath -Force -ErrorAction Stop

# The retained artifact is qualified by metadata before any byte read.
$retainedItem = Get-Item -LiteralPath $RetainedFixturePath -ErrorAction Stop
$retainedHashBefore = (Get-FileHash -LiteralPath $RetainedFixturePath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-True -Condition ($retainedItem.Length -eq $expectedFixtureLength) -Message 'retained T-075 response must be exactly 165 bytes'
Assert-True -Condition ($retainedHashBefore -ceq $expectedFixtureSha256) -Message 'retained T-075 response SHA-256 must match the pinned value before use'
$retainedBytes = [System.IO.File]::ReadAllBytes($RetainedFixturePath)
$retainedSample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes $retainedBytes
Assert-True -Condition ($retainedSample.PSObject.TypeNames[0] -ceq 'PlayerBots.Autonomy.BotDebugSample') -Message 'parser results must retain their PowerShell type name'
Assert-True -Condition ($retainedSample.classification -ceq 'offline') -Message 'retained T-075 response must classify as valid offline evidence'
Assert-True -Condition ($retainedSample.online -eq $false) -Message 'retained T-075 response must report online=false'
Assert-True -Condition ($retainedSample.bot_id -eq 20001 -and $retainedSample.reported_bot_id -eq 20001) -Message 'retained bot identity must be preserved'
Assert-True -Condition ($null -eq $retainedSample.object_id) -Message 'offline object ID must be nullable'
Assert-True -Condition ($null -eq $retainedSample.diagnostic) -Message 'valid offline evidence must not retain a parse diagnostic'

# Synthetic offline variants keep every optional field strict-mode safe.
$offlineWithoutIdentity = '{"Messages":["[botdebug] No active bot found."]}'
$offlineWithoutIdentitySample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes (ConvertTo-TestBytes $offlineWithoutIdentity)
Assert-True -Condition ($offlineWithoutIdentitySample.classification -ceq 'offline') -Message 'offline response without reported identity must classify'
Assert-True -Condition ($null -eq $offlineWithoutIdentitySample.reported_bot_id) -Message 'absent offline identity must remain null'
Assert-True -Condition ($null -eq $offlineWithoutIdentitySample.command_line) -Message 'absent commandLine must remain null'

$plainOffline = '{"commandLine":"botdebug 20001","Messages":["No active bot found with id 20001."],"ErrorMessages":[]}'
$plainOfflineSample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes (ConvertTo-TestBytes $plainOffline)
Assert-True -Condition ($plainOfflineSample.classification -ceq 'offline') -Message 'plain offline response must classify'
Assert-True -Condition ($plainOfflineSample.reported_bot_id -eq 20001) -Message 'plain offline response must preserve identity'

# Online responses support an optional object ID and optional diagnostic lines.
$onlineFull = @{
    commandLine = 'botdebug 20001'
    commandCharacter = '@system'
    Messages = @(
        "|cFFFFFFFF[botdebug]|r === Bot 'ObserverOne' (Id: 20001, ObjId: 88001) ===",
        '|cFFFFFFFF[botdebug]|r Host metrics: bots=1, active=1, tick_ms_ema=0.25, max=1.50, skipped=2, brain_steps=11, mover_steps=13'
    )
    ErrorMessages = @()
} | ConvertTo-Json -Compress
$onlineFullSample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes (ConvertTo-TestBytes $onlineFull)
Assert-True -Condition ($onlineFullSample.classification -ceq 'online' -and $onlineFullSample.online) -Message 'full online response must classify'
Assert-True -Condition ($onlineFullSample.bot_name -ceq 'ObserverOne') -Message 'online bot name must be preserved'
Assert-True -Condition ($onlineFullSample.object_id -eq 88001) -Message 'online object ID must be parsed when present'
Assert-True -Condition ($onlineFullSample.host_metrics.brain_steps -eq 11 -and $onlineFullSample.host_metrics.mover_steps -eq 13) -Message 'online brain and mover counters must be parsed'
Assert-True -Condition ($onlineFullSample.host_metrics.skipped_ticks -eq 2) -Message 'online skipped counter must be parsed'

$onlineMinimal = '{"Messages":["[botdebug] === Bot ''ObserverTwo'' (Id: 20001) ==="]}'
$onlineMinimalSample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes (ConvertTo-TestBytes $onlineMinimal)
Assert-True -Condition ($onlineMinimalSample.classification -ceq 'online') -Message 'minimal online response must classify'
Assert-True -Condition ($null -eq $onlineMinimalSample.object_id) -Message 'absent online object ID must remain null'
Assert-True -Condition ($null -eq $onlineMinimalSample.host_metrics) -Message 'absent host metrics must remain null'

# Malformed bytes and mismatched fixed-command identities classify without a strict-mode exception.
$invalidJsonSample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes (ConvertTo-TestBytes '{')
Assert-True -Condition ($invalidJsonSample.classification -ceq 'malformed') -Message 'invalid JSON must classify as malformed'
Assert-True -Condition ($invalidJsonSample.online -eq $null) -Message 'malformed online state must remain null'
$mismatchedIdentity = '{"commandLine":"botdebug 20002","Messages":["No active bot found with id 20002."]}'
$mismatchedSample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes (ConvertTo-TestBytes $mismatchedIdentity)
Assert-True -Condition ($mismatchedSample.classification -ceq 'malformed') -Message 'mismatched bot identity must classify as malformed'
Assert-True -Condition ($mismatchedSample.diagnostic -ceq 'command-bot-identity-mismatch') -Message 'identity mismatch diagnostic must be explicit'
$invalidFieldSample = ConvertFrom-AutonomyBotDebugResponse -BotId 20001 -ResponseBytes (ConvertTo-TestBytes '{"Messages":{"value":1}}')
Assert-True -Condition ($invalidFieldSample.classification -ceq 'malformed') -Message 'invalid optional response field type must classify as malformed'

# Two separate offline captures prove the immutable arm and liveness boundaries.
$armBody = '{"Messages":["[botdebug] No active bot found with id 20001."]}'
$liveBody = '{"commandLine":"botdebug 20001","commandCharacter":"@system","Messages":["No active bot found with id 20001."]}'
$armServer = Start-HttpFixtureServer -Name 'arm-server' -Bodies @($armBody, $liveBody) -StatusCodes @(200, 200)
$observerOutput = Join-Path $ArtifactsDirectory 'observer-output'
$observerDiagnostics = @(
    & pwsh -NoLogo -NoProfile -File $entryPointPath `
        -BotId 20001 `
        -OutputPath $observerOutput `
        -ApiBase "http://127.0.0.1:$($armServer.Port)/api" `
        -SampleIntervalMilliseconds 10 `
        -TimeoutSeconds 5 `
        -MaximumSamples 2 2>&1 | ForEach-Object { "$_" }
)
$observerExitCode = $LASTEXITCODE
Assert-ServerCompleted -Server $armServer -Name 'arm/liveness'
Assert-True -Condition ($observerExitCode -eq 0) -Message "bounded observer must succeed after arm and liveness; diagnostics=$($observerDiagnostics -join '; ')"
Assert-True -Condition ((Get-ChildItem -LiteralPath (Join-Path $observerOutput 'raw') -File).Count -eq 2) -Message 'observer must preserve two raw response files'
Assert-True -Condition ((Get-ChildItem -LiteralPath (Join-Path $observerOutput 'derived') -File).Count -eq 2) -Message 'observer must preserve two derived sample files'
Assert-True -Condition ((Get-ChildItem -LiteralPath (Join-Path $observerOutput 'transport') -File).Count -eq 2) -Message 'observer must preserve two transport metadata files'

$armedBoundary = Read-TestJson -Path (Join-Path $observerOutput 'boundaries/armed.json')
$liveBoundary = Read-TestJson -Path (Join-Path $observerOutput 'boundaries/live.json')
Assert-True -Condition ($armedBoundary.boundary -ceq 'armed' -and $armedBoundary.sample_index -eq 0) -Message 'first successful offline sample must prove arm boundary'
Assert-True -Condition ($liveBoundary.boundary -ceq 'live' -and $liveBoundary.sample_index -eq 1) -Message 'second successful offline sample must prove liveness boundary'
Assert-True -Condition ($armedBoundary.online -eq $false -and $liveBoundary.online -eq $false) -Message 'arm and liveness boundaries must both be offline'

$rawZeroPath = Join-Path $observerOutput 'raw/000000-botdebug.response.bin'
$rawOnePath = Join-Path $observerOutput 'raw/000001-botdebug.response.bin'
$derivedZeroPath = Join-Path $observerOutput 'derived/000000-botdebug.sample.json'
$derivedZero = Read-TestJson -Path $derivedZeroPath
Assert-True -Condition ([System.IO.File]::ReadAllText($rawZeroPath, $utf8) -ceq $armBody) -Message 'first raw file must preserve exact response bytes'
Assert-True -Condition ([System.IO.File]::ReadAllText($rawOnePath, $utf8) -ceq $liveBody) -Message 'second raw file must preserve exact response bytes'
Assert-True -Condition ($derivedZero.classification -ceq 'offline') -Message 'derived sample must classify independently from raw capture'
Assert-True -Condition ($derivedZero.raw.path -ceq 'raw/000000-botdebug.response.bin') -Message 'derived sample must reference rather than embed raw bytes'
Assert-True -Condition (-not ([System.IO.File]::ReadAllText($derivedZeroPath, $utf8).Contains('No active bot found'))) -Message 'derived sample must not embed the raw response message'

$expectedRequestBody = '{"character":"@system","arguments":"20001"}'
foreach ($requestIndex in 0..1) {
    $requestText = Get-Content -LiteralPath (Join-Path $armServer.Root ("request-{0:D2}.txt" -f $requestIndex)) -Raw
    $requestParts = $requestText -split "`n", 2
    Assert-True -Condition ($requestParts[0] -ceq 'POST /api/commands/botdebug HTTP/1.1') -Message 'transport route must be the fixed botdebug endpoint'
    Assert-True -Condition ($requestParts[1] -ceq $expectedRequestBody) -Message 'transport body must contain only the fixed actor and declared bot ID'
}

# A non-success HTTP response retains its body but derives a transport-error sample and no boundary.
$errorBody = '{"error":"temporarily unavailable"}'
$errorServer = Start-HttpFixtureServer -Name 'error-server' -Bodies @($errorBody) -StatusCodes @(503)
$errorOutput = Join-Path $ArtifactsDirectory 'transport-error-output'
$errorResult = Start-AutonomyObserver `
    -BotId 20001 `
    -ApiBase "http://127.0.0.1:$($errorServer.Port)/api" `
    -OutputPath $errorOutput `
    -SampleIntervalMilliseconds 10 `
    -TimeoutSeconds 5 `
    -MaximumSamples 1
Assert-ServerCompleted -Server $errorServer -Name 'transport-error'
$errorSample = Read-TestJson -Path (Join-Path $errorOutput 'derived/000000-botdebug.sample.json')
Assert-True -Condition (-not $errorResult.armed -and -not $errorResult.live) -Message 'transport error must not arm the observer'
Assert-True -Condition ($errorSample.classification -ceq 'transport-error' -and $errorSample.status_code -eq 503) -Message 'HTTP error must classify with its status'
Assert-True -Condition ([System.IO.File]::ReadAllText((Join-Path $errorOutput 'raw/000000-botdebug.response.bin'), $utf8) -ceq $errorBody) -Message 'HTTP error response body must remain raw and exact'
Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $errorOutput 'boundaries/armed.json'))) -Message 'transport error must not emit an arm boundary'

# Existing output paths are retained byte-for-byte and refused before transport.
$existingOutput = Join-Path $ArtifactsDirectory 'existing-output'
[void](New-Item -ItemType Directory -Path $existingOutput -ErrorAction Stop)
$existingMarker = Join-Path $existingOutput 'retained.marker'
Write-NewTextFile -Path $existingMarker -Content 'retain-me'
$existingHashBefore = (Get-FileHash -LiteralPath $existingMarker -Algorithm SHA256).Hash
$existingRefused = $false
try {
    [void](Start-AutonomyObserver `
        -BotId 20001 `
        -ApiBase 'http://127.0.0.1:1/api' `
        -OutputPath $existingOutput `
        -SampleIntervalMilliseconds 10 `
        -TimeoutSeconds 1 `
        -MaximumSamples 1)
}
catch {
    $existingRefused = $_.Exception.Message -like 'OutputPath must be a new path*'
}
Assert-True -Condition $existingRefused -Message 'existing OutputPath must be refused'
Assert-True -Condition ((Get-FileHash -LiteralPath $existingMarker -Algorithm SHA256).Hash -ceq $existingHashBefore) -Message 'existing-path refusal must preserve retained bytes'
Assert-True -Condition (@(Get-ChildItem -LiteralPath $existingOutput -Force).Count -eq 1) -Message 'existing-path refusal must create no child output'

$nonLoopbackOutput = Join-Path $ArtifactsDirectory 'non-loopback-output'
$nonLoopbackRefused = $false
try {
    [void](Start-AutonomyObserver -BotId 20001 -ApiBase 'http://192.0.2.1:1280/api' -OutputPath $nonLoopbackOutput -MaximumSamples 1)
}
catch {
    $nonLoopbackRefused = $_.Exception.Message -like 'ApiBase must use HTTP on an explicit loopback host.*'
}
Assert-True -Condition ($nonLoopbackRefused -and -not (Test-Path -LiteralPath $nonLoopbackOutput)) -Message 'non-loopback transport must be refused before output creation'

$injectedPathOutput = Join-Path $ArtifactsDirectory 'injected-path-output'
$injectedPathRefused = $false
try {
    [void](Start-AutonomyObserver -BotId 20001 -ApiBase 'http://127.0.0.1:1280/api/commands/example' -OutputPath $injectedPathOutput -MaximumSamples 1)
}
catch {
    $injectedPathRefused = $_.Exception.Message -like "ApiBase path must be empty or '/api'.*"
}
Assert-True -Condition ($injectedPathRefused -and -not (Test-Path -LiteralPath $injectedPathOutput)) -Message 'caller-supplied command paths must be refused before output creation'

# Static allowlisting proves the production command and parameter surfaces are closed.
$productionSource = (Get-Content -LiteralPath $moduleSourcePath -Raw) + "`n" + (Get-Content -LiteralPath $entryPointPath -Raw)
$webRoutes = @(
    [regex]::Matches($productionSource, '(?i)/commands/[a-z0-9_-]+') |
        ForEach-Object { $_.Value.ToLowerInvariant() } |
        Sort-Object -Unique
)
Assert-True -Condition (($webRoutes -join ',') -ceq '/commands/botdebug') -Message 'production Web API command routes must contain only botdebug'

$moduleTokens = $null
$moduleParseErrors = $null
$moduleAst = [System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path $moduleSourcePath),
    [ref]$moduleTokens,
    [ref]$moduleParseErrors)
$entryTokens = $null
$entryParseErrors = $null
$entryAst = [System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path $entryPointPath),
    [ref]$entryTokens,
    [ref]$entryParseErrors)
Assert-True -Condition ($moduleParseErrors.Count -eq 0 -and $entryParseErrors.Count -eq 0) -Message 'production PowerShell must parse without errors'

$expectedEntryParameters = @('ApiBase', 'BotId', 'MaximumSamples', 'OutputPath', 'SampleIntervalMilliseconds', 'TimeoutSeconds')
$actualEntryParameters = @($entryAst.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath } | Sort-Object)
Assert-True -Condition (($actualEntryParameters -join ',') -ceq ($expectedEntryParameters -join ',')) -Message 'entry point must expose only observation parameters'

$expectedModuleCommands = @(
    'Add-Member', 'ConvertFrom-AutonomyBotDebugResponse', 'ConvertFrom-Json',
    'ConvertTo-CleanBotDebugMessage', 'ConvertTo-Json', 'ConvertTo-NullableDouble',
    'ConvertTo-NullableInt64', 'ConvertTo-NullableUInt32', 'ConvertTo-StringList',
    'Export-ModuleMember', 'Get-MatchGroupValue', 'Get-ObjectProperty', 'Get-OptionalString',
    'Get-Sha256Hex', 'Join-Path', 'New-BotDebugSample', 'New-Item',
    'New-MalformedBotDebugSample', 'New-TransportErrorSample', 'Resolve-BotDebugEndpoint',
    'Set-StrictMode', 'Start-Sleep', 'Test-Path', 'Write-NewBytes', 'Write-NewJson'
) | Sort-Object
$actualModuleCommands = @(
    $moduleAst.FindAll(
        { param($node) $node -is [System.Management.Automation.Language.CommandAst] },
        $true) |
        ForEach-Object { $_.GetCommandName() } |
        Where-Object { $null -ne $_ } |
        Sort-Object -Unique
)
Assert-True -Condition (($actualModuleCommands -join ',') -ceq ($expectedModuleCommands -join ',')) -Message 'module command AST must match the fixed observation-only allowlist'

$expectedModuleTypes = @(
    'Convert', 'DateTimeOffset', 'double', 'int', 'long', 'regex', 'string',
    'System.Array', 'System.Collections.Generic.List[string]', 'System.Collections.IEnumerable',
    'System.Globalization.CultureInfo', 'System.Globalization.NumberStyles',
    'System.IO.FileAccess', 'System.IO.FileMode', 'System.IO.FileShare', 'System.IO.FileStream',
    'System.IO.Path', 'System.IO.StreamWriter', 'System.Net.Http.HttpClient',
    'System.Net.Http.HttpClientHandler', 'System.Net.Http.HttpMethod',
    'System.Net.Http.HttpRequestMessage', 'System.Net.Http.StringContent',
    'System.Security.Cryptography.SHA256', 'System.Text.UTF8Encoding', 'TimeSpan',
    'uint32', 'Uri', 'UriKind'
) | Sort-Object
$actualModuleTypes = @(
    $moduleAst.FindAll(
        { param($node) $node -is [System.Management.Automation.Language.TypeExpressionAst] },
        $true) |
        ForEach-Object { $_.TypeName.FullName } |
        Sort-Object -Unique
)
Assert-True -Condition (($actualModuleTypes -join ',') -ceq ($expectedModuleTypes -join ',')) -Message 'module .NET type AST must match the transport/file/parser-only allowlist'

$expectedEntryCommands = @('ConvertTo-Json', 'Import-Module', 'Join-Path', 'Set-StrictMode', 'Start-AutonomyObserver', 'Write-Error') | Sort-Object
$actualEntryCommands = @(
    $entryAst.FindAll(
        { param($node) $node -is [System.Management.Automation.Language.CommandAst] },
        $true) |
        ForEach-Object { $_.GetCommandName() } |
        Where-Object { $null -ne $_ } |
        Sort-Object -Unique
)
Assert-True -Condition (($actualEntryCommands -join ',') -ceq ($expectedEntryCommands -join ',')) -Message 'entry point command AST must match the fixed observation-only allowlist'

$module = Get-Module -Name AutonomyObserver
$exportedFunctions = @($module.ExportedFunctions.Keys | Sort-Object)
Assert-True -Condition (($exportedFunctions -join ',') -ceq 'ConvertFrom-AutonomyBotDebugResponse,Start-AutonomyObserver') -Message 'module must export only parser and fixed observer functions'

$retainedHashAfter = (Get-FileHash -LiteralPath $RetainedFixturePath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-True -Condition ($retainedHashAfter -ceq $retainedHashBefore) -Message 'retained T-075 fixture must remain byte-identical after qualification'
$copiedFixtureCount = @(
    Get-ChildItem -LiteralPath $ArtifactsDirectory -File -Recurse |
        Where-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ceq $expectedFixtureSha256 }
).Count
Assert-True -Condition ($copiedFixtureCount -eq 0) -Message 'retained T-075 fixture bytes must not be copied into test artifacts'

$summary = [pscustomobject][ordered]@{
    schema_version = 'playerbots.autonomy-observer-test.v1'
    verdict = 'PASS'
    assertions = $assertionCount
    retained_fixture_length = $retainedItem.Length
    retained_fixture_sha256 = $retainedHashAfter
    retained_fixture_copies = $copiedFixtureCount
    production_web_command_routes = $webRoutes
    module_commands = $actualModuleCommands
    module_types = $actualModuleTypes
    entry_commands = $actualEntryCommands
    runtime_started = $false
    database_accessed = $false
    client_accessed = $false
    artifacts = $ArtifactsDirectory
}
$summaryPath = Join-Path $ArtifactsDirectory 'summary.json'
Write-NewTextFile -Path $summaryPath -Content (($summary | ConvertTo-Json -Depth 10).Replace("`r`n", "`n") + "`n")

Write-Output "PASS: $assertionCount deterministic observer assertions."
Write-Output "Artifacts: $ArtifactsDirectory"
