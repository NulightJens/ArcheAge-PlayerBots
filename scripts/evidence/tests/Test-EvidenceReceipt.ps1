[CmdletBinding()]
param(
    [string]$ArtifactsDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testRoot = $PSScriptRoot
$evidenceRoot = Split-Path -Parent $testRoot
$translatorPath = Join-Path $evidenceRoot 'New-EvidenceReceipt.ps1'
$fixtureRoot = Join-Path $testRoot 'fixtures'
if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $ArtifactsDirectory = Join-Path $evidenceRoot ".test-runs/run-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ'))-$PID"
}
$ArtifactsDirectory = [System.IO.Path]::GetFullPath($ArtifactsDirectory)
if (Test-Path -LiteralPath $ArtifactsDirectory) {
    throw "ArtifactsDirectory must be a new path so prior attempts are retained: $ArtifactsDirectory"
}
[void](New-Item -ItemType Directory -Path $ArtifactsDirectory -Force)

$utf8 = [System.Text.UTF8Encoding]::new($false)
$attempts = [System.Collections.Generic.List[object]]::new()
$assertionCount = 0

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
    $script:assertionCount++
}

function Write-NewUtf8File {
    param([string]$Path, [string]$Content)
    if (Test-Path -LiteralPath $Path) {
        throw "Test refuses to overwrite retained artifact: $Path"
    }
    [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Invoke-Translator {
    param(
        [string]$Name,
        [string]$MetadataPath,
        [string]$EvidencePath,
        [string]$OutputPath,
        [string]$Format = 'Auto'
    )

    $diagnostics = @(
        & pwsh -NoLogo -NoProfile -File $translatorPath `
            -MetadataPath $MetadataPath `
            -EvidencePath $EvidencePath `
            -OutputPath $OutputPath `
            -EvidenceFormat $Format 2>&1 | ForEach-Object { "$_" }
    )
    $exitCode = $LASTEXITCODE
    $attempts.Add([pscustomobject][ordered]@{
        name = $Name
        exit_code = $exitCode
        output_created = Test-Path -LiteralPath $OutputPath -PathType Leaf
        diagnostics = $diagnostics
    })
    return [pscustomobject]@{
        ExitCode = $exitCode
        Diagnostics = $diagnostics
    }
}

function Assert-RejectedWithoutOutput {
    param(
        [string]$Name,
        [string]$MetadataPath,
        [string]$EvidencePath,
        [string]$OutputPath,
        [string]$Format = 'Auto'
    )

    $result = Invoke-Translator -Name $Name -MetadataPath $MetadataPath -EvidencePath $EvidencePath -OutputPath $OutputPath -Format $Format
    Assert-True -Condition ($result.ExitCode -ne 0) -Message "$Name must fail"
    Assert-True -Condition (-not (Test-Path -LiteralPath $OutputPath)) -Message "$Name must not create output"
}

function Read-Receipt {
    param([string]$Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 20 -DateKind String
}

$metadataPath = Join-Path $fixtureRoot 'metadata.valid.json'
$passJsonPath = Join-Path $fixtureRoot 'evidence.pass.json'
$passLinesPath = Join-Path $fixtureRoot 'evidence.pass.lines'
$failJsonPath = Join-Path $fixtureRoot 'evidence.fail.json'
$incompleteJsonPath = Join-Path $fixtureRoot 'evidence.incomplete.json'
$malformedJsonPath = Join-Path $fixtureRoot 'evidence.malformed.json'

$fixturePaths = @($metadataPath, $passJsonPath, $passLinesPath, $failJsonPath, $incompleteJsonPath, $malformedJsonPath)
$beforeHashes = @{}
foreach ($path in $fixturePaths) {
    $beforeHashes[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

$stableAPath = Join-Path $ArtifactsDirectory 'stable-a.json'
$stableBPath = Join-Path $ArtifactsDirectory 'stable-b.json'
$stableA = Invoke-Translator -Name 'stable-output-a' -MetadataPath $metadataPath -EvidencePath $passJsonPath -OutputPath $stableAPath
$stableB = Invoke-Translator -Name 'stable-output-b' -MetadataPath $metadataPath -EvidencePath $passJsonPath -OutputPath $stableBPath
Assert-True -Condition ($stableA.ExitCode -eq 0 -and $stableB.ExitCode -eq 0) -Message 'identical valid inputs must succeed'
$stableAHash = (Get-FileHash -LiteralPath $stableAPath -Algorithm SHA256).Hash
$stableBHash = (Get-FileHash -LiteralPath $stableBPath -Algorithm SHA256).Hash
Assert-True -Condition ($stableAHash -ceq $stableBHash) -Message 'identical inputs must produce byte-identical outputs'

$passReceipt = Read-Receipt -Path $stableAPath
Assert-True -Condition ($passReceipt.schema_version -ceq 'playerbots.evidence-receipt.v1') -Message 'receipt schema must be versioned'
Assert-True -Condition ($passReceipt.verdict.value -ceq 'PASS') -Message 'all-PASS checks must yield PASS'
Assert-True -Condition (($passReceipt.checks.id -join ',') -ceq 'alpha,zeta') -Message 'checks must use stable ordinal ordering'
Assert-True -Condition ($passReceipt.inputs[0].sha256 -ceq $beforeHashes[$metadataPath].ToLowerInvariant()) -Message 'metadata SHA-256 must be retained'
Assert-True -Condition ($passReceipt.inputs[1].sha256 -ceq $beforeHashes[$passJsonPath].ToLowerInvariant()) -Message 'evidence SHA-256 must be retained'

$requiredFingerprintFields = @(
    'module_source_commit',
    'aaemu_host_base_commit',
    'compatibility_patch_sha256',
    'client_provenance_sha256_or_not_applicable',
    'database_provenance',
    'gate_definition_version',
    'command_and_environment_fingerprint'
)
foreach ($field in $requiredFingerprintFields) {
    Assert-True -Condition ($null -ne $passReceipt.fingerprint.PSObject.Properties[$field]) -Message "receipt must retain fingerprint field $field"
}

$linesOutputPath = Join-Path $ArtifactsDirectory 'lines-pass.json'
$linesResult = Invoke-Translator -Name 'line-evidence-pass' -MetadataPath $metadataPath -EvidencePath $passLinesPath -OutputPath $linesOutputPath
Assert-True -Condition ($linesResult.ExitCode -eq 0) -Message 'valid line evidence must succeed'
$linesReceipt = Read-Receipt -Path $linesOutputPath
Assert-True -Condition ($linesReceipt.verdict.value -ceq 'PASS') -Message 'line evidence must derive PASS'
Assert-True -Condition ($linesReceipt.inputs[1].format -ceq 'playerbots.evidence-lines.v1') -Message 'line format provenance must be explicit'

$failOutputPath = Join-Path $ArtifactsDirectory 'fail.json'
$failResult = Invoke-Translator -Name 'explicit-fail-verdict' -MetadataPath $metadataPath -EvidencePath $failJsonPath -OutputPath $failOutputPath
Assert-True -Condition ($failResult.ExitCode -eq 0) -Message 'valid FAIL evidence must translate successfully'
Assert-True -Condition ((Read-Receipt -Path $failOutputPath).verdict.value -ceq 'FAIL') -Message 'a FAIL check must yield FAIL'

$incompleteOutputPath = Join-Path $ArtifactsDirectory 'incomplete.json'
$incompleteResult = Invoke-Translator -Name 'explicit-incomplete-verdict' -MetadataPath $metadataPath -EvidencePath $incompleteJsonPath -OutputPath $incompleteOutputPath
Assert-True -Condition ($incompleteResult.ExitCode -eq 0) -Message 'valid INCOMPLETE evidence must translate successfully'
Assert-True -Condition ((Read-Receipt -Path $incompleteOutputPath).verdict.value -ceq 'INCOMPLETE') -Message 'INCOMPLETE must take fail-closed precedence'

$overwriteHashBefore = (Get-FileHash -LiteralPath $stableAPath -Algorithm SHA256).Hash
$overwriteAttempt = Invoke-Translator -Name 'overwrite-refusal' -MetadataPath $metadataPath -EvidencePath $failJsonPath -OutputPath $stableAPath
Assert-True -Condition ($overwriteAttempt.ExitCode -ne 0) -Message 'existing output must be rejected'
Assert-True -Condition ((Get-FileHash -LiteralPath $stableAPath -Algorithm SHA256).Hash -ceq $overwriteHashBefore) -Message 'overwrite refusal must preserve existing bytes'

Assert-RejectedWithoutOutput `
    -Name 'malformed-json' `
    -MetadataPath $metadataPath `
    -EvidencePath $malformedJsonPath `
    -OutputPath (Join-Path $ArtifactsDirectory 'malformed-json.json') `
    -Format 'Json'

$malformedLinesPath = Join-Path $ArtifactsDirectory 'malformed.lines'
Write-NewUtf8File -Path $malformedLinesPath -Content "playerbots.evidence-lines.v1`nmissing-tab-field`n"
Assert-RejectedWithoutOutput `
    -Name 'malformed-lines' `
    -MetadataPath $metadataPath `
    -EvidencePath $malformedLinesPath `
    -OutputPath (Join-Path $ArtifactsDirectory 'malformed-lines.json') `
    -Format 'Lines'

Assert-RejectedWithoutOutput `
    -Name 'missing-evidence-file' `
    -MetadataPath $metadataPath `
    -EvidencePath (Join-Path $ArtifactsDirectory 'absent-evidence.json') `
    -OutputPath (Join-Path $ArtifactsDirectory 'missing-evidence.json')

$malformedMetadataPath = Join-Path $ArtifactsDirectory 'metadata-malformed.json'
Write-NewUtf8File -Path $malformedMetadataPath -Content "{`n"
Assert-RejectedWithoutOutput `
    -Name 'malformed-metadata-json' `
    -MetadataPath $malformedMetadataPath `
    -EvidencePath $passJsonPath `
    -OutputPath (Join-Path $ArtifactsDirectory 'malformed-metadata.receipt.json')

$metadataSource = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json -Depth 20 -DateKind String
foreach ($field in $requiredFingerprintFields) {
    $copy = $metadataSource | ConvertTo-Json -Depth 20 | ConvertFrom-Json -Depth 20 -DateKind String
    $copy.fingerprint.PSObject.Properties.Remove($field)
    $missingFieldPath = Join-Path $ArtifactsDirectory "metadata-missing-$field.json"
    Write-NewUtf8File -Path $missingFieldPath -Content (($copy | ConvertTo-Json -Depth 20).Replace("`r`n", "`n") + "`n")
    Assert-RejectedWithoutOutput `
        -Name "missing-fingerprint-$field" `
        -MetadataPath $missingFieldPath `
        -EvidencePath $passJsonPath `
        -OutputPath (Join-Path $ArtifactsDirectory "missing-fingerprint-$field.receipt.json")
}

$invalidFingerprint = $metadataSource | ConvertTo-Json -Depth 20 | ConvertFrom-Json -Depth 20 -DateKind String
$invalidFingerprint.fingerprint.command_and_environment_fingerprint = 'not-a-sha256'
$invalidFingerprintPath = Join-Path $ArtifactsDirectory 'metadata-invalid-command-fingerprint.json'
Write-NewUtf8File -Path $invalidFingerprintPath -Content (($invalidFingerprint | ConvertTo-Json -Depth 20).Replace("`r`n", "`n") + "`n")
Assert-RejectedWithoutOutput `
    -Name 'invalid-command-and-environment-fingerprint' `
    -MetadataPath $invalidFingerprintPath `
    -EvidencePath $passJsonPath `
    -OutputPath (Join-Path $ArtifactsDirectory 'invalid-command-fingerprint.receipt.json')

foreach ($path in $fixturePaths) {
    $afterHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    Assert-True -Condition ($afterHash -ceq $beforeHashes[$path]) -Message "input fixture must remain unchanged: $path"
}

$attemptsPath = Join-Path $ArtifactsDirectory 'attempts.json'
$summaryPath = Join-Path $ArtifactsDirectory 'summary.json'
Write-NewUtf8File -Path $attemptsPath -Content ((@($attempts) | ConvertTo-Json -Depth 10).Replace("`r`n", "`n") + "`n")
$summary = [pscustomobject][ordered]@{
    schema_version = 'playerbots.evidence-tool-test.v1'
    verdict = 'PASS'
    assertions = $assertionCount
    attempts = $attempts.Count
    stable_receipt_sha256 = $stableAHash.ToLowerInvariant()
    retained_artifacts = $ArtifactsDirectory
    runtime_started = $false
    database_started = $false
}
Write-NewUtf8File -Path $summaryPath -Content (($summary | ConvertTo-Json -Depth 5).Replace("`r`n", "`n") + "`n")

Write-Output "PASS: $assertionCount assertions across $($attempts.Count) retained translator attempts."
Write-Output "Artifacts: $ArtifactsDirectory"
