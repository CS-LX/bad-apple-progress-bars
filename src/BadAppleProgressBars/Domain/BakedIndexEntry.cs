namespace BadAppleProgressBars.Domain;

/// <summary>
/// Maps a frame number to the byte offset of its event block in a .bpb file.
/// </summary>
public readonly record struct BakedIndexEntry(int FrameStart, long FileOffset);
