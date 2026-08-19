using BadAppleProgressBars.Baking;

namespace BadAppleProgressBars.Tests;

public class FfmpegVideoBakerTests
{
    [Fact]
    public async Task BakeAsync_MissingBundledExecutable_ThrowsAReadableError()
    {
        var sourcePath = Path.GetTempFileName();
        var destinationPath = Path.Combine(Path.GetTempPath(), $"bad-apple-progress-bars-{Guid.NewGuid():N}.bpb");

        try
        {
            var exception = await Assert.ThrowsAsync<FfmpegBakeException>(() => new FfmpegVideoBaker().BakeAsync(
                sourcePath,
                destinationPath,
                new FfmpegBakeOptions { FfmpegExecutablePath = destinationPath + ".missing.exe" }));

            Assert.Contains("Bundled ffmpeg.exe is missing", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(sourcePath);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
        }
    }
}
