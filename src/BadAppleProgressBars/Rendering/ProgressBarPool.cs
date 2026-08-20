using System.Windows;
using System.Windows.Controls;
using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Rendering;

/// <summary>
/// Owns a fixed set of native WPF <see cref="ProgressBar"/> controls placed on a canvas.
/// </summary>
public sealed class ProgressBarPool
{
    /// <summary>
    /// The fixed device-independent spacing between distinct progress bars.
    /// </summary>
    public const double InterProgressBarGap = 2;

    private readonly Canvas _canvas;
    private readonly List<ProgressBar> _bars = [];
    private Style? _progressBarStyle;
    private int[] _slotGenerations = [];
    private int[] _gapColumnGenerations = [];
    private int[] _gapsBeforeColumn = [];
    private int _slotGeneration;
    private int _gapGeneration;
    private int _gridWidth;
    private int _gridHeight;

    public ProgressBarPool(Canvas canvas, Style? progressBarStyle = null)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _progressBarStyle = progressBarStyle;
    }

    /// <summary>
    /// Gets the number of controls currently owned by the pool.
    /// </summary>
    public int Count => _bars.Count;

    /// <summary>
    /// Selects the application-provided WPF style used when the pool is first created.
    /// </summary>
    public void SetStyle(Style progressBarStyle)
    {
        ArgumentNullException.ThrowIfNull(progressBarStyle);

        if (_bars.Count != 0)
        {
            throw new InvalidOperationException("The progress-bar style must be selected before configuring the pool.");
        }

        _progressBarStyle = progressBarStyle;
    }

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
        _slotGenerations = new int[poolSize];
        _gapColumnGenerations = new int[width];
        _gapsBeforeColumn = new int[width + 1];
        _slotGeneration = 0;
        _gapGeneration = 0;

        for (var index = 0; index < poolSize; index++)
        {
            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Visibility = Visibility.Hidden,
            };

            if (_progressBarStyle is not null)
            {
                progressBar.Style = _progressBarStyle;
            }

            _bars.Add(progressBar);
            _canvas.Children.Add(progressBar);
        }
    }

    /// <summary>
    /// Applies a row-major frame to the existing controls without adding or removing canvas children.
    /// </summary>
    public void ApplyFrame(
        IReadOnlyList<IReadOnlyList<MonotonicBlock>> rows,
        double surfaceWidth,
        double surfaceHeight)
    {
        ArgumentNullException.ThrowIfNull(rows);

        EnsureReady(surfaceWidth, surfaceHeight);

        if (rows.Count > _gridHeight)
        {
            throw new ArgumentException("The frame has more rows than the configured grid.", nameof(rows));
        }

        BeginGapLayout();

        foreach (var blocks in rows)
        {
            if (blocks is null)
            {
                throw new ArgumentException("A frame row cannot be null.", nameof(rows));
            }

            foreach (var block in blocks)
            {
                ValidateBlock(block);
                MarkGapColumn(block.StartX);
            }
        }

        var layout = CompleteLayout(surfaceWidth, surfaceHeight);

        var barIndex = 0;

        for (var row = 0; row < rows.Count; row++)
        {
            var blocks = rows[row] ?? throw new ArgumentException("A frame row cannot be null.", nameof(rows));

            foreach (var block in blocks)
            {
                if (barIndex >= _bars.Count)
                {
                    throw new ArgumentException("The frame requires more progress bars than the configured pool.", nameof(rows));
                }

                ApplyBarLayout(
                    _bars[barIndex++],
                    row,
                    block.StartX,
                    block.Length,
                    block.BlackPrefixLength,
                    layout);
            }
        }

        for (; barIndex < _bars.Count; barIndex++)
        {
            _bars[barIndex].Visibility = Visibility.Hidden;
        }
    }

    /// <summary>
    /// Applies the explicit slot states stored in a baked frame without changing canvas children.
    /// States omitted from the frame are hidden.
    /// </summary>
    public void ApplyStates(
        IReadOnlyList<BarState> states,
        double surfaceWidth,
        double surfaceHeight)
    {
        ArgumentNullException.ThrowIfNull(states);
        EnsureReady(surfaceWidth, surfaceHeight);

        if (_slotGeneration == int.MaxValue)
        {
            Array.Clear(_slotGenerations);
            _slotGeneration = 0;
        }

        _slotGeneration++;
        BeginGapLayout();

        foreach (var state in states)
        {
            ValidateState(state);

            if (state.Visible)
            {
                MarkGapColumn(state.StartX);
            }
        }

        var layout = CompleteLayout(surfaceWidth, surfaceHeight);

        foreach (var state in states)
        {
            if (_slotGenerations[state.SlotId] == _slotGeneration)
            {
                throw new ArgumentException("A baked frame cannot contain the same slot more than once.", nameof(states));
            }

            _slotGenerations[state.SlotId] = _slotGeneration;
            var progressBar = _bars[state.SlotId];

            if (!state.Visible)
            {
                progressBar.Visibility = Visibility.Hidden;
                continue;
            }

            ApplyBarLayout(
                progressBar,
                state.Row,
                state.StartX,
                state.Length,
                state.Value,
                layout);
        }

        for (var slotId = 0; slotId < _bars.Count; slotId++)
        {
            if (_slotGenerations[slotId] != _slotGeneration)
            {
                _bars[slotId].Visibility = Visibility.Hidden;
            }
        }
    }

    /// <summary>
    /// Hides every pooled control while preserving the pool itself.
    /// </summary>
    public void Clear()
    {
        foreach (var progressBar in _bars)
        {
            progressBar.Visibility = Visibility.Hidden;
        }
    }

    private void EnsureReady(double surfaceWidth, double surfaceHeight)
    {
        if (_gridWidth == 0 || _gridHeight == 0)
        {
            throw new InvalidOperationException("ConfigureGrid must be called before applying a frame.");
        }

        if (!IsPositiveFinite(surfaceWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceWidth));
        }

        if (!IsPositiveFinite(surfaceHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceHeight));
        }
    }

    private void BeginGapLayout()
    {
        if (_gapGeneration == int.MaxValue)
        {
            Array.Clear(_gapColumnGenerations);
            _gapGeneration = 0;
        }

        _gapGeneration++;
    }

    private void MarkGapColumn(int column)
    {
        if (column > 0)
        {
            _gapColumnGenerations[column] = _gapGeneration;
        }
    }

    private FrameLayout CompleteLayout(double surfaceWidth, double surfaceHeight)
    {
        var gapCount = 0;

        for (var column = 0; column <= _gridWidth; column++)
        {
            _gapsBeforeColumn[column] = gapCount;

            if (column < _gridWidth && _gapColumnGenerations[column] == _gapGeneration)
            {
                gapCount++;
            }
        }

        var cellWidth = (surfaceWidth - (gapCount * InterProgressBarGap)) / _gridWidth;
        var cellHeight = (surfaceHeight - ((_gridHeight - 1) * InterProgressBarGap)) / _gridHeight;

        if (!IsPositiveFinite(cellWidth) || !IsPositiveFinite(cellHeight))
        {
            throw new InvalidOperationException("The playback surface is too small to fit the grid and 2px progress-bar gaps.");
        }

        return new FrameLayout(cellWidth, cellHeight);
    }

    private void ApplyBarLayout(
        ProgressBar progressBar,
        int row,
        int startX,
        int length,
        int blackPrefixLength,
        FrameLayout layout)
    {
        var endX = checked(startX + length);
        var left = GetPhysicalXAfterGap(startX, layout.CellWidth);
        var right = GetPhysicalXBeforeGap(endX, layout.CellWidth);
        var maximum = right - left;
        var value = blackPrefixLength == 0
            ? 0
            : GetPhysicalXBeforeGap(startX + blackPrefixLength, layout.CellWidth) - left;

        Canvas.SetLeft(progressBar, left);
        Canvas.SetTop(progressBar, row * (layout.CellHeight + InterProgressBarGap));
        progressBar.Width = maximum;
        progressBar.Height = layout.CellHeight;
        progressBar.Maximum = maximum;
        progressBar.Value = value;
        progressBar.Visibility = Visibility.Visible;
    }

    private double GetPhysicalXBeforeGap(int column, double cellWidth) =>
        (column * cellWidth) + (_gapsBeforeColumn[column] * InterProgressBarGap);

    private double GetPhysicalXAfterGap(int column, double cellWidth) =>
        GetPhysicalXBeforeGap(column, cellWidth) +
        (_gapColumnGenerations[column] == _gapGeneration ? InterProgressBarGap : 0);

    private void ValidateState(BarState state)
    {
        if (state.SlotId < 0 ||
            state.SlotId >= _bars.Count ||
            state.Row < 0 ||
            state.Row >= _gridHeight ||
            state.StartX < 0 ||
            state.Length < 0 ||
            state.Maximum < 0 ||
            state.Value < 0 ||
            state.Value > state.Maximum ||
            (state.Visible && (state.Length == 0 || state.StartX > _gridWidth - state.Length)))
        {
            throw new ArgumentException("A baked bar state is invalid.");
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

    private readonly record struct FrameLayout(double CellWidth, double CellHeight);
}
