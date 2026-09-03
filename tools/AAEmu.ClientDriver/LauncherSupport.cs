using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AAEmu.ClientDriver;

internal sealed record LauncherCommandOptions(string ProfilePath, string? WaitFor, int TimeoutMs)
{
    private static readonly HashSet<string> WaitTargets =
    [
        "process_started",
        "login_connected",
        "world_authorized",
        "world_loaded"
    ];

    public static LauncherCommandOptions Parse(string[] args, bool requireWaitTarget)
    {
        string? profilePath = null;
        string? waitFor = null;
        var timeoutMs = 120_000;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--profile":
                    profilePath = ReadValue(args, ref index, "--profile");
                    break;
                case "--wait-for":
                    waitFor = ReadValue(args, ref index, "--wait-for").ToLowerInvariant();
                    break;
                case "--timeout-ms":
                    if (!int.TryParse(ReadValue(args, ref index, "--timeout-ms"), out timeoutMs) ||
                        timeoutMs is < 1_000 or > 300_000)
                        throw new ArgumentException("--timeout-ms must be between 1000 and 300000.");
                    break;
                default:
                    if (args[index].StartsWith("--password", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Password command-line options are forbidden; use console or redirected standard input.");
                    throw new ArgumentException($"Unknown launcher option '{args[index]}'. Passwords are never accepted as command-line options.");
            }
        }

        if (string.IsNullOrWhiteSpace(profilePath))
            throw new ArgumentException("--profile is required.");
        if (requireWaitTarget && string.IsNullOrWhiteSpace(waitFor))
            throw new ArgumentException("--wait-for is required for launch.");
        if (waitFor != null && !WaitTargets.Contains(waitFor))
            throw new ArgumentException($"Unsupported --wait-for target '{waitFor}'.");

        return new LauncherCommandOptions(profilePath, waitFor, timeoutMs);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}

internal sealed record CloseCommandOptions(string ProfilePath, int ProcessId, int TimeoutMs)
{
    public static CloseCommandOptions Parse(string[] args)
    {
        string? profilePath = null;
        var processId = 0;
        var timeoutMs = 30_000;

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
                case "--timeout-ms":
                    if (!int.TryParse(ReadValue(args, ref index, "--timeout-ms"), out timeoutMs) ||
                        timeoutMs is < 1_000 or > 120_000)
                        throw new ArgumentException("--timeout-ms must be between 1000 and 120000.");
                    break;
                default:
                    throw new ArgumentException($"Unknown close option '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(profilePath))
            throw new ArgumentException("--profile is required.");
        if (processId <= 0)
            throw new ArgumentException("--process-id is required.");
        return new CloseCommandOptions(profilePath, processId, timeoutMs);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}

internal static partial class LauncherCommands
{
    private const uint WindowCloseMessage = 0x0010;

    public static int VerifyProfile(LauncherCommandOptions options)
    {
        var profile = ClientLaunchProfile.LoadAndValidate(options.ProfilePath);
        Console.WriteLine(JsonSerializer.Serialize(profile.SafePlan(), Program.JsonOptions));
        return 0;
    }

    public static int ProbeLauncher(LauncherCommandOptions options)
    {
        var profile = ClientLaunchProfile.LoadAndValidate(options.ProfilePath);
        EnsureExclusive(profile);
        using var launcher = new ReflectionLauncher(profile);
        launcher.Prepare("aaemu-client-driver-probe", "probe-only-not-a-real-password");
        var initialized = launcher.Initialize();
        var launchArguments = SanitizeLaunchArguments(launcher.LaunchArguments);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            initialized,
            launcherType = ReflectionLauncher.LauncherTypeName,
            launchArguments,
            processStarted = false,
            credential = "synthetic_probe_only"
        }, Program.JsonOptions));
        return initialized ? 0 : 3;
    }

    public static async Task<int> LaunchAsync(LauncherCommandOptions options)
    {
        var profile = ClientLaunchProfile.LoadAndValidate(options.ProfilePath);
        EnsureExclusive(profile);
        var credentials = ReadCredentials();
        var requestedAt = DateTimeOffset.Now;

        using var launcher = new ReflectionLauncher(profile);
        launcher.Prepare(credentials.UserName, credentials.Password);
        credentials = credentials with { Password = string.Empty };
        if (!launcher.Initialize())
            throw new InvalidOperationException("The launcher assembly rejected initialization.");
        if (!launcher.Launch())
            throw new InvalidOperationException("The launcher assembly did not create the client process.");

        using var process = launcher.RunningProcess ??
                            throw new InvalidOperationException("The launcher reported success without a client process handle.");
        var processId = process.Id;
        var processStartedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        var finalized = launcher.FinalizeLaunch();
        var target = options.WaitFor!;

        var outcome = target == "process_started"
            ? new LaunchWaitOutcome(true, "reached", target, 0, Program.CaptureStatus(profile.StatusOptions()))
            : await WaitForLifecycleAsync(profile, process, requestedAt, target, options.TimeoutMs);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            outcome.Status,
            outcome.Reached,
            outcome.Target,
            outcome.ElapsedMs,
            processId,
            processStartedAtUtc = processStartedAt,
            launcherFinalized = finalized,
            passwordSource = "console_or_redirected_standard_input",
            profile = profile.SafePlan(),
            client = outcome.Snapshot
        }, Program.JsonOptions));

        return outcome.Status switch
        {
            "reached" => 0,
            "timeout" => 3,
            "process_exited" => 4,
            _ => 5
        };
    }

    public static async Task<int> RequestCloseAsync(CloseCommandOptions options)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Graceful client close requires Windows.");

        var profile = ClientLaunchProfile.LoadAndValidate(options.ProfilePath);
        using var process = Process.GetProcessById(options.ProcessId);
        if (!string.Equals(process.ProcessName, profile.ProcessName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The requested PID does not have the allowlisted client process name.");

        var executablePath = Path.GetFullPath(process.MainModule?.FileName ?? string.Empty);
        if (!string.Equals(executablePath, profile.ClientExecutablePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The requested PID is not running the allowlisted client executable path.");
        var executableHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath)));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(executableHash),
                Convert.FromHexString(profile.ClientExecutableSha256)))
            throw new InvalidOperationException("The requested PID executable no longer matches the allowlisted SHA-256.");

        process.Refresh();
        var windowHandle = process.MainWindowHandle;
        if (windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("The allowlisted client process does not currently expose a main window.");
        if (!PostMessage(windowHandle, WindowCloseMessage, IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException("Windows rejected the graceful close request.");

        var exited = await WaitForExitAsync(process, options.TimeoutMs);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            status = exited ? "exited" : "close_requested",
            closeRequested = true,
            processExited = exited,
            processId = options.ProcessId,
            executablePath,
            executableSha256 = executableHash,
            timeoutMs = options.TimeoutMs,
            forcedTermination = false
        }, Program.JsonOptions));
        return exited ? 0 : 3;
    }

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds <= timeoutMs)
        {
            if (process.HasExited)
                return true;
            await Task.Delay(100);
        }
        return process.HasExited;
    }

    private static async Task<LaunchWaitOutcome> WaitForLifecycleAsync(
        ClientLaunchProfile profile,
        Process process,
        DateTimeOffset requestedAt,
        string target,
        int timeoutMs)
    {
        var marker = target switch
        {
            "login_connected" => "loginConnected",
            "world_authorized" => "worldAuthorized",
            "world_loaded" => "worldLoaded",
            _ => throw new ArgumentException($"Unsupported lifecycle target '{target}'.")
        };
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds <= timeoutMs)
        {
            if (process.HasExited)
            {
                return new LaunchWaitOutcome(
                    false,
                    "process_exited",
                    target,
                    stopwatch.ElapsedMilliseconds,
                    Program.CaptureStatus(profile.StatusOptions()));
            }

            var log = Program.ParseLog(profile.LogPath);
            var belongsToThisLaunch = log.SessionStartedAt.HasValue &&
                                      log.SessionStartedAt.Value >= requestedAt.AddSeconds(-5);
            if (belongsToThisLaunch && log.Milestones.ContainsKey(marker))
            {
                return new LaunchWaitOutcome(
                    true,
                    "reached",
                    target,
                    stopwatch.ElapsedMilliseconds,
                    Program.CaptureStatus(profile.StatusOptions()));
            }

            await Task.Delay(250);
        }

        return new LaunchWaitOutcome(
            false,
            "timeout",
            target,
            stopwatch.ElapsedMilliseconds,
            Program.CaptureStatus(profile.StatusOptions()));
    }

    private static void EnsureExclusive(ClientLaunchProfile profile)
    {
        var conflicts = new List<string>();
        foreach (var processName in new[] { profile.ProcessName, "aaemu.launcher" }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                    conflicts.Add($"{process.ProcessName}:{process.Id}");
            }
        }

        if (conflicts.Count > 0)
            throw new InvalidOperationException($"Refusing an ambiguous launch while matching processes exist: {string.Join(", ", conflicts)}.");
    }

    private static CredentialPair ReadCredentials()
    {
        if (Console.IsInputRedirected)
        {
            var redirectedUser = Console.ReadLine();
            var redirectedPassword = Console.ReadLine();
            return ValidateCredentials(redirectedUser, redirectedPassword);
        }

        Console.Error.Write("Username: ");
        var userName = Console.ReadLine();
        Console.Error.Write("Password: ");
        var password = ReadMaskedPassword();
        Console.Error.WriteLine();
        return ValidateCredentials(userName, password);
    }

    private static string ReadMaskedPassword()
    {
        var characters = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count > 0)
                    characters.RemoveAt(characters.Count - 1);
                continue;
            }
            if (!char.IsControl(key.KeyChar))
                characters.Add(key.KeyChar);
        }

        return new string(characters.ToArray());
    }

    private static CredentialPair ValidateCredentials(string? userName, string? password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            throw new ArgumentException("A username line followed by a password line is required on standard input.");
        if (userName.Contains('\r') || userName.Contains('\n'))
            throw new ArgumentException("The username must be one line.");
        return new CredentialPair(userName, password);
    }

    private static string SanitizeLaunchArguments(string value) =>
        HandleArgumentRegex().Replace(value, "-handle <redacted>");

    [GeneratedRegex("-handle\\s+[0-9A-Fa-f]+:[0-9A-Fa-f]+", RegexOptions.CultureInvariant)]
    private static partial Regex HandleArgumentRegex();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    private sealed record CredentialPair(string UserName, string Password);
    private sealed record LaunchWaitOutcome(bool Reached, string Status, string Target, long ElapsedMs, ClientStatus Snapshot);
}

internal sealed partial record ClientLaunchProfile(
    int SchemaVersion,
    string LauncherAssemblyPath,
    string LauncherAssemblySha256,
    string ClientExecutablePath,
    string ClientExecutableSha256,
    string ServerAddress,
    ushort ServerPort,
    string Locale,
    string LoginType,
    string LogPath,
    string ProcessName,
    bool HideSplash)
{
    public static ClientLaunchProfile LoadAndValidate(string profilePath)
    {
        var fullProfilePath = Path.GetFullPath(profilePath);
        using var stream = new FileStream(fullProfilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var loaded = JsonSerializer.Deserialize<ClientLaunchProfile>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("The launch profile is empty or invalid JSON.");

        var profileDirectory = Path.GetDirectoryName(fullProfilePath) ?? Environment.CurrentDirectory;
        var profile = loaded with
        {
            LauncherAssemblyPath = ResolvePath(profileDirectory, loaded.LauncherAssemblyPath),
            ClientExecutablePath = ResolvePath(profileDirectory, loaded.ClientExecutablePath),
            LogPath = ResolvePath(profileDirectory, loaded.LogPath),
            LauncherAssemblySha256 = NormalizeHash(loaded.LauncherAssemblySha256, "launcherAssemblySha256"),
            ClientExecutableSha256 = NormalizeHash(loaded.ClientExecutableSha256, "clientExecutableSha256")
        };
        profile.Validate();
        return profile;
    }

    public object SafePlan()
    {
        var launcherName = AssemblyName.GetAssemblyName(LauncherAssemblyPath);
        var clientVersion = FileVersionInfo.GetVersionInfo(ClientExecutablePath);
        return new
        {
            schemaVersion = SchemaVersion,
            launcher = new
            {
                path = LauncherAssemblyPath,
                sha256 = LauncherAssemblySha256,
                assembly = launcherName.Name,
                version = launcherName.Version?.ToString()
            },
            client = new
            {
                path = ClientExecutablePath,
                sha256 = ClientExecutableSha256,
                fileVersion = clientVersion.FileVersion,
                processName = ProcessName
            },
            server = new { address = ServerAddress, port = ServerPort },
            locale = Locale,
            loginType = LoginType,
            logPath = LogPath,
            hideSplash = HideSplash,
            credentialsStoredInProfile = false,
            plaintextLauncherPasswordRead = false
        };
    }

    public DriverOptions StatusOptions() => new(LogPath, ProcessName, 45831, false, false, 30_000);

    private void Validate()
    {
        if (SchemaVersion != 1)
            throw new ArgumentException($"Unsupported launch profile schema {SchemaVersion}; expected 1.");
        ValidateFile(LauncherAssemblyPath, LauncherAssemblySha256, ".dll", "launcher assembly");
        ValidateFile(ClientExecutablePath, ClientExecutableSha256, ".exe", "client executable");
        var launcherName = AssemblyName.GetAssemblyName(LauncherAssemblyPath);
        if (!string.Equals(launcherName.Name, "AAEmu.Common.Launcher", StringComparison.Ordinal))
            throw new ArgumentException("The allowlisted launcher assembly is not AAEmu.Common.Launcher.");
        if (!string.Equals(LoginType, "trino_1_2", StringComparison.Ordinal))
            throw new ArgumentException("Stage 1 supports only the verified trino_1_2 launch type.");
        if (!IPAddress.TryParse(ServerAddress, out var address) || !IPAddress.IsLoopback(address))
            throw new ArgumentException("Stage 1 permits only an explicit loopback server address.");
        if (ServerPort == 0)
            throw new ArgumentException("The server port must be non-zero.");
        if (!LocaleRegex().IsMatch(Locale))
            throw new ArgumentException("The locale must look like en_us or ru.");
        if (string.IsNullOrWhiteSpace(ProcessName) ||
            !string.Equals(Path.GetFileNameWithoutExtension(ClientExecutablePath), ProcessName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("processName must exactly match the allowlisted client executable name.");
        if (File.Exists(Path.Combine(Path.GetDirectoryName(LauncherAssemblyPath)!, "customticket.xml")))
            throw new ArgumentException("customticket.xml is present beside the launcher assembly; Stage 1 refuses custom credential tickets.");
    }

    private static void ValidateFile(string path, string expectedHash, string extension, string description)
    {
        if (!File.Exists(path))
            throw new ArgumentException($"The {description} does not exist: {path}");
        if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The {description} must be a {extension} file.");
        var actualHash = Convert.FromHexString(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var expectedHashBytes = Convert.FromHexString(expectedHash);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHashBytes))
            throw new ArgumentException($"The {description} SHA-256 does not match the allowlisted profile.");
    }

    private static string ResolvePath(string profileDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Launch profile paths must not be empty.");
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(profileDirectory, path));
    }

    private static string NormalizeHash(string hash, string field)
    {
        var normalized = hash?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException($"{field} must be exactly 64 hexadecimal SHA-256 characters.");
        return normalized;
    }

    [GeneratedRegex("^[a-z]{2}(?:_[a-z]{2})?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LocaleRegex();
}

internal sealed class ReflectionLauncher : IDisposable
{
    public const string LauncherTypeName = "AAEmu.Launcher.Trion12.Trion_1_2_Launcher";

    private readonly ClientLaunchProfile _profile;
    private readonly LauncherAssemblyContext _context;
    private readonly object _instance;
    private readonly Type _type;
    private bool _disposed;

    public ReflectionLauncher(ClientLaunchProfile profile)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Trion launcher adapter requires Windows.");
        _profile = profile;
        _context = new LauncherAssemblyContext(Path.GetDirectoryName(profile.LauncherAssemblyPath)!);
        var assembly = _context.LoadFromAssemblyPath(profile.LauncherAssemblyPath);
        _type = assembly.GetType(LauncherTypeName, true, false)!;
        _instance = Activator.CreateInstance(_type) ?? throw new InvalidOperationException("Could not instantiate the Trion 1.2 launcher.");
    }

    public string LaunchArguments => GetProperty<string>("LaunchArguments") ?? string.Empty;
    public Process? RunningProcess => GetProperty<Process>("RunningProcess");

    public void Prepare(string userName, string password)
    {
        SetProperty("UserName", userName);
        SetProperty("LoginServerAdress", _profile.ServerAddress);
        SetProperty("LoginServerPort", _profile.ServerPort);
        SetProperty("GameExeFilePath", _profile.ClientExecutablePath);
        SetProperty("Locale", _profile.Locale);
        SetProperty("HShieldArgs", "+acpxmk");
        SetProperty("ExtraArguments", _profile.HideSplash ? "-nosplash" : string.Empty);
        if (!InvokeBoolean("SetPassword", password))
            throw new InvalidOperationException("The launcher assembly rejected the password hashing step.");
    }

    public bool Initialize() => InLauncherDirectory(() => InvokeBoolean("InitializeForLaunch"));
    public bool Launch() => InLauncherDirectory(() => InvokeBoolean("Launch"));
    public bool FinalizeLaunch() => InvokeBoolean("FinalizeLaunch");

    public void Dispose()
    {
        if (_disposed)
            return;
        try
        {
            _type.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance)?.Invoke(_instance, null);
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException($"Launcher cleanup failed: {exception.InnerException?.Message ?? exception.Message}", exception.InnerException ?? exception);
        }
        finally
        {
            _context.Unload();
            _disposed = true;
        }
    }

    private T InLauncherDirectory<T>(Func<T> action)
    {
        var previousDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.GetDirectoryName(_profile.LauncherAssemblyPath)!;
        try
        {
            return action();
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
        }
    }

    private void SetProperty(string name, object value)
    {
        var property = _type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) ??
                       throw new MissingMemberException(LauncherTypeName, name);
        property.SetValue(_instance, value);
    }

    private T? GetProperty<T>(string name) where T : class
    {
        var property = _type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) ??
                       throw new MissingMemberException(LauncherTypeName, name);
        return property.GetValue(_instance) as T;
    }

    private bool InvokeBoolean(string name, params object[] arguments)
    {
        var method = _type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance) ??
                     throw new MissingMethodException(LauncherTypeName, name);
        try
        {
            return method.Invoke(_instance, arguments) is true;
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException($"Launcher {name} failed: {exception.InnerException?.Message ?? exception.Message}", exception.InnerException ?? exception);
        }
    }

    private sealed class LauncherAssemblyContext(string launcherDirectory) : AssemblyLoadContext(true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var candidate = Path.Combine(launcherDirectory, $"{assemblyName.Name}.dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }
    }
}
