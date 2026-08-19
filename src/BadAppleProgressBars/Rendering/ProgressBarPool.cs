using System.Windows;
using System.Windows.Controls;
using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Rendering;

/// <summary>
/// Owns a fixed set of native WPF <see cref="ProgressBar"/> controls placed on a canvas.
/// </summary>
public sealed class ProgressBarPool
{
    private readonly Canvas _canvas;
    private readonly List<ProgressBar> _bars = [];
    private int _gridWidth;
    private int _gridHeight;

    public ProgressBarPool(Canvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    }

    /// <summary>
    /// Gets the number of controls currently owned by the pool.
    /// </summary>
    public int Count => _bars.Count;

    /// <summary>
    /// Creates the pool for a grid, or leaves the existing controls untouched when its size is unchanged.
    /// </summary>
    public void ConfigureGrid(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (width == _gridWidth && height == _gridHeight)
        {
            return;
        }

        _canvas.Children.Clear();
        _bars.Clear();
        _gridWidth = width;
        _gridHeight = height;

        var poolSize = checked(height * ((width + 1) / 2));

        for (var index = 0; index < poolSize; index++)
        {
            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Visibility = Visibility.Hidden,
            };

            _bars.Add(progressBar);
            _canvas.Children.Add(progressBar);
        }
    }

    /// <summary>
    /// Applies a row-major frame to the existing controls without adding or removing canvas children.
    /// </summary>
    public void ApplyFrame(
        IReadOnlyList<IReadOnlyList<MonotonicBlock>> rows,
        double cellWidth,
        double cellHeight)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (_gridWidth == 0 || _gridHeight == 0)
        {
            throw new InvalidOperationException("ConfigureGrid must be called before applying a frame.");
        }

        if (rows.Count > _gridHeight)
        {
            throw new ArgumentException("The frame has more rows than the configured grid.", nameof(rows));
        }

        if (!IsPositiveFinite(cellWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(cellWidth));
        }

        if (!IsPositiveFinite(cellHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(cellHeight));
        }

        var barIndex = 0;

        for (var row = 0; row < rows.Count; row++)
        {
            var blocks = rows[row] ?? throw new ArgumentException("A frame row cannot be null.", nameof(rows));

            foreach (var block in blocks)
            {
                ValidateBlock(block);

                if (barIndex >= _bars.Count)
                {
                    throw new ArgumentException("The frame requires more progress bars than the configured pool.", nameof(rows));
                }

                var progressBar = _bars[barIndex++];
                Canvas.SetLeft(progressBar, block.StartX * cellWidth);
                Canvas.SetTop(progressBar, row * cellHeight);
                progressBar.Width = block.Length * cellWidth;
                progressBar.Height = cellHeight;
                progressBar.Maximum = block.Length;
                progressBar.Value = block.BlackPrefixLength;
                progressBar.Visibility = Visibility.Visible;
            }
        }

        for (; barIndex < _bars.Count; barIndex++)
        {
            _bars[barIndex].Visibility = Visibility.Hidden;
        }
    }

    private void ValidateBlock(MonotonicBlock block)
    {
        if (block.StartX < 0 ||
            block.Length <= 0 ||
            block.BlackPrefixLength < 0 ||
            block.BlackPrefixLength > block.Length)
        {
            throw new ArgumentException("A block has invalid dimensions.");
        }

        if (block.StartX > _gridWidth - block.Length)
        {
            throw new ArgumentException("A block exceeds the configured grid width.");
        }
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;
}
