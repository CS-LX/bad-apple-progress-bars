using System.IO;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Fixed render settings for the first FFmpeg/OpenCV baking pipeline.
/// </summary>
public sealed class FfmpegBakeOptions
{
    public string FfmpegExecutablePath { get; init; } = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    public int Width { get; init; } = 80;

    public int Height { get; init; } = 45;

    public int FramesPerSecond { get; init; } = 30;

    public byte Threshold { get; init; } = 128;

    public bool InvertBlackAndWhite { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(FfmpegExecutablePath))
        {
            throw new ArgumentException("A bundled ffmpeg.exe path is required.", nameof(FfmpegExecutablePath));
        }

        if (Width <= 0 || Height <= 0 || FramesPerSecond <= 0 || Threshold == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FfmpegExecutablePath), "The FFmpeg bake profile contains an invalid dimension, frame rate, or threshold.");
        }
    }
}
