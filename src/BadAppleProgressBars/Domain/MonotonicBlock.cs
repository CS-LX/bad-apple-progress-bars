namespace BadAppleProgressBars.Domain;

/// <summary>
/// A contiguous row segment that can be represented by one progress bar.
/// The segment always has the form B*W*.
/// </summary>
/// <param name="StartX">Zero-based column where the segment starts.</param>
/// <param name="Length">Total number of pixels in the segment.</param>
/// <param name="BlackPrefixLength">Number of black pixels at the segment's start.</param>
public readonly record struct MonotonicBlock(
    int StartX,
    int Length,
    int BlackPrefixLength);
