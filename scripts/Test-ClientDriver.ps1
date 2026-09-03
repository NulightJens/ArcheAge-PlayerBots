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
    $fixtureTemplatePath = Join-Path $artifactRoot "accent-template-$runId.bmp"
    $imageAssertionSpecPath = Join-Path $artifactRoot "image-assertions-$runId.json"
    $failingAssertionSpecPath = Join-Path $artifactRoot "image-assertions-failing-$runId.json"
    $unknownAssertionSpecPath = Join-Path $artifactRoot "image-assertions-unknown-$runId.json"
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

        function Write-SolidBmp32 {
            param(
                [Parameter(Mandatory)] [string] $Path,
                [Parameter(Mandatory)] [int] $Width,
                [Parameter(Mandatory)] [int] $Height,
                [Parameter(Mandatory)] [byte] $Red,
                [Parameter(Mandatory)] [byte] $Green,
                [Parameter(Mandatory)] [byte] $Blue
            )
            [byte[]]$pixels = [byte[]]::new($Width * $Height * 4)
            for ($index = 0; $index -lt $pixels.Length; $index += 4) {
                $pixels[$index] = $Blue
                $pixels[$index + 1] = $Green
                $pixels[$index + 2] = $Red
            }
            $stream = [System.IO.FileStream]::new(
                $Path,
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::Read)
            $writer = [System.IO.BinaryWriter]::new($stream)
            try {
                $writer.Write([uint16]0x4D42)
                $writer.Write([int](54 + $pixels.Length))
                $writer.Write([uint32]0)
                $writer.Write([int]54)
                $writer.Write([uint32]40)
                $writer.Write([int]$Width)
                $writer.Write([int](-$Height))
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]0)
                $writer.Write([uint32]$pixels.Length)
                $writer.Write([int]0)
                $writer.Write([int]0)
                $writer.Write([uint32]0)
                $writer.Write([uint32]0)
                $writer.Write($pixels)
            }
            finally {
                $writer.Dispose()
            }
        }

        Write-SolidBmp32 -Path $fixtureTemplatePath -Width 10 -Height 10 -Red 221 -Green 68 -Blue 85
        $imageAssertionSpec = [ordered]@{
            schemaVersion = 1
            regionAssertions = @(
                [ordered]@{
                    name = 'background-region'
                    rectangle = [ordered]@{ x = 10; y = 10; width = 16; height = 16 }
                    expectedRgbSha256 = 'B26E76AF265678AD797661503BFD83FB17397D927C4D056C5C62A20941893DDA'
                },
                [ordered]@{
                    name = 'accent-region'
                    rectangle = [ordered]@{ x = 90; y = 90; width = 10; height = 10 }
                    expectedRgbSha256 = '1BBD69086C14C478BD78F9154071DF142E3B1FB0D765914EF967FEE116ED3972'
                }
            )
            templateAssertions = @(
                [ordered]@{
                    name = 'unique-accent-template'
                    templatePath = [System.IO.Path]::GetFileName($fixtureTemplatePath)
                    searchRectangle = [ordered]@{ x = 50; y = 60; width = 100; height = 80 }
                    expectedMatches = @([ordered]@{ x = 90; y = 90 })
                }
            )
        }
        [System.IO.File]::WriteAllText(
            $imageAssertionSpecPath,
            ($imageAssertionSpec | ConvertTo-Json -Depth 8),
            [System.Text.UTF8Encoding]::new($false))

        $imageAssertionResult = (& dotnet $driver assert-image `
            --capture $fixtureCapturePath `
            --spec $imageAssertionSpecPath | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0 -or $imageAssertionResult.status -ne 'passed' -or
            $imageAssertionResult.summary.assertionCount -ne 3 -or
            $imageAssertionResult.summary.passedCount -ne 3 -or
            $imageAssertionResult.summary.ocrUsed -or
            $imageAssertionResult.summary.comparison -ne 'exact_rgb' -or
            $imageAssertionResult.templates[0].actualMatches.Count -ne 1 -or
            $imageAssertionResult.templates[0].actualMatches[0].x -ne 90 -or
            $imageAssertionResult.templates[0].actualMatches[0].y -ne 90) {
            throw 'The reusable exact-region/template assertion gate did not pass with the expected evidence.'
        }

        $failingAssertionSpec = [ordered]@{
            schemaVersion = 1
            regionAssertions = @(
                [ordered]@{
                    name = 'known-mismatch'
                    rectangle = [ordered]@{ x = 10; y = 10; width = 16; height = 16 }
                    expectedRgbSha256 = ('0' * 64)
                }
            )
            templateAssertions = @(
                [ordered]@{
                    name = 'wrong-location'
                    templatePath = [System.IO.Path]::GetFileName($fixtureTemplatePath)
                    searchRectangle = [ordered]@{ x = 50; y = 60; width = 100; height = 80 }
                    expectedMatches = @([ordered]@{ x = 91; y = 90 })
                }
            )
        }
        [System.IO.File]::WriteAllText(
            $failingAssertionSpecPath,
            ($failingAssertionSpec | ConvertTo-Json -Depth 8),
            [System.Text.UTF8Encoding]::new($false))
        $failingAssertionResult = (& dotnet $driver assert-image `
            --capture $fixtureCapturePath `
            --spec $failingAssertionSpecPath | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 3 -or $failingAssertionResult.status -ne 'failed' -or
            $failingAssertionResult.summary.failedCount -ne 2 -or
            $failingAssertionResult.regions[0].passed -or
            $failingAssertionResult.templates[0].passed) {
            throw 'The reusable image assertion command did not return explicit mismatch evidence and exit code 3.'
        }
        $global:LASTEXITCODE = 0

        $unknownAssertionSpec = [ordered]@{
            schemaVersion = 1
            regionAssertions = @(
                [ordered]@{
                    name = 'unknown-field'
                    rectangle = [ordered]@{ x = 10; y = 10; width = 1; height = 1 }
                    expectedRgbSha256 = 'B26E76AF265678AD797661503BFD83FB17397D927C4D056C5C62A20941893DDA'
                    tolerance = 1
                }
            )
            templateAssertions = @()
        }
        [System.IO.File]::WriteAllText(
            $unknownAssertionSpecPath,
            ($unknownAssertionSpec | ConvertTo-Json -Depth 8),
            [System.Text.UTF8Encoding]::new($false))
        $unknownAssertionRejection = (& dotnet $driver assert-image `
            --capture $fixtureCapturePath `
            --spec $unknownAssertionSpecPath 2>&1 | Out-String)
        if ($LASTEXITCODE -eq 0 -or $unknownAssertionRejection -notmatch 'tolerance') {
            throw 'The image assertion spec did not fail closed for an unknown tolerance field.'
        }
        $global:LASTEXITCODE = 0

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

Write-Host 'AAEmu.ClientDriver validation passed: build, lifecycle parser, loopback status, secret rejection, optional launcher probe, exact-window guarded input/capture, reusable exact-region/template assertions, redacted audit, fail-closed cases, and graceful close.'
