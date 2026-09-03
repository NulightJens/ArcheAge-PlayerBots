using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace AAEmu.ClientDriver;

internal sealed record CaptureCommandOptions(
    string ProfilePath,
    int ProcessId,
    long WindowHandle,
    string OutputPath)
{
    public static CaptureCommandOptions Parse(string[] args)
    {
        string? profilePath = null;
        string? outputPath = null;
        var processId = 0;
        long windowHandle = 0;

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
                    if (!long.TryParse(ReadValue(args, ref index, "--window-handle"), out windowHandle) ||
                        windowHandle <= 0)
                        throw new ArgumentException("--window-handle must be a positive decimal integer.");
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref index, "--output");
                    break;
                default:
                    throw new ArgumentException($"Unknown capture option '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(profilePath))
            throw new ArgumentException("--profile is required.");
        if (processId <= 0)
            throw new ArgumentException("--process-id is required.");
        if (windowHandle <= 0)
            throw new ArgumentException("--window-handle is required.");
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("--output is required.");

        var fullOutputPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(fullOutputPath), ".bmp", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Stage 3 capture output must use the .bmp extension.");
        return new CaptureCommandOptions(
            Path.GetFullPath(profilePath), processId, windowHandle, fullOutputPath);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}

internal static class CaptureCommands
{
    public static int Capture(CaptureCommandOptions options)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Verified client-window capture requires Windows.");
        if (File.Exists(options.OutputPath))
            throw new ArgumentException("The capture output already exists; Stage 3 will not overwrite evidence.");
        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            throw new ArgumentException("The capture output's parent directory must already exist.");

        var profile = ClientLaunchProfile.LoadAndValidate(options.ProfilePath);
        using var target = VerifiedInputTarget.Open(profile, options.ProcessId, new IntPtr(options.WindowHandle));
        var snapshot = target.Validate(true);
        if (snapshot.ClientWidth > 16_384 || snapshot.ClientHeight > 16_384)
            throw new InvalidOperationException("The verified client rectangle exceeds the 16384-pixel capture bound.");

        var insetX = Math.Min(2, snapshot.ClientWidth - 1);
        var insetY = Math.Min(2, snapshot.ClientHeight - 1);
        var rightX = Math.Max(insetX, snapshot.ClientWidth - 1 - insetX);
        var bottomY = Math.Max(insetY, snapshot.ClientHeight - 1 - insetY);
        ClientPoint[] samples =
        [
            target.ResolveClientPoint(insetX, insetY),
            target.ResolveClientPoint(rightX, insetY),
            target.ResolveClientPoint(snapshot.ClientWidth / 2, snapshot.ClientHeight / 2),
            target.ResolveClientPoint(insetX, bottomY),
            target.ResolveClientPoint(rightX, bottomY)
        ];
        _ = target.Validate(true);

        var originX = samples[0].ScreenX - insetX;
        var originY = samples[0].ScreenY - insetY;
        ForegroundBitmapCapture.WriteBmp(
            options.OutputPath, originX, originY, snapshot.ClientWidth, snapshot.ClientHeight);
        var file = new FileInfo(options.OutputPath);
        var captureHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(options.OutputPath)));

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            status = "captured",
            capturedAtUtc = DateTimeOffset.UtcNow,
            outputPath = options.OutputPath,
            outputSha256 = captureHash,
            outputBytes = file.Length,
            width = snapshot.ClientWidth,
            height = snapshot.ClientHeight,
            captureMethod = "foreground_desktop_bitblt",
            occlusionSampleCount = samples.Length,
            target = snapshot,
            rawWindowOrDesktopAccess = false
        }, Program.JsonOptions));
        return 0;
    }
}

internal static class ForegroundBitmapCapture
{
    private const uint SourceCopy = 0x00CC0020;
    private const uint CaptureLayeredWindows = 0x40000000;
    private const uint DibRgbColors = 0;
    private const uint BitmapCompressionRgb = 0;
    private const int BitmapFileHeaderBytes = 14;
    private const int BitmapInfoHeaderBytes = 40;

    public static void WriteBmp(string outputPath, int screenX, int screenY, int width, int height)
    {
        var imageBytes = checked(width * height * 4);
        if (imageBytes > 512 * 1024 * 1024)
            throw new InvalidOperationException("The verified client capture exceeds the 512 MiB memory bound.");

        var screenDevice = GetDC(IntPtr.Zero);
        if (screenDevice == IntPtr.Zero)
            throw new InvalidOperationException("Windows did not provide the foreground desktop device context.");
        var memoryDevice = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousObject = IntPtr.Zero;
        byte[] pixels;
        try
        {
            memoryDevice = CreateCompatibleDC(screenDevice);
            if (memoryDevice == IntPtr.Zero)
                throw new InvalidOperationException("Windows did not create the bounded capture device context.");
            bitmap = CreateCompatibleBitmap(screenDevice, width, height);
            if (bitmap == IntPtr.Zero)
                throw new InvalidOperationException("Windows did not create the bounded capture bitmap.");
            previousObject = SelectObject(memoryDevice, bitmap);
            if (previousObject == IntPtr.Zero || previousObject == new IntPtr(-1))
                throw new InvalidOperationException("Windows did not select the bounded capture bitmap.");
            if (!BitBlt(
                    memoryDevice, 0, 0, width, height, screenDevice, screenX, screenY,
                    SourceCopy | CaptureLayeredWindows))
                throw new InvalidOperationException("Windows rejected the foreground client-area capture.");

            pixels = new byte[imageBytes];
            var bitmapInfo = new BitmapInfoHeader
            {
                Size = BitmapInfoHeaderBytes,
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = BitmapCompressionRgb,
                SizeImage = (uint)imageBytes
            };
            var copiedLines = GetDIBits(
                memoryDevice, bitmap, 0, (uint)height, pixels, ref bitmapInfo, DibRgbColors);
            if (copiedLines != height)
                throw new InvalidOperationException(
                    $"Windows returned {copiedLines} of {height} requested capture scan lines.");
        }
        finally
        {
            if (previousObject != IntPtr.Zero && previousObject != new IntPtr(-1) && memoryDevice != IntPtr.Zero)
                _ = SelectObject(memoryDevice, previousObject);
            if (bitmap != IntPtr.Zero)
                _ = DeleteObject(bitmap);
            if (memoryDevice != IntPtr.Zero)
                _ = DeleteDC(memoryDevice);
            _ = ReleaseDC(IntPtr.Zero, screenDevice);
        }

        var fileSize = checked(BitmapFileHeaderBytes + BitmapInfoHeaderBytes + pixels.Length);
        using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x4D42);
        writer.Write(fileSize);
        writer.Write(0u);
        writer.Write(BitmapFileHeaderBytes + BitmapInfoHeaderBytes);
        writer.Write((uint)BitmapInfoHeaderBytes);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(BitmapCompressionRgb);
        writer.Write((uint)pixels.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(pixels);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr deviceContext, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        IntPtr destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr deviceContext,
        IntPtr bitmap,
        uint startScan,
        uint scanLines,
        byte[] bits,
        ref BitmapInfoHeader bitmapInfo,
        uint usage);
}
