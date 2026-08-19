using System.Diagnostics;
using BadAppleProgressBars.Baking;
using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Playback;

namespace BadAppleProgressBars.Tests;

public class BakedVideoStreamPlayerTests
{
    [Fact]
    public async Task StartAsync_StreamsBakedFramesInOrderWithoutExceedingTheQueueLimit()
    {
        var bakedFilePath = WriteSyntheticVideo();
        long timestamp = 0;
        var clock = new PlaybackClock(() => timestamp, TimeSpan.TicksPerSecond);
        var expected = SyntheticFrameFactory.Create().Select(BarStateFrameConverter.FromPlaybackFrame).ToArray();

        try
        {
            using var player = new BakedVideoStreamPlayer(bakedFilePath, queueCapacity: 1, clock);
            await player.StartAsync();

            for (var frameIndex = 0; frameIndex < expected.Length; frameIndex++)
            {
                await WaitUntilAsync(() => player.BufferedFrameCount > 0 || player.Failure is not null);
                Assert.Null(player.Failure);
                Assert.InRange(player.BufferedFrameCount, 0, player.QueueCapacity);

                timestamp = expected[frameIndex].Timestamp.Ticks;
                Assert.True(player.TryDequeueDueFrame(out var actual));
                Assert.Equal(frameIndex, player.CurrentFrameIndex);
                Assert.Equal(expected[frameIndex].Timestamp, actual.Timestamp);
                Assert.Equal(expected[frameIndex].States, actual.States);
            }

            await WaitUntilAsync(() =>
            {
                player.TryDequeueDueFrame(out _);
                return player.IsCompleted;
            });
        }
        finally
        {
            File.Delete(bakedFilePath);
        }
    }

    [Fact]
    public async Task SeekAsync_ReplacesReadAheadAndMakesTargetFrameImmediatelyDue()
    {
        var bakedFilePath = WriteSyntheticVideo();
        long timestamp = 0;
        var clock = new PlaybackClock(() => timestamp, TimeSpan.TicksPerSecond);
        var expected = SyntheticFrameFactory.Create().Select(BarStateFrameConverter.FromPlaybackFrame).ToArray();

        try
        {
            using var player = new BakedVideoStreamPlayer(bakedFilePath, queueCapacity: 2, clock);
            await player.StartAsync();
            await WaitUntilAsync(() => player.BufferedFrameCount > 0);

            await player.SeekAsync(frameIndex: 3);
            await WaitUntilAsync(() => player.BufferedFrameCount > 0 || player.Failure is not null);

            Assert.Null(player.Failure);
            Assert.True(player.TryDequeueDueFrame(out var actual));
            Assert.Equal(3, player.CurrentFrameIndex);
            Assert.Equal(expected[3].Timestamp, actual.Timestamp);
            Assert.Equal(expected[3].States, actual.States);
        }
        finally
        {
            File.Delete(bakedFilePath);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();

        while (!condition())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(5))
            {
                throw new TimeoutException("The baked-video reader did not produce the expected frame in time.");
            }

            await Task.Delay(10);
        }
    }

    private static string WriteSyntheticVideo()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bad-apple-progress-bars-{Guid.NewGuid():N}.bpb");
        var frames = SyntheticFrameFactory.Create().Select(BarStateFrameConverter.FromPlaybackFrame).ToArray();
        var metadata = new BakedVideoMetadata(
            new byte[BakedVideoMetadata.HashLength],
            new byte[BakedVideoMetadata.HashLength],
            SyntheticFrameFactory.GridWidth,
            SyntheticFrameFactory.GridHeight,
            frameRateNumerator: 4,
            frameRateDenominator: 3,
            frameCount: frames.Length);

        using var stream = File.Create(path);
        BakedVideoWriter.Write(stream, metadata, frames);
        return path;
    }
}
