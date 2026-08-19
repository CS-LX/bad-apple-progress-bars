using BadAppleProgressBars.Baking;
using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Playback;

namespace BadAppleProgressBars.Tests;

public class BakedVideoFormatTests
{
    [Fact]
    public void WriteThenRead_RoundTripsHeaderAndEveryBarStateOneFrameAtATime()
    {
        var metadata = CreateMetadata();
        var expectedFrames = SyntheticFrameFactory.Create()
            .Select(BarStateFrameConverter.FromPlaybackFrame)
            .ToArray();

        using var stream = new MemoryStream();
        BakedVideoWriter.Write(stream, metadata, expectedFrames);
        stream.Position = 0;

        using var reader = new BakedVideoReader(stream, leaveOpen: true);
        AssertHeader(metadata, reader.Header);

        var actualFrames = new List<BakedFrame>();
        Assert.True(reader.TryReadNextFrame(out var firstFrame));
        actualFrames.Add(firstFrame);
        Assert.True(stream.Position < reader.Header.IndexOffset);

        while (reader.TryReadNextFrame(out var frame))
        {
            actualFrames.Add(frame);
        }

        Assert.Equal(reader.Header.IndexOffset, stream.Position);
        var indexEntries = new List<BakedIndexEntry>();

        while (reader.TryReadNextIndexEntry(out var entry))
        {
            indexEntries.Add(entry);
        }

        Assert.Equal(expectedFrames.Length, indexEntries.Count);
        Assert.All(indexEntries.Select((entry, index) => (entry, index)), pair =>
        {
            (BakedIndexEntry entry, int index) = pair;
            Assert.Equal(index, entry.FrameStart);
            Assert.InRange(entry.FileOffset, BakedVideoFormat.HeaderSize, reader.Header.IndexOffset - 1);
        });
        AssertFramesEqual(expectedFrames, actualFrames);
    }

    [Fact]
    public void Reader_InvalidMagic_ThrowsClearFormatError()
    {
        using var stream = new MemoryStream("BAD!"u8.ToArray());

        var exception = Assert.Throws<BakedVideoFormatException>(() => new BakedVideoReader(stream, leaveOpen: true));

        Assert.Contains("magic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reader_TruncatedFile_ThrowsClearFormatError()
    {
        var bytes = WriteSyntheticVideo();
        using var stream = new MemoryStream(bytes[..120]);

        var exception = Assert.Throws<BakedVideoFormatException>(() => new BakedVideoReader(stream, leaveOpen: true));

        Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] WriteSyntheticVideo()
    {
        var frames = SyntheticFrameFactory.Create()
            .Select(BarStateFrameConverter.FromPlaybackFrame)
            .ToArray();
        using var stream = new MemoryStream();
        BakedVideoWriter.Write(stream, CreateMetadata(), frames);
        return stream.ToArray();
    }

    private static BakedVideoMetadata CreateMetadata()
    {
        var sourceHash = Enumerable.Range(0, BakedVideoMetadata.HashLength).Select(value => (byte)value).ToArray();
        var profileHash = Enumerable.Range(0, BakedVideoMetadata.HashLength).Select(value => (byte)(255 - value)).ToArray();

        return new BakedVideoMetadata(
            sourceHash,
            profileHash,
            SyntheticFrameFactory.GridWidth,
            SyntheticFrameFactory.GridHeight,
            frameRateNumerator: 4,
            frameRateDenominator: 3,
            frameCount: SyntheticFrameFactory.Create().Length);
    }

    private static void AssertHeader(BakedVideoMetadata expected, BakedVideoHeader actual)
    {
        Assert.Equal(BakedVideoFormat.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.SourceHash.ToArray(), actual.Metadata.SourceHash.ToArray());
        Assert.Equal(expected.ProfileHash.ToArray(), actual.Metadata.ProfileHash.ToArray());
        Assert.Equal(expected.Width, actual.Metadata.Width);
        Assert.Equal(expected.Height, actual.Metadata.Height);
        Assert.Equal(expected.FrameRateNumerator, actual.Metadata.FrameRateNumerator);
        Assert.Equal(expected.FrameRateDenominator, actual.Metadata.FrameRateDenominator);
        Assert.Equal(expected.FrameCount, actual.Metadata.FrameCount);
        Assert.True(actual.IndexOffset >= BakedVideoFormat.HeaderSize);
    }

    private static void AssertFramesEqual(IReadOnlyList<BakedFrame> expected, IReadOnlyList<BakedFrame> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].Timestamp, actual[index].Timestamp);
            Assert.Equal(expected[index].States, actual[index].States);
        }
    }
}
