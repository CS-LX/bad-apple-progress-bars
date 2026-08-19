using System.IO;
using System.Threading.Channels;
using BadAppleProgressBars.Baking;
using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Playback;

/// <summary>
/// Plays a baked video through a bounded, background read-ahead queue.
/// The playback path opens only the .bpb cache and never its source video.
/// </summary>
public sealed class BakedVideoStreamPlayer : IDisposable
{
    private readonly string _bakedFilePath;
    private readonly int _queueCapacity;
    private readonly PlaybackClock _clock;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private Channel<BakedFrame>? _frameQueue;
    private CancellationTokenSource? _producerCancellation;
    private Task? _producerTask;
    private BakedFrame? _nextFrame;
    private TimeSpan _timelineOrigin;
    private int _nextFrameIndex;
    private int _bufferedFrameCount;
    private bool _disposed;

    public BakedVideoStreamPlayer(string bakedFilePath, int queueCapacity = 8, PlaybackClock? clock = null)
    {
        if (string.IsNullOrWhiteSpace(bakedFilePath))
        {
            throw new ArgumentException("A .bpb file path is required.", nameof(bakedFilePath));
        }

        if (queueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }

        _bakedFilePath = Path.GetFullPath(bakedFilePath);
        _queueCapacity = queueCapacity;
        _clock = clock ?? new PlaybackClock();

        using var stream = OpenBakedFile();
        using var reader = new BakedVideoReader(stream);
        Header = reader.Header;
        CurrentFrameIndex = -1;
    }

    public BakedVideoHeader Header { get; }

    public int QueueCapacity => _queueCapacity;

    public int BufferedFrameCount => Math.Clamp(Volatile.Read(ref _bufferedFrameCount), 0, _queueCapacity);

    public bool IsPaused => _clock.IsPaused;

    public bool IsCompleted { get; private set; }

    public int CurrentFrameIndex { get; private set; }

    /// <summary>
    /// Contains a human-readable source or format error raised by the reader thread.
    /// </summary>
    public Exception? Failure { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default) => BeginAtFrameAsync(0, cancellationToken);

    /// <summary>
    /// Cancels prefetch, seeks through the on-disk index, and makes the requested frame immediately due.
    /// </summary>
    public Task SeekAsync(int frameIndex, CancellationToken cancellationToken = default) =>
        BeginAtFrameAsync(frameIndex, cancellationToken);

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
    /// Returns one due frame at a time. Frames that are not due remain buffered for a later UI tick.
    /// </summary>
    public bool TryDequeueDueFrame(out BakedFrame frame)
    {
        ThrowIfDisposed();

        if (Failure is not null || IsCompleted || _clock.IsPaused)
        {
            frame = null!;
            return false;
        }

        if (_nextFrame is null && _frameQueue is not null && _frameQueue.Reader.TryRead(out var bufferedFrame))
        {
            Interlocked.Decrement(ref _bufferedFrameCount);
            _nextFrame = bufferedFrame;
        }

        if (_nextFrame is null)
        {
            UpdateCompletion();
            frame = null!;
            return false;
        }

        if (_nextFrame.Timestamp - _timelineOrigin > _clock.Elapsed)
        {
            frame = null!;
            return false;
        }

        frame = _nextFrame;
        _nextFrame = null;
        CurrentFrameIndex = _nextFrameIndex++;
        UpdateCompletion();
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _producerCancellation?.Cancel();

        if (_producerTask is not null)
        {
            try
            {
                _producerTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the normal disposal path.
            }
        }

        _producerCancellation?.Dispose();
        _lifecycleGate.Dispose();
    }

    private async Task BeginAtFrameAsync(int frameIndex, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (frameIndex < 0 || frameIndex > Header.Metadata.FrameCount ||
            (frameIndex == Header.Metadata.FrameCount && Header.Metadata.FrameCount != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_producerCancellation is not null)
            {
                _producerCancellation.Cancel();
            }

            if (_producerTask is not null)
            {
                try
                {
                    await _producerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Replacing a read-ahead operation is an expected seek path.
                }
            }

            _producerCancellation?.Dispose();
            _producerCancellation = new CancellationTokenSource();
            _frameQueue = Channel.CreateBounded<BakedFrame>(new BoundedChannelOptions(_queueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });
            _nextFrame = null;
            _timelineOrigin = Header.Metadata.GetFrameTimestamp(frameIndex);
            _nextFrameIndex = frameIndex;
            _bufferedFrameCount = 0;
            CurrentFrameIndex = frameIndex - 1;
            Failure = null;
            IsCompleted = false;
            _clock.Start();

            var queue = _frameQueue;
            var cancellation = _producerCancellation.Token;
            _producerTask = Header.Metadata.FrameCount == 0
                ? Task.CompletedTask
                : Task.Run(() => ProduceFramesAsync(queue.Writer, frameIndex, cancellation), CancellationToken.None);

            if (Header.Metadata.FrameCount == 0)
            {
                queue.Writer.TryComplete();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ProduceFramesAsync(ChannelWriter<BakedFrame> writer, int startFrameIndex, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = OpenBakedFile();
            using var reader = new BakedVideoReader(stream);
            reader.SeekToFrame(startFrameIndex);

            while (reader.TryReadNextFrame(out var frame))
            {
                await writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _bufferedFrameCount);
            }

            writer.TryComplete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            Failure = exception;
            writer.TryComplete(exception);
        }
    }

    private void UpdateCompletion()
    {
        if (_nextFrame is not null || Failure is not null || _frameQueue is null || !_frameQueue.Reader.Completion.IsCompleted)
        {
            return;
        }

        IsCompleted = true;
    }

    private FileStream OpenBakedFile() => new(
        _bakedFilePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 4096,
        FileOptions.RandomAccess);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
