using System.IO;

namespace BadAppleProgressBars.Baking;

public sealed class FfmpegBakeException : IOException
{
    public FfmpegBakeException(string message)
        : base(message)
    {
    }

    public FfmpegBakeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
