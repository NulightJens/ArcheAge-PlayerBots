[CmdletBinding()]
param(
    [string] $LauncherProfile = ''
)

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\AAEmu.ClientDriver.csproj'
$windowFixtureProject = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver.WindowFixture\AAEmu.ClientDriver.WindowFixture.csproj'
$fixture = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\fixtures\world-loaded.log'

& dotnet build $project --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Client driver build failed.'
}

& dotnet build $windowFixtureProject --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Client driver native-window fixture build failed.'
}

$driver = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\bin\Debug\net10.0\AAEmu.ClientDriver.dll'
$status = (& dotnet $driver status --log $fixture --ignore-process | ConvertFrom-Json)
if ($status.schemaVersion -ne 1 -or $status.state -ne 'world_loaded') {
    throw "Unexpected fixture status: schema=$($status.schemaVersion), state=$($status.state)"
}
if ($null -eq $status.log.sessionStartedAt) {
    throw 'The client log session timestamp was not parsed.'
}
if ($status.log.milestones.worldAuthorized -ne '17:00:06' -or
    $status.log.milestones.worldLoaded -ne '17:00:28') {
    throw 'The client lifecycle milestone parser did not preserve expected log times.'
}

$portProbe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$portProbe.Start()
$port = ([System.Net.IPEndPoint]$portProbe.LocalEndpoint).Port
$portProbe.Stop()

$server = Start-Process -FilePath 'dotnet' -ArgumentList @(
    $driver,
    'serve',
    '--log', $fixture,
    '--ignore-process',
    '--port', $port,
    '--once',
    '--idle-timeout-ms', '10000'
) -WindowStyle Hidden -PassThru

$apiStatus = $null
for ($attempt = 0; $attempt -lt 20 -and $null -eq $apiStatus; $attempt++) {
    try {
        $apiStatus = Invoke-RestMethod -Uri "http://127.0.0.1:$port/v1/status" -TimeoutSec 1
    }
    catch {
        Start-Sleep -Milliseconds 100
    }
}

$null = $server.WaitForExit(12000)
if (-not $server.HasExited) {
    throw 'Client driver did not honor its one-request/idle-timeout exit contract.'
}
if ($null -eq $apiStatus -or $apiStatus.state -ne 'world_loaded') {
    throw 'Loopback client status API did not return the expected fixture state.'
}

$forbiddenSecret = 'must-not-appear-in-output'
$rejection = (& dotnet $driver launch --profile $fixture --password $forbiddenSecret 2>&1 | Out-String)
if ($LASTEXITCODE -eq 0) {
    throw 'The client driver accepted a forbidden command-line password option.'
}
if ($rejection.Contains($forbiddenSecret, [System.StringComparison]::Ordinal)) {
    throw 'The rejected command-line password was reflected into output.'
}
$global:LASTEXITCODE = 0

if ($LauncherProfile -ne '') {
    $plan = (& dotnet $driver verify-profile --profile $LauncherProfile | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0 -or $plan.credentialsStoredInProfile -or $plan.plaintextLauncherPasswordRead) {
        throw 'The launcher profile did not validate under the no-stored-credentials contract.'
    }

    $probe = (& dotnet $driver probe-launcher --profile $LauncherProfile | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0 -or -not $probe.initialized -or $probe.processStarted) {
        throw 'The launcher assembly probe failed or started a process.'
    }
    if ($probe.launchArguments -notmatch '-handle <redacted>') {
        throw 'The launcher probe did not redact inherited handle values.'
    }

    $missingProcess = (& dotnet $driver request-close --profile $LauncherProfile --process-id 2147483647 2>&1 | Out-String)
    if ($LASTEXITCODE -eq 0) {
        throw 'The graceful-close command accepted a nonexistent process ID.'
    }
    if ($missingProcess -notmatch 'not running') {
        throw 'The graceful-close command did not fail closed for a nonexistent process ID.'
    }
    $global:LASTEXITCODE = 0

    $inputSourceRoot = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver'
    $inputSource = (Get-ChildItem -LiteralPath $inputSourceRoot -Filter '*.cs' -File |
        ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }) -join [Environment]::NewLine
    $forbiddenPrimitivePattern = '(?i)\b(CreateRemoteThread|WriteProcessMemory|VirtualAllocEx|TerminateProcess)\b|\.\s*Kill\s*\('
    if ($inputSource -match $forbiddenPrimitivePattern) {
        throw 'The guarded-input implementation contains a forbidden injection or forced-termination primitive.'
    }

    $artifactRoot = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver\bin\Debug\net10.0\TestArtifacts'
    $null = New-Item -ItemType Directory -Path $artifactRoot -Force
    $runId = '{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [Guid]::NewGuid().ToString('N')
    $fixtureExecutable = Join-Path $moduleRoot 'tools\AAEmu.ClientDriver.WindowFixture\bin\Debug\net10.0\AAEmu.ClientDriver.WindowFixture.exe'
    $fixtureProfilePath = Join-Path $artifactRoot "input-fixture-$runId.json"
    $inputAuditPath = Join-Path $artifactRoot "input-audit-$runId.jsonl"
    $fixtureCapturePath = Join-Path $artifactRoot "window-capture-$runId.bmp"
    $sourceProfile = Get-Content -LiteralPath $LauncherProfile -Raw | ConvertFrom-Json
    $fixtureProfile = [ordered]@{
        schemaVersion = 1
        launcherAssemblyPath = $sourceProfile.launcherAssemblyPath
        launcherAssemblySha256 = $sourceProfile.launcherAssemblySha256
        clientExecutablePath = $fixtureExecutable
        clientExecutableSha256 = (Get-FileHash -LiteralPath $fixtureExecutable -Algorithm SHA256).Hash
        serverAddress = '127.0.0.1'
        serverPort = 1337
        locale = 'en_us'
        loginType = 'trino_1_2'
        logPath = $fixture
        processName = 'AAEmu.ClientDriver.WindowFixture'
        hideSplash = $false
    }
    [System.IO.File]::WriteAllText(
        $fixtureProfilePath,
        ($fixtureProfile | ConvertTo-Json -Depth 4),
        [System.Text.UTF8Encoding]::new($false))

    $fixtureProcess = $null
    $inputServer = $null
    $httpClient = $null
    try {
        $fixtureStart = [System.Diagnostics.ProcessStartInfo]::new()
        $fixtureStart.FileName = $fixtureExecutable
        $fixtureStart.UseShellExecute = $false
        $fixtureProcess = [System.Diagnostics.Process]::Start($fixtureStart)
        if ($null -eq $fixtureProcess) {
            throw 'The native-window fixture did not start.'
        }

        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            if ($fixtureProcess.HasExited) {
                throw "The native-window fixture exited early with code $($fixtureProcess.ExitCode)."
            }
            $fixtureProcess.Refresh()
            if ($fixtureProcess.MainWindowHandle -ne [IntPtr]::Zero) {
                break
            }
            Start-Sleep -Milliseconds 100
        }
        if ($fixtureProcess.MainWindowHandle -eq [IntPtr]::Zero) {
            throw 'The native-window fixture did not expose a main window.'
        }

        $inputPortProbe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $inputPortProbe.Start()
        $inputPort = ([System.Net.IPEndPoint]$inputPortProbe.LocalEndpoint).Port
        $inputPortProbe.Stop()

        $inputStart = [System.Diagnostics.ProcessStartInfo]::new()
        $inputStart.FileName = 'dotnet'
        $inputStart.UseShellExecute = $false
        $inputStart.RedirectStandardOutput = $true
        $inputStart.RedirectStandardError = $true
        $inputStart.CreateNoWindow = $true
        foreach ($argument in @(
            $driver,
            'serve-input',
            '--profile', $fixtureProfilePath,
            '--process-id', $fixtureProcess.Id,
            '--window-handle', $fixtureProcess.MainWindowHandle.ToInt64(),
            '--audit', $inputAuditPath,
            '--port', $inputPort,
            '--lease-ttl-ms', '15000',
            '--max-actions', '8'
        )) {
            $inputStart.ArgumentList.Add([string]$argument)
        }
        $inputServer = [System.Diagnostics.Process]::Start($inputStart)
        if ($null -eq $inputServer) {
            throw 'The guarded-input server did not start.'
        }

        $startupTask = $inputServer.StandardOutput.ReadLineAsync()
        if (-not $startupTask.Wait(10000)) {
            throw 'The guarded-input server did not issue a lease within 10 seconds.'
        }
        $startupLine = $startupTask.Result
        if ([string]::IsNullOrWhiteSpace($startupLine)) {
            $startupError = $inputServer.StandardError.ReadToEnd()
            throw "The guarded-input server exited before issuing a lease: $startupError"
        }
        $lease = $startupLine | ConvertFrom-Json
        if ($lease.target.processId -ne $fixtureProcess.Id -or
            $lease.target.windowHandle -ne $fixtureProcess.MainWindowHandle.ToInt64() -or
            $lease.maxActions -ne 8 -or
            $lease.rawTypedTextAudited) {
            throw 'The guarded-input lease did not bind to the exact fixture PID/window or safe audit contract.'
        }

        $httpClient = [System.Net.Http.HttpClient]::new()
        $httpClient.DefaultRequestHeaders.Authorization =
            [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', [string]$lease.leaseToken)

        function Invoke-InputAction {
            param(
                [Parameter(Mandatory)] [string] $Path,
                [Parameter(Mandatory)] [string] $Body,
                [Parameter(Mandatory)] [int] $ExpectedStatus
            )
            $content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, 'application/json')
            $response = $null
            try {
                $response = $httpClient.PostAsync("http://127.0.0.1:$inputPort$Path", $content).GetAwaiter().GetResult()
                $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                if ([int]$response.StatusCode -ne $ExpectedStatus) {
                    throw "Input action $Path returned HTTP $([int]$response.StatusCode), expected $ExpectedStatus`: $responseBody"
                }
                return $responseBody | ConvertFrom-Json
            }
            finally {
                $content.Dispose()
                if ($null -ne $response) {
                    $response.Dispose()
                }
            }
        }

        $focusResult = Invoke-InputAction -Path '/v1/focus' -Body '{}' -ExpectedStatus 200
        if (-not $focusResult.accepted -or -not $focusResult.target.foreground) {
            throw 'The guarded-input focus action did not establish verified foreground ownership.'
        }

        $captureResult = (& dotnet $driver capture-window `
            --profile $fixtureProfilePath `
            --process-id $fixtureProcess.Id `
            --window-handle $fixtureProcess.MainWindowHandle.ToInt64() `
            --output $fixtureCapturePath | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0 -or $captureResult.status -ne 'captured' -or
            $captureResult.captureMethod -ne 'foreground_desktop_bitblt' -or
            $captureResult.occlusionSampleCount -ne 5 -or
            -not (Test-Path -LiteralPath $fixtureCapturePath)) {
            throw 'The verified fixture-window capture did not complete under the exact-target contract.'
        }

        function Get-BmpPixel {
            param([string] $Path, [int] $X, [int] $Y)
            $bytes = [System.IO.File]::ReadAllBytes($Path)
            $pixelOffset = [BitConverter]::ToInt32($bytes, 10)
            $width = [BitConverter]::ToInt32($bytes, 18)
            $signedHeight = [BitConverter]::ToInt32($bytes, 22)
            $height = [Math]::Abs($signedHeight)
            if ($X -lt 0 -or $Y -lt 0 -or $X -ge $width -or $Y -ge $height) {
                throw 'Requested BMP assertion pixel is outside the capture.'
            }
            $row = if ($signedHeight -lt 0) { $Y } else { $height - 1 - $Y }
            $index = $pixelOffset + (($row * $width + $X) * 4)
            return '{0},{1},{2}' -f $bytes[$index + 2], $bytes[$index + 1], $bytes[$index]
        }

        if ((Get-BmpPixel -Path $fixtureCapturePath -X 20 -Y 20) -ne '17,34,51' -or
            (Get-BmpPixel -Path $fixtureCapturePath -X 60 -Y 70) -ne '34,204,102') {
            throw 'The exact fixture-window pixel assertions did not match the painted reference pattern.'
        }
        $captureHashBeforeOverwriteAttempt = (Get-FileHash -LiteralPath $fixtureCapturePath -Algorithm SHA256).Hash
        $captureOverwriteRejection = (& dotnet $driver capture-window `
            --profile $fixtureProfilePath `
            --process-id $fixtureProcess.Id `
            --window-handle $fixtureProcess.MainWindowHandle.ToInt64() `
            --output $fixtureCapturePath 2>&1 | Out-String)
        if ($LASTEXITCODE -eq 0 -or $captureOverwriteRejection -notmatch 'already exists' -or
            (Get-FileHash -LiteralPath $fixtureCapturePath -Algorithm SHA256).Hash -ne $captureHashBeforeOverwriteAttempt) {
            throw 'The capture command did not fail closed without changing pre-existing evidence.'
        }
        $global:LASTEXITCODE = 0

        $keyResult = Invoke-InputAction -Path '/v1/key' -Body '{"key":"f6"}' -ExpectedStatus 200
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $fixtureProcess.Refresh()
            if ($fixtureProcess.MainWindowTitle -match '\| key=117 \|') { break }
            Start-Sleep -Milliseconds 50
        }
        if (-not $keyResult.accepted -or $fixtureProcess.MainWindowTitle -notmatch '\| key=117 \|') {
            throw 'The allowlisted F6 input did not reach the exact fixture window.'
        }

        $clickResult = Invoke-InputAction -Path '/v1/click-client' -Body '{"x":40,"y":40}' -ExpectedStatus 200
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $fixtureProcess.Refresh()
            if ($fixtureProcess.MainWindowTitle -match '\| click=40,40 \|') { break }
            Start-Sleep -Milliseconds 50
        }
        if (-not $clickResult.accepted -or $fixtureProcess.MainWindowTitle -notmatch '\| click=40,40 \|') {
            throw 'The client-relative click did not reach the verified fixture coordinate.'
        }

        $chatCommand = '/fixture ping'
        $chatResult = Invoke-InputAction -Path '/v1/type-chat-command' -Body '{"command":"/fixture ping"}' -ExpectedStatus 200
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $fixtureProcess.Refresh()
            if ($fixtureProcess.MainWindowTitle -match 'text=/fixture ping$') { break }
            Start-Sleep -Milliseconds 50
        }
        if (-not $chatResult.accepted -or $fixtureProcess.MainWindowTitle -notmatch 'text=/fixture ping$') {
            throw 'The bounded slash command did not reach the exact fixture window.'
        }

        $invalidKey = Invoke-InputAction -Path '/v1/key' -Body '{"key":"a"}' -ExpectedStatus 409
        $invalidClick = Invoke-InputAction -Path '/v1/click-client' -Body '{"x":-1,"y":40}' -ExpectedStatus 409
        $invalidChat = Invoke-InputAction -Path '/v1/type-chat-command' -Body '{"command":"fixture ping"}' -ExpectedStatus 409
        $unknownField = Invoke-InputAction -Path '/v1/key' -Body '{"key":"f6","extra":true}' -ExpectedStatus 409
        if ($invalidKey.accepted -or $invalidClick.accepted -or $invalidChat.accepted -or $unknownField.accepted) {
            throw 'A guarded-input fail-closed case was unexpectedly accepted.'
        }

        if (-not $inputServer.WaitForExit(10000)) {
            throw 'The guarded-input server did not stop after its exact eight-action cap.'
        }
        $remainingOutput = $inputServer.StandardOutput.ReadToEnd()
        $inputError = $inputServer.StandardError.ReadToEnd()
        if ($inputServer.ExitCode -ne 0) {
            throw "The guarded-input server exited with code $($inputServer.ExitCode): $inputError"
        }
        if ($remainingOutput -notmatch '"stopReason":"max_actions"') {
            throw 'The guarded-input server did not report its max-actions stop reason.'
        }

        $auditText = [System.IO.File]::ReadAllText($inputAuditPath)
        if ($auditText.Contains($chatCommand, [System.StringComparison]::Ordinal)) {
            throw 'The guarded-input audit leaked raw typed text.'
        }
        $auditRecords = @(Get-Content -LiteralPath $inputAuditPath | ForEach-Object { $_ | ConvertFrom-Json })
        if ($auditRecords.Count -ne 8 -or @($auditRecords | Where-Object accepted).Count -ne 4) {
            throw 'The guarded-input audit did not retain exactly four accepted and four rejected actions.'
        }
        $chatAudit = $auditRecords | Where-Object action -eq 'type-chat-command'
        if ($chatAudit.detail.commandVerb -ne 'fixture' -or
            $chatAudit.detail.characterCount -ne $chatCommand.Length -or
            $chatAudit.detail.rawTextRecorded) {
            throw 'The guarded-input chat audit did not retain safe command metadata.'
        }
    }
    finally {
        if ($null -ne $httpClient) {
            $httpClient.Dispose()
        }
        if ($null -ne $inputServer -and -not $inputServer.HasExited) {
            $null = $inputServer.WaitForExit(16000)
        }
        if ($null -ne $fixtureProcess -and -not $fixtureProcess.HasExited) {
            $null = $fixtureProcess.CloseMainWindow()
            $null = $fixtureProcess.WaitForExit(5000)
        }
        if ($null -ne $fixtureProcess -and -not $fixtureProcess.HasExited) {
            throw 'The native-window fixture did not honor graceful WM_CLOSE; it was left running rather than force-terminated.'
        }
    }
}

Write-Host 'AAEmu.ClientDriver validation passed: build, lifecycle parser, loopback status, secret rejection, optional launcher probe, exact-window guarded input/capture, exact pixels, redacted audit, fail-closed cases, and graceful close.'
