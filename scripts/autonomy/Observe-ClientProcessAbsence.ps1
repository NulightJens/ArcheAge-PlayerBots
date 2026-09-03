[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][string]$StopSentinel,
    [ValidateRange(10, 60000)][int]$SampleIntervalMilliseconds = 500,
    [string[]]$ProcessName = @('archeage', 'archeageclient', 'game_pak', 'cryengine')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$zeroHash = '0' * 64

function Get-Sha256HexFromBytes {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $algorithm.ComputeHash($Bytes)
    }
    finally {
        $algorithm.Dispose()
    }

    return -join @($hashBytes | ForEach-Object { $_.ToString('x2') })
}

function Get-Sha256HexFromFile {
    param([Parameter(Mandatory)][string]$LiteralPath)

    $stream = [System.IO.File]::Open(
        $LiteralPath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $algorithm.ComputeHash($stream)
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }

    return -join @($hashBytes | ForEach-Object { $_.ToString('x2') })
}

function Write-AtomicBytes {
    param(
        [Parameter(Mandatory)][string]$FinalPath,
        [Parameter(Mandatory)][byte[]]$Bytes
    )

    $directory = Split-Path -Parent $FinalPath
    $pendingPath = Join-Path $directory ('.pending-{0}' -f [Guid]::NewGuid().ToString('N'))
    $stream = [System.IO.File]::Open(
        $pendingPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }

    [System.IO.File]::Move($pendingPath, $FinalPath)
}

function Write-AtomicJson {
    param(
        [Parameter(Mandatory)][string]$FinalPath,
        [Parameter(Mandatory)][object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 10 -Compress
    Write-AtomicBytes -FinalPath $FinalPath -Bytes $utf8NoBom.GetBytes($json)
}

function Get-ObservedProcessIdentities {
    param([Parameter(Mandatory)][string[]]$Names)

    $processes = @(Get-Process -ErrorAction Stop | Where-Object { $Names -contains $_.ProcessName })
    $identities = @(
        $processes |
            Sort-Object -Property ProcessName, Id |
            ForEach-Object {
                [ordered]@{
                    name = [string]$_.ProcessName
                    processId = [int64]$_.Id
                }
            }
    )
    return $identities
}

function Test-StopRequested {
    param([Parameter(Mandatory)][string]$LiteralPath)
    return Test-Path -LiteralPath $LiteralPath -PathType Leaf
}

$outputFullPath = $null
$stopFullPath = $null
$outputCreated = $false
$ledgerStream = $null
$ledgerWriter = $null
$sequence = 0
$exitCode = 2

try {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        throw 'OutputPath must be an explicit non-empty path.'
    }
    if ([string]::IsNullOrWhiteSpace($StopSentinel)) {
        throw 'StopSentinel must be an explicit non-empty path.'
    }

    $outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
    $stopFullPath = [System.IO.Path]::GetFullPath($StopSentinel)
    if ([string]::Equals($outputFullPath, $stopFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputPath and StopSentinel must be different paths.'
    }
    if (Test-Path -LiteralPath $outputFullPath) {
        throw "Refusing existing output path: $outputFullPath"
    }

    $outputParent = Split-Path -Parent $outputFullPath
    if ([string]::IsNullOrWhiteSpace($outputParent) -or
        -not (Test-Path -LiteralPath $outputParent -PathType Container)) {
        throw "OutputPath parent must be an existing directory: $outputParent"
    }
    $stopParent = Split-Path -Parent $stopFullPath
    if ([string]::IsNullOrWhiteSpace($stopParent) -or
        -not (Test-Path -LiteralPath $stopParent -PathType Container)) {
        throw "StopSentinel parent must be an existing directory: $stopParent"
    }
    if (Test-Path -LiteralPath $stopFullPath) {
        throw "Refusing a pre-existing stop sentinel: $stopFullPath"
    }

    $requestedNames = @($ProcessName)
    if ($requestedNames.Count -eq 0) {
        throw 'ProcessName must contain at least one exact process base name.'
    }
    $normalizedNames = @()
    foreach ($requestedName in $requestedNames) {
        if ($null -eq $requestedName) {
            throw 'ProcessName entries cannot be null.'
        }
        $trimmedName = $requestedName.Trim()
        if ($trimmedName -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$') {
            throw "Invalid process base name '$requestedName'. Use only letters, digits, underscore, or hyphen without an extension or wildcard."
        }
        if ($normalizedNames -contains $trimmedName) {
            throw "Duplicate process base name '$trimmedName'."
        }
        $normalizedNames += $trimmedName
    }

    [void][System.IO.Directory]::CreateDirectory($outputFullPath)
    $outputCreated = $true
    $rawDirectory = Join-Path $outputFullPath 'raw'
    [void][System.IO.Directory]::CreateDirectory($rawDirectory)

    $ledgerPath = Join-Path $outputFullPath 'samples.jsonl'
    $ledgerStream = [System.IO.FileStream]::new(
        $ledgerPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::Read)
    $ledgerWriter = [System.IO.StreamWriter]::new($ledgerStream, $utf8NoBom, 4096, $true)

    $firstTimestamp = $null
    $lastTimestamp = $null
    $maximumGapMilliseconds = 0.0
    $nonzeroSampleCount = 0
    $previousRowHash = $zeroHash
    $stopRequested = $false

    while (-not $stopRequested) {
        $capturedAt = [DateTime]::UtcNow
        $identities = @(Get-ObservedProcessIdentities -Names $normalizedNames)
        if ($identities.Count -gt 0) {
            $nonzeroSampleCount++
        }

        $rawRelativePath = 'raw/{0:D6}.json' -f $sequence
        $rawPath = Join-Path $rawDirectory ('{0:D6}.json' -f $sequence)
        $rawPayload = [ordered]@{
            schemaVersion = 'playerbots.client-process-snapshot.v1'
            capturedAtUtc = $capturedAt.ToString('o')
            sequence = $sequence
            processNames = @($normalizedNames)
            processCount = $identities.Count
            processIdentities = @($identities)
        }
        $rawJson = $rawPayload | ConvertTo-Json -Depth 10 -Compress
        $rawBytes = $utf8NoBom.GetBytes($rawJson)
        Write-AtomicBytes -FinalPath $rawPath -Bytes $rawBytes

        $rawInfo = Get-Item -LiteralPath $rawPath -ErrorAction Stop
        $rawSha256 = Get-Sha256HexFromFile -LiteralPath $rawPath
        $rowMaterial = [ordered]@{
            schemaVersion = 'playerbots.client-process-ledger-row.v1'
            capturedAtUtc = $capturedAt.ToString('o')
            sequence = $sequence
            processCount = $identities.Count
            processIdentities = @($identities)
            rawRelativePath = $rawRelativePath
            rawLength = [int64]$rawInfo.Length
            rawSha256 = $rawSha256
            previousRowHash = $previousRowHash
        }
        $rowMaterialJson = $rowMaterial | ConvertTo-Json -Depth 10 -Compress
        $currentRowHash = Get-Sha256HexFromBytes -Bytes $utf8NoBom.GetBytes($rowMaterialJson)
        $ledgerRow = [ordered]@{}
        foreach ($key in $rowMaterial.Keys) {
            $ledgerRow[$key] = $rowMaterial[$key]
        }
        $ledgerRow['currentRowHash'] = $currentRowHash
        $ledgerWriter.WriteLine(($ledgerRow | ConvertTo-Json -Depth 10 -Compress))
        $ledgerWriter.Flush()
        $ledgerStream.Flush($true)

        if ($null -eq $firstTimestamp) {
            $firstTimestamp = $capturedAt
            $readyPayload = [ordered]@{
                schemaVersion = 'playerbots.client-process-observer-ready.v1'
                publishedAtUtc = [DateTime]::UtcNow.ToString('o')
                firstSampleAtUtc = $capturedAt.ToString('o')
                sequence = $sequence
                rawRelativePath = $rawRelativePath
                firstRowHash = $currentRowHash
            }
            Write-AtomicJson -FinalPath (Join-Path $outputFullPath 'ready.json') -Value $readyPayload
        }
        elseif ($null -ne $lastTimestamp) {
            $gapMilliseconds = ($capturedAt - $lastTimestamp).TotalMilliseconds
            if ($gapMilliseconds -gt $maximumGapMilliseconds) {
                $maximumGapMilliseconds = $gapMilliseconds
            }
        }

        $lastTimestamp = $capturedAt
        $previousRowHash = $currentRowHash
        $sequence++

        if (Test-StopRequested -LiteralPath $stopFullPath) {
            $stopRequested = $true
            continue
        }

        $sleepUntil = [DateTime]::UtcNow.AddMilliseconds($SampleIntervalMilliseconds)
        while ([DateTime]::UtcNow -lt $sleepUntil) {
            if (Test-StopRequested -LiteralPath $stopFullPath) {
                $stopRequested = $true
                break
            }
            $remainingMilliseconds = [int][Math]::Ceiling(($sleepUntil - [DateTime]::UtcNow).TotalMilliseconds)
            $sleepMilliseconds = [Math]::Min(50, [Math]::Max(1, $remainingMilliseconds))
            Start-Sleep -Milliseconds $sleepMilliseconds
        }
    }

    $summary = [ordered]@{
        schemaVersion = 'playerbots.client-process-observer-summary.v1'
        exit = 'cooperative-sentinel'
        sampleCount = $sequence
        firstSampleAtUtc = $firstTimestamp.ToString('o')
        lastSampleAtUtc = $lastTimestamp.ToString('o')
        maximumAdjacentGapMilliseconds = [Math]::Round($maximumGapMilliseconds, 3)
        nonzeroSampleCount = $nonzeroSampleCount
        errorCount = 0
        terminalChainHash = $previousRowHash
    }
    Write-AtomicJson -FinalPath (Join-Path $outputFullPath 'summary.json') -Value $summary
    Write-Output ($summary | ConvertTo-Json -Depth 5 -Compress)
    $exitCode = 0
}
catch {
    $failure = $_
    $message = [string]$failure.Exception.Message
    if ($message.Length -gt 500) {
        $message = $message.Substring(0, 500)
    }

    if ($outputCreated -and $null -ne $outputFullPath) {
        $errorPayload = [ordered]@{
            schemaVersion = 'playerbots.client-process-observer-error.v1'
            atUtc = [DateTime]::UtcNow.ToString('o')
            sequence = $sequence
            errorType = $failure.Exception.GetType().FullName
            message = $message
        }
        try {
            Write-AtomicJson -FinalPath (Join-Path $outputFullPath 'error.json') -Value $errorPayload
        }
        catch {
            [Console]::Error.WriteLine("Observer failure '$message'; error record also failed: $($_.Exception.Message)")
        }
    }

    [Console]::Error.WriteLine("Client-process observer failed closed: $message")
    $exitCode = 2
}
finally {
    if ($null -ne $ledgerWriter) {
        try { $ledgerWriter.Dispose() } catch { }
    }
    if ($null -ne $ledgerStream) {
        try { $ledgerStream.Dispose() } catch { }
    }
}

exit $exitCode
