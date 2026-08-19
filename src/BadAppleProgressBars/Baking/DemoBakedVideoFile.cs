using System.IO;
using BadAppleProgressBars.Domain;
using BadAppleProgressBars.Playback;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Creates a small pre-baked fixture for running the application before a real video baker exists.
/// Playback still opens this fixture through the same .bpb-only streaming path.
/// </summary>
internal static class DemoBakedVideoFile
{
    public static string EnsureCreated()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BadAppleProgressBars");
        var path = Path.Combine(directory, "synthetic-demo-v1.bpb");

        if (File.Exists(path))
        {
            return path;
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        var frames = SyntheticFrameFactory.Create().Select(BarStateFrameConverter.FromPlaybackFrame).ToArray();
        var metadata = new BakedVideoMetadata(
            new byte[BakedVideoMetadata.HashLength],
            new byte[BakedVideoMetadata.HashLength],
            SyntheticFrameFactory.GridWidth,
            SyntheticFrameFactory.GridHeight,
            frameRateNumerator: 4,
            frameRateDenominator: 3,
            frameCount: frames.Length);

        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            BakedVideoWriter.Write(stream, metadata, frames);
        }

        File.Move(temporaryPath, path, overwrite: true);
        return path;
    }
}
