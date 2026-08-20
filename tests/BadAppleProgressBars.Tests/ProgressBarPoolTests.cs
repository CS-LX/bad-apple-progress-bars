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
    public void ApplyFrame_InsertsAlignedGapsWithoutChangingCanvasChildren()
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

            pool.ApplyFrame(frame, surfaceWidth: 40, surfaceHeight: 12);

            Assert.Equal(4, canvas.Children.Count);
            Assert.Same(originalBars[0], canvas.Children[0]);
            Assert.Same(originalBars[3], canvas.Children[3]);
            // W→B splits at columns 1 and 2 create aligned 2px gap columns for both rows.
            // Bars that cross a gap column gain its width, preserving their logical fill fraction.
            AssertBar(originalBars[0], left: 0, top: 0, width: 20, height: 5, maximum: 20, value: 9);
            AssertBar(originalBars[1], left: 22, top: 0, width: 18, height: 5, maximum: 18, value: 9);
            AssertBar(originalBars[2], left: 0, top: 7, width: 9, height: 5, maximum: 9, value: 0);
            AssertBar(originalBars[3], left: 11, top: 7, width: 20, height: 5, maximum: 20, value: 9);

            pool.ApplyFrame([RowBlockEncoder.Encode(ToPixels("BWBW"))], surfaceWidth: 40, surfaceHeight: 12);

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

    [Fact]
    public void ApplyFrame_ExpandsBarsThatCrossAnotherRowsGapColumn()
    {
        RunInSta(() =>
        {
            var canvas = new Canvas();
            var pool = new ProgressBarPool(canvas);
            pool.ConfigureGrid(width: 6, height: 2);
            var bars = canvas.Children.OfType<ProgressBar>().ToArray();

            pool.ApplyFrame(
            [
                RowBlockEncoder.Encode(ToPixels("BWBWWW")),
                RowBlockEncoder.Encode(ToPixels("BWWWWW")),
            ],
            surfaceWidth: 62,
            surfaceHeight: 12);

            // The first row creates a 2px gap before column 2. The long second-row
            // bar crosses that column, so its physical width includes the same 2px.
            AssertBar(bars[0], left: 0, top: 0, width: 20, height: 5, maximum: 20, value: 10);
            AssertBar(bars[1], left: 22, top: 0, width: 40, height: 5, maximum: 40, value: 10);
            AssertBar(bars[2], left: 0, top: 7, width: 62, height: 5, maximum: 62, value: 10);
        });
    }

    [Fact]
    public void ApplyStates_UsesBakedSlotIdsAndHidesSlotsOmittedByTheSnapshot()
    {
        RunInSta(() =>
        {
            var canvas = new Canvas();
            var pool = new ProgressBarPool(canvas);
            pool.ConfigureGrid(width: 4, height: 1);
            var bars = canvas.Children.OfType<ProgressBar>().ToArray();

            pool.ApplyStates(
            [
                new BarState(SlotId: 1, Visible: true, Row: 0, StartX: 2, Length: 2, Maximum: 2, Value: 1),
            ],
            surfaceWidth: 40,
            surfaceHeight: 5);

            Assert.Equal(Visibility.Hidden, bars[0].Visibility);
            AssertBar(bars[1], left: 21, top: 0, width: 19, height: 5, maximum: 19, value: 9.5);

            pool.ApplyStates(
            [
                new BarState(SlotId: 0, Visible: false, Row: 0, StartX: 0, Length: 0, Maximum: 0, Value: 0),
            ],
            surfaceWidth: 40,
            surfaceHeight: 5);

            Assert.All(bars, bar => Assert.Equal(Visibility.Hidden, bar.Visibility));
            Assert.Equal(2, canvas.Children.Count);
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
