using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Produces the source and render-profile hashes that identify a reusable baked video.
/// </summary>
public static class BakedVideoIdentityService
{
    private const string BakeAlgorithmVersion = "opencv-gray-threshold-v1";

    public static async Task<byte[]> ComputeSourceHashAsync(string sourceVideoPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVideoPath);
        await using var source = new FileStream(sourceVideoPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65_536, useAsync: true);
        return await SHA256.HashDataAsync(source, cancellationToken).ConfigureAwait(false);
    }

    public static byte[] ComputeProfileHash(FfmpegBakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var profile = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"bpb{BakedVideoFormat.FormatVersion}|{BakeAlgorithmVersion}|{options.Width}x{options.Height}|fps={options.FramesPerSecond}|threshold={options.Threshold}|invert={options.InvertBlackAndWhite}|fit=letterbox");
        return SHA256.HashData(Encoding.UTF8.GetBytes(profile));
    }
}
