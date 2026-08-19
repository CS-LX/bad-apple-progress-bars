using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Rendering;
using BadAppleProgressBars.Segmentation;

namespace BadAppleProgressBars.Tests;

public class ProgressBarPoolTests
{
    [Fact]
    public void ConfigureGrid_CreatesTheExpectedNumberOfHiddenNativeProgressBars()
    {
        RunInSta(() =>
        {
            var canvas = new Canvas();
            var pool = new ProgressBarPool(canvas);

            pool.ConfigureGrid(width: 4, height: 2);

            var bars = canvas.Children.OfType<ProgressBar>().ToArray();
            Assert.Equal(4, pool.Count);
            Assert.Equal(4, bars.Length);
            Assert.All(bars, bar =>
            {
                Assert.Equal(0, bar.Minimum);
                Assert.Equal(1, bar.Maximum);
                Assert.Equal(0, bar.Value);
                Assert.Equal(Visibility.Hidden, bar.Visibility);
                Assert.Same(DependencyProperty.UnsetValue, bar.ReadLocalValue(Control.ForegroundProperty));
            });
        });
    }

    [Fact]
    public void ApplyFrame_MapsBlocksWithoutChangingCanvasChildren()
    {
        RunInSta(() =>
        {
            var canvas = new Canvas();
            var pool = new ProgressBarPool(canvas);
            pool.ConfigureGrid(width: 4, height: 2);
            var originalBars = canvas.Children.OfType<ProgressBar>().ToArray();

            IReadOnlyList<MonotonicBlock>[] frame =
            [
                RowBlockEncoder.Encode(ToPixels("BWBW")),
                RowBlockEncoder.Encode(ToPixels("WBW")),
            ];

            pool.ApplyFrame(frame, cellWidth: 10, cellHeight: 5);

            Assert.Equal(4, canvas.Children.Count);
            Assert.Same(originalBars[0], canvas.Children[0]);
            Assert.Same(originalBars[3], canvas.Children[3]);
            AssertBar(originalBars[0], left: 0, top: 0, width: 20, height: 5, maximum: 2, value: 1);
            AssertBar(originalBars[1], left: 20, top: 0, width: 20, height: 5, maximum: 2, value: 1);
            AssertBar(originalBars[2], left: 0, top: 5, width: 10, height: 5, maximum: 1, value: 0);
            AssertBar(originalBars[3], left: 10, top: 5, width: 20, height: 5, maximum: 2, value: 1);

            pool.ApplyFrame([RowBlockEncoder.Encode(ToPixels("BWBW"))], cellWidth: 10, cellHeight: 5);

            Assert.Equal(4, canvas.Children.Count);
            Assert.All(originalBars.Take(2), bar => Assert.Equal(Visibility.Visible, bar.Visibility));
            Assert.All(originalBars.Skip(2), bar => Assert.Equal(Visibility.Hidden, bar.Visibility));
        });
    }

    [Fact]
    public void ConfigureGrid_WithSameDimensions_ReusesExistingControls()
    {
        RunInSta(() =>
        {
            var canvas = new Canvas();
            var pool = new ProgressBarPool(canvas);
            pool.ConfigureGrid(width: 3, height: 2);
            var originalBars = canvas.Children.OfType<ProgressBar>().ToArray();

            pool.ConfigureGrid(width: 3, height: 2);

            Assert.Equal(originalBars.Length, canvas.Children.Count);
            Assert.All(originalBars.Select((bar, index) => (bar, index)), pair =>
                Assert.Same(pair.bar, canvas.Children[pair.index]));
        });
    }

    private static void AssertBar(
        ProgressBar bar,
        double left,
        double top,
        double width,
        double height,
        double maximum,
        double value)
    {
        Assert.Equal(Visibility.Visible, bar.Visibility);
        Assert.Equal(left, Canvas.GetLeft(bar));
        Assert.Equal(top, Canvas.GetTop(bar));
        Assert.Equal(width, bar.Width);
        Assert.Equal(height, bar.Height);
        Assert.Equal(maximum, bar.Maximum);
        Assert.Equal(value, bar.Value);
    }

    private static bool[] ToPixels(string row) => [.. row.Select(pixel => pixel == 'B')];

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
