# Bad Apple Progress Bars

WPF video playback built from native Windows `ProgressBar` controls.

## Development launch

Without an argument, the program plays a small pre-baked synthetic animation:

```powershell
dotnet run --project .\src\BadAppleProgressBars\BadAppleProgressBars.csproj
```

Pass a `.bpb` file to stream it directly. Pass a video file to bake it first with the bundled FFmpeg and OpenCV pipeline, then play the resulting cached `.bpb`. Cache entries are stored in `%LocalAppData%\BadAppleProgressBars\cache` and are reused only when both the source-file and render-profile hashes match:

```powershell
dotnet run --project .\src\BadAppleProgressBars\BadAppleProgressBars.csproj -- .\video.mp4
```

For video input, a reviewed Windows x64 `ffmpeg.exe` must be placed at `src/BadAppleProgressBars/third_party/ffmpeg/ffmpeg.exe` before build/publish. It is copied beside the application as `ffmpeg/ffmpeg.exe`; the program does not use the user's PATH. See [the FFmpeg distribution notes](./src/BadAppleProgressBars/third_party/ffmpeg/README.md) before adding a binary.

Create either Windows x64 ZIP locally with a reviewed GPL FFmpeg distribution root:

```powershell
.\scripts\Publish-WinX64.ps1 -FfmpegRoot C:\ffmpeg -SelfContained true
.\scripts\Publish-WinX64.ps1 -FfmpegRoot C:\ffmpeg -SelfContained false
```

The ZIP is written beneath `artifacts/` and is intentionally excluded from Git.

## GitHub Actions

Pushes to `main` run tests and create two x64 workflow artifacts: `self-contained` for machines without .NET and `framework-dependent` for machines with the .NET 8 Desktop Runtime. Pushing a tag in the exact form `vX.X.X.X` additionally creates or updates a GitHub Release containing both ZIPs. Commit artifacts use a short SHA in their name; release artifacts use the tag.

During playback, `Space` pauses/resumes, `R` or `Home` restarts, and Left/Right moves one frame.
