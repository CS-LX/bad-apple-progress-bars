using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BadAppleProgressBars.Baking;
using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Playback;
using BadAppleProgressBars.Rendering;

namespace BadAppleProgressBars;

/// <summary>
/// Hosts only the native progress-bar surface; file prefetch and playback state live outside the window.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ProgressBarPool _progressBarPool;
    private readonly DispatcherTimer _playbackTimer;
    private readonly CancellationTokenSource _windowCancellation = new();
    private BakedVideoStreamPlayer? _streamPlayer;
    private BakedFrame? _lastFrame;
    private string? _temporaryBakedFilePath;

    public MainWindow()
    {
        InitializeComponent();
        _progressBarPool = new ProgressBarPool(PlaybackCanvas);
        _playbackTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(8),
        };

        _playbackTimer.Tick += OnPlaybackTimerTick;
        Loaded += OnLoaded;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
        PlaybackCanvas.SizeChanged += OnPlaybackCanvasSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RestartPlaybackAsync();
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs e)
    {
        ApplyDueFrames();

        if (_streamPlayer?.Failure is { } failure)
        {
            ReportFailure(failure);
        }
        else if (_streamPlayer?.IsCompleted == true)
        {
            _playbackTimer.Stop();
        }
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_streamPlayer is null)
        {
            return;
        }

        if (e.Key == Key.Space && !_streamPlayer.IsCompleted)
        {
            if (_streamPlayer.IsPaused)
            {
                _streamPlayer.Resume();
                _playbackTimer.Start();
            }
            else
            {
                _streamPlayer.Pause();
                _playbackTimer.Stop();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.R || e.Key == Key.Home)
        {
            await RestartPlaybackAsync();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right)
        {
            var currentFrame = Math.Max(0, _streamPlayer.CurrentFrameIndex);
            var offset = e.Key == Key.Left ? -1 : 1;
            var targetFrame = Math.Clamp(
                currentFrame + offset,
                0,
                _streamPlayer.Header.Metadata.FrameCount - 1);
            await SeekAsync(targetFrame);
            e.Handled = true;
        }
    }

    private async Task RestartPlaybackAsync()
    {
        try
        {
            _playbackTimer.Stop();

            if (_streamPlayer is null)
            {
                _streamPlayer = new BakedVideoStreamPlayer(await ResolveBakedFilePathAsync());
                var metadata = _streamPlayer.Header.Metadata;
                _progressBarPool.ConfigureGrid(metadata.Width, metadata.Height);
            }

            _progressBarPool.Clear();
            _lastFrame = null;
            await _streamPlayer.StartAsync();
            _playbackTimer.Start();
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
        }
    }

    private async Task SeekAsync(int frameIndex)
    {
        try
        {
            _playbackTimer.Stop();
            _progressBarPool.Clear();
            _lastFrame = null;
            await _streamPlayer!.SeekAsync(frameIndex);
            _playbackTimer.Start();
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
        }
    }

    private void ApplyDueFrames()
    {
        if (_streamPlayer is null)
        {
            return;
        }

        while (_streamPlayer.TryDequeueDueFrame(out var frame))
        {
            _lastFrame = frame;
            ApplyFrame(frame);
        }
    }

    private void OnPlaybackCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_lastFrame is not null)
        {
            ApplyFrame(_lastFrame);
        }
    }

    private void ApplyFrame(BakedFrame frame)
    {
        if (_streamPlayer is null || PlaybackCanvas.ActualWidth <= 0 || PlaybackCanvas.ActualHeight <= 0)
        {
            return;
        }

        var metadata = _streamPlayer.Header.Metadata;
        _progressBarPool.ApplyStates(
            frame.States,
            PlaybackCanvas.ActualWidth / metadata.Width,
            PlaybackCanvas.ActualHeight / metadata.Height);
    }

    private async Task<string> ResolveBakedFilePathAsync()
    {
        var suppliedFile = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(File.Exists);

        if (suppliedFile is null)
        {
            return DemoBakedVideoFile.EnsureCreated();
        }

        var inputPath = Path.GetFullPath(suppliedFile);

        if (string.Equals(Path.GetExtension(inputPath), ".bpb", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "BadAppleProgressBars", "bakes");
        Directory.CreateDirectory(temporaryDirectory);
        _temporaryBakedFilePath = Path.Combine(temporaryDirectory, $"startup-{Guid.NewGuid():N}.bpb");
        var progress = new Progress<FfmpegBakeProgress>(value =>
            Title = $"Bad Apple Progress Bars - baking {value.CompletedFrames}/{value.TotalFrames}");
        await new FfmpegVideoBaker().BakeAsync(
            inputPath,
            _temporaryBakedFilePath,
            progress: progress,
            cancellationToken: _windowCancellation.Token);
        Title = "Bad Apple Progress Bars";
        return _temporaryBakedFilePath;
    }

    private void ReportFailure(Exception exception)
    {
        _playbackTimer.Stop();
        _progressBarPool.Clear();
        Title = $"Bad Apple Progress Bars - cache error: {exception.Message}";
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _playbackTimer.Stop();
        _windowCancellation.Cancel();
        _streamPlayer?.Dispose();

        if (_temporaryBakedFilePath is not null && File.Exists(_temporaryBakedFilePath))
        {
            File.Delete(_temporaryBakedFilePath);
        }

        _windowCancellation.Dispose();
    }
}
