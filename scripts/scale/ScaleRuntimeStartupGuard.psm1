Set-StrictMode -Version Latest

function New-ScaleLoginStartupPatterns {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$LoginDatabaseName,
        [Parameter(Mandatory)][ValidateRange(1, 65535)][int]$LoginHttpPort
    )

    $selectedSchemaMessage = "LoginService - Selected Login database schema: $LoginDatabaseName"
    return [string[]]@(
        '(?m)' + [regex]::Escape($selectedSchemaMessage) + '\r?$'
        [regex]::Escape('InternalNetwork started')
        [regex]::Escape("Now listening on: http://127.0.0.1:$LoginHttpPort")
    )
}

function New-ScaleGameStartupPatterns {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$GameDatabaseName,
        [Parameter(Mandatory)][ValidateRange(1, 65535)][int]$GameWebApiPort
    )

    $selectedSchemaMessage = "GameService - Selected Game database schema: $GameDatabaseName"
    $infoLinePrefix = '(?m)^\d{2}:\d{2}:\d{2} \[INFO\] '
    return [string[]]@(
        $infoLinePrefix + [regex]::Escape($selectedSchemaMessage) + '\r?$'
        $infoLinePrefix + [regex]::Escape('GameNetwork - Network started') + '\r?$'
        $infoLinePrefix + [regex]::Escape('StreamNetwork - StreamNetwork started') + '\r?$'
        $infoLinePrefix + [regex]::Escape('GameService - Server started! Took ') + '[^\r\n]+\r?$'
        $infoLinePrefix + [regex]::Escape("WebApiService - WebApi server started on 127.0.0.1:$GameWebApiPort") + '\r?$'
    )
}

function Test-ScaleRuntimeStartupEvidence {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][AllowNull()][AllowEmptyString()][object]$Content,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$RequiredPatterns
    )

    if ($Content -isnot [string]) {
        return $false
    }

    foreach ($pattern in $RequiredPatterns) {
        if (-not [regex]::IsMatch($Content, $pattern, [Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            return $false
        }
    }
    return $true
}

Export-ModuleMember -Function New-ScaleLoginStartupPatterns, New-ScaleGameStartupPatterns, Test-ScaleRuntimeStartupEvidence
