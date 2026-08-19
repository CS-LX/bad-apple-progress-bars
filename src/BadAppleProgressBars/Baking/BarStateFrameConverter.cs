using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Playback;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Converts row blocks into the explicit pooled-slot state used by .bpb v1.
/// </summary>
public static class BarStateFrameConverter
{
    public static BakedFrame FromPlaybackFrame(PlaybackFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var states = new List<BarState>();
        var slotId = 0;

        for (var row = 0; row < frame.Rows.Count; row++)
        {
            var blocks = frame.Rows[row] ?? throw new ArgumentException("A playback row cannot be null.", nameof(frame));

            foreach (var block in blocks)
            {
                states.Add(new BarState(
                    SlotId: slotId++,
                    Visible: true,
                    Row: row,
                    StartX: block.StartX,
                    Length: block.Length,
                    Maximum: block.Length,
                    Value: block.BlackPrefixLength));
            }
        }

        return new BakedFrame(frame.Timestamp, states);
    }
}
