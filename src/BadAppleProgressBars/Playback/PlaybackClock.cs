using System.Diagnostics;

namespace BadAppleProgressBars.Playback;

/// <summary>
/// Measures active playback time from a monotonic timestamp source.
/// </summary>
public sealed class PlaybackClock
{
    private readonly Func<long> _timestampProvider;
    private readonly long _timestampFrequency;
    private long _startTimestamp;
    private long _pausedAtTimestamp;
    private long _pausedTimestampCount;

    /// <summary>
    /// Creates a clock backed by <see cref="Stopwatch"/>.
    /// </summary>
    public PlaybackClock()
        : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
    {
    }

    /// <summary>
    /// Creates a clock with an explicit monotonic timestamp source.
    /// </summary>
    public PlaybackClock(Func<long> timestampProvider, long timestampFrequency)
    {
        _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));

        if (timestampFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
        }

        _timestampFrequency = timestampFrequency;
    }

    public bool IsRunning { get; private set; }

    public bool IsPaused { get; private set; }

    /// <summary>
    /// Gets the elapsed time excluding time spent paused.
    /// </summary>
    public TimeSpan Elapsed
    {
        get
        {
            if (!IsRunning)
            {
                return TimeSpan.Zero;
            }

            var currentTimestamp = IsPaused ? _pausedAtTimestamp : _timestampProvider();
            var activeTimestampCount = currentTimestamp - _startTimestamp - _pausedTimestampCount;
            return TimeSpan.FromSeconds(Math.Max(0, activeTimestampCount) / (double)_timestampFrequency);
        }
    }

    /// <summary>
    /// Starts the clock from zero, discarding any prior elapsed time.
    /// </summary>
    public void Start()
    {
        _startTimestamp = _timestampProvider();
        _pausedAtTimestamp = 0;
        _pausedTimestampCount = 0;
        IsRunning = true;
        IsPaused = false;
    }

    /// <summary>
    /// Freezes elapsed playback time until <see cref="Resume"/> is called.
    /// </summary>
    public void Pause()
    {
        if (!IsRunning || IsPaused)
        {
            return;
        }

        _pausedAtTimestamp = _timestampProvider();
        IsPaused = true;
    }

    /// <summary>
    /// Continues the clock from its paused elapsed time.
    /// </summary>
    public void Resume()
    {
        if (!IsRunning || !IsPaused)
        {
            return;
        }

        _pausedTimestampCount += _timestampProvider() - _pausedAtTimestamp;
        IsPaused = false;
    }
}
