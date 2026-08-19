using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Playback;
using BadAppleProgressBars.Rendering;
using BadAppleProgressBars.Segmentation;
using System.Windows.Threading;

namespace BadAppleProgressBars;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const double CellWidth = 60;
    private const double CellHeight = 24;

    private readonly ProgressBarPool _progressBarPool;
    private readonly PlaybackSession _playbackSession;
    private readonly DispatcherTimer _playbackTimer;

    public MainWindow()
    {
        InitializeComponent();
        _progressBarPool = new ProgressBarPool(PlaybackCanvas);
        _progressBarPool.ConfigureGrid(SyntheticFrameFactory.GridWidth, SyntheticFrameFactory.GridHeight);
        _playbackSession = new PlaybackSession(SyntheticFrameFactory.Create());
        _playbackTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(8),
        };

        _playbackTimer.Tick += OnPlaybackTimerTick;
        Loaded += OnLoaded;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestartPlayback();
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs e)
    {
        ApplyDueFrames();

        if (_playbackSession.IsCompleted)
        {
            _playbackTimer.Stop();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && !_playbackSession.IsCompleted)
        {
            if (_playbackSession.IsPaused)
            {
                _playbackSession.Resume();
                _playbackTimer.Start();
            }
            else
            {
                _playbackSession.Pause();
                _playbackTimer.Stop();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.R)
        {
            RestartPlayback();
            e.Handled = true;
        }
    }

    private void RestartPlayback()
    {
        _playbackTimer.Stop();
        _playbackSession.Start();
        ApplyDueFrames();

        if (!_playbackSession.IsCompleted)
        {
            _playbackTimer.Start();
        }
    }

    private void ApplyDueFrames()
    {
        while (_playbackSession.TryDequeueDueFrame(out var frame))
        {
            _progressBarPool.ApplyFrame(frame.Rows, CellWidth, CellHeight);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _playbackTimer.Stop();
    }
}
