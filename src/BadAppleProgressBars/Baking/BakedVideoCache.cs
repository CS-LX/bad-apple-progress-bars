using System.IO;
using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Resolves stable .bpb cache paths and validates a cache entry before it is played.
/// </summary>
public sealed class BakedVideoCache
{
    private readonly string _cacheDirectory;

    public BakedVideoCache(string? cacheDirectory = null)
    {
        _cacheDirectory = Path.GetFullPath(cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BadAppleProgressBars",
            "cache"));
    }

    public async Task<BakedVideoCacheResult> GetOrBakeAsync(
        string sourceVideoPath,
        FfmpegBakeOptions? options = null,
        IProgress<FfmpegBakeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVideoPath);
        options ??= new FfmpegBakeOptions();
        options.Validate();
        var sourcePath = Path.GetFullPath(sourceVideoPath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The source video file does not exist.", sourcePath);
        }

        var sourceHash = await BakedVideoIdentityService.ComputeSourceHashAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var profileHash = BakedVideoIdentityService.ComputeProfileHash(options);
        var cachePath = GetCachePath(sourceHash, profileHash);

        if (IsValidCache(cachePath, sourceHash, profileHash))
        {
            return new BakedVideoCacheResult(cachePath, CacheHit: true);
        }

        Directory.CreateDirectory(_cacheDirectory);
        await new FfmpegVideoBaker().BakeAsync(sourcePath, cachePath, options, progress, cancellationToken).ConfigureAwait(false);
        return new BakedVideoCacheResult(cachePath, CacheHit: false);
    }

    public string GetCachePath(ReadOnlySpan<byte> sourceHash, ReadOnlySpan<byte> profileHash)
    {
        if (sourceHash.Length != BakedVideoMetadata.HashLength || profileHash.Length != BakedVideoMetadata.HashLength)
        {
            throw new ArgumentException("Cache identity hashes must match the .bpb header hash length.");
        }

        return Path.Combine(_cacheDirectory, $"{Convert.ToHexString(sourceHash)}_{Convert.ToHexString(profileHash)}.bpb");
    }

    private static bool IsValidCache(string path, ReadOnlySpan<byte> sourceHash, ReadOnlySpan<byte> profileHash)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BakedVideoReader(stream);
            return reader.Header.FormatVersion == BakedVideoFormat.FormatVersion &&
                   reader.Header.Metadata.SourceHash.Span.SequenceEqual(sourceHash) &&
                   reader.Header.Metadata.ProfileHash.Span.SequenceEqual(profileHash);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
