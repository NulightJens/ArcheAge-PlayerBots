[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$ProcessId,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$target = Get-Process -Id $ProcessId -ErrorAction Stop
$helper = Join-Path $PSScriptRoot 'Send-GracefulCtrlC.ps1'
$helperProcess = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $helper, '-ProcessId', "$ProcessId"
) -WindowStyle Hidden -Wait -PassThru
if ($helperProcess.ExitCode -ne 0) {
    Write-Error "Ctrl+C helper failed with exit code $($helperProcess.ExitCode); process $ProcessId was left untouched."
    exit 2
}
if (-not $target.WaitForExit($TimeoutSeconds * 1000)) {
    Write-Error "Process $ProcessId did not exit within $TimeoutSeconds seconds and was deliberately left running; force termination is prohibited."
    exit 2
}
Write-Host "Process $ProcessId exited after a graceful Ctrl+C (exit code $($target.ExitCode))."
exit 0
