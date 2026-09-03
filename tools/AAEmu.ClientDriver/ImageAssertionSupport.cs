using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AAEmu.ClientDriver;

internal sealed record ImageAssertionCommandOptions(string CapturePath, string SpecPath)
{
    public static ImageAssertionCommandOptions Parse(string[] args)
    {
        string? capturePath = null;
        string? specPath = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--capture":
                    capturePath = ReadValue(args, ref index, "--capture");
                    break;
                case "--spec":
                    specPath = ReadValue(args, ref index, "--spec");
                    break;
                default:
                    throw new ArgumentException($"Unknown image-assertion option '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(capturePath))
            throw new ArgumentException("--capture is required.");
        if (string.IsNullOrWhiteSpace(specPath))
            throw new ArgumentException("--spec is required.");
        return new ImageAssertionCommandOptions(
            RequireExtension(capturePath, ".bmp", "capture"),
            RequireExtension(specPath, ".json", "assertion spec"));
    }

    private static string RequireExtension(string path, string extension, string description)
    {
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The {description} must use the {extension} extension.");
        if (!File.Exists(fullPath))
            throw new ArgumentException($"The {description} does not exist: {fullPath}");
        return fullPath;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}

internal static class ImageAssertionCommands
{
    public static int Assert(ImageAssertionCommandOptions options)
    {
        var spec = ImageAssertionSpec.LoadAndValidate(options.SpecPath);
        var capture = RgbBitmap.Load(options.CapturePath);
        var regionResults = spec.Regions.Select(region => AssertRegion(capture, region)).ToArray();
        var specDirectory = Path.GetDirectoryName(options.SpecPath)!;
        var templateResults = spec.Templates
            .Select(template => AssertTemplate(capture, template, specDirectory))
            .ToArray();
        var passed = regionResults.All(result => result.Passed) &&
                     templateResults.All(result => result.Passed);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            status = passed ? "passed" : "failed",
            assertedAtUtc = DateTimeOffset.UtcNow,
            capture = new
            {
                path = options.CapturePath,
                sha256 = capture.SourceSha256,
                capture.Width,
                capture.Height,
                pixelFormat = "rgb24_top_down"
            },
            spec = new
            {
                path = options.SpecPath,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(options.SpecPath))),
                schemaVersion = spec.SchemaVersion
            },
            regions = regionResults,
            templates = templateResults,
            summary = new
            {
                assertionCount = regionResults.Length + templateResults.Length,
                passedCount = regionResults.Count(result => result.Passed) +
                              templateResults.Count(result => result.Passed),
                failedCount = regionResults.Count(result => !result.Passed) +
                              templateResults.Count(result => !result.Passed),
                ocrUsed = false,
                comparison = "exact_rgb"
            }
        }, Program.JsonOptions));
        return passed ? 0 : 3;
    }

    private static RegionAssertionResult AssertRegion(RgbBitmap capture, ExactRegionAssertion assertion)
    {
        assertion.Rectangle.ValidateWithin(capture.Width, capture.Height, assertion.Name, "region");
        var actualHash = capture.HashRegion(assertion.Rectangle);
        return new RegionAssertionResult(
            assertion.Name,
            assertion.Rectangle,
            assertion.ExpectedRgbSha256,
            actualHash,
            CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(assertion.ExpectedRgbSha256),
                Convert.FromHexString(actualHash)));
    }

    private static TemplateAssertionResult AssertTemplate(
        RgbBitmap capture,
        ExactTemplateAssertion assertion,
        string specDirectory)
    {
        assertion.SearchRectangle.ValidateWithin(capture.Width, capture.Height, assertion.Name, "template search");
        var templatePath = Path.GetFullPath(Path.IsPathRooted(assertion.TemplatePath)
            ? assertion.TemplatePath
            : Path.Combine(specDirectory, assertion.TemplatePath));
        if (!string.Equals(Path.GetExtension(templatePath), ".bmp", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(templatePath))
            throw new ArgumentException($"Template '{assertion.Name}' must resolve to an existing .bmp file.");
        var template = RgbBitmap.Load(templatePath);
        var actualMatches = capture.FindExactMatches(template, assertion.SearchRectangle);
        var expectedMatches = assertion.ExpectedMatches
            .OrderBy(point => point.Y)
            .ThenBy(point => point.X)
            .ToArray();
        var passed = actualMatches.SequenceEqual(expectedMatches);
        return new TemplateAssertionResult(
            assertion.Name,
            templatePath,
            template.SourceSha256,
            template.Width,
            template.Height,
            assertion.SearchRectangle,
            expectedMatches,
            actualMatches,
            passed);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ImageAssertionSpec(
    int SchemaVersion,
    ExactRegionAssertion[]? RegionAssertions,
    ExactTemplateAssertion[]? TemplateAssertions)
{
    public ExactRegionAssertion[] Regions => RegionAssertions ?? [];
    public ExactTemplateAssertion[] Templates => TemplateAssertions ?? [];

    public static ImageAssertionSpec LoadAndValidate(string path)
    {
        var file = new FileInfo(path);
        if (file.Length > 1024 * 1024)
            throw new ArgumentException("The image-assertion spec exceeds the 1 MiB bound.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var spec = JsonSerializer.Deserialize<ImageAssertionSpec>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("The image-assertion spec is empty or invalid JSON.");
        spec.Validate();
        return spec;
    }

    private void Validate()
    {
        if (SchemaVersion != 1)
            throw new ArgumentException($"Unsupported image-assertion schema {SchemaVersion}; expected 1.");
        if (Regions.Length + Templates.Length is < 1 or > 64)
            throw new ArgumentException("An image-assertion spec must contain between 1 and 64 assertions.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in Regions)
        {
            ValidateName(region.Name, names);
            region.Rectangle.ValidateShape(region.Name, "region");
            _ = NormalizeHash(region.ExpectedRgbSha256, region.Name);
        }
        foreach (var template in Templates)
        {
            ValidateName(template.Name, names);
            template.SearchRectangle.ValidateShape(template.Name, "template search");
            if (string.IsNullOrWhiteSpace(template.TemplatePath))
                throw new ArgumentException($"Template '{template.Name}' has an empty templatePath.");
            if (template.ExpectedMatches is null || template.ExpectedMatches.Length > 16)
                throw new ArgumentException($"Template '{template.Name}' must declare zero to sixteen expected matches.");
            if (template.ExpectedMatches.Distinct().Count() != template.ExpectedMatches.Length)
                throw new ArgumentException($"Template '{template.Name}' contains duplicate expected matches.");
            foreach (var point in template.ExpectedMatches)
            {
                if (!template.SearchRectangle.Contains(point.X, point.Y))
                    throw new ArgumentException($"Template '{template.Name}' has an expected match outside its search rectangle.");
            }
        }
    }

    private static void ValidateName(string name, HashSet<string> names)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            throw new ArgumentException("Assertion names must contain 1 to 80 characters.");
        if (!names.Add(name))
            throw new ArgumentException($"Assertion name '{name}' is duplicated.");
    }

    private static string NormalizeHash(string hash, string name)
    {
        var normalized = hash?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException($"Region '{name}' expectedRgbSha256 must be 64 hexadecimal characters.");
        return normalized;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ExactRegionAssertion(
    string Name,
    ImageRectangle Rectangle,
    string ExpectedRgbSha256);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ExactTemplateAssertion(
    string Name,
    string TemplatePath,
    ImageRectangle SearchRectangle,
    ImagePoint[] ExpectedMatches);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ImageRectangle(int X, int Y, int Width, int Height)
{
    public void ValidateShape(string name, string description)
    {
        if (X < 0 || Y < 0 || Width <= 0 || Height <= 0)
            throw new ArgumentException($"Assertion '{name}' has an invalid {description} rectangle.");
    }

    public void ValidateWithin(int imageWidth, int imageHeight, string name, string description)
    {
        ValidateShape(name, description);
        if ((long)X + Width > imageWidth || (long)Y + Height > imageHeight)
            throw new ArgumentException($"Assertion '{name}' {description} rectangle is outside the capture.");
    }

    public bool Contains(int x, int y) =>
        x >= X && y >= Y && (long)x < (long)X + Width && (long)y < (long)Y + Height;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ImagePoint(int X, int Y);

internal sealed record RegionAssertionResult(
    string Name,
    ImageRectangle Rectangle,
    string ExpectedRgbSha256,
    string ActualRgbSha256,
    bool Passed);

internal sealed record TemplateAssertionResult(
    string Name,
    string TemplatePath,
    string TemplateSha256,
    int TemplateWidth,
    int TemplateHeight,
    ImageRectangle SearchRectangle,
    ImagePoint[] ExpectedMatches,
    ImagePoint[] ActualMatches,
    bool Passed);

internal sealed class RgbBitmap
{
    private const int MaximumDimension = 16_384;
    private const int MaximumPixelBytes = 512 * 1024 * 1024;
    private const long MaximumSourceBytes = 512L * 1024 * 1024;
    private const long MaximumTemplatePixelComparisons = 100_000_000;
    private readonly byte[] _pixels;

    private RgbBitmap(int width, int height, byte[] pixels, string sourceSha256)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
        SourceSha256 = sourceSha256;
    }

    public int Width { get; }
    public int Height { get; }
    public string SourceSha256 { get; }

    public static RgbBitmap Load(string path)
    {
        var file = new FileInfo(path);
        if (file.Length is < 54 or > MaximumSourceBytes)
            throw new ArgumentException("BMP input must be between 54 bytes and 512 MiB.");
        var sourceBytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(sourceBytes, false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt16() != 0x4D42)
            throw new ArgumentException("Image input is not a BMP file.");
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var pixelOffset = reader.ReadUInt32();
        var headerSize = reader.ReadUInt32();
        if (headerSize < 40 || pixelOffset < 14 + headerSize || pixelOffset > sourceBytes.Length)
            throw new ArgumentException("BMP header or pixel offset is unsupported.");
        var width = reader.ReadInt32();
        var signedHeight = reader.ReadInt32();
        var planes = reader.ReadUInt16();
        var bitCount = reader.ReadUInt16();
        var compression = reader.ReadUInt32();
        if (width is <= 0 or > MaximumDimension || signedHeight is 0 or int.MinValue ||
            Math.Abs(signedHeight) > MaximumDimension)
            throw new ArgumentException("BMP dimensions are outside the 1..16384 bound.");
        if (planes != 1 || bitCount is not (24 or 32) || compression != 0)
            throw new ArgumentException("Only uncompressed 24-bit or 32-bit BMP input is supported.");

        var height = Math.Abs(signedHeight);
        var bytesPerPixel = bitCount / 8;
        var rowStride = checked(((width * bytesPerPixel) + 3) & ~3);
        var requiredBytes = checked((long)pixelOffset + (long)rowStride * height);
        if (requiredBytes > sourceBytes.Length)
            throw new ArgumentException("BMP pixel data is truncated.");
        var canonicalBytes = checked(width * height * 3);
        if (canonicalBytes > MaximumPixelBytes)
            throw new ArgumentException("Decoded BMP pixels exceed the 512 MiB bound.");

        var pixels = new byte[canonicalBytes];
        for (var y = 0; y < height; y++)
        {
            var sourceRow = signedHeight < 0 ? y : height - 1 - y;
            var sourceIndex = checked((int)pixelOffset + sourceRow * rowStride);
            var destinationIndex = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                pixels[destinationIndex++] = sourceBytes[sourceIndex + 2];
                pixels[destinationIndex++] = sourceBytes[sourceIndex + 1];
                pixels[destinationIndex++] = sourceBytes[sourceIndex];
                sourceIndex += bytesPerPixel;
            }
        }

        return new RgbBitmap(
            width,
            height,
            pixels,
            Convert.ToHexString(SHA256.HashData(sourceBytes)));
    }

    public string HashRegion(ImageRectangle rectangle)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rowBytes = rectangle.Width * 3;
        for (var y = rectangle.Y; y < rectangle.Y + rectangle.Height; y++)
            hash.AppendData(_pixels, (y * Width + rectangle.X) * 3, rowBytes);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public ImagePoint[] FindExactMatches(RgbBitmap template, ImageRectangle search)
    {
        if (template.Width > search.Width || template.Height > search.Height)
            return [];
        var candidateColumns = search.Width - template.Width + 1;
        var candidateRows = search.Height - template.Height + 1;
        var comparisonBound = checked((long)candidateColumns * candidateRows * template.Width * template.Height);
        if (comparisonBound > MaximumTemplatePixelComparisons)
            throw new ArgumentException("Exact template search exceeds the 100,000,000-pixel comparison bound.");

        var matches = new List<ImagePoint>();
        var lastX = search.X + candidateColumns - 1;
        var lastY = search.Y + candidateRows - 1;
        for (var y = search.Y; y <= lastY; y++)
        {
            for (var x = search.X; x <= lastX; x++)
            {
                if (!MatchesAt(template, x, y))
                    continue;
                matches.Add(new ImagePoint(x, y));
                if (matches.Count > 16)
                    throw new ArgumentException("Exact template search found more than the 16-match evidence bound.");
            }
        }
        return matches.OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();
    }

    private bool MatchesAt(RgbBitmap template, int x, int y)
    {
        var rowBytes = template.Width * 3;
        for (var templateY = 0; templateY < template.Height; templateY++)
        {
            var captureOffset = ((y + templateY) * Width + x) * 3;
            var templateOffset = templateY * rowBytes;
            if (!_pixels.AsSpan(captureOffset, rowBytes).SequenceEqual(
                    template._pixels.AsSpan(templateOffset, rowBytes)))
                return false;
        }
        return true;
    }
}
