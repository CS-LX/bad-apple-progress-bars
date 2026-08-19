namespace BadAppleProgressBars.Playback;

/// <summary>
/// Releases synthetic or baked frames when their playback timestamps become due.
/// </summary>
public sealed class PlaybackSession
{
    private readonly PlaybackClock _clock;
    private readonly PlaybackFrame[] _frames;
    private int _nextFrameIndex;

    public PlaybackSession(IReadOnlyList<PlaybackFrame> frames, PlaybackClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _frames = [.. frames];

        for (var index = 0; index < _frames.Length; index++)
        {
            if (_frames[index] is null)
            {
                throw new ArgumentException("A playback frame cannot be null.", nameof(frames));
            }

            if (index > 0 && _frames[index].Timestamp < _frames[index - 1].Timestamp)
            {
                throw new ArgumentException("Frame timestamps must be nondecreasing.", nameof(frames));
            }
        }

        _clock = clock ?? new PlaybackClock();
        CurrentFrameIndex = -1;
    }

    public bool IsPaused => _clock.IsPaused;

    public bool IsCompleted { get; private set; }

    public int CurrentFrameIndex { get; private set; }

    public void Start()
    {
        _clock.Start();
        _nextFrameIndex = 0;
        CurrentFrameIndex = -1;
        IsCompleted = _frames.Length == 0;
    }

    public void Pause()
    {
        if (!IsCompleted)
        {
            _clock.Pause();
        }
    }

    public void Resume()
    {
        if (!IsCompleted)
        {
            _clock.Resume();
        }
    }

    /// <summary>
    /// Gets one due frame at a time, preserving frame order when the UI is briefly behind schedule.
    /// </summary>
    public bool TryDequeueDueFrame(out PlaybackFrame frame)
    {
        if (!_clock.IsRunning || IsPaused || IsCompleted || _nextFrameIndex >= _frames.Length)
        {
            frame = null!;
            return false;
        }

        var nextFrame = _frames[_nextFrameIndex];

        if (nextFrame.Timestamp > _clock.Elapsed)
        {
            frame = null!;
            return false;
        }

        frame = nextFrame;
        CurrentFrameIndex = _nextFrameIndex++;
        IsCompleted = _nextFrameIndex == _frames.Length;
        return true;
    }
}
