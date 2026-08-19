using System.Diagnostics;
using System.IO;
using BadAppleProgressBars.Domain;
using OpenCvSharp;

namespace BadAppleProgressBars.Baking;

/// <summary>
/// Bakes a video using the application-bundled FFmpeg executable and OpenCV image processing.
/// </summary>
public sealed class FfmpegVideoBaker
{
    public async Task BakeAsync(
        string sourceVideoPath,
        string destinationBakedPath,
        FfmpegBakeOptions? options = null,
        IProgress<FfmpegBakeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceVideoPath))
        {
            throw new ArgumentException("A source video path is required.", nameof(sourceVideoPath));
        }

        if (string.IsNullOrWhiteSpace(destinationBakedPath))
        {
            throw new ArgumentException("A destination .bpb path is required.", nameof(destinationBakedPath));
        }

        options ??= new FfmpegBakeOptions();
        options.Validate();
        var sourcePath = Path.GetFullPath(sourceVideoPath);
        var destinationPath = Path.GetFullPath(destinationBakedPath);
        var ffmpegPath = Path.GetFullPath(options.FfmpegExecutablePath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The source video file does not exist.", sourcePath);
        }

        if (!File.Exists(ffmpegPath))
        {
            throw new FfmpegBakeException($"Bundled ffmpeg.exe is missing: {ffmpegPath}");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new ArgumentException("The destination .bpb path must include a directory.", nameof(destinationBakedPath));
        Directory.CreateDirectory(destinationDirectory);

        var temporaryRawPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.bgr.tmp");
        var temporaryBakedPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.bpb.tmp");

        try
        {
            await DecodeToRawFramesAsync(sourcePath, temporaryRawPath, ffmpegPath, options, cancellationToken).ConfigureAwait(false);
            var frameByteLength = checked(options.Width * options.Height * 3);
            var rawLength = new FileInfo(temporaryRawPath).Length;

            if (rawLength % frameByteLength != 0)
            {
                throw new FfmpegBakeException("FFmpeg produced a partial BGR frame.");
            }

            var frameCountLong = rawLength / frameByteLength;

            if (frameCountLong <= 0 || frameCountLong > int.MaxValue)
            {
                throw new FfmpegBakeException("The source video produced no usable frames or exceeds the .bpb frame limit.");
            }

            var sourceHash = await BakedVideoIdentityService.ComputeSourceHashAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            var profileHash = BakedVideoIdentityService.ComputeProfileHash(options);
            var metadata = new BakedVideoMetadata(
                sourceHash,
                profileHash,
                options.Width,
                options.Height,
                options.FramesPerSecond,
                frameRateDenominator: 1,
                frameCount: (int)frameCountLong);

            progress?.Report(new FfmpegBakeProgress(0, metadata.FrameCount));
            await Task.Run(
                () => WriteBakedVideo(temporaryRawPath, temporaryBakedPath, metadata, options, progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryBakedPath, destinationPath, overwrite: true);
        }
        finally
        {
            DeleteIfPresent(temporaryRawPath);
            DeleteIfPresent(temporaryBakedPath);
        }
    }

    private static async Task DecodeToRawFramesAsync(
        string sourcePath,
        string rawPath,
        string ffmpegPath,
        FfmpegBakeOptions options,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:v:0");
        startInfo.ArgumentList.Add("-an");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add(
            $"fps={options.FramesPerSecond},scale={options.Width}:{options.Height}:force_original_aspect_ratio=decrease,pad={options.Width}:{options.Height}:(ow-iw)/2:(oh-ih)/2:color=white");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("bgr24");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("rawvideo");
        startInfo.ArgumentList.Add("pipe:1");

        using var process = Process.Start(startInfo)
            ?? throw new FfmpegBakeException("Unable to start the bundled ffmpeg.exe process.");
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await using var rawFile = new FileStream(
                rawPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65_536,
                useAsync: true);
            await process.StandardOutput.BaseStream.CopyToAsync(rawFile, 65_536, cancellationToken).ConfigureAwait(false);
            await rawFile.FlushAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            await WaitForTerminationAsync(process).ConfigureAwait(false);
            throw;
        }
        catch
        {
            TryTerminate(process);
            await WaitForTerminationAsync(process).ConfigureAwait(false);
            throw;
        }

        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new FfmpegBakeException($"Bundled ffmpeg.exe failed with exit code {process.ExitCode}: {error.Trim()}");
        }
    }

    private static void WriteBakedVideo(
        string rawPath,
        string destinationPath,
        BakedVideoMetadata metadata,
        FfmpegBakeOptions options,
        IProgress<FfmpegBakeProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        BakedVideoWriter.Write(
            destination,
            metadata,
            EnumerateProcessedFrames(rawPath, metadata, options, progress, cancellationToken));
    }

    private static IEnumerable<BakedFrame> EnumerateProcessedFrames(
        string rawPath,
        BakedVideoMetadata metadata,
        FfmpegBakeOptions options,
        IProgress<FfmpegBakeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var frameByteLength = checked(metadata.Width * metadata.Height * 3);
        var bgrPixels = new byte[frameByteLength];
        var binaryPixels = new byte[checked(metadata.Width * metadata.Height)];
        using var rawFile = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65_536);
        using var bgrFrame = new Mat(metadata.Height, metadata.Width, MatType.CV_8UC3);
        using var grayFrame = new Mat();
        using var binaryFrame = new Mat();

        for (var frameIndex = 0; frameIndex < metadata.FrameCount; frameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadExactly(rawFile, bgrPixels);
            System.Runtime.InteropServices.Marshal.Copy(bgrPixels, 0, bgrFrame.Data, bgrPixels.Length);
            Cv2.CvtColor(bgrFrame, grayFrame, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(
                grayFrame,
                binaryFrame,
                options.Threshold - 1,
                255,
                options.InvertBlackAndWhite ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary);
            System.Runtime.InteropServices.Marshal.Copy(binaryFrame.Data, binaryPixels, 0, binaryPixels.Length);

            yield return new BakedFrame(
                metadata.GetFrameTimestamp(frameIndex),
                BuildBarStates(binaryPixels, metadata.Width, metadata.Height));
            progress?.Report(new FfmpegBakeProgress(frameIndex + 1, metadata.FrameCount));
        }
    }

    private static List<BarState> BuildBarStates(ReadOnlySpan<byte> binaryPixels, int width, int height)
    {
        var states = new List<BarState>(checked(height * ((width + 1) / 2)));
        var slotId = 0;

        for (var row = 0; row < height; row++)
        {
            var pixels = binaryPixels.Slice(row * width, width);
            var startX = 0;

            for (var x = 1; x < width; x++)
            {
                if (pixels[x - 1] != 0 && pixels[x] == 0)
                {
                    AddState(states, ref slotId, row, startX, x, pixels);
                    startX = x;
                }
            }

            AddState(states, ref slotId, row, startX, width, pixels);
        }

        return states;
    }

    private static void AddState(List<BarState> states, ref int slotId, int row, int startX, int endX, ReadOnlySpan<byte> pixels)
    {
        var blackPrefixLength = 0;

        while (startX + blackPrefixLength < endX && pixels[startX + blackPrefixLength] == 0)
        {
            blackPrefixLength++;
        }

        var length = endX - startX;
        states.Add(new BarState(slotId++, true, row, startX, length, length, blackPrefixLength));
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);

            if (read == 0)
            {
                throw new EndOfStreamException("The temporary raw-video file ended before the expected frame count.");
            }

            totalRead += read;
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process ended concurrently with cancellation.
        }
    }

    private static async Task WaitForTerminationAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // The process exited while cancellation was being handled.
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
