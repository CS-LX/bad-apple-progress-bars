using BadAppleProgressBars.Playback;

namespace BadAppleProgressBars.Tests;

public class PlaybackSessionTests
{
    [Fact]
    public void Session_EmitsFramesInOrderAndResumesWithoutRestarting()
    {
        long timestamp = 0;
        var clock = new PlaybackClock(() => timestamp, timestampFrequency: 1_000);
        var session = new PlaybackSession(
        [
            CreateFrame(0),
            CreateFrame(100),
            CreateFrame(200),
        ],
        clock);

        session.Start();

        Assert.True(session.TryDequeueDueFrame(out var firstFrame));
        Assert.Equal(TimeSpan.Zero, firstFrame.Timestamp);
        Assert.False(session.TryDequeueDueFrame(out _));

        timestamp = 100;
        Assert.True(session.TryDequeueDueFrame(out var secondFrame));
        Assert.Equal(TimeSpan.FromMilliseconds(100), secondFrame.Timestamp);
        Assert.Equal(1, session.CurrentFrameIndex);

        session.Pause();
        timestamp = 1_100;
        Assert.False(session.TryDequeueDueFrame(out _));
        Assert.Equal(1, session.CurrentFrameIndex);

        session.Resume();
        timestamp = 1_199;
        Assert.False(session.TryDequeueDueFrame(out _));

        timestamp = 1_200;
        Assert.True(session.TryDequeueDueFrame(out var thirdFrame));
        Assert.Equal(TimeSpan.FromMilliseconds(200), thirdFrame.Timestamp);
        Assert.Equal(2, session.CurrentFrameIndex);
        Assert.True(session.IsCompleted);
        Assert.False(session.TryDequeueDueFrame(out _));
    }

    [Fact]
    public void Session_WithNoFrames_CompletesImmediately()
    {
        var session = new PlaybackSession([]);

        session.Start();

        Assert.True(session.IsCompleted);
        Assert.False(session.TryDequeueDueFrame(out _));
    }

    private static PlaybackFrame CreateFrame(int milliseconds) =>
        new(TimeSpan.FromMilliseconds(milliseconds), Array.Empty<IReadOnlyList<Domain.MonotonicBlock>>());
}
