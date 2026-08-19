using System.Text;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Constants shared by the v1 .bpb writer and streaming reader.
/// </summary>
public static class BakedVideoFormat
{
    public const int FormatVersion = 1;
    public const int HeaderSize = 100;
    public const int FrameBlockHeaderSize = 16;
    public const int BarStateSize = 25;
    public const int IndexEntrySize = 12;

    internal static readonly byte[] Magic = Encoding.ASCII.GetBytes("BPB1");
}
