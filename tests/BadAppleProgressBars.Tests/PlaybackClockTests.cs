using BadAppleProgressBars.Playback;

namespace BadAppleProgressBars.Tests;

public class PlaybackClockTests
{
    [Fact]
    public void Pause_FreezesElapsedTimeUntilResume()
    {
        long timestamp = 0;
        var clock = new PlaybackClock(() => timestamp, timestampFrequency: 1_000);

        clock.Start();
        timestamp = 250;
        Assert.Equal(TimeSpan.FromMilliseconds(250), clock.Elapsed);

        clock.Pause();
        timestamp = 1_250;
        Assert.Equal(TimeSpan.FromMilliseconds(250), clock.Elapsed);

        clock.Resume();
        timestamp = 1_500;
        Assert.Equal(TimeSpan.FromMilliseconds(500), clock.Elapsed);
    }

    [Fact]
    public void Start_RestartsElapsedTimeFromZero()
    {
        long timestamp = 100;
        var clock = new PlaybackClock(() => timestamp, timestampFrequency: 1_000);

        clock.Start();
        timestamp = 600;
        Assert.Equal(TimeSpan.FromMilliseconds(500), clock.Elapsed);

        clock.Start();
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }
}
