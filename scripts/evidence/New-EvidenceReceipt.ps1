[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MetadataPath,
    [Parameter(Mandatory)][string]$EvidencePath,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateSet('Auto', 'Json', 'Lines')][string]$EvidenceFormat = 'Auto'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)
$utf8Output = [System.Text.UTF8Encoding]::new($false)

function Read-StrictUtf8File {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label does not exist or is not a file: $Path"
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    try {
        $text = [System.IO.File]::ReadAllText($resolved, $utf8Strict)
    }
    catch {
        throw "$Label is not valid UTF-8: $($_.Exception.Message)"
    }

    return [pscustomobject]@{
        Path = $resolved
        Text = $text
        Sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Assert-PlainObject {
    param($Value, [string]$Label)

    if ($Value -isnot [System.Management.Automation.PSCustomObject]) {
        throw "$Label must be a JSON object."
    }
}

function Assert-ExactProperties {
    param($Value, [string[]]$Expected, [string]$Label)

    $actual = @($Value.PSObject.Properties | ForEach-Object { $_.Name })
    $unexpected = @($actual | Where-Object { $Expected -cnotcontains $_ })
    $missing = @($Expected | Where-Object { $actual -cnotcontains $_ })
    if ($unexpected.Count -gt 0 -or $missing.Count -gt 0) {
        throw "$Label properties are invalid (missing=$($missing -join ','); unexpected=$($unexpected -join ','))."
    }
}

function Assert-BoundedText {
    param(
        $Value,
        [string]$Label,
        [int]$MaximumLength = 512
    )

    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value) -or
        $Value.Length -gt $MaximumLength -or $Value -match '[\x00-\x1f\x7f]') {
        throw "$Label must be non-empty bounded text without control characters."
    }
}

function Assert-HexValue {
    param($Value, [int]$Length, [string]$Label)

    Assert-BoundedText -Value $Value -Label $Label -MaximumLength $Length
    if ($Value -cnotmatch "^[0-9a-fA-F]{$Length}$") {
        throw "$Label must contain exactly $Length hexadecimal characters."
    }
}

function ConvertFrom-StrictJson {
    param([string]$Text, [string]$Label)

    try {
        return $Text | ConvertFrom-Json -Depth 20 -DateKind String
    }
    catch {
        throw "$Label is malformed JSON: $($_.Exception.Message)"
    }
}

function Add-NormalizedCheck {
    param(
        [System.Collections.Generic.SortedDictionary[string, string]]$Checks,
        $Id,
        $Verdict,
        [string]$Label
    )

    Assert-BoundedText -Value $Id -Label "$Label id" -MaximumLength 128
    if ($Id -cnotmatch '^[a-z0-9][a-z0-9._-]{0,127}$') {
        throw "$Label id must use lowercase letters, digits, dots, underscores, or hyphens."
    }
    if ($Verdict -isnot [string] -or @('PASS', 'FAIL', 'INCOMPLETE') -cnotcontains $Verdict) {
        throw "$Label verdict must be PASS, FAIL, or INCOMPLETE."
    }
    if ($Checks.ContainsKey($Id)) {
        throw "Evidence contains duplicate check id '$Id'."
    }
    $Checks.Add($Id, $Verdict)
}

function ConvertFrom-JsonEvidence {
    param([string]$Text)

    $source = ConvertFrom-StrictJson -Text $Text -Label 'Evidence input'
    Assert-PlainObject -Value $source -Label 'Evidence input'
    Assert-ExactProperties -Value $source -Expected @('schema_version', 'checks') -Label 'Evidence input'
    if ($source.schema_version -cne 'playerbots.evidence-input.v1') {
        throw "Evidence schema_version must be 'playerbots.evidence-input.v1'."
    }
    if ($null -eq $source.checks -or $source.checks -isnot [System.Array]) {
        throw 'Evidence checks must be a JSON array.'
    }

    $checks = [System.Collections.Generic.SortedDictionary[string, string]]::new(
        [System.StringComparer]::Ordinal)
    $index = 0
    foreach ($check in $source.checks) {
        Assert-PlainObject -Value $check -Label "Evidence check $index"
        Assert-ExactProperties -Value $check -Expected @('id', 'verdict') -Label "Evidence check $index"
        Add-NormalizedCheck -Checks $checks -Id $check.id -Verdict $check.verdict -Label "Evidence check $index"
        $index++
    }
    if ($checks.Count -eq 0) {
        throw 'Evidence must contain at least one check.'
    }
    return $checks
}

function ConvertFrom-LineEvidence {
    param([string]$Text)

    $lines = [System.Text.RegularExpressions.Regex]::Split($Text, "\r\n|\n|\r")
    if ($lines.Count -gt 0 -and $lines[-1] -ceq '') {
        $lines = @($lines[0..($lines.Count - 2)])
    }
    if ($lines.Count -eq 0 -or $lines[0] -cne 'playerbots.evidence-lines.v1') {
        throw "Line evidence must start with 'playerbots.evidence-lines.v1'."
    }

    $checks = [System.Collections.Generic.SortedDictionary[string, string]]::new(
        [System.StringComparer]::Ordinal)
    for ($index = 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -ceq '') {
            throw "Line evidence contains a blank line at line $($index + 1)."
        }
        $parts = $lines[$index].Split("`t")
        if ($parts.Count -ne 2) {
            throw "Line evidence line $($index + 1) must be '<check-id><TAB><verdict>'."
        }
        Add-NormalizedCheck -Checks $checks -Id $parts[0] -Verdict $parts[1] -Label "Line evidence line $($index + 1)"
    }
    if ($checks.Count -eq 0) {
        throw 'Line evidence must contain at least one check.'
    }
    return $checks
}

$metadataInput = Read-StrictUtf8File -Path $MetadataPath -Label 'Metadata input'
$evidenceInput = Read-StrictUtf8File -Path $EvidencePath -Label 'Evidence input'

$metadata = ConvertFrom-StrictJson -Text $metadataInput.Text -Label 'Metadata input'
Assert-PlainObject -Value $metadata -Label 'Metadata input'
Assert-ExactProperties -Value $metadata -Expected @(
    'schema_version',
    'receipt_id',
    'generated_at_utc',
    'gate_id',
    'fingerprint'
) -Label 'Metadata input'

if ($metadata.schema_version -cne 'playerbots.evidence-metadata.v1') {
    throw "Metadata schema_version must be 'playerbots.evidence-metadata.v1'."
}
Assert-BoundedText -Value $metadata.receipt_id -Label 'receipt_id' -MaximumLength 128
if ($metadata.receipt_id -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') {
    throw 'receipt_id contains unsupported characters.'
}
Assert-BoundedText -Value $metadata.gate_id -Label 'gate_id' -MaximumLength 128
if ($metadata.gate_id -cnotmatch '^[a-z0-9][a-z0-9._-]{0,127}$') {
    throw 'gate_id contains unsupported characters.'
}
Assert-BoundedText -Value $metadata.generated_at_utc -Label 'generated_at_utc' -MaximumLength 32
if ($metadata.generated_at_utc -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$') {
    throw 'generated_at_utc must be a caller-supplied ISO-8601 UTC value ending in Z.'
}
try {
    [void][System.DateTimeOffset]::Parse(
        $metadata.generated_at_utc,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AssumeUniversal)
}
catch {
    throw 'generated_at_utc is not a valid calendar timestamp.'
}

Assert-PlainObject -Value $metadata.fingerprint -Label 'fingerprint'
$requiredFingerprintFields = @(
    'module_source_commit',
    'aaemu_host_base_commit',
    'compatibility_patch_sha256',
    'client_provenance_sha256_or_not_applicable',
    'database_provenance',
    'gate_definition_version',
    'command_and_environment_fingerprint'
)
Assert-ExactProperties -Value $metadata.fingerprint -Expected $requiredFingerprintFields -Label 'fingerprint'
Assert-HexValue -Value $metadata.fingerprint.module_source_commit -Length 40 -Label 'module_source_commit'
Assert-HexValue -Value $metadata.fingerprint.aaemu_host_base_commit -Length 40 -Label 'aaemu_host_base_commit'
Assert-HexValue -Value $metadata.fingerprint.compatibility_patch_sha256 -Length 64 -Label 'compatibility_patch_sha256'

$clientProvenance = $metadata.fingerprint.client_provenance_sha256_or_not_applicable
if ($clientProvenance -cne 'not-applicable') {
    Assert-HexValue -Value $clientProvenance -Length 64 -Label 'client_provenance_sha256_or_not_applicable'
}
Assert-BoundedText -Value $metadata.fingerprint.database_provenance -Label 'database_provenance'
if ("$($metadata.fingerprint.gate_definition_version)" -cnotmatch '^[1-9][0-9]*$') {
    throw 'gate_definition_version must be a positive integer.'
}
Assert-HexValue -Value $metadata.fingerprint.command_and_environment_fingerprint -Length 64 -Label 'command_and_environment_fingerprint'

$resolvedFormat = $EvidenceFormat
if ($resolvedFormat -ceq 'Auto') {
    $resolvedFormat = if ($evidenceInput.Text.TrimStart().StartsWith('{', [System.StringComparison]::Ordinal)) {
        'Json'
    }
    else {
        'Lines'
    }
}

$checkMap = if ($resolvedFormat -ceq 'Json') {
    ConvertFrom-JsonEvidence -Text $evidenceInput.Text
}
else {
    ConvertFrom-LineEvidence -Text $evidenceInput.Text
}

$normalizedChecks = @(
    foreach ($entry in $checkMap.GetEnumerator()) {
        [pscustomobject][ordered]@{
            id = $entry.Key
            verdict = $entry.Value
        }
    }
)
$passCount = @($normalizedChecks | Where-Object { $_.verdict -ceq 'PASS' }).Count
$failCount = @($normalizedChecks | Where-Object { $_.verdict -ceq 'FAIL' }).Count
$incompleteCount = @($normalizedChecks | Where-Object { $_.verdict -ceq 'INCOMPLETE' }).Count
$verdict = if ($incompleteCount -gt 0) {
    'INCOMPLETE'
}
elseif ($failCount -gt 0) {
    'FAIL'
}
else {
    'PASS'
}

$receipt = [pscustomobject][ordered]@{
    schema_version = 'playerbots.evidence-receipt.v1'
    receipt_id = $metadata.receipt_id
    generated_at_utc = $metadata.generated_at_utc
    gate = [pscustomobject][ordered]@{
        id = $metadata.gate_id
        definition_version = [long]$metadata.fingerprint.gate_definition_version
    }
    fingerprint = [pscustomobject][ordered]@{
        module_source_commit = $metadata.fingerprint.module_source_commit.ToLowerInvariant()
        aaemu_host_base_commit = $metadata.fingerprint.aaemu_host_base_commit.ToLowerInvariant()
        compatibility_patch_sha256 = $metadata.fingerprint.compatibility_patch_sha256.ToLowerInvariant()
        client_provenance_sha256_or_not_applicable = $clientProvenance.ToLowerInvariant()
        database_provenance = $metadata.fingerprint.database_provenance
        gate_definition_version = [long]$metadata.fingerprint.gate_definition_version
        command_and_environment_fingerprint = $metadata.fingerprint.command_and_environment_fingerprint.ToLowerInvariant()
    }
    inputs = @(
        [pscustomobject][ordered]@{
            role = 'metadata'
            format = 'playerbots.evidence-metadata.v1'
            sha256 = $metadataInput.Sha256
        },
        [pscustomobject][ordered]@{
            role = 'evidence'
            format = if ($resolvedFormat -ceq 'Json') { 'playerbots.evidence-input.v1' } else { 'playerbots.evidence-lines.v1' }
            sha256 = $evidenceInput.Sha256
        }
    )
    verdict = [pscustomobject][ordered]@{
        value = $verdict
        derivation = 'INCOMPLETE-if-any-INCOMPLETE-else-FAIL-if-any-FAIL-else-PASS'
        counts = [pscustomobject][ordered]@{
            pass = $passCount
            fail = $failCount
            incomplete = $incompleteCount
            total = $normalizedChecks.Count
        }
    }
    checks = $normalizedChecks
}

$absoluteOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputParent = Split-Path -Parent $absoluteOutputPath
if (-not (Test-Path -LiteralPath $outputParent -PathType Container)) {
    throw "Output parent directory does not exist: $outputParent"
}

$json = $receipt | ConvertTo-Json -Depth 10
$json = $json.Replace("`r`n", "`n").Replace("`r", "`n") + "`n"
$bytes = $utf8Output.GetBytes($json)
$stream = $null
try {
    $stream = [System.IO.FileStream]::new(
        $absoluteOutputPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    $stream.Write($bytes, 0, $bytes.Length)
}
catch [System.IO.IOException] {
    throw "Refusing to overwrite existing output or create an already-claimed path: $absoluteOutputPath"
}
finally {
    if ($null -ne $stream) {
        $stream.Dispose()
    }
}

Write-Output $absoluteOutputPath
