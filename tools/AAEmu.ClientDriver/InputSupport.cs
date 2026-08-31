using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AAEmu.ClientDriver;

internal sealed record InputServerOptions(
    string ProfilePath,
    int ProcessId,
    long WindowHandle,
    string AuditPath,
    int Port,
    int LeaseTtlMs,
    int MaxActions)
{
    public static InputServerOptions Parse(string[] args)
    {
        string? profilePath = null;
        string? auditPath = null;
        var processId = 0;
        long windowHandle = 0;
        var port = 45_832;
        var leaseTtlMs = 15_000;
        var maxActions = 8;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--profile":
                    profilePath = ReadValue(args, ref index, "--profile");
                    break;
                case "--process-id":
                    if (!int.TryParse(ReadValue(args, ref index, "--process-id"), out processId) || processId <= 0)
                        throw new ArgumentException("--process-id must be a positive integer.");
                    break;
                case "--window-handle":
                    if (!long.TryParse(ReadValue(args, ref index, "--window-handle"), out windowHandle) || windowHandle <= 0)
                        throw new ArgumentException("--window-handle must be a positive decimal integer.");
                    break;
                case "--audit":
                    auditPath = ReadValue(args, ref index, "--audit");
                    break;
                case "--port":
                    if (!int.TryParse(ReadValue(args, ref index, "--port"), out port) || port is < 1024 or > 65535)
                        throw new ArgumentException("--port must be between 1024 and 65535.");
                    break;
                case "--lease-ttl-ms":
                    if (!int.TryParse(ReadValue(args, ref index, "--lease-ttl-ms"), out leaseTtlMs) ||
                        leaseTtlMs is < 1_000 or > 30_000)
                        throw new ArgumentException("--lease-ttl-ms must be between 1000 and 30000.");
                    break;
                case "--max-actions":
                    if (!int.TryParse(ReadValue(args, ref index, "--max-actions"), out maxActions) ||
                        maxActions is < 1 or > 16)
                        throw new ArgumentException("--max-actions must be between 1 and 16.");
                    break;
                default:
                    if (args[index].Contains("token", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Input lease tokens are generated in memory and are never accepted as command-line options.");
                    throw new ArgumentException($"Unknown input-server option '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(profilePath))
            throw new ArgumentException("--profile is required.");
        if (processId <= 0)
            throw new ArgumentException("--process-id is required.");
        if (windowHandle <= 0)
            throw new ArgumentException("--window-handle is required.");
        if (string.IsNullOrWhiteSpace(auditPath))
            throw new ArgumentException("--audit is required.");
        return new InputServerOptions(
            Path.GetFullPath(profilePath), processId, windowHandle, Path.GetFullPath(auditPath), port, leaseTtlMs,
            maxActions);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}

internal static partial class InputCommands
{
    private const int MaximumRequestBodyBytes = 4 * 1024;
    private const int MaximumHeaderLineCharacters = 4 * 1024;

    private static readonly JsonSerializerOptions StrictJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> ServeAsync(InputServerOptions options)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Guarded client input requires Windows.");

        var profile = ClientLaunchProfile.LoadAndValidate(options.ProfilePath);
        using var target = VerifiedInputTarget.Open(profile, options.ProcessId, new IntPtr(options.WindowHandle));
        var initial = target.Validate(false);
        var auditDirectory = Path.GetDirectoryName(options.AuditPath);
        if (string.IsNullOrWhiteSpace(auditDirectory) || !Directory.Exists(auditDirectory))
            throw new ArgumentException("The audit file's parent directory must already exist.");

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMilliseconds(options.LeaseTtlMs);
        using var listener = new TcpListener(IPAddress.Loopback, options.Port);
        listener.Start();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            listening = $"http://127.0.0.1:{options.Port}",
            leaseToken = token,
            issuedAtUtc = issuedAt,
            expiresAtUtc = expiresAt,
            maxActions = options.MaxActions,
            target = initial,
            auditPath = options.AuditPath,
            rawTypedTextAudited = false
        }, CompactJsonOptions));
        Console.Out.Flush();

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        var actionCount = 0;
        while (!shutdown.IsCancellationRequested && actionCount < options.MaxActions && DateTimeOffset.UtcNow < expiresAt)
        {
            var remaining = expiresAt - DateTimeOffset.UtcNow;
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            wait.CancelAfter(remaining <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : remaining);
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(wait.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using (client)
            {
                var counted = await HandleRequestAsync(
                    client, target, options, token, expiresAt, actionCount, shutdown.Token);
                if (counted)
                    actionCount++;
            }
        }

        CryptographicOperations.ZeroMemory(tokenBytes);
        token = string.Empty;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            eventName = "input_server_stopped",
            actionCount,
            stopReason = shutdown.IsCancellationRequested
                ? "cancelled"
                : actionCount >= options.MaxActions ? "max_actions" : "lease_expired"
        }, CompactJsonOptions));
        return 0;
    }

    private static async Task<bool> HandleRequestAsync(
        TcpClient client,
        VerifiedInputTarget target,
        InputServerOptions options,
        string token,
        DateTimeOffset expiresAt,
        int priorActionCount,
        CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        // Latin-1 preserves the one-byte/one-character relationship required by Content-Length.
        // All accepted Stage 2 payloads are ASCII; non-ASCII text is parsed and rejected by the action validator.
        using var reader = new StreamReader(stream, Encoding.Latin1, false, leaveOpen: true);
        string requestLine;
        Dictionary<string, string> headers;
        string body;
        try
        {
            requestLine = await ReadBoundedLineAsync(reader, cancellationToken) ?? string.Empty;
            headers = await ReadHeadersAsync(reader, cancellationToken);
            body = await ReadBodyAsync(reader, headers, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            await WriteResponseAsync(stream, 400, "Bad Request", new { error = exception.Message }, cancellationToken);
            return false;
        }

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var method = parts.ElementAtOrDefault(0);
        var path = parts.ElementAtOrDefault(1)?.Split('?', 2)[0];
        if (method != "POST")
        {
            await WriteResponseAsync(stream, 405, "Method Not Allowed", new { error = "post_required" }, cancellationToken);
            return false;
        }

        if (!headers.TryGetValue("authorization", out var authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.Ordinal) ||
            !FixedTimeTokenEquals(authorization[7..], token))
        {
            await WriteResponseAsync(stream, 401, "Unauthorized", new { error = "invalid_lease" }, cancellationToken);
            return false;
        }

        if (DateTimeOffset.UtcNow >= expiresAt)
        {
            await WriteResponseAsync(stream, 410, "Gone", new { error = "lease_expired" }, cancellationToken);
            return false;
        }

        var actionNumber = priorActionCount + 1;
        InputActionResult result;
        try
        {
            result = path switch
            {
                "/v1/focus" => ExecuteFocus(target, body),
                "/v1/key" => ExecuteKey(target, Deserialize<KeyRequest>(body)),
                "/v1/click-client" => ExecuteClick(target, Deserialize<ClickRequest>(body)),
                "/v1/type-chat-command" => ExecuteChatCommand(target, Deserialize<ChatCommandRequest>(body)),
                _ => new InputActionResult(false, "unknown_action", path ?? string.Empty, null, null, null)
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            result = new InputActionResult(false, exception.Message, path ?? string.Empty, null, null, null);
        }

        AppendAudit(options.AuditPath, options, actionNumber, result);
        var statusCode = result.Accepted ? 200 : result.Reason == "unknown_action" ? 404 : 409;
        await WriteResponseAsync(stream, statusCode, result.Accepted ? "OK" : statusCode == 404 ? "Not Found" : "Conflict", new
        {
            schemaVersion = 1,
            result.Accepted,
            result.Action,
            result.Reason,
            actionNumber,
            remainingActions = options.MaxActions - actionNumber,
            expiresAtUtc = expiresAt,
            result.Key,
            result.Target,
            result.Detail
        }, cancellationToken);
        return true;
    }

    private static InputActionResult ExecuteFocus(VerifiedInputTarget target, string body)
    {
        EnsureEmptyBody(body);
        var before = target.Validate(false);
        var focus = target.Focus();
        if (!focus.Succeeded)
            return new InputActionResult(
                false,
                "The verified client window did not become foreground.",
                "focus",
                before,
                null,
                focus);
        var after = target.Validate(true);
        return new InputActionResult(true, "accepted", "focus", after, null, new
        {
            foregroundBefore = before.Foreground,
            foregroundAfter = after.Foreground,
            focus.Attempts,
            focus.ForegroundBeforeWindowHandle,
            focus.ForegroundBeforeProcessId,
            focus.ForegroundAfterWindowHandle,
            focus.ForegroundAfterProcessId,
            focus.LastActivePopupWindowHandle
        });
    }

    private static InputActionResult ExecuteKey(VerifiedInputTarget target, KeyRequest request)
    {
        var snapshot = target.Validate(true);
        var normalized = request.Key?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!VirtualKeys.TryGetValue(normalized, out var virtualKey))
            throw new ArgumentException("The key is not in the Stage 2 allowlist.");
        SendVirtualKey(virtualKey);
        return new InputActionResult(true, "accepted", "key", snapshot, normalized, null);
    }

    private static InputActionResult ExecuteClick(VerifiedInputTarget target, ClickRequest request)
    {
        var snapshot = target.Validate(true);
        var point = target.ResolveClientPoint(request.X, request.Y);
        SendLeftClick(point.ScreenX, point.ScreenY);
        return new InputActionResult(true, "accepted", "click-client", snapshot, null, new
        {
            clientX = request.X,
            clientY = request.Y,
            point.ScreenX,
            point.ScreenY,
            point.ClientWidth,
            point.ClientHeight
        });
    }

    private static InputActionResult ExecuteChatCommand(VerifiedInputTarget target, ChatCommandRequest request)
    {
        var snapshot = target.Validate(true);
        var command = request.Command ?? string.Empty;
        if (!ChatCommandRegex().IsMatch(command))
            throw new ArgumentException("The chat command must be 2-160 printable ASCII characters, begin with '/', and contain no line breaks.");
        var verbEnd = command.IndexOf(' ');
        var verb = command[1..(verbEnd < 0 ? command.Length : verbEnd)].ToLowerInvariant();
        SendVirtualKey(VirtualKeys["enter"]);
        Thread.Sleep(75);
        _ = target.Validate(true);
        SendUnicodeText(command);
        Thread.Sleep(25);
        _ = target.Validate(true);
        SendVirtualKey(VirtualKeys["enter"]);
        return new InputActionResult(true, "accepted", "type-chat-command", snapshot, null, new
        {
            commandVerb = verb,
            characterCount = command.Length,
            commandSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command))),
            rawTextRecorded = false
        });
    }

    private static T Deserialize<T>(string body) where T : class
    {
        if (body.Length > MaximumRequestBodyBytes)
            throw new ArgumentException("The request body exceeds the 4096-byte limit.");
        return JsonSerializer.Deserialize<T>(body, StrictJsonOptions) ??
               throw new ArgumentException("The request body is missing or invalid JSON.");
    }

    private static void EnsureEmptyBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return;
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any())
            throw new ArgumentException("The focus request body must be empty or {}.");
    }

    private static async Task<string?> ReadBoundedLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line?.Length > MaximumHeaderLineCharacters)
            throw new ArgumentException("An HTTP line exceeds the 4096-character limit.");
        return line;
    }

    private static async Task<Dictionary<string, string>> ReadHeadersAsync(
        StreamReader reader, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var count = 0; count < 64; count++)
        {
            var line = await ReadBoundedLineAsync(reader, cancellationToken);
            if (string.IsNullOrEmpty(line))
                return headers;
            var separator = line.IndexOf(':');
            if (separator <= 0)
                throw new ArgumentException("An HTTP header is malformed.");
            headers[line[..separator].Trim().ToLowerInvariant()] = line[(separator + 1)..].Trim();
        }
        throw new ArgumentException("The HTTP request has too many headers.");
    }

    private static async Task<string> ReadBodyAsync(
        StreamReader reader, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (!headers.TryGetValue("content-length", out var value))
            return string.Empty;
        if (!int.TryParse(value, out var length) || length is < 0 or > MaximumRequestBodyBytes)
            throw new ArgumentException("Content-Length must be between 0 and 4096.");
        var buffer = new char[length];
        var read = 0;
        while (read < length)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (count == 0)
                throw new ArgumentException("The HTTP request body ended early.");
            read += count;
        }
        return new string(buffer);
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int statusCode,
        string reason,
        object body,
        CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, Program.JsonOptions));
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(headers, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
    }

    private static bool FixedTimeTokenEquals(string supplied, string expected)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        try
        {
            return suppliedBytes.Length == expectedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedBytes);
            CryptographicOperations.ZeroMemory(expectedBytes);
        }
    }

    private static void AppendAudit(string path, InputServerOptions options, int actionNumber, InputActionResult result)
    {
        var record = new
        {
            schemaVersion = 1,
            recordedAtUtc = DateTimeOffset.UtcNow,
            actionNumber,
            result.Action,
            result.Accepted,
            result.Reason,
            processId = options.ProcessId,
            windowHandle = options.WindowHandle,
            result.Key,
            result.Detail
        };
        File.AppendAllText(
            path,
            JsonSerializer.Serialize(record, CompactJsonOptions) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static void SendVirtualKey(ushort virtualKey)
    {
        Input[] inputs =
        [
            Input.Keyboard(virtualKey, 0),
            Input.Keyboard(virtualKey, KeyEventKeyUp)
        ];
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            throw new InvalidOperationException("Windows did not accept the complete allowlisted key input.");
    }

    private static void SendUnicodeText(string text)
    {
        var inputs = new List<Input>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(Input.Unicode(character, KeyEventUnicode));
            inputs.Add(Input.Unicode(character, KeyEventUnicode | KeyEventKeyUp));
        }
        var buffer = inputs.ToArray();
        if (SendInput((uint)buffer.Length, buffer, Marshal.SizeOf<Input>()) != buffer.Length)
            throw new InvalidOperationException("Windows did not accept the complete bounded text input.");
    }

    private static void SendLeftClick(int screenX, int screenY)
    {
        var virtualX = GetSystemMetrics(SystemMetricXVirtualScreen);
        var virtualY = GetSystemMetrics(SystemMetricYVirtualScreen);
        var virtualWidth = GetSystemMetrics(SystemMetricCxVirtualScreen);
        var virtualHeight = GetSystemMetrics(SystemMetricCyVirtualScreen);
        if (virtualWidth <= 1 || virtualHeight <= 1)
            throw new InvalidOperationException("Windows returned an invalid virtual desktop rectangle.");
        var absoluteX = (int)Math.Round((screenX - virtualX) * 65535d / (virtualWidth - 1));
        var absoluteY = (int)Math.Round((screenY - virtualY) * 65535d / (virtualHeight - 1));
        Input[] inputs =
        [
            Input.Mouse(absoluteX, absoluteY, MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk),
            Input.Mouse(absoluteX, absoluteY, MouseEventLeftDown),
            Input.Mouse(absoluteX, absoluteY, MouseEventLeftUp)
        ];
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            throw new InvalidOperationException("Windows did not accept the complete bounded click input.");
    }

    private static readonly IReadOnlyDictionary<string, ushort> VirtualKeys = new Dictionary<string, ushort>(StringComparer.Ordinal)
    {
        ["enter"] = 0x0D,
        ["escape"] = 0x1B,
        ["tab"] = 0x09,
        ["space"] = 0x20,
        ["left"] = 0x25,
        ["up"] = 0x26,
        ["right"] = 0x27,
        ["down"] = 0x28,
        ["f1"] = 0x70,
        ["f2"] = 0x71,
        ["f3"] = 0x72,
        ["f4"] = 0x73,
        ["f5"] = 0x74,
        ["f6"] = 0x75,
        ["f7"] = 0x76,
        ["f8"] = 0x77,
        ["f9"] = 0x78,
        ["f10"] = 0x79,
        ["f11"] = 0x7A,
        ["f12"] = 0x7B
    };

    [GeneratedRegex("^/[ -~]{1,159}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChatCommandRegex();

    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventAbsolute = 0x8000;
    private const uint MouseEventVirtualDesk = 0x4000;
    private const int SystemMetricXVirtualScreen = 76;
    private const int SystemMetricYVirtualScreen = 77;
    private const int SystemMetricCxVirtualScreen = 78;
    private const int SystemMetricCyVirtualScreen = 79;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;

        public static Input Keyboard(ushort key, uint flags) => new()
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = flags } }
        };

        public static Input Unicode(char character, uint flags) => new()
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { ScanCode = character, Flags = flags } }
        };

        public static Input Mouse(int x, int y, uint flags) => new()
        {
            Type = 0,
            Data = new InputUnion { Mouse = new MouseInput { X = x, Y = y, Flags = flags } }
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record KeyRequest(string? Key);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ClickRequest(int X, int Y);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ChatCommandRequest(string? Command);

    private sealed record InputActionResult(
        bool Accepted,
        string Reason,
        string Action,
        InputTargetSnapshot? Target,
        string? Key,
        object? Detail);
}

internal sealed class VerifiedInputTarget : IDisposable
{
    private const uint GetAncestorRoot = 2;
    private const int ShowWindowRestore = 9;
    private const uint SetWindowPositionNoMove = 0x0002;
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionShowWindow = 0x0040;

    private readonly ClientLaunchProfile _profile;
    private readonly Process _process;
    private readonly IntPtr _windowHandle;

    private VerifiedInputTarget(ClientLaunchProfile profile, Process process, IntPtr windowHandle)
    {
        _profile = profile;
        _process = process;
        _windowHandle = windowHandle;
    }

    public static VerifiedInputTarget Open(ClientLaunchProfile profile, int processId, IntPtr windowHandle)
    {
        var process = Process.GetProcessById(processId);
        return new VerifiedInputTarget(profile, process, windowHandle);
    }

    public InputTargetSnapshot Validate(bool requireForeground)
    {
        if (_process.HasExited)
            throw new InvalidOperationException("The verified client process has exited.");
        _process.Refresh();
        if (!string.Equals(_process.ProcessName, _profile.ProcessName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The target PID no longer has the allowlisted process name.");
        var executablePath = Path.GetFullPath(_process.MainModule?.FileName ?? string.Empty);
        if (!string.Equals(executablePath, _profile.ClientExecutablePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The target PID no longer has the allowlisted executable path.");
        var executableHash = SHA256.HashData(File.ReadAllBytes(executablePath));
        if (!CryptographicOperations.FixedTimeEquals(executableHash, Convert.FromHexString(_profile.ClientExecutableSha256)))
            throw new InvalidOperationException("The target PID executable no longer matches the allowlisted SHA-256.");
        if (!IsWindow(_windowHandle))
            throw new InvalidOperationException("The requested client window handle is no longer valid.");
        _ = GetWindowThreadProcessId(_windowHandle, out var windowProcessId);
        if (windowProcessId != _process.Id)
            throw new InvalidOperationException("The requested window handle is not owned by the target PID.");
        if (_process.MainWindowHandle != _windowHandle)
            throw new InvalidOperationException("The target PID's current main window differs from the leased handle.");
        if (!IsWindowVisible(_windowHandle))
            throw new InvalidOperationException("The verified client window is not visible.");
        if (!GetClientRect(_windowHandle, out var rectangle) || rectangle.Right <= 0 || rectangle.Bottom <= 0)
            throw new InvalidOperationException("The verified client window has no usable client rectangle.");
        var foreground = IsForeground();
        if (requireForeground && !foreground)
            throw new InvalidOperationException("The verified client window is not foreground; call /v1/focus first.");
        return new InputTargetSnapshot(
            _process.Id,
            _windowHandle.ToInt64(),
            executablePath,
            Convert.ToHexString(executableHash),
            _process.MainWindowTitle,
            rectangle.Right,
            rectangle.Bottom,
            foreground);
    }

    public FocusAttemptResult Focus()
    {
        var foregroundBefore = GetForegroundWindow();
        _ = GetWindowThreadProcessId(foregroundBefore, out var foregroundBeforeProcessId);
        var attempts = 0;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            attempts = attempt + 1;
            _process.Refresh();
            if (_process.HasExited || !IsWindow(_windowHandle) || _process.MainWindowHandle != _windowHandle)
                break;

            _ = ShowWindow(_windowHandle, ShowWindowRestore);
            var foregroundWindow = GetForegroundWindow();
            var currentThread = GetCurrentThreadId();
            var targetThread = GetWindowThreadProcessId(_windowHandle, out _);
            var foregroundThread = foregroundWindow == IntPtr.Zero
                ? 0
                : GetWindowThreadProcessId(foregroundWindow, out _);
            var attachedForeground = foregroundThread != 0 && foregroundThread != currentThread &&
                                     AttachThreadInput(currentThread, foregroundThread, true);
            var attachedTarget = targetThread != 0 && targetThread != currentThread &&
                                 AttachThreadInput(currentThread, targetThread, true);
            try
            {
                _ = SetWindowPos(
                    _windowHandle,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    SetWindowPositionNoMove | SetWindowPositionNoSize | SetWindowPositionShowWindow);
                _ = BringWindowToTop(_windowHandle);
                _ = SetActiveWindow(_windowHandle);
                _ = SetFocus(_windowHandle);
                _ = SetForegroundWindow(_windowHandle);
                SwitchToThisWindow(_windowHandle, true);
            }
            finally
            {
                if (attachedTarget)
                    _ = AttachThreadInput(currentThread, targetThread, false);
                if (attachedForeground)
                    _ = AttachThreadInput(currentThread, foregroundThread, false);
            }

            var attemptWait = Stopwatch.StartNew();
            while (attemptWait.ElapsedMilliseconds < 500)
            {
                if (IsForeground())
                    return CaptureFocusAttempt(
                        true, attempts, foregroundBefore, foregroundBeforeProcessId);
                Thread.Sleep(25);
            }
        }

        return CaptureFocusAttempt(
            IsForeground(), attempts, foregroundBefore, foregroundBeforeProcessId);
    }

    public ClientPoint ResolveClientPoint(int x, int y)
    {
        if (!GetClientRect(_windowHandle, out var rectangle))
            throw new InvalidOperationException("Windows could not read the verified client rectangle.");
        if (x < 0 || y < 0 || x >= rectangle.Right || y >= rectangle.Bottom)
            throw new ArgumentException("The click point is outside the verified client rectangle.");
        var point = new NativePoint { X = x, Y = y };
        if (!ClientToScreen(_windowHandle, ref point))
            throw new InvalidOperationException("Windows could not transform the client-relative click point.");
        var windowAtPoint = WindowFromPoint(point);
        if (windowAtPoint == IntPtr.Zero || GetAncestor(windowAtPoint, GetAncestorRoot) != _windowHandle)
            throw new InvalidOperationException("The client-relative click point is occluded by a different top-level window.");
        return new ClientPoint(x, y, point.X, point.Y, rectangle.Right, rectangle.Bottom);
    }

    public void Dispose() => _process.Dispose();

    private bool IsForeground()
    {
        var foreground = GetForegroundWindow();
        return foreground != IntPtr.Zero && GetAncestor(foreground, GetAncestorRoot) == _windowHandle;
    }

    private FocusAttemptResult CaptureFocusAttempt(
        bool succeeded,
        int attempts,
        IntPtr foregroundBefore,
        uint foregroundBeforeProcessId)
    {
        var foregroundAfter = GetForegroundWindow();
        _ = GetWindowThreadProcessId(foregroundAfter, out var foregroundAfterProcessId);
        return new FocusAttemptResult(
            succeeded,
            attempts,
            foregroundBefore.ToInt64(),
            foregroundBeforeProcessId,
            foregroundAfter.ToInt64(),
            foregroundAfterProcessId,
            GetAncestor(foregroundAfter, GetAncestorRoot).ToInt64(),
            _windowHandle.ToInt64(),
            GetLastActivePopup(_windowHandle).ToInt64(),
            IsWindowVisible(_windowHandle),
            IsIconic(_windowHandle));
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetLastActivePopup(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr windowHandle, bool altTab);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint sourceThreadId, uint targetThreadId, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}

internal sealed record InputTargetSnapshot(
    int ProcessId,
    long WindowHandle,
    string ExecutablePath,
    string ExecutableSha256,
    string WindowTitle,
    int ClientWidth,
    int ClientHeight,
    bool Foreground);

internal sealed record ClientPoint(
    int ClientX,
    int ClientY,
    int ScreenX,
    int ScreenY,
    int ClientWidth,
    int ClientHeight);

internal sealed record FocusAttemptResult(
    bool Succeeded,
    int Attempts,
    long ForegroundBeforeWindowHandle,
    uint ForegroundBeforeProcessId,
    long ForegroundAfterWindowHandle,
    uint ForegroundAfterProcessId,
    long ForegroundAfterRootWindowHandle,
    long TargetWindowHandle,
    long LastActivePopupWindowHandle,
    bool TargetVisible,
    bool TargetMinimized);
