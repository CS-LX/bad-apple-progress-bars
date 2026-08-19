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
using BadAppleProgressBars.Rendering;
using BadAppleProgressBars.Segmentation;

namespace BadAppleProgressBars;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ProgressBarPool _progressBarPool;

    public MainWindow()
    {
        InitializeComponent();
        _progressBarPool = new ProgressBarPool(PlaybackCanvas);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        const double cellWidth = 80;
        const double cellHeight = 24;

        IReadOnlyList<MonotonicBlock>[] sampleFrame =
        [
            RowBlockEncoder.Encode(ToPixels("BWBW")),
            RowBlockEncoder.Encode(ToPixels("WBW")),
        ];

        _progressBarPool.ConfigureGrid(width: 4, height: sampleFrame.Length);
        _progressBarPool.ApplyFrame(sampleFrame, cellWidth, cellHeight);
    }

    private static bool[] ToPixels(string row) => [.. row.Select(pixel => pixel == 'B')];
}
