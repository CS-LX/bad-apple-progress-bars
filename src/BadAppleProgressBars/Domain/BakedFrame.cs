namespace BadAppleProgressBars.Domain;

/// <summary>
/// A timestamped snapshot of all visible progress-bar states for one baked frame.
/// </summary>
public sealed class BakedFrame
{
    public BakedFrame(TimeSpan timestamp, IReadOnlyList<BarState> states)
    {
        if (timestamp < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp));
        }

        Timestamp = timestamp;
        States = states ?? throw new ArgumentNullException(nameof(states));
    }

    public TimeSpan Timestamp { get; }

    public IReadOnlyList<BarState> States { get; }
}
