namespace BadAppleProgressBars.Baking;

/// <summary>
/// Reports the conversion of fixed-size raw video frames into baked frames.
/// </summary>
public readonly record struct FfmpegBakeProgress(int CompletedFrames, int TotalFrames);
