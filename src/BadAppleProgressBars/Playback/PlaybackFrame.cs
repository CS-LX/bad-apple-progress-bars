using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Playback;

/// <summary>
/// A complete progress-bar frame scheduled at an elapsed playback timestamp.
/// </summary>
public sealed class PlaybackFrame
{
    public PlaybackFrame(TimeSpan timestamp, IReadOnlyList<IReadOnlyList<MonotonicBlock>> rows)
    {
        if (timestamp < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp));
        }

        Timestamp = timestamp;
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    public TimeSpan Timestamp { get; }

    public IReadOnlyList<IReadOnlyList<MonotonicBlock>> Rows { get; }
}
