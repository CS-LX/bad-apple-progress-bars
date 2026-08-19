using BadAppleProgressBars.Baking;
using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Tests;

public class BakedVideoCacheTests
{
    [Fact]
    public async Task GetOrBakeAsync_UsesAValidMatchingCacheWithoutStartingFfmpeg()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bad-apple-progress-bars-{Guid.NewGuid():N}");
        var sourcePath = Path.Combine(directory, "source.mp4");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
        var cache = new BakedVideoCache(directory);
        var options = new FfmpegBakeOptions { FfmpegExecutablePath = Path.Combine(directory, "missing.exe") };

        try
        {
            var sourceHash = await BakedVideoIdentityService.ComputeSourceHashAsync(sourcePath);
            var profileHash = BakedVideoIdentityService.ComputeProfileHash(options);
            var cachePath = cache.GetCachePath(sourceHash, profileHash);
            var metadata = new BakedVideoMetadata(sourceHash, profileHash, 80, 45, 30, 1, 1);
            using (var destination = File.Create(cachePath))
            {
                BakedVideoWriter.Write(
                    destination,
                    metadata,
                    new List<BakedFrame> { new(TimeSpan.Zero, Array.Empty<BarState>()) });
            }

            var result = await cache.GetOrBakeAsync(sourcePath, options);

            Assert.True(result.CacheHit);
            Assert.Equal(cachePath, result.BakedFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GetCachePath_ChangesWhenRenderProfileChanges()
    {
        var cache = new BakedVideoCache(Path.Combine(Path.GetTempPath(), $"bad-apple-progress-bars-{Guid.NewGuid():N}"));
        var sourceHash = Enumerable.Repeat((byte)1, BakedVideoMetadata.HashLength).ToArray();
        var normalProfile = BakedVideoIdentityService.ComputeProfileHash(new FfmpegBakeOptions());
        var invertedProfile = BakedVideoIdentityService.ComputeProfileHash(new FfmpegBakeOptions { InvertBlackAndWhite = true });

        Assert.NotEqual(cache.GetCachePath(sourceHash, normalProfile), cache.GetCachePath(sourceHash, invertedProfile));
    }
}
