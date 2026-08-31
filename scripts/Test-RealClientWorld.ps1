[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $LauncherProfile,

    [Parameter(Mandatory)]
    [string] $EvidenceDirectory,

    [Parameter(Mandatory)]
    [int] $ProcessId,

    [Parameter(Mandatory)]
    [long] $WindowHandle,

    [Parameter(Mandatory)]
    [string] $ExpectedCharacterName,

    [Parameter(Mandatory)]
    [string] $HostRoot,

    [string] $WebApiBase = 'http://127.0.0.1:1380/api',

    [int] $CharacterSlotX = 1707,

    [int] $CharacterSlotY = 222,

    [int] $StartGameX = 1802,

    [int] $StartGameY = 1014,

    [int] $ExpectedClientWidth = 1920,

    [int] $ExpectedClientHeight = 1080,

    [ValidateRange(30, 300)]
    [int] $TimeoutSeconds = 180,

    [ValidateRange(0, 1000)]
    [int] $FixtureCreationComputerUseActions = 0,

    [switch] $AllowDirtyModule
)

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\AAEmu.ClientDriver.csproj'
$driver = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\bin\Debug\net10.0\AAEmu.ClientDriver.dll'
$profilePath = [System.IO.Path]::GetFullPath($LauncherProfile)
$evidenceRoot = [System.IO.Path]::GetFullPath($EvidenceDirectory)
$hostPath = [System.IO.Path]::GetFullPath($HostRoot)
$embeddedModulePath = Join-Path $hostPath 'modules\archeage-playerbots'
$runId = '{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [Guid]::NewGuid().ToString('N')
$summaryPath = Join-Path $evidenceRoot "real-client-world-$runId.json"
$selectionAuditPath = Join-Path $evidenceRoot "real-client-world-selection-$runId.jsonl"
$gameplayAuditPath = Join-Path $evidenceRoot "real-client-world-gameplay-$runId.jsonl"
$selectionCapturePath = Join-Path $evidenceRoot "real-client-world-selection-$runId.bmp"
$gameplayCapturePath = Join-Path $evidenceRoot "real-client-world-gameplay-$runId.bmp"
$startedAtUtc = [DateTimeOffset]::UtcNow
$profile = $null
$moduleIdentity = $null
$hostIdentity = $null
$embeddedModuleIdentity = $null
$statusBefore = $null
$statusAfter = $null
$charactersBefore = $null
$charactersAfter = $null
$selectionCapture = $null
$gameplayCapture = $null
$selectionAudit = $null
$gameplayAudit = $null
$metricsCommand = $null
$metrics = $null

function Write-NewUtf8File {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Content
    )

    $stream = [System.IO.FileStream]::new(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::Read)
    try {
        $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
        try {
            $writer.Write($Content)
            $writer.Flush()
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function New-DriverStartInfo {
    param([string[]] $Arguments)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet'
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }
    return $startInfo
}

function Invoke-BoundedJsonProcess {
    param(
        [Parameter(Mandatory)] [string[]] $Arguments,
        [int] $TimeoutMilliseconds = 15000
    )

    $process = [System.Diagnostics.Process]::Start((New-DriverStartInfo -Arguments $Arguments))
    if ($null -eq $process) {
        throw 'The client-driver subprocess did not start.'
    }
    try {
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            throw 'A client-driver subprocess exceeded its bounded wait and was left running rather than force-terminated.'
        }
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        if ($process.ExitCode -ne 0) {
            throw "A client-driver subprocess exited with code $($process.ExitCode): $stderr"
        }
        if ([string]::IsNullOrWhiteSpace($stdout)) {
            throw "A client-driver subprocess returned no JSON: $stderr"
        }
        return $stdout | ConvertFrom-Json
    }
    finally {
        $process.Dispose()
    }
}

function Get-Sha256Text {
    param([string[]] $Lines)

    $text = $Lines -join "`n"
    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
}

function Get-GitIdentity {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [switch] $IncludeUntracked
    )

    if (-not (Test-Path -LiteralPath (Join-Path $Path '.git'))) {
        throw "Git identity root is missing: $Path"
    }
    $branch = (& git -C $Path rev-parse --abbrev-ref HEAD 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Could not read the Git branch for $Path`: $branch" }
    $head = (& git -C $Path rev-parse HEAD 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Could not read the Git HEAD for $Path`: $head" }
    $statusArguments = @('-C', $Path, 'status', '--porcelain=v1')
    if (-not $IncludeUntracked) { $statusArguments += '--untracked-files=no' }
    $status = @(& git @statusArguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Could not read Git status for $Path`." }
    $trackedDiff = @(& git -C $Path diff --no-ext-diff --binary 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Could not fingerprint the tracked diff for $Path`." }
    return [ordered]@{
        path = $Path
        branch = $branch.Trim()
        head = $head.Trim()
        clean = $status.Count -eq 0
        status = $status
        trackedDiffSha256 = Get-Sha256Text -Lines $trackedDiff
    }
}

function Get-ClientStatus {
    return Invoke-BoundedJsonProcess -Arguments @(
        $driver,
        'status',
        '--log', [string]$profile.logPath,
        '--process-name', [string]$profile.processName)
}

function Assert-ExactClientStatus {
    param([Parameter(Mandatory)] $Status)

    $instances = @($Status.process.instances)
    if (-not $Status.process.running -or $instances.Count -ne 1) {
        throw "Expected exactly one running $($profile.processName) process, observed $($instances.Count)."
    }
    if ($instances[0].processId -ne $ProcessId -or $instances[0].mainWindowHandle -ne $WindowHandle) {
        throw 'The client status no longer identifies the exact requested PID and main-window handle.'
    }
}

function Start-InputLease {
    param(
        [Parameter(Mandatory)] [string] $AuditPath,
        [Parameter(Mandatory)] [int] $MaxActions
    )

    $portProbe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $portProbe.Start()
    try {
        $inputPort = ([System.Net.IPEndPoint]$portProbe.LocalEndpoint).Port
    }
    finally {
        $portProbe.Stop()
    }

    $startInfo = New-DriverStartInfo -Arguments @(
        $driver,
        'serve-input',
        '--profile', $profilePath,
        '--process-id', [string]$ProcessId,
        '--window-handle', [string]$WindowHandle,
        '--audit', $AuditPath,
        '--port', [string]$inputPort,
        '--lease-ttl-ms', '30000',
        '--max-actions', [string]$MaxActions)
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw 'The guarded-input server did not start.' }

    $startupTask = $process.StandardOutput.ReadLineAsync()
    if (-not $startupTask.Wait(10000)) {
        $process.Dispose()
        throw 'The guarded-input server did not issue a lease within 10 seconds.'
    }
    $startupLine = $startupTask.Result
    if ([string]::IsNullOrWhiteSpace($startupLine)) {
        $errorText = $process.StandardError.ReadToEnd()
        $process.Dispose()
        throw "The guarded-input server exited before issuing a lease: $errorText"
    }
    $lease = $startupLine | ConvertFrom-Json
    if ($lease.target.processId -ne $ProcessId -or
        $lease.target.windowHandle -ne $WindowHandle -or
        $lease.target.executableSha256 -ne $profile.clientExecutableSha256 -or
        $lease.maxActions -ne $MaxActions -or
        $lease.rawTypedTextAudited) {
        $process.Dispose()
        throw 'The guarded-input lease did not preserve the exact-target, cap, and no-raw-text contract.'
    }

    $client = [System.Net.Http.HttpClient]::new()
    $client.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', [string]$lease.leaseToken)
    return [pscustomobject]@{
        Process = $process
        Lease = $lease
        Port = $inputPort
        Client = $client
    }
}

function Invoke-InputAction {
    param(
        [Parameter(Mandatory)] $InputLease,
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Body
    )

    $content = [System.Net.Http.StringContent]::new($Body, [Text.Encoding]::UTF8, 'application/json')
    $response = $null
    try {
        $response = $InputLease.Client.PostAsync(
            "http://127.0.0.1:$($InputLease.Port)$Path", $content).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ([int]$response.StatusCode -ne 200) {
            throw "Guarded input $Path returned HTTP $([int]$response.StatusCode): $responseBody"
        }
        $result = $responseBody | ConvertFrom-Json
        if (-not $result.accepted) { throw "Guarded input $Path was not accepted." }
        return $result
    }
    finally {
        $content.Dispose()
        if ($null -ne $response) { $response.Dispose() }
    }
}

function Complete-InputLease {
    param(
        [Parameter(Mandatory)] $InputLease,
        [Parameter(Mandatory)] [int] $ExpectedActions
    )

    try {
        if (-not $InputLease.Process.WaitForExit(35000)) {
            throw 'The guarded-input lease exceeded its bounded wait and was left running rather than force-terminated.'
        }
        $stdout = $InputLease.Process.StandardOutput.ReadToEnd()
        $stderr = $InputLease.Process.StandardError.ReadToEnd()
        if ($InputLease.Process.ExitCode -ne 0 -or
            $stdout -notmatch '"stopReason":"max_actions"' -or
            $stdout -notmatch ('"actionCount":' + $ExpectedActions)) {
            throw "The guarded-input lease did not end at its exact action cap: $stderr $stdout"
        }
    }
    finally {
        $InputLease.Client.Dispose()
        $InputLease.Process.Dispose()
    }
}

function Capture-ExactClient {
    param([Parameter(Mandatory)] [string] $OutputPath)

    $capture = Invoke-BoundedJsonProcess -Arguments @(
        $driver,
        'capture-window',
        '--profile', $profilePath,
        '--process-id', [string]$ProcessId,
        '--window-handle', [string]$WindowHandle,
        '--output', $OutputPath)
    if ($capture.status -ne 'captured' -or
        $capture.target.processId -ne $ProcessId -or
        $capture.target.windowHandle -ne $WindowHandle -or
        $capture.target.executableSha256 -ne $profile.clientExecutableSha256 -or
        $capture.width -ne $ExpectedClientWidth -or
        $capture.height -ne $ExpectedClientHeight -or
        $capture.occlusionSampleCount -ne 5 -or
        -not (Test-Path -LiteralPath $OutputPath) -or
        (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash -ne $capture.outputSha256) {
        throw 'The verified capture did not preserve exact identity, dimensions, occlusion sampling, file, and hash.'
    }
    return $capture
}

function Get-ExpectedCharacter {
    param([Parameter(Mandatory)] $Characters)

    $matches = @($Characters | Where-Object { $_.Name -ceq $ExpectedCharacterName })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one server character named '$ExpectedCharacterName', observed $($matches.Count)."
    }
    return $matches[0]
}

try {
    if (-not (Test-Path -LiteralPath $profilePath -PathType Leaf)) {
        throw "The launch profile does not exist: $profilePath"
    }
    if (-not (Test-Path -LiteralPath $hostPath -PathType Container)) {
        throw "The retained host root does not exist: $hostPath"
    }
    if ($ProcessId -le 0 -or $WindowHandle -le 0) {
        throw 'ProcessId and WindowHandle must be positive exact identifiers.'
    }
    if ([string]::IsNullOrWhiteSpace($ExpectedCharacterName)) {
        throw 'ExpectedCharacterName cannot be empty.'
    }
    if ($ExpectedClientWidth -le 0 -or $ExpectedClientHeight -le 0) {
        throw 'Expected client dimensions must be positive.'
    }
    foreach ($point in @(
        @($CharacterSlotX, $CharacterSlotY),
        @($StartGameX, $StartGameY))) {
        if ($point[0] -lt 0 -or $point[0] -ge $ExpectedClientWidth -or
            $point[1] -lt 0 -or $point[1] -ge $ExpectedClientHeight) {
            throw 'A requested click coordinate falls outside the expected physical client rectangle.'
        }
    }

    $apiUri = [Uri]$WebApiBase
    if ($apiUri.Scheme -ne 'http' -or $apiUri.Host -notin @('127.0.0.1', 'localhost', '::1')) {
        throw 'The scenario permits only a loopback HTTP AAEmu Web API endpoint.'
    }
    $apiBase = $WebApiBase.TrimEnd('/')
    if (-not (Test-Path -LiteralPath $evidenceRoot)) {
        $null = New-Item -ItemType Directory -Path $evidenceRoot
    }

    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    if ($profile.schemaVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$profile.logPath) -or
        [string]::IsNullOrWhiteSpace([string]$profile.processName)) {
        throw 'The launch profile is missing its versioned log/process contract.'
    }

    & dotnet build $project --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Client driver build failed.' }
    $verifiedProfile = Invoke-BoundedJsonProcess -Arguments @($driver, 'verify-profile', '--profile', $profilePath)
    if ($verifiedProfile.client.sha256 -ne $profile.clientExecutableSha256) {
        throw 'The verified profile did not return the pinned client hash.'
    }

    $moduleIdentity = Get-GitIdentity -Path $moduleRoot -IncludeUntracked
    if (-not $AllowDirtyModule -and -not $moduleIdentity.clean) {
        throw 'The canonical module worktree is dirty; authoritative scenario evidence requires a clean source identity.'
    }
    $hostIdentity = Get-GitIdentity -Path $hostPath -IncludeUntracked
    if (Test-Path -LiteralPath (Join-Path $embeddedModulePath '.git')) {
        $embeddedModuleIdentity = Get-GitIdentity -Path $embeddedModulePath -IncludeUntracked
    }

    $statusBefore = Get-ClientStatus
    Assert-ExactClientStatus -Status $statusBefore
    $charactersBefore = @(Invoke-RestMethod -Method Get -Uri "$apiBase/character/list")
    $expectedBefore = Get-ExpectedCharacter -Characters $charactersBefore
    if ($expectedBefore.IsOnline) {
        throw "The expected character '$ExpectedCharacterName' is already online; no offline-to-online transition can be proven."
    }

    $selectionLease = Start-InputLease -AuditPath $selectionAuditPath -MaxActions 3
    try {
        $focus = Invoke-InputAction -InputLease $selectionLease -Path '/v1/focus' -Body '{}'
        if (-not $focus.target.foreground) { throw 'The exact character-selection window did not become foreground.' }
        $selectionCapture = Capture-ExactClient -OutputPath $selectionCapturePath
        $slotClick = Invoke-InputAction -InputLease $selectionLease -Path '/v1/click-client' -Body (
            @{ x = $CharacterSlotX; y = $CharacterSlotY } | ConvertTo-Json -Compress)
        Start-Sleep -Milliseconds 750
        $startClick = Invoke-InputAction -InputLease $selectionLease -Path '/v1/click-client' -Body (
            @{ x = $StartGameX; y = $StartGameY } | ConvertTo-Json -Compress)
        if ($slotClick.detail.clientX -ne $CharacterSlotX -or $slotClick.detail.clientY -ne $CharacterSlotY -or
            $startClick.detail.clientX -ne $StartGameX -or $startClick.detail.clientY -ne $StartGameY) {
            throw 'The guarded input response did not preserve the requested physical click coordinates.'
        }
    }
    finally {
        Complete-InputLease -InputLease $selectionLease -ExpectedActions 3
    }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $transitionProved = $false
    $lastObservationError = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 750
        try {
            $statusAfter = Get-ClientStatus
            Assert-ExactClientStatus -Status $statusAfter
            $charactersAfter = @(Invoke-RestMethod -Method Get -Uri "$apiBase/character/list")
            $expectedAfter = Get-ExpectedCharacter -Characters $charactersAfter
            $freshAuthorized = $null -ne $statusAfter.log.milestones.worldAuthorized -and
                $statusAfter.log.milestones.worldAuthorized -ne $statusBefore.log.milestones.worldAuthorized
            $freshLoaded = $null -ne $statusAfter.log.milestones.worldLoaded -and
                $statusAfter.log.milestones.worldLoaded -ne $statusBefore.log.milestones.worldLoaded
            $sameLogSession = $statusAfter.log.sessionStartedAt -eq $statusBefore.log.sessionStartedAt
            $logAdvanced = $statusAfter.log.bytes -gt $statusBefore.log.bytes -and
                ([DateTimeOffset]$statusAfter.log.lastWriteUtc) -gt ([DateTimeOffset]$statusBefore.log.lastWriteUtc)
            if ($sameLogSession -and $logAdvanced -and $freshAuthorized -and $freshLoaded -and
                $statusAfter.state -eq 'world_loaded' -and $expectedAfter.IsOnline) {
                $transitionProved = $true
                break
            }
        }
        catch {
            $lastObservationError = $_.Exception.Message
        }
    }
    if (-not $transitionProved) {
        throw "The exact client did not prove fresh world authorization/loading plus server offline-to-online within $TimeoutSeconds seconds. Last observation error: $lastObservationError"
    }

    $requestBody = @{
        character = '@system'
        arguments = 'snapshot'
    } | ConvertTo-Json -Compress
    $metricsCommand = Invoke-RestMethod -Method Post -Uri "$apiBase/commands/botmetrics" `
        -ContentType 'application/json' -Body $requestBody
    $commandErrors = @($metricsCommand.ErrorMessages)
    if ($commandErrors.Count -ne 0 -or $metricsCommand.commandCharacter -ne '@system') {
        throw "The authoritative botmetrics command failed: $($commandErrors -join '; ')"
    }
    $metricsLine = @($metricsCommand.Messages | Where-Object { $_ -match 'T021_METRICS\s+\{' }) |
        Select-Object -First 1
    if (-not $metricsLine) { throw 'The botmetrics response did not contain a T021_METRICS document.' }
    $metrics = $metricsLine.Substring($metricsLine.IndexOf('{')) | ConvertFrom-Json
    if ($metrics.schemaVersion -ne 't021.scale-metrics.v1') {
        throw "Unexpected botmetrics schema '$($metrics.schemaVersion)'."
    }

    $gameplayLease = Start-InputLease -AuditPath $gameplayAuditPath -MaxActions 1
    try {
        $gameplayFocus = Invoke-InputAction -InputLease $gameplayLease -Path '/v1/focus' -Body '{}'
        if (-not $gameplayFocus.target.foreground) { throw 'The exact gameplay window did not become foreground.' }
        $gameplayCapture = Capture-ExactClient -OutputPath $gameplayCapturePath
    }
    finally {
        Complete-InputLease -InputLease $gameplayLease -ExpectedActions 1
    }

    $selectionAudit = @(Get-Content -LiteralPath $selectionAuditPath | ForEach-Object { $_ | ConvertFrom-Json })
    $gameplayAudit = @(Get-Content -LiteralPath $gameplayAuditPath | ForEach-Object { $_ | ConvertFrom-Json })
    if ($selectionAudit.Count -ne 3 -or @($selectionAudit | Where-Object accepted).Count -ne 3 -or
        ($selectionAudit.action -join ',') -ne 'focus,click-client,click-client' -or
        $gameplayAudit.Count -ne 1 -or -not $gameplayAudit[0].accepted -or
        $gameplayAudit[0].action -ne 'focus') {
        throw 'The retained input audits do not contain the exact bounded character-entry and gameplay-capture sequences.'
    }

    $expectedAfter = Get-ExpectedCharacter -Characters $charactersAfter
    $summary = [ordered]@{
        schemaVersion = 'playerbots.real-client-world.v1'
        runId = $runId
        verdict = 'pass'
        startedAtUtc = $startedAtUtc
        completedAtUtc = [DateTimeOffset]::UtcNow
        source = [ordered]@{
            module = $moduleIdentity
            retainedHost = $hostIdentity
            embeddedModule = $embeddedModuleIdentity
        }
        profile = $verifiedProfile
        target = [ordered]@{
            processId = $ProcessId
            windowHandle = $WindowHandle
            executablePath = $selectionCapture.target.executablePath
            executableSha256 = $selectionCapture.target.executableSha256
            width = $selectionCapture.width
            height = $selectionCapture.height
        }
        actions = [ordered]@{
            characterSlot = [ordered]@{ x = $CharacterSlotX; y = $CharacterSlotY }
            startGame = [ordered]@{ x = $StartGameX; y = $StartGameY }
            selectionAuditPath = $selectionAuditPath
            gameplayAuditPath = $gameplayAuditPath
            scenarioComputerUseActions = 0
            fixtureCreationComputerUseActions = $FixtureCreationComputerUseActions
        }
        lifecycle = [ordered]@{
            before = $statusBefore
            after = $statusAfter
            freshWorldAuthorized = $true
            freshWorldLoaded = $true
            logBytesAdvanced = $statusAfter.log.bytes -gt $statusBefore.log.bytes
        }
        server = [ordered]@{
            endpoint = $apiBase
            characterBefore = $expectedBefore
            characterAfter = $expectedAfter
            offlineToOnline = (-not $expectedBefore.IsOnline) -and $expectedAfter.IsOnline
            botmetricsCommand = $metricsCommand
            botmetrics = $metrics
        }
        captures = [ordered]@{
            characterSelection = $selectionCapture
            gameplayWorld = $gameplayCapture
        }
        safety = [ordered]@{
            exactProcessWindowPathAndHash = $true
            loopbackOnly = $true
            credentialsReadOrPersisted = $false
            arbitraryTextInput = $false
            injectionOrMemoryEditing = $false
            fabricatedCharacterOrWorldState = $false
            forcedTermination = $false
            evidenceOverwrite = $false
        }
    }
    $summaryJson = $summary | ConvertTo-Json -Depth 40
    Write-NewUtf8File -Path $summaryPath -Content $summaryJson
    $summaryJson
}
catch {
    $failure = [ordered]@{
        schemaVersion = 'playerbots.real-client-world.v1'
        runId = $runId
        verdict = 'fail'
        startedAtUtc = $startedAtUtc
        failedAtUtc = [DateTimeOffset]::UtcNow
        reason = $_.Exception.Message
        source = [ordered]@{
            module = $moduleIdentity
            retainedHost = $hostIdentity
            embeddedModule = $embeddedModuleIdentity
        }
        target = [ordered]@{ processId = $ProcessId; windowHandle = $WindowHandle }
        lifecycle = [ordered]@{ before = $statusBefore; last = $statusAfter }
        server = [ordered]@{ charactersBefore = $charactersBefore; lastCharacters = $charactersAfter }
        retainedEvidence = [ordered]@{
            summaryPath = $summaryPath
            selectionAuditPath = $selectionAuditPath
            gameplayAuditPath = $gameplayAuditPath
            selectionCapturePath = $selectionCapturePath
            gameplayCapturePath = $gameplayCapturePath
        }
        safety = [ordered]@{
            scenarioComputerUseActions = 0
            credentialsReadOrPersisted = $false
            injectionOrMemoryEditing = $false
            forcedTermination = $false
            evidenceOverwrite = $false
        }
    }
    if (Test-Path -LiteralPath $evidenceRoot) {
        Write-NewUtf8File -Path $summaryPath -Content ($failure | ConvertTo-Json -Depth 40)
    }
    throw
}
