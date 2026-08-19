namespace BadAppleProgressBars.Domain;

/// <summary>
/// Describes the fixed-rate grid and cache identity of a baked video.
/// </summary>
public sealed class BakedVideoMetadata
{
    public const int HashLength = 32;

    private readonly byte[] _sourceHash;
    private readonly byte[] _profileHash;

    public BakedVideoMetadata(
        ReadOnlySpan<byte> sourceHash,
        ReadOnlySpan<byte> profileHash,
        int width,
        int height,
        int frameRateNumerator,
        int frameRateDenominator,
        int frameCount)
    {
        if (sourceHash.Length != HashLength)
        {
            throw new ArgumentException($"Source hash must be {HashLength} bytes.", nameof(sourceHash));
        }

        if (profileHash.Length != HashLength)
        {
            throw new ArgumentException($"Profile hash must be {HashLength} bytes.", nameof(profileHash));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (frameRateNumerator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameRateNumerator));
        }

        if (frameRateDenominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameRateDenominator));
        }

        if (frameCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }

        _sourceHash = sourceHash.ToArray();
        _profileHash = profileHash.ToArray();
        Width = width;
        Height = height;
        FrameRateNumerator = frameRateNumerator;
        FrameRateDenominator = frameRateDenominator;
        FrameCount = frameCount;
    }

    public ReadOnlyMemory<byte> SourceHash => _sourceHash;

    public ReadOnlyMemory<byte> ProfileHash => _profileHash;

    public int Width { get; }

    public int Height { get; }

    public int FrameRateNumerator { get; }

    public int FrameRateDenominator { get; }

    public int FrameCount { get; }

    public TimeSpan GetFrameTimestamp(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        var ticks = checked(frameIndex * TimeSpan.TicksPerSecond * FrameRateDenominator / FrameRateNumerator);
        return TimeSpan.FromTicks(ticks);
    }
}
