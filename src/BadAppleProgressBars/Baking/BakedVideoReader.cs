using System.IO;
using System.Text;
using BadAppleProgressBars.Domain;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Reads v1 .bpb files sequentially, materializing only one frame at a time.
/// </summary>
public sealed class BakedVideoReader : IDisposable
{
    private readonly Stream _stream;
    private readonly BinaryReader _reader;
    private int _nextFrameIndex;
    private int _nextIndexEntry;

    public BakedVideoReader(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The source stream must be readable.", nameof(stream));
        }

        _stream = stream;
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen);
        Header = ReadHeader();

        if (Header.IndexOffset < BakedVideoFormat.HeaderSize)
        {
            throw new BakedVideoFormatException("The .bpb index offset precedes the frame data.");
        }

        if (stream.CanSeek && Header.IndexOffset > stream.Length)
        {
            throw new BakedVideoFormatException("The .bpb file is truncated before its index.");
        }
    }

    public BakedVideoHeader Header { get; }

    /// <summary>
    /// Positions a seekable source at a frame block by reading only that frame's index entry.
    /// </summary>
    public void SeekToFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex > Header.Metadata.FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        if (!_stream.CanSeek)
        {
            throw new NotSupportedException("Seeking a .bpb file requires a seekable source stream.");
        }

        if (frameIndex == Header.Metadata.FrameCount)
        {
            _stream.Position = Header.IndexOffset;
            _nextFrameIndex = frameIndex;
            _nextIndexEntry = 0;
            return;
        }

        var indexEntryOffset = checked(Header.IndexOffset + ((long)frameIndex * BakedVideoFormat.IndexEntrySize));

        if (indexEntryOffset + BakedVideoFormat.IndexEntrySize > _stream.Length)
        {
            throw new BakedVideoFormatException("The .bpb file is truncated while seeking its index.");
        }

        _stream.Position = indexEntryOffset;

        try
        {
            var indexedFrame = _reader.ReadInt32();
            var frameOffset = _reader.ReadInt64();

            if (indexedFrame != frameIndex ||
                frameOffset < BakedVideoFormat.HeaderSize ||
                frameOffset >= Header.IndexOffset)
            {
                throw new BakedVideoFormatException("The .bpb index contains an invalid frame offset.");
            }

            _stream.Position = frameOffset;
            _nextFrameIndex = frameIndex;
            _nextIndexEntry = 0;
        }
        catch (EndOfStreamException exception)
        {
            throw new BakedVideoFormatException("The .bpb file is truncated while seeking its index.", exception);
        }
    }

    /// <summary>
    /// Reads the next frame block without reading future blocks or the index into memory.
    /// </summary>
    public bool TryReadNextFrame(out BakedFrame frame)
    {
        if (_nextFrameIndex >= Header.Metadata.FrameCount)
        {
            frame = null!;
            return false;
        }

        try
        {
            var blockStartFrame = _reader.ReadInt32();
            var blockFrameCount = _reader.ReadInt32();
            var uncompressedLength = _reader.ReadInt32();
            var compressedLength = _reader.ReadInt32();

            if (blockStartFrame != _nextFrameIndex || blockFrameCount != 1)
            {
                throw new BakedVideoFormatException("The .bpb frame block order is invalid for v1 streaming.");
            }

            if (uncompressedLength < sizeof(int) || compressedLength != uncompressedLength)
            {
                throw new BakedVideoFormatException("The .bpb frame block has unsupported compression metadata.");
            }

            if (_stream.CanSeek && _stream.Position + compressedLength > Header.IndexOffset)
            {
                throw new BakedVideoFormatException("The .bpb file is truncated inside a frame block.");
            }

            var states = ReadStates(uncompressedLength);
            frame = new BakedFrame(Header.Metadata.GetFrameTimestamp(_nextFrameIndex), states);
            _nextFrameIndex++;
            return true;
        }
        catch (EndOfStreamException exception)
        {
            throw new BakedVideoFormatException("The .bpb file is truncated while reading frame data.", exception);
        }
    }

    /// <summary>
    /// Reads one index entry after all frame blocks have been consumed.
    /// </summary>
    public bool TryReadNextIndexEntry(out BakedIndexEntry entry)
    {
        if (_nextFrameIndex < Header.Metadata.FrameCount)
        {
            throw new InvalidOperationException("Read all frame blocks before reading the index.");
        }

        if (_nextIndexEntry >= Header.Metadata.FrameCount)
        {
            entry = default;
            return false;
        }

        try
        {
            var frameStart = _reader.ReadInt32();
            var fileOffset = _reader.ReadInt64();

            if (frameStart != _nextIndexEntry ||
                fileOffset < BakedVideoFormat.HeaderSize ||
                fileOffset >= Header.IndexOffset)
            {
                throw new BakedVideoFormatException("The .bpb index contains an invalid frame offset.");
            }

            entry = new BakedIndexEntry(frameStart, fileOffset);
            _nextIndexEntry++;
            return true;
        }
        catch (EndOfStreamException exception)
        {
            throw new BakedVideoFormatException("The .bpb file is truncated while reading its index.", exception);
        }
    }

    public void Dispose() => _reader.Dispose();

    private BakedVideoHeader ReadHeader()
    {
        try
        {
            var magic = ReadRequiredBytes(BakedVideoFormat.Magic.Length);

            if (!magic.SequenceEqual(BakedVideoFormat.Magic))
            {
                throw new BakedVideoFormatException("Invalid .bpb magic. Expected BPB1.");
            }

            var formatVersion = _reader.ReadInt32();

            if (formatVersion != BakedVideoFormat.FormatVersion)
            {
                throw new BakedVideoFormatException($"Unsupported .bpb format version: {formatVersion}.");
            }

            var sourceHash = ReadRequiredBytes(BakedVideoMetadata.HashLength);
            var profileHash = ReadRequiredBytes(BakedVideoMetadata.HashLength);
            var width = _reader.ReadInt32();
            var height = _reader.ReadInt32();
            var frameRateNumerator = _reader.ReadInt32();
            var frameRateDenominator = _reader.ReadInt32();
            var frameCount = _reader.ReadInt32();
            var indexOffset = _reader.ReadInt64();
            var metadata = new BakedVideoMetadata(
                sourceHash,
                profileHash,
                width,
                height,
                frameRateNumerator,
                frameRateDenominator,
                frameCount);

            return new BakedVideoHeader(metadata, formatVersion, indexOffset);
        }
        catch (EndOfStreamException exception)
        {
            throw new BakedVideoFormatException("The .bpb file is truncated while reading its header.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new BakedVideoFormatException("The .bpb header contains invalid metadata.", exception);
        }
    }

    private List<BarState> ReadStates(int payloadLength)
    {
        var stateCount = _reader.ReadInt32();

        if (stateCount < 0)
        {
            throw new BakedVideoFormatException("The .bpb frame has a negative state count.");
        }

        var expectedPayloadLength = checked(sizeof(int) + stateCount * BakedVideoFormat.BarStateSize);

        if (expectedPayloadLength != payloadLength)
        {
            throw new BakedVideoFormatException("The .bpb frame payload length does not match its state count.");
        }

        var states = new List<BarState>(stateCount);

        for (var index = 0; index < stateCount; index++)
        {
            var state = new BarState(
                SlotId: _reader.ReadInt32(),
                Visible: _reader.ReadBoolean(),
                Row: _reader.ReadInt32(),
                StartX: _reader.ReadInt32(),
                Length: _reader.ReadInt32(),
                Maximum: _reader.ReadInt32(),
                Value: _reader.ReadInt32());

            ValidateState(state);
            states.Add(state);
        }

        return states;
    }

    private byte[] ReadRequiredBytes(int count)
    {
        var bytes = _reader.ReadBytes(count);

        if (bytes.Length != count)
        {
            throw new EndOfStreamException();
        }

        return bytes;
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
            throw new BakedVideoFormatException("The .bpb frame contains an invalid bar state.");
        }
    }
}
