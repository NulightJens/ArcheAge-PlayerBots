Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:SampleTypeName = 'PlayerBots.Autonomy.BotDebugSample'
$script:SampleSchema = 'playerbots.autonomy-botdebug-sample.v2'
$script:TransportSchema = 'playerbots.autonomy-botdebug-transport.v1'
$script:BoundarySchema = 'playerbots.autonomy-observer-boundary.v1'

function Get-ObjectProperty {
    param(
        [Parameter(Mandatory)][object]$InputObject,
        [Parameter(Mandatory)][string]$Name
    )

    $properties = @($InputObject.PSObject.Properties.Match($Name))
    if ($properties.Count -eq 0) {
        return [pscustomobject]@{ Exists = $false; Value = $null }
    }

    return [pscustomobject]@{ Exists = $true; Value = $properties[0].Value }
}

function ConvertTo-StringList {
    param(
        [Parameter(Mandatory)][object]$InputObject,
        [Parameter(Mandatory)][string]$Name
    )

    $property = Get-ObjectProperty -InputObject $InputObject -Name $Name
    if (-not $property.Exists -or $null -eq $property.Value) {
        return @()
    }
    if ($property.Value -is [string]) {
        return @([string]$property.Value)
    }
    if ($property.Value -isnot [System.Collections.IEnumerable]) {
        throw "Response field '$Name' is not a string or array."
    }

    $values = [System.Collections.Generic.List[string]]::new()
    foreach ($value in $property.Value) {
        if ($value -isnot [string]) {
            throw "Response field '$Name' contains a non-string value."
        }
        $values.Add([string]$value)
    }
    return @($values)
}

function Get-OptionalString {
    param(
        [Parameter(Mandatory)][object]$InputObject,
        [Parameter(Mandatory)][string]$Name
    )

    $property = Get-ObjectProperty -InputObject $InputObject -Name $Name
    if (-not $property.Exists -or $null -eq $property.Value) {
        return $null
    }
    if ($property.Value -isnot [string]) {
        throw "Response field '$Name' is not a string."
    }
    return [string]$property.Value
}

function Get-MatchGroupValue {
    param(
        [Parameter(Mandatory)][System.Text.RegularExpressions.Match]$Match,
        [Parameter(Mandatory)][string]$Name
    )

    $group = $Match.Groups[$Name]
    if ($null -eq $group -or -not $group.Success) {
        return $null
    }
    return $group.Value
}

function ConvertTo-NullableUInt32 {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }
    $parsed = 0u
    if (-not [uint32]::TryParse($Value, [ref]$parsed)) {
        return $null
    }
    return $parsed
}

function ConvertTo-NullableInt64 {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }
    $parsed = 0L
    if (-not [long]::TryParse(
        $Value,
        [System.Globalization.NumberStyles]::Integer,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$parsed)) {
        return $null
    }
    return $parsed
}

function ConvertTo-NullableDouble {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }
    $parsed = 0.0
    if (-not [double]::TryParse(
        $Value,
        [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$parsed)) {
        return $null
    }
    return $parsed
}

function ConvertTo-CleanBotDebugMessage {
    param([Parameter(Mandatory)][string]$Message)

    $withoutColor = [regex]::Replace($Message, '\|c[0-9A-Fa-f]{8}|\|r', '')
    return [regex]::Replace($withoutColor, '^\[botdebug\]\s*', '')
}

function New-BotDebugSample {
    param(
        [Parameter(Mandatory)][uint32]$BotId,
        [Parameter(Mandatory)][ValidateSet('offline', 'online', 'malformed', 'transport-error')][string]$Classification,
        [AllowNull()][object]$Online,
        [AllowNull()][object]$ReportedBotId,
        [AllowNull()][object]$BotName,
        [AllowNull()][object]$ObjectId,
        [AllowNull()][object]$CommandLine,
        [AllowNull()][object]$CommandCharacter,
        [int]$MessageCount,
        [int]$ErrorCount,
        [AllowNull()][object]$HostMetrics,
        [AllowNull()][object]$RuntimeMetrics,
        [AllowNull()][object]$Diagnostic,
        [AllowNull()][object]$StatusCode
    )

    $sample = [pscustomobject][ordered]@{
        schema_version = $script:SampleSchema
        classification = $Classification
        online = $Online
        bot_id = $BotId
        reported_bot_id = $ReportedBotId
        bot_name = $BotName
        object_id = $ObjectId
        command_line = $CommandLine
        command_character = $CommandCharacter
        message_count = $MessageCount
        error_count = $ErrorCount
        host_metrics = $HostMetrics
        runtime_metrics = $RuntimeMetrics
        diagnostic = $Diagnostic
        status_code = $StatusCode
    }
    $sample.PSObject.TypeNames.Insert(0, $script:SampleTypeName)
    return $sample
}

function New-MalformedBotDebugSample {
    param(
        [Parameter(Mandatory)][uint32]$BotId,
        [Parameter(Mandatory)][string]$Diagnostic,
        [AllowNull()][object]$ReportedBotId,
        [AllowNull()][object]$BotName,
        [AllowNull()][object]$ObjectId,
        [AllowNull()][object]$CommandLine,
        [AllowNull()][object]$CommandCharacter,
        [int]$MessageCount,
        [int]$ErrorCount
    )

    return New-BotDebugSample `
        -BotId $BotId `
        -Classification 'malformed' `
        -Online $null `
        -ReportedBotId $ReportedBotId `
        -BotName $BotName `
        -ObjectId $ObjectId `
        -CommandLine $CommandLine `
        -CommandCharacter $CommandCharacter `
        -MessageCount $MessageCount `
        -ErrorCount $ErrorCount `
        -HostMetrics $null `
        -RuntimeMetrics $null `
        -Diagnostic $Diagnostic `
        -StatusCode $null
}

function ConvertFrom-AutonomyBotDebugResponse {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateRange(1, [uint32]::MaxValue)][uint32]$BotId,
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$ResponseBytes
    )

    $commandLine = $null
    $commandCharacter = $null
    $reportedBotId = $null
    $botName = $null
    $objectId = $null
    $messages = @()
    $errors = @()

    if ($ResponseBytes.Count -eq 0) {
        return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'empty-response' `
            -ReportedBotId $null -BotName $null -ObjectId $null -CommandLine $null `
            -CommandCharacter $null -MessageCount 0 -ErrorCount 0
    }

    try {
        $encoding = [System.Text.UTF8Encoding]::new($false, $true)
        $text = $encoding.GetString($ResponseBytes)
    }
    catch {
        return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'invalid-utf8' `
            -ReportedBotId $null -BotName $null -ObjectId $null -CommandLine $null `
            -CommandCharacter $null -MessageCount 0 -ErrorCount 0
    }

    try {
        $response = $text | ConvertFrom-Json -Depth 20 -DateKind String
        if ($null -eq $response -or $response -is [System.Array] -or $response -is [string]) {
            throw 'The response root is not an object.'
        }
        $commandLine = Get-OptionalString -InputObject $response -Name 'commandLine'
        $commandCharacter = Get-OptionalString -InputObject $response -Name 'commandCharacter'
        $messages = @(ConvertTo-StringList -InputObject $response -Name 'Messages')
        $errors = @(ConvertTo-StringList -InputObject $response -Name 'ErrorMessages')
    }
    catch {
        return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'invalid-json-or-response-fields' `
            -ReportedBotId $reportedBotId -BotName $botName -ObjectId $objectId `
            -CommandLine $commandLine -CommandCharacter $commandCharacter `
            -MessageCount $messages.Count -ErrorCount $errors.Count
    }

    if ($null -ne $commandCharacter -and $commandCharacter -cne '@system') {
        return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'unexpected-command-character' `
            -ReportedBotId $null -BotName $null -ObjectId $null -CommandLine $commandLine `
            -CommandCharacter $commandCharacter -MessageCount $messages.Count -ErrorCount $errors.Count
    }

    if ($null -ne $commandLine) {
        $commandMatch = [regex]::Match($commandLine, '^botdebug\s+(?<bot>\d+)$')
        if (-not $commandMatch.Success) {
            return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'unexpected-command-line' `
                -ReportedBotId $null -BotName $null -ObjectId $null -CommandLine $commandLine `
                -CommandCharacter $commandCharacter -MessageCount $messages.Count -ErrorCount $errors.Count
        }
        $reportedBotId = ConvertTo-NullableUInt32 (Get-MatchGroupValue -Match $commandMatch -Name 'bot')
        if ($null -eq $reportedBotId -or $reportedBotId -ne $BotId) {
            return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'command-bot-identity-mismatch' `
                -ReportedBotId $reportedBotId -BotName $null -ObjectId $null -CommandLine $commandLine `
                -CommandCharacter $commandCharacter -MessageCount $messages.Count -ErrorCount $errors.Count
        }
    }

    $onlineMatch = $null
    $offlineMatch = $null
    $hostMetrics = $null
    $runtimeMetrics = $null
    $runtimeMetricsLineCount = 0
    $runtimeMetricsMalformed = $false
    foreach ($message in $messages) {
        $clean = ConvertTo-CleanBotDebugMessage -Message $message
        if ($null -eq $onlineMatch) {
            $candidate = [regex]::Match(
                $clean,
                "^=== Bot '(?<name>[^']+)' \(Id:\s*(?<bot>\d+)(?:,\s*ObjId:\s*(?<obj>\d+))?\) ===$")
            if ($candidate.Success) {
                $onlineMatch = $candidate
            }
        }
        if ($null -eq $offlineMatch) {
            $candidate = [regex]::Match(
                $clean,
                '^No active bot found(?: with id (?<bot>\d+))?\.$')
            if ($candidate.Success) {
                $offlineMatch = $candidate
            }
        }
        if ($null -eq $hostMetrics) {
            $candidate = [regex]::Match(
                $clean,
                '^Host metrics: bots=(?<bots>\d+), active=(?<active>\d+), tick_ms_ema=(?<ema>-?\d+(?:\.\d+)?), max=(?<max>-?\d+(?:\.\d+)?), skipped=(?<skipped>\d+), brain_steps=(?<brain>\d+), mover_steps=(?<mover>\d+)$')
            if ($candidate.Success) {
                $hostMetrics = [pscustomobject][ordered]@{
                    bots = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'bots')
                    active = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'active')
                    tick_ms_ema = ConvertTo-NullableDouble (Get-MatchGroupValue -Match $candidate -Name 'ema')
                    max_tick_ms = ConvertTo-NullableDouble (Get-MatchGroupValue -Match $candidate -Name 'max')
                    skipped_ticks = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'skipped')
                    brain_steps = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'brain')
                    mover_steps = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'mover')
                }
            }
        }
        if ($clean -cmatch '^Runtime metrics:') {
            $runtimeMetricsLineCount++
            $candidate = [regex]::Match(
                $clean,
                '^Runtime metrics: brain_steps=(?<brain>[0-9]+), mover_steps=(?<mover>[0-9]+), errors=(?<errors>[0-9]+)$')
            if (-not $candidate.Success) {
                $runtimeMetricsMalformed = $true
                continue
            }

            $brainSteps = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'brain')
            $moverSteps = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'mover')
            $runtimeErrors = ConvertTo-NullableInt64 (Get-MatchGroupValue -Match $candidate -Name 'errors')
            if ($null -eq $brainSteps -or $null -eq $moverSteps -or $null -eq $runtimeErrors) {
                $runtimeMetricsMalformed = $true
                continue
            }

            $runtimeMetrics = [pscustomobject][ordered]@{
                brain_steps = $brainSteps
                mover_steps = $moverSteps
                errors = $runtimeErrors
            }
        }
    }

    if ($null -ne $onlineMatch -and $null -ne $offlineMatch) {
        return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'ambiguous-online-offline-response' `
            -ReportedBotId $reportedBotId -BotName $null -ObjectId $null -CommandLine $commandLine `
            -CommandCharacter $commandCharacter -MessageCount $messages.Count -ErrorCount $errors.Count
    }

    if ($null -ne $onlineMatch) {
        $onlineBotId = ConvertTo-NullableUInt32 (Get-MatchGroupValue -Match $onlineMatch -Name 'bot')
        $botName = Get-MatchGroupValue -Match $onlineMatch -Name 'name'
        $objectId = ConvertTo-NullableUInt32 (Get-MatchGroupValue -Match $onlineMatch -Name 'obj')
        if ($null -eq $onlineBotId -or $onlineBotId -ne $BotId) {
            return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'online-bot-identity-mismatch' `
                -ReportedBotId $onlineBotId -BotName $botName -ObjectId $objectId `
                -CommandLine $commandLine -CommandCharacter $commandCharacter `
                -MessageCount $messages.Count -ErrorCount $errors.Count
        }
        if ($runtimeMetricsLineCount -eq 0) {
            return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'missing-runtime-metrics' `
                -ReportedBotId $onlineBotId -BotName $botName -ObjectId $objectId `
                -CommandLine $commandLine -CommandCharacter $commandCharacter `
                -MessageCount $messages.Count -ErrorCount $errors.Count
        }
        if ($runtimeMetricsLineCount -ne 1) {
            return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'duplicate-runtime-metrics' `
                -ReportedBotId $onlineBotId -BotName $botName -ObjectId $objectId `
                -CommandLine $commandLine -CommandCharacter $commandCharacter `
                -MessageCount $messages.Count -ErrorCount $errors.Count
        }
        if ($runtimeMetricsMalformed -or $null -eq $runtimeMetrics) {
            return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'malformed-runtime-metrics' `
                -ReportedBotId $onlineBotId -BotName $botName -ObjectId $objectId `
                -CommandLine $commandLine -CommandCharacter $commandCharacter `
                -MessageCount $messages.Count -ErrorCount $errors.Count
        }
        return New-BotDebugSample -BotId $BotId -Classification 'online' -Online $true `
            -ReportedBotId $onlineBotId -BotName $botName -ObjectId $objectId `
            -CommandLine $commandLine -CommandCharacter $commandCharacter `
            -MessageCount $messages.Count -ErrorCount $errors.Count -HostMetrics $hostMetrics `
            -RuntimeMetrics $runtimeMetrics `
            -Diagnostic $null -StatusCode $null
    }

    if ($null -ne $offlineMatch) {
        $offlineBotId = ConvertTo-NullableUInt32 (Get-MatchGroupValue -Match $offlineMatch -Name 'bot')
        if ($null -eq $offlineBotId) {
            $offlineBotId = $reportedBotId
        }
        if ($null -ne $offlineBotId -and $offlineBotId -ne $BotId) {
            return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'offline-bot-identity-mismatch' `
                -ReportedBotId $offlineBotId -BotName $null -ObjectId $null `
                -CommandLine $commandLine -CommandCharacter $commandCharacter `
                -MessageCount $messages.Count -ErrorCount $errors.Count
        }
        return New-BotDebugSample -BotId $BotId -Classification 'offline' -Online $false `
            -ReportedBotId $offlineBotId -BotName $null -ObjectId $null `
            -CommandLine $commandLine -CommandCharacter $commandCharacter `
            -MessageCount $messages.Count -ErrorCount $errors.Count -HostMetrics $null `
            -RuntimeMetrics $null `
            -Diagnostic $null -StatusCode $null
    }

    return New-MalformedBotDebugSample -BotId $BotId -Diagnostic 'unrecognized-response-shape' `
        -ReportedBotId $reportedBotId -BotName $null -ObjectId $null `
        -CommandLine $commandLine -CommandCharacter $commandCharacter `
        -MessageCount $messages.Count -ErrorCount $errors.Count
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes)

    $hash = [System.Security.Cryptography.SHA256]::HashData($Bytes)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}

function Write-NewBytes {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes
    )

    $stream = [System.IO.FileStream]::new(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $stream.Write($Bytes, 0, $Bytes.Count)
    }
    finally {
        $stream.Dispose()
    }
}

function Write-NewJson {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$Value,
        [int]$Depth = 20
    )

    $json = ($Value | ConvertTo-Json -Depth $Depth).Replace("`r`n", "`n") + "`n"
    $encoding = [System.Text.UTF8Encoding]::new($false)
    $stream = [System.IO.FileStream]::new(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $writer = [System.IO.StreamWriter]::new($stream, $encoding)
        try {
            $writer.Write($json)
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

function Resolve-BotDebugEndpoint {
    param([Parameter(Mandatory)][string]$ApiBase)

    $uri = $null
    if (-not [Uri]::TryCreate($ApiBase, [UriKind]::Absolute, [ref]$uri)) {
        throw 'ApiBase must be an absolute URI.'
    }
    if ($uri.Scheme -cne 'http' -or $uri.Host -notin @('127.0.0.1', 'localhost', '::1')) {
        throw 'ApiBase must use HTTP on an explicit loopback host.'
    }
    if (-not [string]::IsNullOrEmpty($uri.Query) -or -not [string]::IsNullOrEmpty($uri.Fragment) -or
        -not [string]::IsNullOrEmpty($uri.UserInfo)) {
        throw 'ApiBase must not contain user information, a query, or a fragment.'
    }

    $basePath = $uri.AbsolutePath.TrimEnd('/')
    if ($basePath -notin @('', '/api')) {
        throw "ApiBase path must be empty or '/api'."
    }
    $base = $ApiBase.TrimEnd('/')
    if ([string]::IsNullOrEmpty($basePath)) {
        $base += '/api'
    }
    return [Uri]("$base/commands/botdebug")
}

function New-TransportErrorSample {
    param(
        [Parameter(Mandatory)][uint32]$BotId,
        [Parameter(Mandatory)][string]$Diagnostic,
        [AllowNull()][object]$StatusCode
    )

    return New-BotDebugSample -BotId $BotId -Classification 'transport-error' -Online $null `
        -ReportedBotId $null -BotName $null -ObjectId $null -CommandLine $null `
        -CommandCharacter $null -MessageCount 0 -ErrorCount 0 -HostMetrics $null `
        -RuntimeMetrics $null `
        -Diagnostic $Diagnostic -StatusCode $StatusCode
}

function Start-AutonomyObserver {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateRange(1, [uint32]::MaxValue)][uint32]$BotId,
        [Parameter(Mandatory)][string]$ApiBase,
        [Parameter(Mandatory)][string]$OutputPath,
        [ValidateRange(10, 60000)][int]$SampleIntervalMilliseconds = 1000,
        [ValidateRange(1, 120)][int]$TimeoutSeconds = 10,
        [ValidateRange(0, [int]::MaxValue)][int]$MaximumSamples = 0
    )

    $endpoint = Resolve-BotDebugEndpoint -ApiBase $ApiBase
    $root = [System.IO.Path]::GetFullPath($OutputPath)
    if (Test-Path -LiteralPath $root) {
        throw "OutputPath must be a new path; existing content is retained: $root"
    }

    [void](New-Item -ItemType Directory -Path $root -ErrorAction Stop)
    $rawRoot = Join-Path $root 'raw'
    $derivedRoot = Join-Path $root 'derived'
    $transportRoot = Join-Path $root 'transport'
    $boundaryRoot = Join-Path $root 'boundaries'
    foreach ($directory in @($rawRoot, $derivedRoot, $transportRoot, $boundaryRoot)) {
        [void](New-Item -ItemType Directory -Path $directory -ErrorAction Stop)
    }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $handler.UseProxy = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    $requestBody = '{"character":"@system","arguments":"' +
        $BotId.ToString([System.Globalization.CultureInfo]::InvariantCulture) + '"}'
    $requestEncoding = [System.Text.UTF8Encoding]::new($false)

    $sampleIndex = 0
    $offlineSuccesses = 0
    $armed = $false
    $live = $false
    try {
        while ($MaximumSamples -eq 0 -or $sampleIndex -lt $MaximumSamples) {
            $capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
            $statusCode = $null
            $responseBytes = $null
            $transportDiagnostic = $null
            try {
                $request = [System.Net.Http.HttpRequestMessage]::new(
                    [System.Net.Http.HttpMethod]::Post,
                    $endpoint)
                try {
                    $request.Content = [System.Net.Http.StringContent]::new(
                        $requestBody,
                        $requestEncoding,
                        'application/json')
                    $response = $client.Send($request)
                    try {
                        $statusCode = [int]$response.StatusCode
                        $responseBytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                        if (-not $response.IsSuccessStatusCode) {
                            $transportDiagnostic = "http-status-$statusCode"
                        }
                    }
                    finally {
                        $response.Dispose()
                    }
                }
                finally {
                    $request.Dispose()
                }
            }
            catch {
                $transportDiagnostic = "network-error:$($_.Exception.GetType().FullName)"
            }

            $stem = $sampleIndex.ToString('D6', [System.Globalization.CultureInfo]::InvariantCulture) + '-botdebug'
            $rawRelativePath = $null
            $rawLength = 0
            $rawSha256 = $null
            if ($null -ne $responseBytes) {
                $rawName = "$stem.response.bin"
                $rawPath = Join-Path $rawRoot $rawName
                Write-NewBytes -Path $rawPath -Bytes $responseBytes
                $rawRelativePath = "raw/$rawName"
                $rawLength = $responseBytes.Count
                $rawSha256 = Get-Sha256Hex -Bytes $responseBytes
            }

            $transport = [pscustomobject][ordered]@{
                schema_version = $script:TransportSchema
                sample_index = $sampleIndex
                captured_at_utc = $capturedAtUtc
                command = 'botdebug'
                bot_id = $BotId
                endpoint = $endpoint.AbsoluteUri
                status_code = $statusCode
                response_received = ($null -ne $responseBytes)
                raw_path = $rawRelativePath
                raw_length = $rawLength
                raw_sha256 = $rawSha256
                diagnostic = $transportDiagnostic
            }
            $transportName = "$stem.transport.json"
            Write-NewJson -Path (Join-Path $transportRoot $transportName) -Value $transport

            if ($null -ne $transportDiagnostic) {
                $sample = New-TransportErrorSample -BotId $BotId -Diagnostic $transportDiagnostic -StatusCode $statusCode
            }
            else {
                $sample = ConvertFrom-AutonomyBotDebugResponse -BotId $BotId -ResponseBytes $responseBytes
                $sample.status_code = $statusCode
            }
            $sample | Add-Member -NotePropertyName sample_index -NotePropertyValue $sampleIndex
            $sample | Add-Member -NotePropertyName captured_at_utc -NotePropertyValue $capturedAtUtc
            $sample | Add-Member -NotePropertyName raw -NotePropertyValue ([pscustomobject][ordered]@{
                path = $rawRelativePath
                length = $rawLength
                sha256 = $rawSha256
            })
            $derivedName = "$stem.sample.json"
            Write-NewJson -Path (Join-Path $derivedRoot $derivedName) -Value $sample

            if ($sample.classification -ceq 'offline') {
                $offlineSuccesses++
                if (-not $armed) {
                    $boundary = [pscustomobject][ordered]@{
                        schema_version = $script:BoundarySchema
                        boundary = 'armed'
                        bot_id = $BotId
                        sample_index = $sampleIndex
                        classification = $sample.classification
                        online = $sample.online
                        raw_path = $rawRelativePath
                        raw_sha256 = $rawSha256
                        derived_path = "derived/$derivedName"
                    }
                    Write-NewJson -Path (Join-Path $boundaryRoot 'armed.json') -Value $boundary
                    $armed = $true
                }
                elseif (-not $live -and $offlineSuccesses -ge 2) {
                    $boundary = [pscustomobject][ordered]@{
                        schema_version = $script:BoundarySchema
                        boundary = 'live'
                        bot_id = $BotId
                        sample_index = $sampleIndex
                        classification = $sample.classification
                        online = $sample.online
                        raw_path = $rawRelativePath
                        raw_sha256 = $rawSha256
                        derived_path = "derived/$derivedName"
                    }
                    Write-NewJson -Path (Join-Path $boundaryRoot 'live.json') -Value $boundary
                    $live = $true
                }
            }

            $sampleIndex++
            if ($MaximumSamples -eq 0 -or $sampleIndex -lt $MaximumSamples) {
                Start-Sleep -Milliseconds $SampleIntervalMilliseconds
            }
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }

    return [pscustomobject][ordered]@{
        schema_version = 'playerbots.autonomy-observer-result.v1'
        output_path = $root
        bot_id = $BotId
        samples = $sampleIndex
        armed = $armed
        live = $live
    }
}

Export-ModuleMember -Function @(
    'ConvertFrom-AutonomyBotDebugResponse',
    'Start-AutonomyObserver'
)
