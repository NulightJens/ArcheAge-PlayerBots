param([Parameter(Mandatory)][int]$ProcessId)

$signature = @'
using System;
using System.Runtime.InteropServices;
public static class T021ConsoleControl {
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GenerateConsoleCtrlEvent(uint controlEvent, uint processGroupId);
}
'@

Add-Type -TypeDefinition $signature
[void][T021ConsoleControl]::FreeConsole()
if (-not [T021ConsoleControl]::AttachConsole([uint32]$ProcessId)) { exit 2 }
[void][T021ConsoleControl]::SetConsoleCtrlHandler([IntPtr]::Zero, $true)
$sent = [T021ConsoleControl]::GenerateConsoleCtrlEvent(0, 0)
Start-Sleep -Milliseconds 500
[void][T021ConsoleControl]::FreeConsole()
if ($sent) { exit 0 }
exit 3
