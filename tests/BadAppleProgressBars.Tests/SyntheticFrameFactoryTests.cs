using BadAppleProgressBars.Playback;

namespace BadAppleProgressBars.Tests;

public class SyntheticFrameFactoryTests
{
    [Fact]
    public void Create_ContainsTheRequiredSyntheticRowsInTimestampOrder()
    {
        var frames = SyntheticFrameFactory.Create();
        var rows = frames.Select(ToRow).ToArray();

        Assert.Equal(
        [
            "BWBW",
            "WBW",
            "BBB",
            "WBBBWBWWWBBWWB",
            "WWWWWWWWWWWWWW",
            "BBBBBBBBBBBBBB",
            "BWBW",
            "BWBW",
        ],
        rows);
        Assert.All(frames.Select((frame, index) => (frame, index)), pair =>
            Assert.Equal(TimeSpan.FromTicks(SyntheticFrameFactory.FrameInterval.Ticks * pair.index), pair.frame.Timestamp));
    }

    private static string ToRow(PlaybackFrame frame)
    {
        var blocks = Assert.Single(frame.Rows);

        return string.Concat(blocks.SelectMany(block =>
            Enumerable.Range(0, block.Length)
                .Select(offset => offset < block.BlackPrefixLength ? 'B' : 'W')));
    }
}
