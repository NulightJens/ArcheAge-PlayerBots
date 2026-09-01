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

function Test-ScaleRuntimeStartupEvidence {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][AllowNull()][AllowEmptyString()][string]$Content,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$RequiredPatterns
    )

    if ($null -eq $Content) {
        return $false
    }

    foreach ($pattern in $RequiredPatterns) {
        if (-not [regex]::IsMatch($Content, $pattern, [Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            return $false
        }
    }
    return $true
}

Export-ModuleMember -Function New-ScaleLoginStartupPatterns, Test-ScaleRuntimeStartupEvidence
