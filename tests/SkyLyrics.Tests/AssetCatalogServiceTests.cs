using System.Security.Cryptography;
using System.Text.Json;
using SkyLyrics.Application.Settings;
using SkyLyrics.Infrastructure.Assets;
using Xunit;

namespace SkyLyrics.Tests;

public sealed class AssetCatalogServiceTests
{
    [Fact]
    public async Task DiscoverAsync_ComputesDimensionsSizeAndSha256FromTheActualFile()
    {
        using var workspace = new TemporaryDirectory();
        var assetPath = Path.Combine(workspace.Path, "Backgrounds", "Aurora-Fjord.png");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        var bytes = CreatePngHeader(1920, 1080);
        await File.WriteAllBytesAsync(assetPath, bytes);

        var metadata = new Dictionary<string, AssetSourceMetadata>
        {
            ["backgrounds/aurora-fjord"] = new()
            {
                Source = "Generated for SkyLyrics",
                Creator = "SkyLyrics asset production",
                License = "Publisher review required",
                Tags = ["aurora", "night"]
            }
        };
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Path, "asset-sources.json"),
            JsonSerializer.Serialize(metadata));

        var service = new AssetCatalogService(workspace.Path);
        var assets = await service.DiscoverAsync();

        var asset = Assert.Single(assets);
        Assert.Equal("backgrounds/aurora-fjord", asset.Id);
        Assert.Equal("Backgrounds/Aurora-Fjord.png", asset.RelativePath);
        Assert.Equal(1920, asset.Width);
        Assert.Equal(1080, asset.Height);
        Assert.Equal(bytes.Length, asset.FileSizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), asset.Sha256);
        Assert.Equal("Generated for SkyLyrics", asset.Source);
        Assert.Equal("Publisher review required", asset.License);
        Assert.Contains("aurora", asset.Tags);
    }

    [Fact]
    public async Task DiscoverAsync_ReportsCorruptedMediaInsteadOfThrowing()
    {
        using var workspace = new TemporaryDirectory();
        var folder = Path.Combine(workspace.Path, "Textures");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Broken-Texture.webp");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5, 6, 7, 8]);

        var service = new AssetCatalogService(workspace.Path);
        var asset = Assert.Single(await service.DiscoverAsync());

        Assert.Equal("textures/broken-texture", asset.Id);
        Assert.NotNull(asset.ValidationWarning);
        Assert.Equal(8, asset.FileSizeBytes);
        Assert.NotEmpty(asset.Sha256);
    }

    [Fact]
    public async Task DiscoverAsync_ExcludesThumbnailVariantsFromTheAssetLibrary()
    {
        using var workspace = new TemporaryDirectory();
        var backgrounds = Path.Combine(workspace.Path, "Backgrounds");
        var thumbnails = Path.Combine(workspace.Path, "Thumbnails");
        Directory.CreateDirectory(backgrounds);
        Directory.CreateDirectory(thumbnails);
        await File.WriteAllBytesAsync(Path.Combine(backgrounds, "Aurora.png"), CreatePngHeader(1920, 1080));
        await File.WriteAllBytesAsync(Path.Combine(thumbnails, "Aurora.webp"), [1, 2, 3]);

        var assets = await new AssetCatalogService(workspace.Path).DiscoverAsync();

        Assert.Single(assets);
        Assert.Equal("backgrounds/aurora", assets[0].Id);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsAnEmptyCatalogWhenTheRootDoesNotExist()
    {
        using var workspace = new TemporaryDirectory();
        var missing = Path.Combine(workspace.Path, "not-created");

        var assets = await new AssetCatalogService(missing).DiscoverAsync();

        Assert.Empty(assets);
    }

    [Fact]
    public void AssetLibraryPreferences_NormalizeFiltersFavoritesAndRejectsUnknownQualityProfiles()
    {
        var preferences = new AssetLibraryPreferences
        {
            SelectedBackgroundId = " BACKGROUNDS/AURORA-FJORD ",
            QualityProfile = "ultra",
            FavoriteAssetIds = ["Textures/Grain", "textures/grain", "", "Backgrounds/Aurora"]
        }.Normalize();

        Assert.Equal("backgrounds/aurora-fjord", preferences.SelectedBackgroundId);
        Assert.Equal("balanced", preferences.QualityProfile);
        Assert.Equal(2, preferences.FavoriteAssetIds.Length);
        Assert.Contains("textures/grain", preferences.FavoriteAssetIds);
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        var bytes = new byte[24];
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(bytes, 0);
        bytes[11] = 13;
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SkyLyrics.AssetCatalog.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
