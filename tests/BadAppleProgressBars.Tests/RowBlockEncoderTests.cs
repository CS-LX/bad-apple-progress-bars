using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Segmentation;

namespace BadAppleProgressBars.Tests;

public class RowBlockEncoderTests
{
    [Theory]
    [InlineData("BWBW", 0, 2, 1, 2, 2, 1)]
    [InlineData("WBW", 0, 1, 0, 1, 2, 1)]
    [InlineData("BBB", 0, 3, 3)]
    [InlineData("WBBBWBWWWBBWWB", 0, 1, 0, 1, 4, 3, 5, 4, 1, 9, 4, 2, 13, 1, 1)]
    [InlineData("WWWW", 0, 4, 0)]
    [InlineData("BBBB", 0, 4, 4)]
    [InlineData("BW", 0, 2, 1)]
    [InlineData("WB", 0, 1, 0, 1, 1, 1)]
    public void Encode_ProducesExpectedBlocks(string row, params int[] expectedValues)
    {
        var actual = RowBlockEncoder.Encode(ToPixels(row));

        Assert.Equal(ToBlocks(expectedValues), actual);
    }

    [Theory]
    [InlineData("BWBW")]
    [InlineData("WBW")]
    [InlineData("BBB")]
    [InlineData("WBBBWBWWWBBWWB")]
    [InlineData("WWWW")]
    [InlineData("BBBB")]
    [InlineData("BW")]
    [InlineData("WB")]
    public void Encode_PreservesPixelsAndProducesMinimalMonotonicBlocks(string row)
    {
        var pixels = ToPixels(row);
        var originalPixels = pixels.ToArray();

        var blocks = RowBlockEncoder.Encode(pixels);

        Assert.Equal(originalPixels, pixels);
        Assert.Equal(CountWhiteToBlackTransitions(pixels) + 1, blocks.Length);
        Assert.Equal(pixels, Reconstruct(blocks));
        Assert.All(blocks, block => Assert.InRange(block.BlackPrefixLength, 0, block.Length));
        AssertBlocksCoverTheRow(blocks, pixels.Length);
    }

    [Fact]
    public void Encode_EmptyRow_ReturnsNoBlocks()
    {
        Assert.Empty(RowBlockEncoder.Encode([]));
    }

    private static bool[] ToPixels(string row) => [.. row.Select(pixel => pixel == 'B')];

    private static MonotonicBlock[] ToBlocks(IReadOnlyList<int> values)
    {
        Assert.True(values.Count % 3 == 0);

        return [.. Enumerable.Range(0, values.Count / 3)
            .Select(index => new MonotonicBlock(
                values[index * 3],
                values[index * 3 + 1],
                values[index * 3 + 2]))];
    }

    private static int CountWhiteToBlackTransitions(ReadOnlySpan<bool> pixels)
    {
        var transitions = 0;

        for (var x = 1; x < pixels.Length; x++)
        {
            if (!pixels[x - 1] && pixels[x])
            {
                transitions++;
            }
        }

        return transitions;
    }

    private static void AssertBlocksCoverTheRow(IEnumerable<MonotonicBlock> blocks, int rowLength)
    {
        var expectedStartX = 0;

        foreach (var block in blocks)
        {
            Assert.Equal(expectedStartX, block.StartX);
            Assert.True(block.Length > 0);
            expectedStartX += block.Length;
        }

        Assert.Equal(rowLength, expectedStartX);
    }

    private static bool[] Reconstruct(IEnumerable<MonotonicBlock> blocks) =>
        [.. blocks.SelectMany(block =>
            Enumerable.Range(0, block.Length)
                .Select(offset => offset < block.BlackPrefixLength))];
}
