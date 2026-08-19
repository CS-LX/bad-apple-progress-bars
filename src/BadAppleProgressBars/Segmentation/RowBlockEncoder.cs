using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Segmentation;

/// <summary>
/// Splits a binary row into the smallest set of progress-bar-compatible B*W* blocks.
/// A pixel value of <see langword="true"/> denotes black; <see langword="false"/> denotes white.
/// </summary>
public static class RowBlockEncoder
{
    /// <summary>
    /// Encodes a binary row without modifying the supplied pixels.
    /// </summary>
    public static MonotonicBlock[] Encode(ReadOnlySpan<bool> pixels)
    {
        if (pixels.IsEmpty)
        {
            return [];
        }

        var blocks = new List<MonotonicBlock>();
        var blockStart = 0;

        for (var x = 1; x < pixels.Length; x++)
        {
            if (!pixels[x - 1] && pixels[x])
            {
                blocks.Add(CreateBlock(pixels[blockStart..x], blockStart));
                blockStart = x;
            }
        }

        blocks.Add(CreateBlock(pixels[blockStart..], blockStart));
        return [.. blocks];
    }

    private static MonotonicBlock CreateBlock(ReadOnlySpan<bool> pixels, int startX)
    {
        var blackPrefixLength = 0;

        while (blackPrefixLength < pixels.Length && pixels[blackPrefixLength])
        {
            blackPrefixLength++;
        }

        return new MonotonicBlock(startX, pixels.Length, blackPrefixLength);
    }
}
