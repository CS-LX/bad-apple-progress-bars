# Bad Apple Progress Bars

WPF video playback built from native Windows `ProgressBar` controls.

## Development launch

Without an argument, the program plays a small pre-baked synthetic animation:

```powershell
dotnet run --project .\src\BadAppleProgressBars\BadAppleProgressBars.csproj
```

Pass a `.bpb` file to stream it directly. Pass a video file to bake it first with the bundled FFmpeg and OpenCV pipeline, then play the resulting temporary `.bpb`:

```powershell
dotnet run --project .\src\BadAppleProgressBars\BadAppleProgressBars.csproj -- .\video.mp4
```

For video input, a reviewed Windows x64 `ffmpeg.exe` must be placed at `src/BadAppleProgressBars/third_party/ffmpeg/ffmpeg.exe` before build/publish. It is copied beside the application as `ffmpeg/ffmpeg.exe`; the program does not use the user's PATH. See [the FFmpeg distribution notes](./src/BadAppleProgressBars/third_party/ffmpeg/README.md) before adding a binary.

Create the self-contained Windows x64 GitHub Release ZIP with a reviewed GPL FFmpeg distribution root:

```powershell
.\scripts\Publish-WinX64.ps1 -FfmpegRoot C:\ffmpeg
```

The ZIP is written beneath `artifacts/` and is intentionally excluded from Git. Upload that ZIP as a GitHub Release asset.

During playback, `Space` pauses/resumes, `R` or `Home` restarts, and Left/Right moves one frame.
