using System.IO;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Indicates that a .bpb stream is malformed, unsupported, or truncated.
/// </summary>
public sealed class BakedVideoFormatException : IOException
{
    public BakedVideoFormatException(string message)
        : base(message)
    {
    }

    public BakedVideoFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
