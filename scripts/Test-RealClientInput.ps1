[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $LauncherProfile,

    [Parameter(Mandatory)]
    [string] $EvidenceDirectory
)

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\AAEmu.ClientDriver.csproj'
$driver = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\bin\Debug\net10.0\AAEmu.ClientDriver.dll'
$profilePath = [System.IO.Path]::GetFullPath($LauncherProfile)
$evidenceRoot = [System.IO.Path]::GetFullPath($EvidenceDirectory)
$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json

& dotnet build $project --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Client driver build failed.'
}

$existing = @(Get-Process -Name $profile.processName -ErrorAction SilentlyContinue)
if ($existing.Count -ne 0) {
    throw "Refusing an ambiguous real-client smoke while $($profile.processName) is already running."
}

$null = New-Item -ItemType Directory -Path $evidenceRoot -Force
$runId = '{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [Guid]::NewGuid().ToString('N')
$auditPath = Join-Path $evidenceRoot "real-client-input-$runId.jsonl"
$summaryPath = Join-Path $evidenceRoot "real-client-input-$runId.json"
$launcherProcess = $null
$inputServer = $null
$clientProcess = $null
$httpClient = $null
$closeCompleted = $false

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

try {
    $launchStart = New-DriverStartInfo -Arguments @(
        $driver,
        'launch',
        '--profile', $profilePath,
        '--wait-for', 'process_started',
        '--timeout-ms', '120000'
    )
    $launchStart.RedirectStandardInput = $true
    $launcherProcess = [System.Diagnostics.Process]::Start($launchStart)
    if ($null -eq $launcherProcess) {
        throw 'The allowlisted client launch process did not start.'
    }

    $suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $userName = "stage2_$suffix"
    $password = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    try {
        $launcherProcess.StandardInput.WriteLine($userName)
        $launcherProcess.StandardInput.WriteLine($password)
        $launcherProcess.StandardInput.Flush()
        $launcherProcess.StandardInput.Close()
    }
    finally {
        $password = $null
        $userName = $null
    }

    $stableWindowHandle = 0L
    $stableWindowObservations = 0
    for ($attempt = 0; $attempt -lt 700; $attempt++) {
        if ($launcherProcess.HasExited) {
            $launchError = $launcherProcess.StandardError.ReadToEnd()
            throw "The launch driver exited before a usable real-client window appeared: $launchError"
        }

        $matches = @(Get-Process -Name $profile.processName -ErrorAction SilentlyContinue)
        if ($matches.Count -gt 1) {
            foreach ($match in $matches) { $match.Dispose() }
            throw 'More than one candidate client process appeared during the exact-target smoke.'
        }
        if ($matches.Count -eq 1) {
            if ($null -ne $clientProcess -and $clientProcess.Id -ne $matches[0].Id) {
                $clientProcess.Dispose()
                $clientProcess = $null
                $matches[0].Dispose()
                throw 'The client PID changed during exact-target discovery.'
            }
            if ($null -eq $clientProcess) {
                $clientProcess = $matches[0]
            }
            else {
                $matches[0].Dispose()
            }
            $clientProcess.Refresh()
            $observedHandle = $clientProcess.MainWindowHandle.ToInt64()
            $observedTitle = $clientProcess.MainWindowTitle
            if ($observedHandle -ne 0 -and -not [string]::IsNullOrWhiteSpace($observedTitle)) {
                if ($observedHandle -eq $stableWindowHandle) {
                    $stableWindowObservations++
                }
                else {
                    $stableWindowHandle = $observedHandle
                    $stableWindowObservations = 1
                }
            }
            else {
                $stableWindowHandle = 0
                $stableWindowObservations = 0
            }
            if ($stableWindowObservations -ge 20) {
                break
            }
        }
        Start-Sleep -Milliseconds 100
    }
    if ($null -eq $clientProcess -or $stableWindowObservations -lt 20) {
        throw 'The allowlisted client did not expose one stable titled main window for two seconds within the 70-second bound.'
    }

    $clientProcess.Refresh()
    $clientProcessId = $clientProcess.Id
    $windowHandle = $clientProcess.MainWindowHandle.ToInt64()
    $clientTitle = $clientProcess.MainWindowTitle

    $portProbe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $portProbe.Start()
    $inputPort = ([System.Net.IPEndPoint]$portProbe.LocalEndpoint).Port
    $portProbe.Stop()

    $inputStart = New-DriverStartInfo -Arguments @(
        $driver,
        'serve-input',
        '--profile', $profilePath,
        '--process-id', [string]$clientProcessId,
        '--window-handle', [string]$windowHandle,
        '--audit', $auditPath,
        '--port', [string]$inputPort,
        '--lease-ttl-ms', '30000',
        '--max-actions', '2'
    )
    $inputServer = [System.Diagnostics.Process]::Start($inputStart)
    if ($null -eq $inputServer) {
        throw 'The guarded-input server did not start for the real client.'
    }

    $startupTask = $inputServer.StandardOutput.ReadLineAsync()
    if (-not $startupTask.Wait(10000)) {
        throw 'The guarded-input server did not issue a real-client lease within 10 seconds.'
    }
    $startupLine = $startupTask.Result
    if ([string]::IsNullOrWhiteSpace($startupLine)) {
        $inputError = $inputServer.StandardError.ReadToEnd()
        throw "The guarded-input server exited before issuing a real-client lease: $inputError"
    }
    $lease = $startupLine | ConvertFrom-Json
    if ($lease.target.processId -ne $clientProcessId -or
        $lease.target.windowHandle -ne $windowHandle -or
        $lease.target.executableSha256 -ne $profile.clientExecutableSha256 -or
        $lease.maxActions -ne 2 -or
        $lease.rawTypedTextAudited) {
        throw 'The real-client input lease did not preserve the exact-target and no-raw-text contract.'
    }

    $httpClient = [System.Net.Http.HttpClient]::new()
    $httpClient.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', [string]$lease.leaseToken)

    function Invoke-RealInputAction {
        param([string] $Path, [string] $Body)
        $content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, 'application/json')
        $response = $null
        try {
            $response = $httpClient.PostAsync("http://127.0.0.1:$inputPort$Path", $content).GetAwaiter().GetResult()
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ([int]$response.StatusCode -ne 200) {
                throw "Real-client input $Path returned HTTP $([int]$response.StatusCode): $responseBody"
            }
            return $responseBody | ConvertFrom-Json
        }
        finally {
            $content.Dispose()
            if ($null -ne $response) { $response.Dispose() }
        }
    }

    $focusResult = Invoke-RealInputAction -Path '/v1/focus' -Body '{}'
    $keyResult = Invoke-RealInputAction -Path '/v1/key' -Body '{"key":"escape"}'
    if (-not $focusResult.accepted -or -not $focusResult.target.foreground -or
        -not $keyResult.accepted -or $keyResult.key -ne 'escape') {
        throw 'The real-client focus/Escape smoke did not return the expected guarded acceptances.'
    }

    if (-not $inputServer.WaitForExit(10000)) {
        throw 'The real-client input server did not stop after its two-action cap.'
    }
    $inputStop = $inputServer.StandardOutput.ReadToEnd()
    $inputError = $inputServer.StandardError.ReadToEnd()
    if ($inputServer.ExitCode -ne 0 -or $inputStop -notmatch '"stopReason":"max_actions"') {
        throw "The real-client input lease did not end cleanly at its action cap: $inputError"
    }

    $closeStart = New-DriverStartInfo -Arguments @(
        $driver,
        'request-close',
        '--profile', $profilePath,
        '--process-id', [string]$clientProcessId,
        '--timeout-ms', '30000'
    )
    $closeProcess = [System.Diagnostics.Process]::Start($closeStart)
    if ($null -eq $closeProcess) {
        throw 'The graceful-close driver did not start.'
    }
    try {
        if (-not $closeProcess.WaitForExit(35000)) {
            throw 'The graceful-close driver exceeded its bounded wait and was left running rather than force-terminated.'
        }
        $closeOutput = $closeProcess.StandardOutput.ReadToEnd()
        $closeError = $closeProcess.StandardError.ReadToEnd()
        if ($closeProcess.ExitCode -ne 0) {
            throw "The real client did not honor graceful close: $closeError"
        }
        $closeResult = $closeOutput | ConvertFrom-Json
        if (-not $closeResult.closeRequested -or -not $closeResult.processExited -or $closeResult.forcedTermination) {
            throw 'The real-client close result did not prove a graceful, non-forced exit.'
        }
        $closeCompleted = $true
    }
    finally {
        $closeProcess.Dispose()
    }

    $processAbsentAfterClose = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        try {
            $postCloseProcess = [System.Diagnostics.Process]::GetProcessById($clientProcessId)
            $postCloseProcess.Dispose()
            Start-Sleep -Milliseconds 100
        }
        catch [System.ArgumentException] {
            $processAbsentAfterClose = $true
            break
        }
    }
    if (-not $processAbsentAfterClose) {
        throw 'The gracefully closed client PID remained discoverable after the five-second process-table bound.'
    }

    if (-not $launcherProcess.WaitForExit(35000)) {
        throw 'The launch driver did not finish after graceful client exit and was left running rather than force-terminated.'
    }
    $launchOutput = $launcherProcess.StandardOutput.ReadToEnd()
    $launchError = $launcherProcess.StandardError.ReadToEnd()
    if ($launcherProcess.ExitCode -ne 0) {
        throw "The launch driver exited with code $($launcherProcess.ExitCode): $launchError"
    }
    $launchResult = $launchOutput | ConvertFrom-Json

    $auditRecords = @(Get-Content -LiteralPath $auditPath | ForEach-Object { $_ | ConvertFrom-Json })
    if ($auditRecords.Count -ne 2 -or @($auditRecords | Where-Object accepted).Count -ne 2 -or
        $auditRecords[0].action -ne 'focus' -or $auditRecords[1].action -ne 'key' -or
        $auditRecords[1].key -ne 'escape') {
        throw 'The real-client input audit did not contain exactly the accepted focus/Escape sequence.'
    }

    $summary = [ordered]@{
        schemaVersion = 1
        runId = $runId
        completedAtUtc = [DateTimeOffset]::UtcNow
        outcome = 'pass'
        target = [ordered]@{
            processId = $clientProcessId
            windowHandle = $windowHandle
            windowTitle = $clientTitle
            executablePath = $lease.target.executablePath
            executableSha256 = $lease.target.executableSha256
        }
        lease = [ordered]@{
            maxActions = 2
            tokenPersisted = $false
            rawTypedTextAudited = $false
            stopReason = 'max_actions'
        }
        acceptedActions = @('focus', 'escape')
        auditPath = $auditPath
        gracefulClose = $closeResult
        processAbsentAfterClose = $processAbsentAfterClose
        launch = $launchResult
        computerUseActions = 0
        injectionOrMemoryEditing = $false
        forcedTermination = $false
    }
    $summaryJson = $summary | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($summaryPath, $summaryJson, [System.Text.UTF8Encoding]::new($false))
    $summaryJson
}
finally {
    if ($null -ne $httpClient) {
        $httpClient.Dispose()
    }
    if ($null -ne $inputServer -and -not $inputServer.HasExited) {
        $null = $inputServer.WaitForExit(31000)
    }
    if (-not $closeCompleted -and $null -ne $clientProcess -and -not $clientProcess.HasExited) {
        $clientProcess.Refresh()
        if ($clientProcess.MainWindowHandle -ne [IntPtr]::Zero) {
            $fallbackClose = New-DriverStartInfo -Arguments @(
                $driver,
                'request-close',
                '--profile', $profilePath,
                '--process-id', [string]$clientProcess.Id,
                '--timeout-ms', '30000'
            )
            $fallbackCloseProcess = [System.Diagnostics.Process]::Start($fallbackClose)
            if ($null -ne $fallbackCloseProcess) {
                $null = $fallbackCloseProcess.WaitForExit(35000)
                $fallbackCloseProcess.Dispose()
            }
        }
    }
    if ($null -ne $launcherProcess -and -not $launcherProcess.HasExited) {
        $null = $launcherProcess.WaitForExit(35000)
    }
    if ($null -ne $clientProcess) { $clientProcess.Dispose() }
    if ($null -ne $inputServer) { $inputServer.Dispose() }
    if ($null -ne $launcherProcess) { $launcherProcess.Dispose() }
}
