using System.IO;
using System.Text;
using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Writes v1 .bpb files as one uncompressed event block per frame.
/// </summary>
public static class BakedVideoWriter
{
    public static void Write(Stream stream, BakedVideoMetadata metadata, IReadOnlyList<BakedFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(frames);

        if (!stream.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(stream));
        }

        if (frames.Count != metadata.FrameCount)
        {
            throw new ArgumentException("Frame count does not match the supplied metadata.", nameof(frames));
        }

        var frameOffsets = new long[frames.Count];
        var payloadLengths = new int[frames.Count];
        long indexOffset = BakedVideoFormat.HeaderSize;

        for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            var frame = frames[frameIndex] ?? throw new ArgumentException("A baked frame cannot be null.", nameof(frames));
            var expectedTimestamp = metadata.GetFrameTimestamp(frameIndex);

            if (frame.Timestamp != expectedTimestamp)
            {
                throw new ArgumentException("Frame timestamps must match the fixed frame rate declared in metadata.", nameof(frames));
            }

            var payloadLength = GetPayloadLength(frame.States);
            payloadLengths[frameIndex] = payloadLength;
            frameOffsets[frameIndex] = indexOffset;
            indexOffset = checked(indexOffset + BakedVideoFormat.FrameBlockHeaderSize + payloadLength);
        }

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteHeader(writer, metadata, indexOffset);

        for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            WriteFrameBlock(writer, frameIndex, frames[frameIndex], payloadLengths[frameIndex]);
        }

        for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            writer.Write(frameIndex);
            writer.Write(frameOffsets[frameIndex]);
        }

        writer.Flush();
    }

    /// <summary>
    /// Writes frames as they are produced. Only index offsets are retained in memory.
    /// The metadata must already contain the exact frame count.
    /// </summary>
    public static void Write(Stream stream, BakedVideoMetadata metadata, IEnumerable<BakedFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(frames);

        if (!stream.CanWrite || !stream.CanSeek)
        {
            throw new ArgumentException("Streaming .bpb writing requires a writable, seekable destination stream.", nameof(stream));
        }

        var frameOffsets = new List<long>(metadata.FrameCount);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteHeader(writer, metadata, indexOffset: 0);
        var frameIndex = 0;

        foreach (var frame in frames)
        {
            if (frameIndex >= metadata.FrameCount)
            {
                throw new ArgumentException("The frame sequence contains more frames than the supplied metadata.", nameof(frames));
            }

            if (frame is null)
            {
                throw new ArgumentException("A baked frame cannot be null.", nameof(frames));
            }

            if (frame.Timestamp != metadata.GetFrameTimestamp(frameIndex))
            {
                throw new ArgumentException("Frame timestamps must match the fixed frame rate declared in metadata.", nameof(frames));
            }

            var payloadLength = GetPayloadLength(frame.States);
            frameOffsets.Add(stream.Position);
            WriteFrameBlock(writer, frameIndex, frame, payloadLength);
            frameIndex++;
        }

        if (frameIndex != metadata.FrameCount)
        {
            throw new ArgumentException("The frame sequence ended before the supplied metadata frame count.", nameof(frames));
        }

        var indexOffset = stream.Position;

        for (var index = 0; index < frameOffsets.Count; index++)
        {
            writer.Write(index);
            writer.Write(frameOffsets[index]);
        }

        var endPosition = stream.Position;
        stream.Position = 0;
        WriteHeader(writer, metadata, indexOffset);
        stream.Position = endPosition;
        writer.Flush();
    }

    private static void WriteHeader(BinaryWriter writer, BakedVideoMetadata metadata, long indexOffset)
    {
        writer.Write(BakedVideoFormat.Magic);
        writer.Write(BakedVideoFormat.FormatVersion);
        writer.Write(metadata.SourceHash.Span);
        writer.Write(metadata.ProfileHash.Span);
        writer.Write(metadata.Width);
        writer.Write(metadata.Height);
        writer.Write(metadata.FrameRateNumerator);
        writer.Write(metadata.FrameRateDenominator);
        writer.Write(metadata.FrameCount);
        writer.Write(indexOffset);
    }

    private static void WriteFrameBlock(BinaryWriter writer, int frameIndex, BakedFrame frame, int payloadLength)
    {
        writer.Write(frameIndex);
        writer.Write(1);
        writer.Write(payloadLength);
        writer.Write(payloadLength);
        writer.Write(frame.States.Count);

        foreach (var state in frame.States)
        {
            ValidateState(state);
            writer.Write(state.SlotId);
            writer.Write(state.Visible);
            writer.Write(state.Row);
            writer.Write(state.StartX);
            writer.Write(state.Length);
            writer.Write(state.Maximum);
            writer.Write(state.Value);
        }
    }

    private static int GetPayloadLength(IReadOnlyList<BarState> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        foreach (var state in states)
        {
            ValidateState(state);
        }

        return checked(sizeof(int) + states.Count * BakedVideoFormat.BarStateSize);
    }

    private static void ValidateState(BarState state)
    {
        if (state.SlotId < 0 ||
            state.Row < 0 ||
            state.StartX < 0 ||
            state.Length < 0 ||
            state.Maximum < 0 ||
            state.Value < 0 ||
            state.Value > state.Maximum ||
            (state.Visible && state.Length == 0))
        {
            throw new ArgumentException("A baked bar state is invalid.");
        }
    }
}
