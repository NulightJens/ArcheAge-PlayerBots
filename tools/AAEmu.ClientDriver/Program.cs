using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AAEmu.ClientDriver;

internal static partial class Program
{
    private const int DefaultPort = 45831;
    private const int DefaultIdleTimeoutMs = 30_000;
    private const int MaximumLogBytes = 8 * 1024 * 1024;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly LifecycleMarker[] LifecycleMarkers =
    [
        new("loginConnected", "login_connected", "Auth: client connected to server"),
        new("gameConnectionRequested", "game_connecting", "connection requested to:"),
        new("gameConnectionCompleted", "game_connected", "connect completed (succeeded:true)"),
        new("worldConnected", "world_connected", "stream: connected to world server"),
        new("worldAuthorized", "world_authorized", "stream: authorized by world server"),
        new("worldLoading", "world_loading", "Level System - Loading Start"),
        new("worldLoaded", "world_loaded", "Level System - Loading Complete"),
        new("worldDisconnected", "disconnected", "stream: disconnected"),
        new("quit", "quit", "System:Quit")
    ];

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                _ = SetProcessDpiAwarenessContext(new IntPtr(-4));
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            return command switch
            {
                "status" => PrintStatus(DriverOptions.Parse(args[1..])),
                "serve" => await ServeAsync(DriverOptions.Parse(args[1..])),
                "verify-profile" => LauncherCommands.VerifyProfile(LauncherCommandOptions.Parse(args[1..], false)),
                "probe-launcher" => LauncherCommands.ProbeLauncher(LauncherCommandOptions.Parse(args[1..], false)),
                "launch" => await LauncherCommands.LaunchAsync(LauncherCommandOptions.Parse(args[1..], true)),
                "request-close" => await LauncherCommands.RequestCloseAsync(CloseCommandOptions.Parse(args[1..])),
                "serve-input" => await InputCommands.ServeAsync(InputServerOptions.Parse(args[1..])),
                "capture-window" => CaptureCommands.Capture(CaptureCommandOptions.Parse(args[1..])),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                error = exception.Message,
                type = exception.GetType().Name
            }, JsonOptions));
            return 2;
        }
    }

    private static int PrintStatus(DriverOptions options)
    {
        Console.WriteLine(JsonSerializer.Serialize(CaptureStatus(options), JsonOptions));
        return 0;
    }

    private static async Task<int> ServeAsync(DriverOptions options)
    {
        using var listener = new TcpListener(IPAddress.Loopback, options.Port);
        listener.Start();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            listening = $"http://127.0.0.1:{options.Port}",
            once = options.Once,
            idleTimeoutMs = options.IdleTimeoutMs
        }, JsonOptions));

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        do
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            idle.CancelAfter(options.IdleTimeoutMs);
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(idle.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using (client)
                await HandleRequestAsync(client, options, shutdown.Token);
        } while (!options.Once && !shutdown.IsCancellationRequested);

        return 0;
    }

    private static async Task HandleRequestAsync(TcpClient client, DriverOptions options, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
        string? header;
        do
        {
            header = await reader.ReadLineAsync(cancellationToken);
        } while (!string.IsNullOrEmpty(header));

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var method = parts.ElementAtOrDefault(0);
        var path = parts.ElementAtOrDefault(1)?.Split('?', 2)[0];
        var (statusCode, reason, body) = (method, path) switch
        {
            ("GET", "/health") => (200, "OK", JsonSerializer.Serialize(new { status = "ok" }, JsonOptions)),
            ("GET", "/v1/status") => (200, "OK", JsonSerializer.Serialize(CaptureStatus(options), JsonOptions)),
            _ => (404, "Not Found", JsonSerializer.Serialize(new { error = "not_found" }, JsonOptions))
        };

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var responseHeaders = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(responseHeaders, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
    }

    internal static ClientStatus CaptureStatus(DriverOptions options)
    {
        var process = options.IgnoreProcess
            ? ClientProcessSnapshot.Disabled(options.ProcessName)
            : CaptureProcesses(options.ProcessName);
        var log = ParseLog(options.LogPath);
        var state = DeriveState(process, log, options.IgnoreProcess);
        return new ClientStatus(1, DateTimeOffset.UtcNow, state, process, log);
    }

    internal static ClientProcessSnapshot CaptureProcesses(string processName)
    {
        var snapshots = new List<ClientProcessInstance>();
        foreach (var process in Process.GetProcessesByName(processName).OrderBy(process => process.Id))
        {
            using (process)
            {
                try
                {
                    var handle = process.MainWindowHandle;
                    WindowRectangle? rectangle = null;
                    if (OperatingSystem.IsWindows() && handle != IntPtr.Zero && GetWindowRect(handle, out var nativeRectangle))
                    {
                        rectangle = new WindowRectangle(
                            nativeRectangle.Left,
                            nativeRectangle.Top,
                            nativeRectangle.Right - nativeRectangle.Left,
                            nativeRectangle.Bottom - nativeRectangle.Top);
                    }

                    snapshots.Add(new ClientProcessInstance(
                        process.Id,
                        process.StartTime.ToUniversalTime(),
                        handle.ToInt64(),
                        process.MainWindowTitle,
                        rectangle));
                }
                catch (InvalidOperationException)
                {
                    // The process exited between discovery and inspection.
                }
            }
        }

        return new ClientProcessSnapshot(true, processName, snapshots.Count > 0, snapshots);
    }

    internal static ClientLogSnapshot ParseLog(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            return new ClientLogSnapshot(fullPath, false, 0, null, null, "not_observed", new Dictionary<string, string>());

        var file = new FileInfo(fullPath);
        var sessionStartedAt = ParseSessionStartedAt(fullPath);
        var milestones = new Dictionary<string, string>(StringComparer.Ordinal);
        var state = "not_observed";
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaximumLogBytes)
            stream.Seek(-MaximumLogBytes, SeekOrigin.End);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        if (stream.Position > 0)
            reader.ReadLine();

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            foreach (var marker in LifecycleMarkers)
            {
                if (!line.Contains(marker.Text, StringComparison.OrdinalIgnoreCase))
                    continue;
                state = marker.State;
                milestones[marker.Name] = ExtractLogTime(line) ?? "observed";
            }
        }

        return new ClientLogSnapshot(fullPath, true, file.Length, file.LastWriteTimeUtc, sessionStartedAt, state, milestones);
    }

    private static DateTimeOffset? ParseSessionStartedAt(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        for (var lineNumber = 0; lineNumber < 20; lineNumber++)
        {
            var line = reader.ReadLine();
            if (line == null)
                break;
            var match = SessionStartRegex().Match(line);
            if (!match.Success)
                continue;
            var value = $"{match.Groups["day"].Value} {match.Groups["month"].Value} " +
                        $"{match.Groups["year"].Value} {match.Groups["hour"].Value} " +
                        $"{match.Groups["minute"].Value} {match.Groups["second"].Value}";
            if (DateTime.TryParseExact(value, "d MMMM yyyy H m s", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var parsed))
                return new DateTimeOffset(parsed);
        }

        return null;
    }

    private static string DeriveState(ClientProcessSnapshot process, ClientLogSnapshot log, bool ignoreProcess)
    {
        if (ignoreProcess)
            return log.State;
        if (process.Running)
        {
            var earliestStart = process.Instances.Min(instance => instance.StartedUtc);
            if (!log.Exists || log.LastWriteUtc < earliestStart)
                return "process_started";
            return log.State == "not_observed" ? "process_started" : log.State;
        }

        if (log.State is "quit" or "disconnected" or "not_observed")
            return log.State == "not_observed" ? "stopped" : log.State;
        return "process_exited";
    }

    private static string? ExtractLogTime(string line) => LogTimeRegex().Match(line) is { Success: true } match
        ? match.Groups["time"].Value
        : null;

    private static bool IsHelp(string value) => value is "help" or "--help" or "-h";

    private static void PrintHelp() => Console.WriteLine(
        "AAEmu.ClientDriver\n" +
        "  status --log <ArcheAge.log> [--process-name archeage] [--ignore-process]\n" +
        "  serve  --log <ArcheAge.log> [--process-name archeage] [--port 45831] [--once] [--idle-timeout-ms 30000]\n" +
        "  verify-profile --profile <launch-profile.json>\n" +
        "  probe-launcher --profile <launch-profile.json>\n" +
        "  launch --profile <launch-profile.json> --wait-for <process_started|login_connected|world_authorized|world_loaded> [--timeout-ms 120000]\n\n" +
        "  request-close --profile <launch-profile.json> --process-id <pid> [--timeout-ms 30000]\n\n" +
        "  serve-input --profile <launch-profile.json> --process-id <pid> --window-handle <handle> --audit <jsonl> [--port 45832] [--lease-ttl-ms 15000] [--max-actions 8]\n\n" +
        "  capture-window --profile <launch-profile.json> --process-id <pid> --window-handle <handle> --output <capture.bmp>\n\n" +
        "The status API is read-only and binds only to 127.0.0.1. Launch credentials are read from the console or redirected standard input and are never accepted as command-line options.");

    [GeneratedRegex("<(?<time>\\d{2}:\\d{2}:\\d{2})>", RegexOptions.CultureInvariant)]
    private static partial Regex LogTimeRegex();

    [GeneratedRegex("Date\\((?<day>\\d{1,2}) (?<month>[A-Za-z]+) (?<year>\\d{4})\\) Time\\((?<hour>\\d{1,2}) (?<minute>\\d{1,2}) (?<second>\\d{1,2})\\)", RegexOptions.CultureInvariant)]
    private static partial Regex SessionStartRegex();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    private readonly record struct LifecycleMarker(string Name, string State, string Text);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

internal sealed record DriverOptions(
    string LogPath,
    string ProcessName,
    int Port,
    bool Once,
    bool IgnoreProcess,
    int IdleTimeoutMs)
{
    public static DriverOptions Parse(string[] args)
    {
        string? logPath = null;
        var processName = "archeage";
        var port = 45831;
        var once = false;
        var ignoreProcess = false;
        var idleTimeoutMs = 30_000;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--log":
                    logPath = ReadValue(args, ref index, "--log");
                    break;
                case "--process-name":
                    processName = ReadValue(args, ref index, "--process-name");
                    break;
                case "--port":
                    if (!int.TryParse(ReadValue(args, ref index, "--port"), out port) || port is < 1024 or > 65535)
                        throw new ArgumentException("--port must be between 1024 and 65535.");
                    break;
                case "--once":
                    once = true;
                    break;
                case "--ignore-process":
                    ignoreProcess = true;
                    break;
                case "--idle-timeout-ms":
                    if (!int.TryParse(ReadValue(args, ref index, "--idle-timeout-ms"), out idleTimeoutMs) ||
                        idleTimeoutMs is < 100 or > 300_000)
                        throw new ArgumentException("--idle-timeout-ms must be between 100 and 300000.");
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(logPath))
            throw new ArgumentException("--log is required.");
        if (string.IsNullOrWhiteSpace(processName) || processName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("--process-name must be a plain process name without a path.");

        return new DriverOptions(logPath, processName, port, once, ignoreProcess, idleTimeoutMs);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}

internal sealed record ClientStatus(
    int SchemaVersion,
    DateTimeOffset ObservedAtUtc,
    string State,
    ClientProcessSnapshot Process,
    ClientLogSnapshot Log);

internal sealed record ClientProcessSnapshot(
    bool Enabled,
    string ProcessName,
    bool Running,
    IReadOnlyList<ClientProcessInstance> Instances)
{
    public static ClientProcessSnapshot Disabled(string processName) => new(false, processName, false, []);
}

internal sealed record ClientProcessInstance(
    int ProcessId,
    DateTime StartedUtc,
    long MainWindowHandle,
    string MainWindowTitle,
    WindowRectangle? Window);

internal sealed record WindowRectangle(int X, int Y, int Width, int Height);

internal sealed record ClientLogSnapshot(
    string Path,
    bool Exists,
    long Bytes,
    DateTime? LastWriteUtc,
    DateTimeOffset? SessionStartedAt,
    string State,
    IReadOnlyDictionary<string, string> Milestones);
