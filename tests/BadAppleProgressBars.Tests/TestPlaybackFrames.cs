using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Playback;
using BadAppleProgressBars.Segmentation;

namespace BadAppleProgressBars.Tests;

/// <summary>
/// Test-only baked-frame fixtures. They are not reachable from the player.
/// </summary>
internal static class TestPlaybackFrames
{
    public const int GridWidth = 14;
    public const int GridHeight = 1;

    public static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(750);

    private static readonly string[] Rows =
    [
        "BWBW",
        "WBW",
        "BBB",
        "WBBBWBWWWBBWWB",
        "WWWWWWWWWWWWWW",
        "BBBBBBBBBBBBBB",
        "BWBW",
        "BWBW",
    ];

    public static PlaybackFrame[] Create()
    {
        var frames = new PlaybackFrame[Rows.Length];

        for (var index = 0; index < Rows.Length; index++)
        {
            IReadOnlyList<MonotonicBlock>[] frameRows = [RowBlockEncoder.Encode(ToPixels(Rows[index]))];
            frames[index] = new PlaybackFrame(TimeSpan.FromTicks(FrameInterval.Ticks * index), frameRows);
        }

        return frames;
    }

    private static bool[] ToPixels(string row) => [.. row.Select(pixel => pixel == 'B')];
}
