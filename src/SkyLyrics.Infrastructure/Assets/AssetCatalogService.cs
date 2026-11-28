using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace SkyLyrics.Infrastructure.Assets;

public sealed record AssetSourceMetadata
{
    public string Source { get; init; } = "Unverified";
    public string Creator { get; init; } = "Unverified";
    public string License { get; init; } = "Unverified — not cleared for redistribution";
    public string[] Tags { get; init; } = [];
}

public sealed record AssetCatalogEntry(
    string Id,
    string Name,
    string Category,
    string RelativePath,
    string Extension,
    long FileSizeBytes,
    int Width,
    int Height,
    int FrameCount,
    double? DurationSeconds,
    string Source,
    string Creator,
    string License,
    string Sha256,
    IReadOnlyList<string> Tags,
    string? ValidationWarning);

public sealed record AssetCatalogDocument(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AssetCatalogEntry> Assets);

public sealed class AssetCatalogService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4", ".webm"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _rootPath;

    public AssetCatalogService(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
    }

    public string RootPath => _rootPath;

    public async Task<IReadOnlyList<AssetCatalogEntry>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootPath))
        {
            return [];
        }

        var sourceMetadata = await ReadSourceMetadataAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<AssetCatalogEntry>();

        foreach (var filePath in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
                     .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                     .Where(path => !IsThumbnailPath(path))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(_rootPath, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var category = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "Uncategorized";
            var id = Path.ChangeExtension(relativePath, null)!.Replace('\\', '/').ToLowerInvariant();
            var name = Path.GetFileNameWithoutExtension(filePath)
                .Replace('-', ' ')
                .Replace('_', ' ');

            var metadata = sourceMetadata.TryGetValue(id, out var source)
                ? source
                : new AssetSourceMetadata();

            var width = 0;
            var height = 0;
            var frameCount = 0;
            double? durationSeconds = null;
            string? validationWarning = null;

            try
            {
                (width, height, frameCount, durationSeconds) = ReadMediaHeader(filePath, extension);
                if (width <= 0 || height <= 0)
                {
                    validationWarning = "Dimensions could not be read from the media header.";
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                validationWarning = $"Media header is invalid: {exception.Message}";
            }

            string sha256;
            long fileSize;
            try
            {
                var fileInfo = new FileInfo(filePath);
                fileSize = fileInfo.Length;
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                sha256 = Convert.ToHexStringLower(hash);
            }
            catch (IOException exception)
            {
                validationWarning = $"File could not be read: {exception.Message}";
                fileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
                sha256 = string.Empty;
            }

            entries.Add(new AssetCatalogEntry(
                id,
                name,
                category,
                relativePath,
                extension,
                fileSize,
                width,
                height,
                frameCount,
                durationSeconds,
                metadata.Source,
                metadata.Creator,
                metadata.License,
                sha256,
                metadata.Tags,
                validationWarning));
        }

        return entries;
    }

    public async Task WriteCatalogAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var assets = await DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var document = new AssetCatalogDocument(1, DateTimeOffset.UtcNow, assets);
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Catalog output directory could not be resolved.");
        Directory.CreateDirectory(directory);

        var temporaryPath = fullPath + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 32 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, fullPath, overwrite: true);
    }

    private async Task<Dictionary<string, AssetSourceMetadata>> ReadSourceMetadataAsync(
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_rootPath, "asset-sources.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, AssetSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<Dictionary<string, AssetSourceMetadata>>(
                   stream, JsonOptions, cancellationToken).ConfigureAwait(false)
               ?? new Dictionary<string, AssetSourceMetadata>(StringComparer.OrdinalIgnoreCase);
    }

    private bool IsThumbnailPath(string path)
    {
        var relative = Path.GetRelativePath(_rootPath, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "Thumbnails", StringComparison.OrdinalIgnoreCase));
    }

    private static (int Width, int Height, int Frames, double? Duration) ReadMediaHeader(
        string path,
        string extension)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        return extension switch
        {
            ".png" => ReadPng(reader),
            ".jpg" or ".jpeg" => ReadJpeg(reader),
            ".gif" => ReadGif(reader),
            ".webp" => ReadWebp(reader),
            _ => (0, 0, 0, null)
        };
    }

    private static (int Width, int Height, int Frames, double? Duration) ReadPng(BinaryReader reader)
    {
        var signature = reader.ReadBytes(8);
        if (signature.Length != 8 ||
            !signature.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            throw new InvalidDataException("PNG signature is missing.");
        }

        var ihdrLength = ReadUInt32BigEndian(reader);
        var chunkType = reader.ReadBytes(4);
        if (ihdrLength != 13 || !chunkType.AsSpan().SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("PNG IHDR chunk is missing.");
        }

        var width = ReadInt32BigEndian(reader);
        var height = ReadInt32BigEndian(reader);
        return (width, height, 1, null);
    }

    private static (int Width, int Height, int Frames, double? Duration) ReadJpeg(BinaryReader reader)
    {
        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
        {
            throw new InvalidDataException("JPEG SOI marker is missing.");
        }

        while (reader.BaseStream.Position < reader.BaseStream.Length - 4)
        {
            if (reader.ReadByte() != 0xFF)
            {
                continue;
            }

            byte marker;
            do { marker = reader.ReadByte(); } while (marker == 0xFF);

            if (marker is 0xD8 or 0xD9 or 0x01 or >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            var segmentLength = ReadUInt16BigEndian(reader);
            if (segmentLength < 2)
            {
                throw new InvalidDataException("JPEG segment length is invalid.");
            }

            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                _ = reader.ReadByte();
                var height = ReadUInt16BigEndian(reader);
                var width = ReadUInt16BigEndian(reader);
                return (width, height, 1, null);
            }

            reader.BaseStream.Seek(segmentLength - 2, SeekOrigin.Current);
        }

        throw new InvalidDataException("JPEG frame dimensions were not found.");
    }

    private static (int Width, int Height, int Frames, double? Duration) ReadGif(BinaryReader reader)
    {
        var signature = reader.ReadBytes(6);
        if (signature.Length != 6 ||
            (!signature.AsSpan().SequenceEqual("GIF87a"u8) && !signature.AsSpan().SequenceEqual("GIF89a"u8)))
        {
            throw new InvalidDataException("GIF signature is invalid.");
        }

        var width = reader.ReadUInt16();
        var height = reader.ReadUInt16();
        var packed = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadByte();

        if ((packed & 0x80) != 0)
        {
            var tableEntries = 1 << ((packed & 0x07) + 1);
            reader.BaseStream.Seek(tableEntries * 3L, SeekOrigin.Current);
        }

        var frames = 0;
        var duration = 0d;
        var frameDelay = 0;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var marker = reader.ReadByte();
            if (marker == 0x3B)
            {
                break;
            }

            if (marker == 0x21)
            {
                var label = reader.ReadByte();
                if (label == 0xF9)
                {
                    var blockSize = reader.ReadByte();
                    if (blockSize != 4)
                    {
                        throw new InvalidDataException("GIF graphic control extension is malformed.");
                    }

                    _ = reader.ReadByte();
                    frameDelay = reader.ReadUInt16();
                    _ = reader.ReadByte();
                    _ = reader.ReadByte();
                }
                else
                {
                    SkipGifSubBlocks(reader);
                }

                continue;
            }

            if (marker == 0x2C)
            {
                _ = reader.ReadBytes(8);
                var imagePacked = reader.ReadByte();
                if ((imagePacked & 0x80) != 0)
                {
                    var tableEntries = 1 << ((imagePacked & 0x07) + 1);
                    reader.BaseStream.Seek(tableEntries * 3L, SeekOrigin.Current);
                }

                _ = reader.ReadByte();
                SkipGifSubBlocks(reader);
                frames++;
                duration += Math.Max(frameDelay, 1) / 100d;
                frameDelay = 0;
                continue;
            }

            throw new InvalidDataException($"Unexpected GIF block marker 0x{marker:X2}.");
        }

        return (width, height, frames, duration > 0 ? duration : null);
    }

    private static void SkipGifSubBlocks(BinaryReader reader)
    {
        while (true)
        {
            var size = reader.ReadByte();
            if (size == 0)
            {
                return;
            }

            var skipped = reader.ReadBytes(size);
            if (skipped.Length != size)
            {
                throw new EndOfStreamException("GIF data sub-block is truncated.");
            }
        }
    }

    private static (int Width, int Height, int Frames, double? Duration) ReadWebp(BinaryReader reader)
    {
        var header = reader.ReadBytes(12);
        if (header.Length != 12 ||
            !header.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            throw new InvalidDataException("WebP RIFF header is invalid.");
        }

        while (reader.BaseStream.Position <= reader.BaseStream.Length - 8)
        {
            var chunk = reader.ReadBytes(4);
            var size = reader.ReadUInt32();
            if (chunk.AsSpan().SequenceEqual("VP8X"u8))
            {
                _ = reader.ReadBytes(4);
                var widthMinusOne = ReadUInt24LittleEndian(reader);
                var heightMinusOne = ReadUInt24LittleEndian(reader);
                return (widthMinusOne + 1, heightMinusOne + 1, 1, null);
            }

            reader.BaseStream.Seek(size + (size % 2), SeekOrigin.Current);
        }

        throw new InvalidDataException("Supported WebP VP8X dimensions were not found.");
    }

    private static int ReadUInt24LittleEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(3);
        if (bytes.Length != 3)
        {
            throw new EndOfStreamException("WebP dimension field is truncated.");
        }

        return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[2];
        reader.BaseStream.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt32BigEndian(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[4];
        reader.BaseStream.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static int ReadInt32BigEndian(BinaryReader reader) =>
        checked((int)ReadUInt32BigEndian(reader));
}
