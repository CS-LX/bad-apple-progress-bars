namespace BadAppleProgressBars.Domain;

/// <summary>
/// The fixed header read from a .bpb file.
/// </summary>
public sealed class BakedVideoHeader
{
    public BakedVideoHeader(BakedVideoMetadata metadata, int formatVersion, long indexOffset)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        if (formatVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(formatVersion));
        }

        if (indexOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indexOffset));
        }

        FormatVersion = formatVersion;
        IndexOffset = indexOffset;
    }

    public BakedVideoMetadata Metadata { get; }

    public int FormatVersion { get; }

    public long IndexOffset { get; }
}
